using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using System.Security.Claims;

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
    private readonly VatSubmissionPeriodRepository _vatSubmissionPeriodRepository;
    private readonly VatSubmissionRepository _vatSubmissionRepository;
    private readonly PortalDbContext _portalDbContext;
    private readonly IProductService _productService;
    private readonly ProductRepository _productRepository;
    private readonly ProductPriceTierRepository _productPriceTierRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<InvoiceService> _logger;

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
        VatSubmissionPeriodRepository vatSubmissionPeriodRepository,
        VatSubmissionRepository vatSubmissionRepository,
        PortalDbContext portalDbContext,
        IProductService productService,
        ProductRepository productRepository,
        ProductPriceTierRepository productPriceTierRepository,
        IHttpContextAccessor httpContextAccessor,
        ILogger<InvoiceService> logger)
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
        _vatSubmissionPeriodRepository = vatSubmissionPeriodRepository;
        _vatSubmissionRepository = vatSubmissionRepository;
        _portalDbContext = portalDbContext;
        _productService = productService;
        _productRepository = productRepository;
        _productPriceTierRepository = productPriceTierRepository;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
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
            var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow);

            // Assign VAT submission period before insert
            var vatSubmissionPeriodId = await AssignVatPeriodAsync(businessId, invoiceDate);

            var invoice = new Invoice
            {
                BusinessId = businessId,
                CustomerId = quotation.CustomerId,
                QuotationId = quotationId,
                InvoiceStatusTypeId = 1,           // Draft
                InvoiceFinancialStatusTypeId = 1,  // Unpaid
                InvoiceNumber = invoiceNumber,
                InvoiceDate = invoiceDate,
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                Subtotal = 0,
                TaxAmount = 0,
                TotalAmount = 0,
                CurrencyCode = "EUR",
                Notes = quotation.Notes,
                IsGrandTotalShown = quotation.IsGrandTotalShown,
                VatSubmissionPeriodId = vatSubmissionPeriodId,
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

                // Resolve ProductTypeId from product (snapshot)
                int? productTypeId = null;
                if (!string.IsNullOrEmpty(line.ProductCode))
                {
                    var product = await _productRepository.GetByProductCodeAndBusinessIdAsync(
                        line.ProductCode, businessId);
                    productTypeId = product?.ProductTypeId;
                }

                // Enforce RC invariant during conversion
                var invoiceVatRate = line.IsReverseCharge ? 0m : line.VatRate;

                var invoiceLine = new InvoiceLine
                {
                    InvoiceId = invoiceId,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    VatRate = invoiceVatRate,
                    Discount = line.Discount,
                    DiscountType = line.DiscountType,
                    CostPrice = line.CostPrice,
                    LineTotal = line.LineTotal,
                    SortOrder = line.SortOrder,
                    ReferenceUrl = line.ReferenceUrl,
                    Subtitle = line.Subtitle,
                    InvoiceSectionId = invoiceSectionId,
                    ProductCode = line.ProductCode,
                    IsReverseCharge = line.IsReverseCharge,
                    ProductTypeId = productTypeId,
                    ProductPriceTierId = line.ProductPriceTierId,
                    PriceTierName = line.PriceTierName
                };

                await _invoiceLineRepository.InsertAsync(invoiceLine);
            }

            // 9. Compute totals (respecting RC lines with VatRate=0)
            var invoiceLines = await _invoiceLineRepository.GetByInvoiceIdAsync(invoiceId);
            var subtotal = invoiceLines.Sum(l => l.LineTotal);
            var taxAmount = Math.Round(invoiceLines.Sum(l => l.LineTotal * l.VatRate / 100m), 2);
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

            // 14. Auto-populate product catalog after successful persistence (outside transaction)
            foreach (var line in quotationLines)
            {
                await _productService.AutoPopulateFromLineItemAsync(
                    line.ProductCode,
                    line.Description,
                    line.UnitPrice,
                    line.VatRate,
                    userId);
            }

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

        // Assign VAT submission period before insert
        var vatSubmissionPeriodId = await AssignVatPeriodAsync(businessId, invoiceDate);

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
            VatSubmissionPeriodId = vatSubmissionPeriodId,
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
        var createInvoiceUserId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? string.Empty;

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

            // Resolve PriceTierName snapshot if a valid, active tier is referenced
            int? productPriceTierId = lineDto.ProductPriceTierId;
            string? priceTierName = null;
            if (productPriceTierId.HasValue)
            {
                var tier = await _productPriceTierRepository.GetByIdAsync(productPriceTierId.Value);
                if (tier != null && tier.IsActive)
                {
                    priceTierName = tier.TierName;
                }
                else
                {
                    // Tier not found or inactive — clear the reference (don't persist stale/invalid tier ID)
                    productPriceTierId = null;
                }
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
                InvoiceSectionId = invoiceSectionId,
                ProductCode = lineDto.ProductCode,
                ProductPriceTierId = productPriceTierId,
                PriceTierName = priceTierName
            };

            await _invoiceLineRepository.InsertAsync(invoiceLine);

            // Auto-populate product catalog after line item persistence
            await _productService.AutoPopulateFromLineItemAsync(
                lineDto.ProductCode,
                lineDto.Description,
                lineDto.UnitPrice,
                lineDto.VatRate,
                createInvoiceUserId);

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

    public async Task<PagedResult<InvoiceListDto>> GetInvoicesPagedAsync(
        int? statusFilter = null,
        int? financialStatusFilter = null,
        int? customerFilter = null,
        string? searchTerm = null,
        int page = 1,
        int pageSize = 15,
        int? vatPeriodId = null)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Clamp page to minimum 1
        if (page < 1) page = 1;

        // Clamp pageSize to a sensible range
        if (pageSize < 1) pageSize = 15;

        int offset = (page - 1) * pageSize;

        var (items, totalCount) = await _invoiceRepository.GetPagedByBusinessIdAsync(
            businessId, statusFilter, financialStatusFilter, customerFilter, searchTerm, offset, pageSize, vatPeriodId);

        return new PagedResult<InvoiceListDto>
        {
            Items = items,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<List<InvoiceListDto>> GetInvoicesFilteredAsync(
        int? statusFilter = null,
        int? financialStatusFilter = null,
        int? customerFilter = null,
        string? searchTerm = null,
        int? vatPeriodId = null)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        return await _invoiceRepository.GetAllFilteredByBusinessIdAsync(
            businessId, statusFilter, financialStatusFilter, customerFilter, searchTerm, vatPeriodId);
    }

    public async Task<Invoice?> GetInvoiceByIdAsync(int id)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        return await _invoiceRepository.GetByIdAndBusinessIdAsync(id, businessId);
    }

    public async Task<Invoice?> GetInvoiceByIdAsync(int id, int businessId)
    {
        return await _invoiceRepository.GetByIdAndBusinessIdUnfilteredAsync(id, businessId);
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
        string? notes, bool isGrandTotalShown, bool isQuotationReferenceShown, string? invoiceNumber = null)
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
        invoice.IsQuotationReferenceShown = isQuotationReferenceShown;
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            invoice.InvoiceNumber = invoiceNumber;
        }
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
        decimal? costPrice, string? referenceUrl, string? subtitle, int? invoiceSectionId,
        string? productCode = null, bool isReverseCharge = false, int? productPriceTierId = null)
    {
        // Validation: Reverse Charge Invariant
        if (isReverseCharge && vatRate > 0)
            throw new ArgumentException("Reverse charge lines require 0% VAT");

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

        // Resolve PriceTierName snapshot if ProductPriceTierId is provided
        string? priceTierName = null;
        if (productPriceTierId.HasValue)
        {
            var tier = await _productPriceTierRepository.GetByIdAsync(productPriceTierId.Value);
            if (tier != null && tier.IsActive)
            {
                priceTierName = tier.TierName;
            }
            else
            {
                // Tier not found or inactive — clear the reference (don't persist stale/invalid tier ID)
                productPriceTierId = null;
            }
        }

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
            InvoiceSectionId = invoiceSectionId,
            ProductCode = productCode,
            IsReverseCharge = isReverseCharge,
            ProductPriceTierId = productPriceTierId,
            PriceTierName = priceTierName
        };

        var lineId = await _invoiceLineRepository.InsertAsync(invoiceLine);
        invoiceLine.Id = lineId;

        // Auto-populate product catalog after line item persistence
        var addLineUserId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? string.Empty;
        await _productService.AutoPopulateFromLineItemAsync(
            productCode,
            description,
            unitPrice,
            vatRate,
            addLineUserId);

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
        decimal? costPrice, string? referenceUrl, string? subtitle, int? invoiceSectionId,
        bool isReverseCharge = false)
    {
        // Validation: Reverse Charge Invariant
        if (isReverseCharge && vatRate > 0)
            throw new ArgumentException("Reverse charge lines require 0% VAT");

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
        line.IsReverseCharge = isReverseCharge;

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

    public async Task<ServiceResult> ReassignVatPeriodAsync(int invoiceId, int targetPeriodId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // 1. Invoice exists and belongs to current business
        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(invoiceId, businessId);
        if (invoice == null)
            return ServiceResult.Fail("Invoice not found.");

        // 2. Invoice is not deleted
        if (invoice.IsDeleted)
            return ServiceResult.Fail("Cannot reassign a deleted invoice.");

        // 3. Target period exists
        var targetPeriod = await _vatSubmissionPeriodRepository.GetByIdAndBusinessIdAsync(targetPeriodId, businessId);
        if (targetPeriod == null)
            return ServiceResult.Fail("Target VAT period not found.");

        // 4. Target period belongs to same business
        if (targetPeriod.BusinessId != invoice.BusinessId)
            return ServiceResult.Fail("Target period does not belong to this business.");

        // 5. Target period is not already submitted
        var submission = await _vatSubmissionRepository.GetByPeriodIdAndBusinessIdAsync(targetPeriodId, businessId);
        if (submission != null && submission.IsSubmitted)
            return ServiceResult.Fail("Cannot reassign to a period that has already been submitted.");

        // 6. Invoice is not already assigned to target period
        if (invoice.VatSubmissionPeriodId == targetPeriodId)
            return ServiceResult.Fail("Invoice is already assigned to this period.");

        // 7. Execute update + audit log
        var oldPeriodId = invoice.VatSubmissionPeriodId;
        await _invoiceRepository.UpdateVatPeriodAsync(invoiceId, targetPeriodId);

        var auditLog = new AuditLog
        {
            BusinessId = businessId,
            UserId = null,
            Action = "VatPeriodReassigned",
            TableName = "Invoice",
            RecordId = invoiceId.ToString(),
            OldValues = oldPeriodId.HasValue ? $"VatSubmissionPeriodId={oldPeriodId.Value}" : "VatSubmissionPeriodId=NULL",
            NewValues = $"VatSubmissionPeriodId={targetPeriodId}",
            Timestamp = DateTime.UtcNow
        };
        await _auditLogRepository.InsertAsync(auditLog);

        return ServiceResult.Ok();
    }

    /// <summary>
    /// Determines the appropriate VAT submission period for an invoice based on its date.
    /// Finds the natural period by date-range match, checks if it's submitted,
    /// and cascades forward to the first unsubmitted period if necessary.
    /// </summary>
    private async Task<int?> AssignVatPeriodAsync(int businessId, DateOnly invoiceDate)
    {
        // 1. Find the natural period (date-range match for businessId)
        var naturalPeriod = await _vatSubmissionPeriodRepository.GetByDateAndBusinessIdAsync(invoiceDate, businessId);

        // 2. If no period found → return null
        if (naturalPeriod == null)
            return null;

        // 3. Check if natural period has a submitted VatSubmission
        var submission = await _vatSubmissionRepository.GetByPeriodIdAndBusinessIdAsync(naturalPeriod.Id, businessId);

        // 4. If not submitted (or no submission exists) → return natural period's Id
        if (submission == null || !submission.IsSubmitted)
            return naturalPeriod.Id;

        // 5. If submitted → search forward for first unsubmitted period
        var unsubmittedPeriods = await _vatSubmissionPeriodRepository.GetUnsubmittedPeriodsFromAsync(
            businessId, naturalPeriod.PeriodEndDate.AddDays(1));

        // 6. If no unsubmitted period found → return null
        if (unsubmittedPeriods.Count == 0)
            return null;

        return unsubmittedPeriods[0].Id;
    }

    public async Task<ServiceResult<ReassignmentImpactDto>> GetReassignmentImpactAsync(int invoiceId, int targetPeriodId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // 1. Get the invoice
        var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(invoiceId, businessId);
        if (invoice == null)
            return ServiceResult<ReassignmentImpactDto>.Fail("Invoice not found.");

        // 2. Get the source period (invoice's current VatSubmissionPeriodId)
        if (!invoice.VatSubmissionPeriodId.HasValue)
            return ServiceResult<ReassignmentImpactDto>.Fail("Invoice is not assigned to a VAT period.");

        var sourcePeriod = await _vatSubmissionPeriodRepository.GetByIdAndBusinessIdAsync(
            invoice.VatSubmissionPeriodId.Value, businessId);
        if (sourcePeriod == null)
            return ServiceResult<ReassignmentImpactDto>.Fail("Source VAT period not found.");

        // 3. Get the target period
        var targetPeriod = await _vatSubmissionPeriodRepository.GetByIdAndBusinessIdAsync(targetPeriodId, businessId);
        if (targetPeriod == null)
            return ServiceResult<ReassignmentImpactDto>.Fail("Target VAT period not found.");

        // 4. Get current Output VAT totals for both periods from VatSubmission records
        var sourceSubmission = await _vatSubmissionRepository.GetByPeriodIdAndBusinessIdAsync(
            sourcePeriod.Id, businessId);
        var targetSubmission = await _vatSubmissionRepository.GetByPeriodIdAndBusinessIdAsync(
            targetPeriod.Id, businessId);

        var sourceCurrentOutputVat = sourceSubmission?.TotalOutputVat ?? 0m;
        var targetCurrentOutputVat = targetSubmission?.TotalOutputVat ?? 0m;

        // 5. Compute projected Output VAT
        var sourceProjected = sourceCurrentOutputVat - invoice.TaxAmount;
        var targetProjected = targetCurrentOutputVat + invoice.TaxAmount;

        // 6. Get currency symbol from business profile
        var profile = await _portalDbContext.BusinessProfiles
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);
        var currencySymbol = profile?.CurrencySymbol ?? "€";

        // 7. Return ReassignmentImpactDto
        var impact = new ReassignmentImpactDto
        {
            InvoiceNumber = invoice.InvoiceNumber,
            TaxAmount = invoice.TaxAmount,
            SourcePeriodLabel = sourcePeriod.PeriodLabel ?? $"{sourcePeriod.PeriodStartDate:MMM yyyy}",
            TargetPeriodLabel = targetPeriod.PeriodLabel ?? $"{targetPeriod.PeriodStartDate:MMM yyyy}",
            SourcePeriodProjectedOutputVat = sourceProjected,
            TargetPeriodProjectedOutputVat = targetProjected,
            CurrencySymbol = currencySymbol
        };

        return ServiceResult<ReassignmentImpactDto>.Ok(impact);
    }

    public async Task<List<VatPeriodOptionDto>> GetUnsubmittedPeriodsAsync(int invoiceId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var periods = await _vatSubmissionPeriodRepository.GetUnsubmittedPeriodsFromAsync(businessId, DateOnly.MinValue);

        return periods
            .Select(p => new VatPeriodOptionDto { Id = p.Id, PeriodLabel = p.PeriodLabel })
            .ToList();
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
