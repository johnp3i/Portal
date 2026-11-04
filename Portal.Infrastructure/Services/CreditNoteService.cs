using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for credit note management including creation, lifecycle transitions,
/// application, voiding, and query operations.
/// </summary>
public class CreditNoteService : ICreditNoteService
{
    private readonly CreditNoteRepository _creditNoteRepository;
    private readonly CreditNoteLineRepository _creditNoteLineRepository;
    private readonly CreditNoteApplicationRepository _creditNoteApplicationRepository;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly PaymentRepository _paymentRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly VatSubmissionPeriodRepository _vatSubmissionPeriodRepository;
    private readonly IFinancialStatusEngine _financialStatusEngine;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _portalDbContext;

    private const int MaxRetryAttempts = 3;

    public CreditNoteService(
        CreditNoteRepository creditNoteRepository,
        CreditNoteLineRepository creditNoteLineRepository,
        CreditNoteApplicationRepository creditNoteApplicationRepository,
        InvoiceRepository invoiceRepository,
        PaymentRepository paymentRepository,
        AuditLogRepository auditLogRepository,
        VatSubmissionPeriodRepository vatSubmissionPeriodRepository,
        IFinancialStatusEngine financialStatusEngine,
        ICurrentTenantService currentTenantService,
        PortalDbContext portalDbContext)
    {
        _creditNoteRepository = creditNoteRepository;
        _creditNoteLineRepository = creditNoteLineRepository;
        _creditNoteApplicationRepository = creditNoteApplicationRepository;
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
        _auditLogRepository = auditLogRepository;
        _vatSubmissionPeriodRepository = vatSubmissionPeriodRepository;
        _financialStatusEngine = financialStatusEngine;
        _currentTenantService = currentTenantService;
        _portalDbContext = portalDbContext;
    }

    /// <inheritdoc />
    public async Task<ServiceResult<int>> CreateCreditNoteAsync(CreateCreditNoteDto dto, int businessId, string? userId, bool accountCreditEnabled)
    {
        var isAccountMode = !dto.InvoiceId.HasValue;

        Invoice? invoice = null;
        int resolvedCustomerId;
        decimal outstandingBalance = 0m;

        if (isAccountMode)
        {
            // === Account-level credit note (no source invoice) ===

            // Capability gate (server-side, beyond the UI toggle).
            if (!accountCreditEnabled)
                return ServiceResult<int>.Fail("Account-level credit notes are not enabled for this business.");

            if (!dto.CustomerId.HasValue)
                return ServiceResult<int>.Fail("A customer is required for an account-level credit note.");

            // Validate the customer belongs to this business (tenant isolation).
            var customerExists = await _portalDbContext.Customers
                .AnyAsync(c => c.Id == dto.CustomerId.Value && c.BusinessId == businessId);
            if (!customerExists)
                return ServiceResult<int>.Fail("Customer not found.");

            resolvedCustomerId = dto.CustomerId.Value;

            var errors = ValidateCreateCreditNoteCommon(dto);
            if (errors.Count > 0)
                return ServiceResult<int>.Fail(string.Join(" | ", errors));
        }
        else
        {
            // === Invoice-linked credit note (classic path) ===

            // 1. Fetch the source invoice
            invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(dto.InvoiceId!.Value, businessId);

            // 2. Compute outstanding balance (needed for validation)
            if (invoice != null)
            {
                var totalPaid = await _paymentRepository.GetTotalPaidAsync(dto.InvoiceId.Value, businessId);
                var totalCredited = await _creditNoteRepository.GetTotalAppliedCreditAsync(dto.InvoiceId.Value, businessId);
                outstandingBalance = invoice.TotalAmount - totalPaid - totalCredited;
            }

            // 3. Run full validation pipeline (collect ALL errors)
            var errors = ValidateCreateCreditNote(dto, invoice, outstandingBalance);
            if (errors.Count > 0)
                return ServiceResult<int>.Fail(string.Join(" | ", errors));

            // 3a. Balance ceiling: an invoice-linked credit note cannot exceed the invoice's
            //     outstanding balance. (Account-level notes have no invoice to bound them here; their
            //     ceiling is enforced per-invoice at application time instead.)
            var (_, _, creditTotal) = ComputeAmounts(dto.Lines);
            if (creditTotal > outstandingBalance)
                return ServiceResult<int>.Fail($"Credit note amount ({creditTotal:F2}) exceeds the remaining balance ({outstandingBalance:F2}).");

            resolvedCustomerId = invoice!.CustomerId;
        }

        // 3b. Check VAT period submission lock: reject if period is already submitted
        var vatSubmission = await _portalDbContext.VatSubmissions
            .FirstOrDefaultAsync(vs => vs.VatSubmissionPeriodId == dto.VatSubmissionPeriodId
                && vs.BusinessId == businessId);

        if (vatSubmission != null && vatSubmission.IsSubmitted)
        {
            return ServiceResult<int>.Fail("Cannot assign a credit note to a submitted VAT period. The period is already filed.");
        }

        // 4. Compute amounts
        var (subtotal, taxAmount, totalAmount) = ComputeAmounts(dto.Lines);

        // 5. Generate credit note number with retry logic
        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            var creditNoteNumber = await GenerateCreditNoteNumberAsync(businessId, dto.IssueDate);

            // 7. Build entity
            var creditNote = new CreditNote
            {
                BusinessId = businessId,
                InvoiceId = dto.InvoiceId, // null for account-level notes
                CustomerId = resolvedCustomerId,
                CreditNoteStatusTypeId = 1, // Draft
                VatSubmissionPeriodId = dto.VatSubmissionPeriodId,
                CreditNoteNumber = creditNoteNumber,
                IssueDate = dto.IssueDate,
                Reason = dto.Reason.Trim(),
                Subtotal = subtotal,
                TaxAmount = taxAmount,
                TotalAmount = totalAmount,
                IssuedAtUtc = null,
                VoidedAtUtc = null,
                CreatedByUserId = userId,
                CreatedAtUtc = DateTime.UtcNow
            };

            try
            {
                // 8. Insert credit note
                var creditNoteId = await _creditNoteRepository.InsertAsync(creditNote);

                // 9. Insert credit note lines
                var lines = new List<CreditNoteLine>();
                for (int i = 0; i < dto.Lines.Count; i++)
                {
                    var lineDto = dto.Lines[i];
                    var lineTotal = lineDto.Quantity * lineDto.UnitPrice;

                    lines.Add(new CreditNoteLine
                    {
                        CreditNoteId = creditNoteId,
                        Description = lineDto.Description.Trim(),
                        Quantity = lineDto.Quantity,
                        UnitPrice = lineDto.UnitPrice,
                        VatRate = lineDto.VatRate,
                        LineTotal = lineTotal,
                        SortOrder = i + 1
                    });
                }

                await _creditNoteLineRepository.InsertBatchAsync(lines);

                // 10. Write audit log entry
                var auditLog = new AuditLog
                {
                    BusinessId = businessId,
                    UserId = userId,
                    Action = "CreditNoteCreated",
                    TableName = "CreditNote",
                    RecordId = creditNoteId.ToString(),
                    OldValues = null,
                    NewValues = $"CreditNoteNumber={creditNoteNumber}, UserId={userId}",
                    Timestamp = DateTime.UtcNow
                };
                await _auditLogRepository.InsertAsync(auditLog);

                return ServiceResult<int>.Ok(creditNoteId);
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxRetryAttempts)
            {
                // Retry: re-query highest number on next iteration
                continue;
            }
        }

        return ServiceResult<int>.Fail("Credit note could not be created due to a numbering conflict. Please try again.");
    }

    /// <inheritdoc />
    public Task<ServiceResult> UpdateCreditNoteAsync(int creditNoteId, UpdateCreditNoteDto dto, int businessId)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> IssueCreditNoteAsync(int creditNoteId, int businessId, string? userId)
    {
        var creditNote = await _creditNoteRepository.GetByIdAndBusinessIdAsync(creditNoteId, businessId);
        if (creditNote == null)
            return ServiceResult.Fail("Credit note not found.");

        if (creditNote.CreditNoteStatusTypeId != 1)
            return ServiceResult.Fail("Credit note can only be issued from Draft status.");

        var issuedAtUtc = DateTime.UtcNow;
        await _creditNoteRepository.UpdateStatusAsync(creditNoteId, 2, issuedAtUtc, null);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = businessId,
            UserId = userId,
            Action = "CreditNoteStatusChanged",
            TableName = "CreditNote",
            RecordId = creditNoteId.ToString(),
            OldValues = "Draft",
            NewValues = $"Issued, UserId={userId}",
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ApplyCreditNoteAsync(int creditNoteId, int businessId, string? userId)
    {
        // 1. Fetch credit note and validate existence
        var creditNote = await _creditNoteRepository.GetByIdAndBusinessIdAsync(creditNoteId, businessId);
        if (creditNote == null)
            return ServiceResult.Fail("Credit note not found.");

        // 2. Validate credit note is in Issued status
        if (creditNote.CreditNoteStatusTypeId != 2)
            return ServiceResult.Fail("Only credit notes in Issued status may be applied.");

        // 2b. This single-shot path is only for invoice-linked notes. Account-level notes (no source
        //     invoice) are applied manually across invoices via ApplyAccountCreditAsync.
        if (creditNote.InvoiceId == null)
            return ServiceResult.Fail("Account-level credit notes must be applied to specific invoices.");

        var sourceInvoiceId = creditNote.InvoiceId.Value;

        // 3. Fetch source invoice and validate eligibility
        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(sourceInvoiceId, businessId);
        if (invoice == null)
            return ServiceResult.Fail("Source invoice not found.");

        // 4. Validate invoice financial status is eligible (not Paid=3 or WrittenOff=5)
        if (invoice.InvoiceFinancialStatusTypeId == 3 || invoice.InvoiceFinancialStatusTypeId == 5)
            return ServiceResult.Fail("The invoice is not eligible for credit note application.");

        // 5. Compute outstanding balance and validate credit note amount does not exceed it
        var totalPaid = await _paymentRepository.GetTotalPaidAsync(sourceInvoiceId, businessId);
        var totalCredited = await _creditNoteRepository.GetTotalAppliedCreditAsync(sourceInvoiceId, businessId);
        var outstandingBalance = invoice.TotalAmount - totalPaid - totalCredited;

        if (creditNote.TotalAmount > outstandingBalance)
            return ServiceResult.Fail($"Credit note amount ({creditNote.TotalAmount:F2}) exceeds the remaining balance ({outstandingBalance:F2}).");

        // 6. Execute all operations within a single database transaction
        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
        try
        {
            // 7. Create CreditNoteApplication record
            var application = new CreditNoteApplication
            {
                CreditNoteId = creditNoteId,
                InvoiceId = sourceInvoiceId,
                AmountApplied = creditNote.TotalAmount,
                AppliedAtUtc = DateTime.UtcNow,
                AppliedByUserId = userId,
                IsVoided = false,
                CreatedAtUtc = DateTime.UtcNow
            };
            await _creditNoteApplicationRepository.InsertAsync(application);

            // 8. Update credit note status to Applied (3)
            await _creditNoteRepository.UpdateStatusAsync(creditNoteId, 3, null, null);

            // 9. Recalculate invoice financial status
            await _financialStatusEngine.RecalculateStatusAsync(sourceInvoiceId, businessId);

            // 10. Write audit log entry
            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = businessId,
                UserId = userId,
                Action = "CreditNoteApplied",
                TableName = "CreditNote",
                RecordId = creditNoteId.ToString(),
                OldValues = null,
                NewValues = $"InvoiceId={sourceInvoiceId}, AmountApplied={creditNote.TotalAmount:F2}, UserId={userId}",
                Timestamp = DateTime.UtcNow
            });

            await transaction.CommitAsync();
            return ServiceResult.Ok();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> ApplyAccountCreditAsync(ApplyAccountCreditDto dto, int businessId, string? userId)
    {
        // 1. Fetch and validate the credit note
        var creditNote = await _creditNoteRepository.GetByIdAndBusinessIdAsync(dto.CreditNoteId, businessId);
        if (creditNote == null)
            return ServiceResult.Fail("Credit note not found.");

        // 2. Must be an account-level note (no source invoice)
        if (creditNote.InvoiceId != null)
            return ServiceResult.Fail("This action applies only to account-level credit notes.");

        // 3. Must be Issued (2) — Draft can't be applied, Applied/Voided are terminal here
        if (creditNote.CreditNoteStatusTypeId != 2)
            return ServiceResult.Fail("Only an Issued account credit note can be applied.");

        // 4. Validate allocations present and positive
        var allocations = (dto.Allocations ?? new List<AccountCreditAllocationDto>())
            .Where(a => a.Amount > 0)
            .ToList();
        if (allocations.Count == 0)
            return ServiceResult.Fail("Enter at least one amount to apply.");

        // 5. Compute remaining credit (total minus non-voided applications already made)
        var existingApplications = await _creditNoteApplicationRepository.GetByCreditNoteIdAsync(dto.CreditNoteId);
        var alreadyApplied = existingApplications.Where(a => !a.IsVoided).Sum(a => a.AmountApplied);
        var remaining = creditNote.TotalAmount - alreadyApplied;

        if (allocations.Sum(a => a.Amount) > remaining)
            return ServiceResult.Fail($"Total applied ({allocations.Sum(a => a.Amount):F2}) exceeds the remaining credit ({remaining:F2}).");

        // 6. Validate each target invoice belongs to the customer & business, is eligible, and the
        //    amount does not exceed that invoice's outstanding balance.
        foreach (var alloc in allocations)
        {
            var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(alloc.InvoiceId, businessId);
            if (invoice == null)
                return ServiceResult.Fail($"Invoice {alloc.InvoiceId} not found.");
            if (invoice.CustomerId != creditNote.CustomerId)
                return ServiceResult.Fail("A selected invoice does not belong to this customer.");
            if (invoice.InvoiceStatusTypeId != 2)
                return ServiceResult.Fail($"Invoice {invoice.InvoiceNumber} is not in Issued status.");
            if (invoice.InvoiceFinancialStatusTypeId == 3 || invoice.InvoiceFinancialStatusTypeId == 5)
                return ServiceResult.Fail($"Invoice {invoice.InvoiceNumber} is not eligible for credit application.");

            var paid = await _paymentRepository.GetTotalPaidAsync(alloc.InvoiceId, businessId);
            var credited = await _creditNoteRepository.GetTotalAppliedCreditAsync(alloc.InvoiceId, businessId);
            var invoiceOutstanding = invoice.TotalAmount - paid - credited;
            if (alloc.Amount > invoiceOutstanding)
                return ServiceResult.Fail($"Amount for invoice {invoice.InvoiceNumber} ({alloc.Amount:F2}) exceeds its outstanding balance ({invoiceOutstanding:F2}).");
        }

        // 7. Apply all allocations atomically
        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
        try
        {
            foreach (var alloc in allocations)
            {
                await _creditNoteApplicationRepository.InsertAsync(new CreditNoteApplication
                {
                    CreditNoteId = dto.CreditNoteId,
                    InvoiceId = alloc.InvoiceId,
                    AmountApplied = alloc.Amount,
                    AppliedAtUtc = DateTime.UtcNow,
                    AppliedByUserId = userId,
                    IsVoided = false,
                    CreatedAtUtc = DateTime.UtcNow
                });

                await _financialStatusEngine.RecalculateStatusAsync(alloc.InvoiceId, businessId);

                await _auditLogRepository.InsertAsync(new AuditLog
                {
                    BusinessId = businessId,
                    UserId = userId,
                    Action = "CreditNoteApplied",
                    TableName = "CreditNote",
                    RecordId = dto.CreditNoteId.ToString(),
                    OldValues = null,
                    NewValues = $"InvoiceId={alloc.InvoiceId}, AmountApplied={alloc.Amount:F2}, UserId={userId}",
                    Timestamp = DateTime.UtcNow
                });
            }

            // 8. If fully drawn down, mark Applied (3); otherwise it stays Issued (2).
            var newTotalApplied = alreadyApplied + allocations.Sum(a => a.Amount);
            if (newTotalApplied >= creditNote.TotalAmount)
            {
                await _creditNoteRepository.UpdateStatusAsync(dto.CreditNoteId, 3, creditNote.IssuedAtUtc, null);
            }

            await transaction.CommitAsync();
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ServiceResult> VoidCreditNoteAsync(int creditNoteId, int businessId, string? userId)
    {
        var creditNote = await _creditNoteRepository.GetByIdAndBusinessIdAsync(creditNoteId, businessId);
        if (creditNote == null)
            return ServiceResult.Fail("Credit note not found.");

        // Validate status: only Draft (1), Issued (2), or Applied (3) can be voided
        if (creditNote.CreditNoteStatusTypeId == 4)
            return ServiceResult.Fail("Credit note is already voided.");

        if (creditNote.CreditNoteStatusTypeId < 1 || creditNote.CreditNoteStatusTypeId > 3)
            return ServiceResult.Fail("Credit note cannot be voided from its current status.");

        // Check VAT period submission lock: if period is submitted AND credit note is NOT Draft, reject
        if (creditNote.CreditNoteStatusTypeId != 1)
        {
            var vatSubmission = await _portalDbContext.VatSubmissions
                .FirstOrDefaultAsync(vs => vs.VatSubmissionPeriodId == creditNote.VatSubmissionPeriodId
                    && vs.BusinessId == businessId);

            if (vatSubmission != null && vatSubmission.IsSubmitted)
                return ServiceResult.Fail("Cannot void a credit note in a submitted VAT period.");
        }

        var oldStatusId = creditNote.CreditNoteStatusTypeId;
        var oldStatusName = GetStatusName(oldStatusId);
        var voidedAtUtc = DateTime.UtcNow;

        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
        try
        {
            // Perform a financial reversal whenever this note has applied credit to reverse.
            //
            //  - Invoice-linked note (has a source invoice): a reversal is needed when it was
            //    Applied (status 3). The affected invoice is creditNote.InvoiceId.
            //  - Account-level note (no source invoice): it may be Applied (3) or still Issued (2)
            //    but partially applied; it can span several invoices. We read the actual application
            //    rows and recalculate EACH distinct affected invoice.
            //
            // We determine the affected invoices from the application rows when available, and fall
            // back to creditNote.InvoiceId for the classic invoice-linked Applied case.
            var applications = await _creditNoteApplicationRepository.GetByCreditNoteIdAsync(creditNoteId);
            var nonVoidedApplications = (applications ?? new List<CreditNoteApplication>())
                .Where(a => !a.IsVoided)
                .ToList();

            var affectedInvoiceIds = nonVoidedApplications.Select(a => a.InvoiceId).Distinct().ToList();
            if (affectedInvoiceIds.Count == 0 && oldStatusId == 3 && creditNote.InvoiceId.HasValue)
            {
                // Invoice-linked Applied note with no enumerated application rows available:
                // reverse against the source invoice (classic single-shot behaviour).
                affectedInvoiceIds.Add(creditNote.InvoiceId.Value);
            }

            if (oldStatusId == 3 || nonVoidedApplications.Count > 0)
            {
                // Void all CreditNoteApplication records for this note
                await _creditNoteApplicationRepository.VoidByCreditNoteIdAsync(creditNoteId);

                // Recalculate every distinct invoice that was reduced by this note
                foreach (var affectedInvoiceId in affectedInvoiceIds)
                {
                    await _financialStatusEngine.RecalculateStatusAsync(affectedInvoiceId, businessId);
                }

                // Write "CreditNoteReversed" audit entry
                var reversedAmount = nonVoidedApplications.Count > 0
                    ? nonVoidedApplications.Sum(a => a.AmountApplied)
                    : creditNote.TotalAmount;
                await _auditLogRepository.InsertAsync(new AuditLog
                {
                    BusinessId = businessId,
                    UserId = userId,
                    Action = "CreditNoteReversed",
                    TableName = "CreditNote",
                    RecordId = creditNoteId.ToString(),
                    OldValues = null,
                    NewValues = $"ReversedAmount={reversedAmount:F2}, Invoices={string.Join(",", affectedInvoiceIds)}, UserId={userId}",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Update status to Voided (4), set VoidedAtUtc
            await _creditNoteRepository.UpdateStatusAsync(creditNoteId, 4, creditNote.IssuedAtUtc, voidedAtUtc);

            // Write "CreditNoteStatusChanged" audit entry
            await _auditLogRepository.InsertAsync(new AuditLog
            {
                BusinessId = businessId,
                UserId = userId,
                Action = "CreditNoteStatusChanged",
                TableName = "CreditNote",
                RecordId = creditNoteId.ToString(),
                OldValues = oldStatusName,
                NewValues = $"Voided, UserId={userId}",
                Timestamp = DateTime.UtcNow
            });

            await transaction.CommitAsync();
            return ServiceResult.Ok();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(List<CreditNoteListDto> Items, int TotalCount)> GetCreditNotesPagedAsync(CreditNoteFilterDto filter, int businessId)
    {
        int offset = (filter.Page - 1) * filter.PageSize;
        return await _creditNoteRepository.GetPagedAsync(
            businessId,
            filter.StatusId,
            filter.CustomerId,
            filter.FromDate,
            filter.ToDate,
            filter.SearchTerm,
            offset,
            filter.PageSize);
    }

    /// <inheritdoc />
    public async Task<CreditNoteDetailDto?> GetCreditNoteDetailAsync(int creditNoteId, int businessId)
    {
        var creditNote = await _creditNoteRepository.GetByIdAndBusinessIdAsync(creditNoteId, businessId);
        if (creditNote == null)
            return null;

        // Get lines
        var lines = await _creditNoteLineRepository.GetByCreditNoteIdAsync(creditNoteId);

        // Get applications
        var applications = await _creditNoteApplicationRepository.GetByCreditNoteIdAsync(creditNoteId);

        // Get related entity names via PortalDbContext for joins. Account-level notes have no source
        // invoice (InvoiceId is null), so skip the fetch and leave the number null.
        var invoice = creditNote.InvoiceId.HasValue
            ? await _invoiceRepository.GetByIdAndBusinessIdAsync(creditNote.InvoiceId.Value, businessId)
            : null;
        var invoiceNumber = invoice?.InvoiceNumber;

        var customerName = await _portalDbContext.Customers
            .Where(c => c.Id == creditNote.CustomerId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync() ?? string.Empty;

        var statusName = await _portalDbContext.CreditNoteStatusTypes
            .Where(s => s.Id == creditNote.CreditNoteStatusTypeId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync() ?? string.Empty;

        var vatPeriodLabel = await _portalDbContext.VatSubmissionPeriods
            .Where(v => v.Id == creditNote.VatSubmissionPeriodId)
            .Select(v => v.PeriodLabel)
            .FirstOrDefaultAsync() ?? string.Empty;

        // Map applications to DTOs with invoice numbers
        var applicationDtos = new List<CreditNoteApplicationDto>();
        foreach (var app in applications)
        {
            var appInvoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(app.InvoiceId, businessId);
            applicationDtos.Add(new CreditNoteApplicationDto
            {
                Id = app.Id,
                AppliedAtUtc = app.AppliedAtUtc,
                InvoiceNumber = appInvoice?.InvoiceNumber ?? string.Empty,
                InvoiceId = app.InvoiceId,
                AmountApplied = app.AmountApplied,
                AppliedByUserId = app.AppliedByUserId ?? string.Empty,
                IsVoided = app.IsVoided
            });
        }

        // Remaining unapplied credit (meaningful for account-level notes; equals total minus the
        // sum of non-voided applications). Reuses the applications already fetched above.
        var appliedSoFar = applications.Where(a => !a.IsVoided).Sum(a => a.AmountApplied);
        var remainingAmount = creditNote.TotalAmount - appliedSoFar;

        return new CreditNoteDetailDto
        {
            Id = creditNote.Id,
            CreditNoteNumber = creditNote.CreditNoteNumber,
            CustomerName = customerName,
            CustomerId = creditNote.CustomerId,
            InvoiceId = creditNote.InvoiceId,
            InvoiceNumber = invoiceNumber,
            RemainingAmount = remainingAmount,
            IssueDate = creditNote.IssueDate,
            Reason = creditNote.Reason,
            CreditNoteStatusTypeId = creditNote.CreditNoteStatusTypeId,
            StatusName = statusName,
            VatPeriodLabel = vatPeriodLabel,
            VatSubmissionPeriodId = creditNote.VatSubmissionPeriodId,
            CreatedByUserId = creditNote.CreatedByUserId,
            IssuedAtUtc = creditNote.IssuedAtUtc,
            VoidedAtUtc = creditNote.VoidedAtUtc,
            Subtotal = creditNote.Subtotal,
            TaxAmount = creditNote.TaxAmount,
            TotalAmount = creditNote.TotalAmount,
            Lines = lines.Select(l => new CreditNoteLineDto
            {
                Id = l.Id,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                VatRate = l.VatRate,
                LineTotal = l.LineTotal
            }).ToList(),
            Applications = applicationDtos
        };
    }

    /// <inheritdoc />
    public async Task<CreditNoteKpiDto> GetKpiAsync(int businessId)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await _creditNoteRepository.GetKpiDataAsync(businessId, monthStart);
    }

    /// <inheritdoc />
    public async Task<List<EligibleInvoiceDto>> GetEligibleInvoicesAsync(int businessId)
    {
        // Get all invoices in Issued status (InvoiceStatusTypeId = 2) for the business
        var invoices = await _portalDbContext.Invoices
            .Where(i => i.BusinessId == businessId
                && i.InvoiceStatusTypeId == 2
                && !i.IsDeleted)
            .Join(_portalDbContext.Customers,
                i => i.CustomerId,
                c => c.Id,
                (i, c) => new { Invoice = i, CustomerName = c.Name })
            .ToListAsync();

        var eligibleInvoices = new List<EligibleInvoiceDto>();

        foreach (var item in invoices)
        {
            var totalPaid = await _paymentRepository.GetTotalPaidAsync(item.Invoice.Id, businessId);
            var totalCredited = await _creditNoteRepository.GetTotalAppliedCreditAsync(item.Invoice.Id, businessId);
            var outstandingBalance = item.Invoice.TotalAmount - totalPaid - totalCredited;

            eligibleInvoices.Add(new EligibleInvoiceDto
            {
                Id = item.Invoice.Id,
                InvoiceNumber = item.Invoice.InvoiceNumber,
                CustomerName = item.CustomerName,
                CustomerId = item.Invoice.CustomerId,
                TotalAmount = item.Invoice.TotalAmount,
                OutstandingBalance = outstandingBalance
            });
        }

        return eligibleInvoices;
    }

    /// <inheritdoc />
    public async Task<List<EligibleInvoiceDto>> GetOutstandingInvoicesForCustomerAsync(int customerId, int businessId)
    {
        // Reuse the FIFO-ordered outstanding-invoice query (already credit-note aware) and map to the
        // shape the apply UI consumes. Only invoices with a positive outstanding balance are returned.
        var outstanding = await _paymentRepository.GetOutstandingInvoicesForCustomerAsync(customerId, businessId);
        return outstanding.Select(o => new EligibleInvoiceDto
        {
            Id = o.InvoiceId,
            InvoiceNumber = o.InvoiceNumber,
            CustomerName = string.Empty, // not needed in this context (single customer)
            CustomerId = customerId,
            TotalAmount = o.TotalAmount,
            OutstandingBalance = o.OutstandingBalance
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<decimal> GetInvoiceOutstandingBalanceAsync(int invoiceId, int businessId)
    {
        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(invoiceId, businessId);
        if (invoice == null)
            return 0m;

        var totalPaid = await _paymentRepository.GetTotalPaidAsync(invoiceId, businessId);
        var totalCredited = await _creditNoteRepository.GetTotalAppliedCreditAsync(invoiceId, businessId);

        return invoice.TotalAmount - totalPaid - totalCredited;
    }

    /// <inheritdoc />
    public async Task<List<AppliedCreditNoteDto>> GetAppliedCreditNotesForInvoiceAsync(int invoiceId, int businessId)
    {
        // Non-voided applications against this invoice, scoped to the tenant via the parent
        // CreditNote.BusinessId — the authoritative join used by GetTotalAppliedCreditAsync.
        return await (
            from app in _portalDbContext.CreditNoteApplications
            join cn in _portalDbContext.CreditNotes on app.CreditNoteId equals cn.Id
            where app.InvoiceId == invoiceId
                  && !app.IsVoided
                  && cn.BusinessId == businessId
            orderby app.AppliedAtUtc
            select new AppliedCreditNoteDto
            {
                CreditNoteId = cn.Id,
                CreditNoteNumber = cn.CreditNoteNumber,
                AmountApplied = app.AmountApplied,
                AppliedAtUtc = app.AppliedAtUtc
            }).ToListAsync();
    }

    #region Private Helpers

    /// <summary>
    /// Validates all fields for INVOICE-LINKED credit note creation (invoice existence + status,
    /// plus the common reason/line validation). Returns ALL applicable errors in a single list.
    /// </summary>
    internal static List<string> ValidateCreateCreditNote(CreateCreditNoteDto dto, Invoice? invoice, decimal outstandingBalance)
    {
        var errors = new List<string>();

        // Invoice existence check
        if (invoice == null)
        {
            errors.Add("Invoice not found.");
            return errors; // Cannot validate further without invoice
        }

        // Invoice status check (must be Issued = 2)
        if (invoice.InvoiceStatusTypeId != 2)
        {
            errors.Add("Credit notes can only be raised against invoices in Issued status.");
        }

        errors.AddRange(ValidateCreateCreditNoteCommon(dto));
        return errors;
    }

    /// <summary>
    /// Validates the fields common to both invoice-linked and account-level credit notes: the reason
    /// and all line items. Used directly by the account-level path (which has no invoice to check).
    /// </summary>
    internal static List<string> ValidateCreateCreditNoteCommon(CreateCreditNoteDto dto)
    {
        var errors = new List<string>();

        // Reason validation
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            errors.Add("A reason is required.");
        }
        else if (dto.Reason.Length > 1000)
        {
            errors.Add("Reason must not exceed 1000 characters.");
        }

        // Line item count validation
        if (dto.Lines == null || dto.Lines.Count == 0)
        {
            errors.Add("At least one line item is required.");
        }
        else if (dto.Lines.Count > 50)
        {
            errors.Add("Maximum of 50 line items exceeded.");
        }

        // Line item field validation
        if (dto.Lines != null && dto.Lines.Count > 0)
        {
            for (int i = 0; i < dto.Lines.Count; i++)
            {
                var line = dto.Lines[i];
                var lineNumber = i + 1;

                if (string.IsNullOrWhiteSpace(line.Description))
                {
                    errors.Add($"Line {lineNumber}: Description is required.");
                }
                else if (line.Description.Length > 250)
                {
                    errors.Add($"Line {lineNumber}: Description must not exceed 250 characters.");
                }

                if (line.Quantity <= 0)
                {
                    errors.Add($"Line {lineNumber}: Quantity must be greater than zero.");
                }

                if (line.UnitPrice <= 0)
                {
                    errors.Add($"Line {lineNumber}: Unit price must be greater than zero.");
                }

                if (line.VatRate < 0 || line.VatRate > 100)
                {
                    errors.Add($"Line {lineNumber}: VAT rate must be between 0 and 100.");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Computes Subtotal, TaxAmount, and TotalAmount from a list of credit note lines.
    /// </summary>
    internal static (decimal Subtotal, decimal TaxAmount, decimal TotalAmount) ComputeAmounts(
        List<CreateCreditNoteLineDto> lines)
    {
        decimal subtotal = 0m;
        decimal taxAmount = 0m;

        foreach (var line in lines)
        {
            decimal lineTotal = line.Quantity * line.UnitPrice;
            decimal lineTax = lineTotal * line.VatRate / 100m;
            subtotal += lineTotal;
            taxAmount += lineTax;
        }

        return (subtotal, taxAmount, subtotal + taxAmount);
    }

    /// <summary>
    /// Generates the next credit note number in the format CN-YYYY-NNNN.
    /// </summary>
    private async Task<string> GenerateCreditNoteNumberAsync(int businessId, DateOnly issueDate)
    {
        int year = issueDate.Year;
        int? highestNumber = await _creditNoteRepository.GetHighestNumberForYearAsync(businessId, year);
        int nextNumber = (highestNumber ?? 0) + 1;

        if (nextNumber > 9999)
        {
            throw new InvalidOperationException("Annual credit note limit (9999) reached for this year.");
        }

        return $"CN-{year}-{nextNumber:D4}";
    }

    /// <summary>
    /// Returns the display name for a credit note status type ID.
    /// </summary>
    private static string GetStatusName(int statusTypeId) => statusTypeId switch
    {
        1 => "Draft",
        2 => "Issued",
        3 => "Applied",
        4 => "Voided",
        _ => "Unknown"
    };

    /// <summary>
    /// Determines if an exception is caused by a unique constraint violation (SQL Server error 2601 or 2627).
    /// </summary>
    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        var sqlEx = ex as SqlException ?? ex.InnerException as SqlException;
        if (sqlEx != null)
        {
            // 2601 = Cannot insert duplicate key row (unique index violation)
            // 2627 = Violation of UNIQUE KEY constraint
            return sqlEx.Number == 2601 || sqlEx.Number == 2627;
        }

        return false;
    }

    #endregion
}
