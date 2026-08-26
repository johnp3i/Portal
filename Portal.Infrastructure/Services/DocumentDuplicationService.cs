using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Handles duplication of invoices and quotations into new Draft documents.
/// Follows the transaction pattern established by ConvertFromQuotationAsync in InvoiceService.
/// </summary>
public class DocumentDuplicationService : IDocumentDuplicationService
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly InvoiceLineRepository _invoiceLineRepository;
    private readonly InvoiceSectionRepository _invoiceSectionRepository;
    private readonly QuotationRepository _quotationRepository;
    private readonly QuotationLineRepository _quotationLineRepository;
    private readonly ProposalSectionRepository _proposalSectionRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly PortalDbContext _portalDbContext;

    public DocumentDuplicationService(
        ICurrentTenantService currentTenantService,
        InvoiceRepository invoiceRepository,
        InvoiceLineRepository invoiceLineRepository,
        InvoiceSectionRepository invoiceSectionRepository,
        QuotationRepository quotationRepository,
        QuotationLineRepository quotationLineRepository,
        ProposalSectionRepository proposalSectionRepository,
        AuditLogRepository auditLogRepository,
        PortalDbContext portalDbContext)
    {
        _currentTenantService = currentTenantService;
        _invoiceRepository = invoiceRepository;
        _invoiceLineRepository = invoiceLineRepository;
        _invoiceSectionRepository = invoiceSectionRepository;
        _quotationRepository = quotationRepository;
        _quotationLineRepository = quotationLineRepository;
        _proposalSectionRepository = proposalSectionRepository;
        _auditLogRepository = auditLogRepository;
        _portalDbContext = portalDbContext;
    }

    public async Task<Invoice> DuplicateInvoiceAsync(int sourceInvoiceId, string userId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // 1. Validate source exists and belongs to current business
        var source = await _invoiceRepository.GetByIdAndBusinessIdAsync(sourceInvoiceId, businessId);
        if (source == null)
        {
            throw new InvalidOperationException("Invoice not found");
        }

        // 2. Begin transaction
        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

        try
        {
            // 3. Generate next sequential invoice number
            var nextNumber = await _invoiceRepository.GetNextSequentialNumberAsync(businessId);
            var invoiceNumber = $"INV-{businessId}-{nextNumber:D5}";

            // 4. Calculate duration gap (DueDate - InvoiceDate) and apply to today
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var durationGap = source.DueDate.DayNumber - source.InvoiceDate.DayNumber;
            var dueDate = today.AddDays(durationGap);

            // 5. Create new Invoice entity with Draft/Unpaid status
            var duplicate = new Invoice
            {
                BusinessId = businessId,
                CustomerId = source.CustomerId,
                QuotationId = null,
                InvoiceStatusTypeId = 1,           // Draft
                InvoiceFinancialStatusTypeId = 1,  // Unpaid
                InvoiceNumber = invoiceNumber,
                InvoiceDate = today,
                DueDate = dueDate,
                Subtotal = 0,
                TaxAmount = 0,
                TotalAmount = 0,
                CurrencyCode = source.CurrencyCode,
                Notes = source.Notes,
                IsGrandTotalShown = source.IsGrandTotalShown,
                IsQuotationReferenceShown = source.IsQuotationReferenceShown,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var duplicateId = await _invoiceRepository.InsertAsync(duplicate);
            duplicate.Id = duplicateId;

            // 6. Copy sections with ID mapping
            var sourceSections = await _invoiceSectionRepository.GetByInvoiceIdAsync(sourceInvoiceId);
            var sectionMapping = new Dictionary<int, int>(); // old InvoiceSectionId → new InvoiceSectionId

            foreach (var section in sourceSections)
            {
                var newSection = new InvoiceSection
                {
                    InvoiceId = duplicateId,
                    Name = section.Name,
                    SortOrder = section.SortOrder,
                    ColumnConfiguration = section.ColumnConfiguration,
                    SectionType = section.SectionType,
                    Description = section.Description,
                    Notes = section.Notes,
                    IsEmphasized = section.IsEmphasized,
                    AccentColor = section.AccentColor,
                    Label = section.Label,
                    IsTotalsTableShown = section.IsTotalsTableShown
                };

                var newSectionId = await _invoiceSectionRepository.InsertAsync(newSection);
                sectionMapping[section.Id] = newSectionId;
            }

            // 7. Copy lines with section mapping and recalculate financials
            var sourceLines = await _invoiceLineRepository.GetByInvoiceIdAsync(sourceInvoiceId);
            decimal subtotal = 0;
            decimal taxAmount = 0;
            decimal adjustmentAmount = 0;

            foreach (var line in sourceLines)
            {
                // Map section
                int? newSectionId = null;
                if (line.InvoiceSectionId.HasValue && sectionMapping.ContainsKey(line.InvoiceSectionId.Value))
                {
                    newSectionId = sectionMapping[line.InvoiceSectionId.Value];
                }

                decimal lineTotal;

                if (line.IsAdjustmentLine)
                {
                    // Adjustment lines: copy LineTotal as-is (will be recalculated for percentage by totals recomputation)
                    lineTotal = line.LineTotal;
                    adjustmentAmount += lineTotal;
                }
                else
                {
                    // Compute discountedPrice based on DiscountType
                    decimal discountedPrice;
                    if (line.DiscountType == "Percentage")
                    {
                        discountedPrice = line.UnitPrice * (1 - line.Discount / 100m);
                    }
                    else // Fixed
                    {
                        discountedPrice = line.UnitPrice - line.Discount;
                    }

                    lineTotal = line.Quantity * discountedPrice;
                    subtotal += lineTotal;
                    taxAmount += Math.Round(lineTotal * line.VatRate / 100m, 2);
                }

                var newLine = new InvoiceLine
                {
                    InvoiceId = duplicateId,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    VatRate = line.VatRate,
                    Discount = line.Discount,
                    DiscountType = line.DiscountType,
                    CostPrice = line.CostPrice,
                    LineTotal = lineTotal,
                    SortOrder = line.SortOrder,
                    ReferenceUrl = line.ReferenceUrl,
                    Subtitle = line.Subtitle,
                    InvoiceSectionId = newSectionId,
                    IsAdjustmentLine = line.IsAdjustmentLine
                };

                await _invoiceLineRepository.InsertAsync(newLine);
            }

            // 8. Update duplicate with computed totals
            var totalAmount = subtotal + adjustmentAmount + taxAmount;
            duplicate.Subtotal = subtotal;
            duplicate.TaxAmount = taxAmount;
            duplicate.TotalAmount = totalAmount;
            duplicate.UpdatedAtUtc = DateTime.UtcNow;

            await _invoiceRepository.UpdateAsync(duplicate);

            // NOTE: We don't call RecomputeAndUpdateTotalsAsync/RecalculateQuotationTotalsAsync here because:
            // 1. We're already inside a transaction (calling it would nest transactions)
            // 2. Lines are exact copies, so inline computation produces correct results
            // 3. For percentage adjustments, the copied LineTotal is valid since the subtotal is identical

            // 9. Write audit log
            var auditLog = new AuditLog
            {
                BusinessId = businessId,
                UserId = userId,
                Action = "Duplicated",
                TableName = "Invoice",
                RecordId = duplicateId.ToString(),
                OldValues = null,
                NewValues = $"Duplicated from Invoice {sourceInvoiceId}",
                Timestamp = DateTime.UtcNow
            };
            await _auditLogRepository.InsertAsync(auditLog);

            // 10. Commit transaction
            await transaction.CommitAsync();

            return duplicate;
        }
        catch(Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Quotation> DuplicateQuotationAsync(int sourceQuotationId, string userId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // 1. Validate source exists and belongs to current business
        var source = await _quotationRepository.GetByIdAndBusinessIdAsync(sourceQuotationId, businessId);
        if (source == null)
        {
            throw new InvalidOperationException("Quotation not found");
        }

        // 2. Begin transaction
        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

        try
        {
            // 3. Generate next sequential quotation reference
            var nextNumber = await _quotationRepository.GetNextSequentialNumberAsync(businessId);
            var reference = $"QUO-{businessId}-{nextNumber:D5}";

            // 4. Calculate ValidUntil from validity gap (or null)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            DateOnly? validUntil = null;

            if (source.ValidUntil.HasValue)
            {
                var sourceCreatedDate = DateOnly.FromDateTime(source.CreatedAtUtc);
                var validityGap = source.ValidUntil.Value.DayNumber - sourceCreatedDate.DayNumber;
                validUntil = today.AddDays(validityGap);
            }

            // 5. Create new Quotation entity with Draft status
            var duplicate = new Quotation
            {
                BusinessId = businessId,
                CustomerId = source.CustomerId,
                QuotationStatusTypeId = 1,  // Draft
                Reference = reference,
                ValidUntil = validUntil,
                Subtotal = 0,
                TaxAmount = 0,
                TotalAmount = 0,
                Notes = source.Notes,
                IsGrandTotalShown = source.IsGrandTotalShown,
                QuotationContactId = null,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var duplicateId = await _quotationRepository.InsertAndReturnIdAsync(duplicate);
            duplicate.Id = duplicateId;

            // 6. Copy sections with ID mapping
            var sourceSections = await _proposalSectionRepository.GetByQuotationIdAsync(sourceQuotationId);
            var sectionMapping = new Dictionary<int, int>(); // old ProposalSectionId → new ProposalSectionId

            foreach (var section in sourceSections)
            {
                var newSection = new ProposalSection
                {
                    QuotationId = duplicateId,
                    Name = section.Name,
                    SortOrder = section.SortOrder,
                    ColumnConfiguration = section.ColumnConfiguration,
                    SectionType = section.SectionType,
                    Description = section.Description,
                    Notes = section.Notes,
                    IsEmphasized = section.IsEmphasized,
                    AccentColor = section.AccentColor,
                    Label = section.Label,
                    IsTotalsTableShown = section.IsTotalsTableShown
                };

                var newSectionId = await _proposalSectionRepository.InsertAndReturnIdAsync(newSection);
                sectionMapping[section.Id] = newSectionId;
            }

            // 7. Copy lines with section mapping and recalculate financials
            var sourceLines = await _quotationLineRepository.GetByQuotationIdAsync(sourceQuotationId);
            decimal subtotal = 0;
            decimal taxAmount = 0;
            decimal adjustmentAmount = 0;

            foreach (var line in sourceLines)
            {
                // Map section
                int? newSectionId = null;
                if (line.ProposalSectionId.HasValue && sectionMapping.ContainsKey(line.ProposalSectionId.Value))
                {
                    newSectionId = sectionMapping[line.ProposalSectionId.Value];
                }

                decimal lineTotal;

                if (line.IsAdjustmentLine)
                {
                    // Adjustment lines: copy LineTotal as-is
                    lineTotal = line.LineTotal;
                    adjustmentAmount += lineTotal;
                }
                else
                {
                    // Compute discountedPrice based on DiscountType
                    decimal discountedPrice;
                    if (line.DiscountType == "Percentage")
                    {
                        discountedPrice = line.UnitPrice * (1 - line.Discount / 100m);
                    }
                    else // Fixed
                    {
                        discountedPrice = line.UnitPrice - line.Discount;
                    }

                    lineTotal = line.Quantity * discountedPrice;
                    subtotal += lineTotal;
                    taxAmount += Math.Round(lineTotal * line.VatRate / 100m, 2);
                }

                var newLine = new QuotationLine
                {
                    QuotationId = duplicateId,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    VatRate = line.VatRate,
                    Discount = line.Discount,
                    DiscountType = line.DiscountType,
                    CostPrice = line.CostPrice,
                    LineTotal = lineTotal,
                    SortOrder = line.SortOrder,
                    ReferenceUrl = line.ReferenceUrl,
                    Subtitle = line.Subtitle,
                    ProposalSectionId = newSectionId,
                    IsAdjustmentLine = line.IsAdjustmentLine
                };

                await _quotationLineRepository.InsertAsync(newLine);
            }

            // 8. Update duplicate with computed totals
            var totalAmount = subtotal + adjustmentAmount + taxAmount;
            duplicate.Subtotal = subtotal;
            duplicate.TaxAmount = taxAmount;
            duplicate.TotalAmount = totalAmount;
            duplicate.UpdatedAtUtc = DateTime.UtcNow;

            await _quotationRepository.UpdateAsync(duplicate);

            // NOTE: We don't call RecomputeAndUpdateTotalsAsync/RecalculateQuotationTotalsAsync here because:
            // 1. We're already inside a transaction (calling it would nest transactions)
            // 2. Lines are exact copies, so inline computation produces correct results
            // 3. For percentage adjustments, the copied LineTotal is valid since the subtotal is identical

            // 9. Write audit log
            var auditLog = new AuditLog
            {
                BusinessId = businessId,
                UserId = userId,
                Action = "Duplicated",
                TableName = "Quotation",
                RecordId = duplicateId.ToString(),
                OldValues = null,
                NewValues = $"Duplicated from Quotation {sourceQuotationId}",
                Timestamp = DateTime.UtcNow
            };
            await _auditLogRepository.InsertAsync(auditLog);

            // 10. Commit transaction
            await transaction.CommitAsync();

            return duplicate;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
