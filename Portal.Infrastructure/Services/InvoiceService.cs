using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for invoice management including quotation conversion, standalone creation,
/// lifecycle transitions, pricing calculations, and audit logging.
/// </summary>
public class InvoiceService : IInvoiceService
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly InvoiceLineRepository _invoiceLineRepository;
    private readonly InvoiceSectionRepository _invoiceSectionRepository;
    private readonly QuotationRepository _quotationRepository;
    private readonly QuotationLineRepository _quotationLineRepository;
    private readonly ProposalSectionRepository _proposalSectionRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly PortalDbContext _portalDbContext;

    private static readonly Dictionary<int, List<int>> ValidTransitionsMap = new()
    {
        { 1, new List<int> { 2, 3 } },  // Draft → Issued, Cancelled
        { 2, new List<int> { 3 } },     // Issued → Cancelled
    };

    private static readonly Dictionary<int, string> StatusNames = new()
    {
        { 1, "Draft" },
        { 2, "Issued" },
        { 3, "Cancelled" }
    };

    public InvoiceService(
        ICurrentTenantService currentTenantService,
        InvoiceRepository invoiceRepository,
        InvoiceLineRepository invoiceLineRepository,
        InvoiceSectionRepository invoiceSectionRepository,
        QuotationRepository quotationRepository,
        QuotationLineRepository quotationLineRepository,
        ProposalSectionRepository proposalSectionRepository,
        CustomerRepository customerRepository,
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
        _customerRepository = customerRepository;
        _auditLogRepository = auditLogRepository;
        _portalDbContext = portalDbContext;
    }

    public async Task<Invoice> ConvertFromQuotationAsync(int quotationId, string userId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // 1. Fetch and validate quotation
        var quotation = await _quotationRepository.GetByIdAndBusinessIdAsync(quotationId, businessId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Quotation not found");
        }

        // Validate quotation is not already converted (4) or archived (5)
        if (quotation.QuotationStatusTypeId == 4)
        {
            throw new InvalidOperationException("Quotation has already been converted to an invoice");
        }
        if (quotation.QuotationStatusTypeId == 5)
        {
            throw new InvalidOperationException("Archived quotations cannot be converted");
        }

        // 2. Validate has lines
        var quotationLines = await _quotationLineRepository.GetByQuotationIdAsync(quotationId);
        if (quotationLines.Count == 0)
        {
            throw new InvalidOperationException("Quotation must have at least one line item to convert");
        }

        // 3. Check no existing invoice for quotationId
        var existingInvoice = await _invoiceRepository.GetByQuotationIdAsync(quotationId);
        if (existingInvoice != null)
        {
            throw new InvalidOperationException("Quotation has already been converted to an invoice");
        }

        // 4. Begin transaction
        using var transaction = await _portalDbContext.Database.BeginTransactionAsync();

        try
        {
            // 5. Generate invoice number
            var nextNumber = await _invoiceRepository.GetNextSequentialNumberAsync(businessId);
            var invoiceNumber = $"INV-{businessId}-{nextNumber:D5}";

            // 6. Insert invoice with Draft (1) and Unpaid (1)
            var invoice = new Invoice
            {
                BusinessId = businessId,
                CustomerId = quotation.CustomerId,
                QuotationId = quotationId,
                InvoiceStatusTypeId = 1,           // Draft
                InvoiceFinancialStatusTypeId = 1,  // Unpaid
                InvoiceNumber = invoiceNumber,
                InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                Subtotal = 0,
                TaxAmount = 0,
                TotalAmount = 0,
                CurrencyCode = "EUR",
                Notes = quotation.Notes,
                IsGrandTotalShown = quotation.IsGrandTotalShown,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var invoiceId = await _invoiceRepository.InsertAsync(invoice);
            invoice.Id = invoiceId;

            // 7. Copy ProposalSections → InvoiceSections (preserving all fields)
            var proposalSections = await _proposalSectionRepository.GetByQuotationIdAsync(quotationId);
            var sectionMapping = new Dictionary<int, int>(); // old ProposalSectionId → new InvoiceSectionId

            foreach (var section in proposalSections)
            {
                var invoiceSection = new InvoiceSection
                {
                    InvoiceId = invoiceId,
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

                var newSectionId = await _invoiceSectionRepository.InsertAsync(invoiceSection);
                sectionMapping[section.Id] = newSectionId;
            }

            // 8. Copy QuotationLines → InvoiceLines with section mapping
            foreach (var line in quotationLines)
            {
                int? invoiceSectionId = null;
                if (line.ProposalSectionId.HasValue && sectionMapping.ContainsKey(line.ProposalSectionId.Value))
                {
                    invoiceSectionId = sectionMapping[line.ProposalSectionId.Value];
                }

                var invoiceLine = new InvoiceLine
                {
                    InvoiceId = invoiceId,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    VatRate = line.VatRate,
                    Discount = line.Discount,
                    DiscountType = line.DiscountType,
                    CostPrice = line.CostPrice,
                    LineTotal = line.LineTotal,
                    SortOrder = line.SortOrder,
                    ReferenceUrl = line.ReferenceUrl,
                    Subtitle = line.Subtitle,
                    InvoiceSectionId = invoiceSectionId
                };

                await _invoiceLineRepository.InsertAsync(invoiceLine);
            }

            // 9. Compute totals
            var subtotal = quotationLines.Sum(l => l.LineTotal);
            var taxAmount = Math.Round(quotationLines.Sum(l => l.LineTotal * l.VatRate / 100m), 2);
            var totalAmount = subtotal + taxAmount;

            // 10. Update invoice with computed totals
            invoice.Subtotal = subtotal;
            invoice.TaxAmount = taxAmount;
            invoice.TotalAmount = totalAmount;
            invoice.UpdatedAtUtc = DateTime.UtcNow;

            await _invoiceRepository.UpdateAsync(invoice);

            // 11. Update quotation status to 4 (Converted)
            quotation.QuotationStatusTypeId = 4;
            quotation.UpdatedAtUtc = DateTime.UtcNow;
            await _quotationRepository.UpdateAsync(quotation);

            // 12. Write audit logs
            // Audit log for Invoice Created
            var invoiceAuditLog = new AuditLog
            {
                BusinessId = businessId,
                UserId = userId,
                Action = "Created",
                TableName = "Invoice",
                RecordId = invoiceId.ToString(),
                OldValues = null,
                NewValues = $"Converted from Quotation {quotationId}",
                Timestamp = DateTime.UtcNow
            };
            await _auditLogRepository.InsertAsync(invoiceAuditLog);

            // Audit log for Quotation Converted
            var quotationAuditLog = new AuditLog
            {
                BusinessId = businessId,
                UserId = userId,
                Action = "Converted",
                TableName = "Quotation",
                RecordId = quotationId.ToString(),
                OldValues = null,
                NewValues = $"Invoice {invoiceId}",
                Timestamp = DateTime.UtcNow
            };
            await _auditLogRepository.InsertAsync(quotationAuditLog);

            // 13. Commit transaction
            await transaction.CommitAsync();

            return invoice;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Invoice> CreateInvoiceAsync(int customerId, DateOnly invoiceDate, DateOnly dueDate,
        string? notes, bool isGrandTotalShown, List<CreateInvoiceLineDto> lines,
        List<CreateInvoiceSectionDto>? sections)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Validate required fields
        if (customerId <= 0)
            throw new ArgumentException("CustomerId is required and must be greater than zero");

        if (lines == null || lines.Count == 0)
            throw new ArgumentException("At least one line item is required");

        // Verify customer belongs to business
        var customer = await _customerRepository.GetByIdAndBusinessIdAsync(customerId, businessId);
        if (customer == null)
            throw new ArgumentException("Customer not found or does not belong to this business");

        // Generate invoice number
        var nextNumber = await _invoiceRepository.GetNextSequentialNumberAsync(businessId);
        var invoiceNumber = $"INV-{businessId}-{nextNumber:D5}";

        // Create invoice entity
        var invoice = new Invoice
        {
            BusinessId = businessId,
            CustomerId = customerId,
            QuotationId = null,
            InvoiceStatusTypeId = 1,           // Draft
            InvoiceFinancialStatusTypeId = 1,  // Unpaid
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            Subtotal = 0,
            TaxAmount = 0,
            TotalAmount = 0,
            CurrencyCode = "EUR",
            Notes = notes,
            IsGrandTotalShown = isGrandTotalShown,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var invoiceId = await _invoiceRepository.InsertAsync(invoice);
        invoice.Id = invoiceId;

        // Insert sections if provided, and build section index mapping
        var sectionIndexToIdMap = new Dictionary<int, int>();
        if (sections != null && sections.Count > 0)
        {
            for (int i = 0; i < sections.Count; i++)
            {
                var sectionDto = sections[i];
                var invoiceSection = new InvoiceSection
                {
                    InvoiceId = invoiceId,
                    Name = sectionDto.Name,
                    SortOrder = i + 1,
                    ColumnConfiguration = sectionDto.ColumnConfiguration,
                    SectionType = sectionDto.SectionType,
                    Description = sectionDto.Description,
                    Notes = sectionDto.Notes,
                    IsEmphasized = sectionDto.IsEmphasized,
                    AccentColor = sectionDto.AccentColor,
                    Label = sectionDto.Label,
                    IsTotalsTableShown = sectionDto.IsTotalsTableShown
                };

                var sectionId = await _invoiceSectionRepository.InsertAsync(invoiceSection);
                sectionIndexToIdMap[i] = sectionId;
            }
        }

        // Insert invoice lines, computing LineTotal for each
        decimal subtotal = 0;
        decimal taxAmount = 0;
        int sortOrder = 1;

        foreach (var lineDto in lines)
        {
            // Compute LineTotal: apply discount first
            decimal lineTotal;
            var baseAmount = lineDto.Quantity * lineDto.UnitPrice;

            if (lineDto.DiscountType == "Percentage")
            {
                lineTotal = baseAmount * (1 - lineDto.Discount / 100m);
            }
            else // Fixed
            {
                lineTotal = baseAmount - lineDto.Discount;
            }

            // Map SectionIndex to actual InvoiceSectionId
            int? invoiceSectionId = null;
            if (lineDto.SectionIndex.HasValue && sectionIndexToIdMap.ContainsKey(lineDto.SectionIndex.Value))
            {
                invoiceSectionId = sectionIndexToIdMap[lineDto.SectionIndex.Value];
            }

            var invoiceLine = new InvoiceLine
            {
                InvoiceId = invoiceId,
                Description = lineDto.Description,
                Quantity = lineDto.Quantity,
                UnitPrice = lineDto.UnitPrice,
                VatRate = lineDto.VatRate,
                Discount = lineDto.Discount,
                DiscountType = lineDto.DiscountType,
                CostPrice = lineDto.CostPrice,
                LineTotal = lineTotal,
                SortOrder = sortOrder++,
                ReferenceUrl = lineDto.ReferenceUrl,
                Subtitle = lineDto.Subtitle,
                InvoiceSectionId = invoiceSectionId
            };

            await _invoiceLineRepository.InsertAsync(invoiceLine);

            subtotal += lineTotal;
            taxAmount += lineTotal * lineDto.VatRate / 100m;
        }

        // Round tax amount
        taxAmount = Math.Round(taxAmount, 2);
        var totalAmount = subtotal + taxAmount;

        // Update invoice with computed totals
        invoice.Subtotal = subtotal;
        invoice.TaxAmount = taxAmount;
        invoice.TotalAmount = totalAmount;
        invoice.UpdatedAtUtc = DateTime.UtcNow;

        await _invoiceRepository.UpdateAsync(invoice);

        // Write audit log
        var auditLog = new AuditLog
        {
            BusinessId = businessId,
            UserId = null,
            Action = "Created",
            TableName = "Invoice",
            RecordId = invoiceId.ToString(),
            OldValues = null,
            NewValues = $"Standalone invoice {invoiceNumber}",
            Timestamp = DateTime.UtcNow
        };
        await _auditLogRepository.InsertAsync(auditLog);

        return invoice;
    }

    public async Task<List<InvoiceListDto>> GetInvoicesAsync(int? statusFilter = null,
        int? financialStatusFilter = null, int? customerFilter = null)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var invoices = await _invoiceRepository.GetAllByBusinessIdAsync(businessId);

        // Apply optional filters in memory
        if (statusFilter.HasValue)
        {
            invoices = invoices.Where(i => i.InvoiceStatusTypeId == statusFilter.Value).ToList();
        }

        if (financialStatusFilter.HasValue)
        {
            invoices = invoices.Where(i => i.InvoiceFinancialStatusTypeId == financialStatusFilter.Value).ToList();
        }

        if (customerFilter.HasValue)
        {
            invoices = invoices.Where(i => i.CustomerId == customerFilter.Value).ToList();
        }

        return invoices;
    }

    public async Task<Invoice?> GetInvoiceByIdAsync(int id)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        return await _invoiceRepository.GetByIdAndBusinessIdAsync(id, businessId);
    }

    public async Task<List<InvoiceLine>> GetInvoiceLinesAsync(int invoiceId)
    {
        return await _invoiceLineRepository.GetByInvoiceIdAsync(invoiceId);
    }

    public async Task TransitionStatusAsync(int invoiceId, int newStatusId, string userId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(invoiceId, businessId);
        if (invoice == null)
        {
            throw new InvalidOperationException("Invoice not found");
        }

        var currentStatusId = invoice.InvoiceStatusTypeId;

        // Validate transition
        if (!ValidTransitionsMap.ContainsKey(currentStatusId) ||
            !ValidTransitionsMap[currentStatusId].Contains(newStatusId))
        {
            throw new InvalidOperationException(
                $"Cannot transition from {StatusNames[currentStatusId]} to {StatusNames[newStatusId]}");
        }

        var previousStatusName = StatusNames[currentStatusId];
        var newStatusName = StatusNames[newStatusId];

        // Update invoice status
        invoice.InvoiceStatusTypeId = newStatusId;
        invoice.UpdatedAtUtc = DateTime.UtcNow;

        await _invoiceRepository.UpdateAsync(invoice);

        // Write audit log
        var auditLog = new AuditLog
        {
            BusinessId = businessId,
            UserId = userId,
            Action = "StatusChanged",
            TableName = "Invoice",
            RecordId = invoiceId.ToString(),
            OldValues = previousStatusName,
            NewValues = newStatusName,
            Timestamp = DateTime.UtcNow
        };
        await _auditLogRepository.InsertAsync(auditLog);
    }

    public async Task UpdateInvoiceAsync(int invoiceId, int customerId, DateOnly invoiceDate, DateOnly dueDate,
        string? notes, bool isGrandTotalShown)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(invoiceId, businessId);
        if (invoice == null)
        {
            throw new InvalidOperationException("Invoice not found");
        }

        if (invoice.InvoiceStatusTypeId != 1)
        {
            throw new InvalidOperationException("Invoice can only be edited in Draft status");
        }

        // Verify customer belongs to business
        var customer = await _customerRepository.GetByIdAndBusinessIdAsync(customerId, businessId);
        if (customer == null)
        {
            throw new ArgumentException("Customer not found or does not belong to this business");
        }

        // Capture old values for audit
        var oldValues = $"CustomerId={invoice.CustomerId}, InvoiceDate={invoice.InvoiceDate}, DueDate={invoice.DueDate}, Notes={invoice.Notes ?? "(null)"}, IsGrandTotalShown={invoice.IsGrandTotalShown}";

        invoice.CustomerId = customerId;
        invoice.InvoiceDate = invoiceDate;
        invoice.DueDate = dueDate;
        invoice.Notes = notes;
        invoice.IsGrandTotalShown = isGrandTotalShown;
        invoice.UpdatedAtUtc = DateTime.UtcNow;

        await _invoiceRepository.UpdateAsync(invoice);

        // Write audit log
        var newValues = $"CustomerId={customerId}, InvoiceDate={invoiceDate}, DueDate={dueDate}, Notes={notes ?? "(null)"}, IsGrandTotalShown={isGrandTotalShown}";
        var auditLog = new AuditLog
        {
            BusinessId = businessId,
            UserId = null,
            Action = "Updated",
            TableName = "Invoice",
            RecordId = invoiceId.ToString(),
            OldValues = oldValues,
            NewValues = newValues,
            Timestamp = DateTime.UtcNow
        };
        await _auditLogRepository.InsertAsync(auditLog);
    }

    public async Task<InvoiceLine> AddLineAsync(int invoiceId, string description, decimal quantity,
        decimal unitPrice, decimal vatRate, decimal discount, string discountType,
        decimal? costPrice, string? referenceUrl, string? subtitle, int? invoiceSectionId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(invoiceId, businessId);
        if (invoice == null)
        {
            throw new InvalidOperationException("Invoice not found");
        }

        if (invoice.InvoiceStatusTypeId != 1)
        {
            throw new InvalidOperationException("Invoice can only be edited in Draft status");
        }

        // Compute LineTotal
        var baseAmount = quantity * unitPrice;
        decimal lineTotal;

        if (discountType == "Percentage")
        {
            lineTotal = baseAmount * (1 - discount / 100m);
        }
        else // Fixed
        {
            lineTotal = baseAmount - discount;
        }

        // Determine next SortOrder
        var existingLines = await _invoiceLineRepository.GetByInvoiceIdAsync(invoiceId);
        var nextSortOrder = existingLines.Count > 0
            ? existingLines.Max(l => l.SortOrder) + 1
            : 1;

        // Insert line
        var invoiceLine = new InvoiceLine
        {
            InvoiceId = invoiceId,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            VatRate = vatRate,
            Discount = discount,
            DiscountType = discountType,
            CostPrice = costPrice,
            LineTotal = lineTotal,
            SortOrder = nextSortOrder,
            ReferenceUrl = referenceUrl,
            Subtitle = subtitle,
            InvoiceSectionId = invoiceSectionId
        };

        var lineId = await _invoiceLineRepository.InsertAsync(invoiceLine);
        invoiceLine.Id = lineId;

        // Recompute invoice totals
        await RecomputeAndUpdateTotalsAsync(invoiceId);

        // Write audit log
        var auditLog = new AuditLog
        {
            BusinessId = businessId,
            UserId = null,
            Action = "LineAdded",
            TableName = "Invoice",
            RecordId = invoiceId.ToString(),
            OldValues = null,
            NewValues = $"LineId={lineId}, Description={description}, Qty={quantity}, UnitPrice={unitPrice}, LineTotal={lineTotal}",
            Timestamp = DateTime.UtcNow
        };
        await _auditLogRepository.InsertAsync(auditLog);

        return invoiceLine;
    }

    public async Task UpdateLineAsync(int lineId, string description, decimal quantity,
        decimal unitPrice, decimal vatRate, decimal discount, string discountType,
        decimal? costPrice, string? referenceUrl, string? subtitle, int? invoiceSectionId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var line = await _invoiceLineRepository.GetByIdAsync(lineId);
        if (line == null)
        {
            throw new InvalidOperationException("Invoice line not found");
        }

        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(line.InvoiceId, businessId);
        if (invoice == null)
        {
            throw new InvalidOperationException("Invoice not found");
        }

        if (invoice.InvoiceStatusTypeId != 1)
        {
            throw new InvalidOperationException("Invoice can only be edited in Draft status");
        }

        // Capture old values for audit
        var oldValues = $"Description={line.Description}, Qty={line.Quantity}, UnitPrice={line.UnitPrice}, VatRate={line.VatRate}, Discount={line.Discount}, DiscountType={line.DiscountType}, LineTotal={line.LineTotal}";

        // Compute new LineTotal
        var baseAmount = quantity * unitPrice;
        decimal lineTotal;

        if (discountType == "Percentage")
        {
            lineTotal = baseAmount * (1 - discount / 100m);
        }
        else // Fixed
        {
            lineTotal = baseAmount - discount;
        }

        // Update line fields
        line.Description = description;
        line.Quantity = quantity;
        line.UnitPrice = unitPrice;
        line.VatRate = vatRate;
        line.Discount = discount;
        line.DiscountType = discountType;
        line.CostPrice = costPrice;
        line.LineTotal = lineTotal;
        line.ReferenceUrl = referenceUrl;
        line.Subtitle = subtitle;
        line.InvoiceSectionId = invoiceSectionId;

        await _invoiceLineRepository.UpdateAsync(line);

        // Recompute invoice totals
        await RecomputeAndUpdateTotalsAsync(line.InvoiceId);

        // Write audit log
        var newValues = $"Description={description}, Qty={quantity}, UnitPrice={unitPrice}, VatRate={vatRate}, Discount={discount}, DiscountType={discountType}, LineTotal={lineTotal}";
        var auditLog = new AuditLog
        {
            BusinessId = businessId,
            UserId = null,
            Action = "LineUpdated",
            TableName = "Invoice",
            RecordId = invoice.Id.ToString(),
            OldValues = oldValues,
            NewValues = newValues,
            Timestamp = DateTime.UtcNow
        };
        await _auditLogRepository.InsertAsync(auditLog);
    }

    public async Task RemoveLineAsync(int lineId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var line = await _invoiceLineRepository.GetByIdAsync(lineId);
        if (line == null)
        {
            throw new InvalidOperationException("Invoice line not found");
        }

        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(line.InvoiceId, businessId);
        if (invoice == null)
        {
            throw new InvalidOperationException("Invoice not found");
        }

        if (invoice.InvoiceStatusTypeId != 1)
        {
            throw new InvalidOperationException("Invoice can only be edited in Draft status");
        }

        // Delete line
        await _invoiceLineRepository.DeleteAsync(lineId);

        // Recompute invoice totals from remaining lines
        await RecomputeAndUpdateTotalsAsync(line.InvoiceId);

        // Write audit log
        var auditLog = new AuditLog
        {
            BusinessId = businessId,
            UserId = null,
            Action = "LineRemoved",
            TableName = "Invoice",
            RecordId = invoice.Id.ToString(),
            OldValues = $"LineId={lineId}, Description={line.Description}, Qty={line.Quantity}, UnitPrice={line.UnitPrice}, LineTotal={line.LineTotal}",
            NewValues = null,
            Timestamp = DateTime.UtcNow
        };
        await _auditLogRepository.InsertAsync(auditLog);
    }

    private async Task RecomputeAndUpdateTotalsAsync(int invoiceId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var lines = await _invoiceLineRepository.GetByInvoiceIdAsync(invoiceId);

        var subtotal = lines.Sum(l => l.LineTotal);
        var taxAmount = Math.Round(lines.Sum(l => l.LineTotal * l.VatRate / 100m), 2);
        var totalAmount = subtotal + taxAmount;

        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(invoiceId, businessId);
        if (invoice != null)
        {
            invoice.Subtotal = subtotal;
            invoice.TaxAmount = taxAmount;
            invoice.TotalAmount = totalAmount;
            invoice.UpdatedAtUtc = DateTime.UtcNow;

            await _invoiceRepository.UpdateAsync(invoice);
        }
    }
}
