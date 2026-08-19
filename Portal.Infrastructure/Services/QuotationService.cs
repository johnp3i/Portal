using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using System.Security.Claims;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for quotation management including lifecycle transitions, pricing calculations, and audit logging.
/// </summary>
public class QuotationService : IQuotationService
{
    private readonly QuotationRepository _quotationRepository;
    private readonly QuotationLineRepository _quotationLineRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly ProposalSectionRepository _sectionRepository;
    private readonly ProductPriceTierRepository _productPriceTierRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILineItemCatalogService _lineItemCatalogService;
    private readonly IProductService _productService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<QuotationService> _logger;

    private static readonly Dictionary<int, List<int>> ValidTransitionsMap = new()
    {
        { 1, new List<int> { 2, 5 } },  // Draft → Sent, Archived
        { 2, new List<int> { 3, 5 } },  // Sent → Accepted, Archived
        { 3, new List<int> { 4, 5 } },  // Accepted → Converted, Archived
        { 5, new List<int> { 1 } },     // Archived → Draft (unarchive)
    };

    private static readonly Dictionary<int, string> StatusNames = new()
    {
        { 1, "Draft" },
        { 2, "Sent" },
        { 3, "Accepted" },
        { 4, "Converted" },
        { 5, "Archived" }
    };

    public QuotationService(
        QuotationRepository quotationRepository,
        QuotationLineRepository quotationLineRepository,
        AuditLogRepository auditLogRepository,
        CustomerRepository customerRepository,
        ProposalSectionRepository sectionRepository,
        ProductPriceTierRepository productPriceTierRepository,
        ICurrentTenantService currentTenantService,
        ILineItemCatalogService lineItemCatalogService,
        IProductService productService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<QuotationService> logger)
    {
        _quotationRepository = quotationRepository;
        _quotationLineRepository = quotationLineRepository;
        _auditLogRepository = auditLogRepository;
        _customerRepository = customerRepository;
        _sectionRepository = sectionRepository;
        _productPriceTierRepository = productPriceTierRepository;
        _currentTenantService = currentTenantService;
        _lineItemCatalogService = lineItemCatalogService;
        _productService = productService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<List<QuotationListDto>> GetQuotationsAsync(int? statusFilter = null, int? customerFilter = null, DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        var quotations = await _quotationRepository.GetAllByBusinessIdAsync(businessId);
        var customers = await _customerRepository.GetAllByBusinessIdAsync(businessId);

        var customerLookup = customers.ToDictionary(c => c.Id, c => c.Name);

        var results = quotations.Select(q => new QuotationListDto
        {
            Id = q.Id,
            Reference = q.Reference,
            CustomerName = customerLookup.GetValueOrDefault(q.CustomerId, "Unknown"),
            StatusName = StatusNames.GetValueOrDefault(q.QuotationStatusTypeId, "Unknown"),
            QuotationStatusTypeId = q.QuotationStatusTypeId,
            TotalAmount = q.TotalAmount,
            ValidUntil = q.ValidUntil,
            CreatedAtUtc = q.CreatedAtUtc,
            IsExpired = IsExpiredInternal(q.ValidUntil)
        }).ToList();

        if (statusFilter.HasValue)
        {
            results = results.Where(q => q.QuotationStatusTypeId == statusFilter.Value).ToList();
        }

        if (customerFilter.HasValue)
        {
            results = results.Where(q => quotations.Any(x => x.Id == q.Id && x.CustomerId == customerFilter.Value)).ToList();
        }

        if (dateFrom.HasValue)
        {
            results = results.Where(q => q.CreatedAtUtc >= dateFrom.Value).ToList();
        }

        if (dateTo.HasValue)
        {
            results = results.Where(q => q.CreatedAtUtc <= dateTo.Value).ToList();
        }

        return results;
    }

    public async Task<PagedResult<QuotationListDto>> GetQuotationsPagedAsync(
        int? statusFilter = null,
        int? customerFilter = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        string? searchTerm = null,
        int page = 1,
        int pageSize = 15)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 15;

        int offset = (page - 1) * pageSize;

        var businessId = _currentTenantService.CurrentBusinessId;
        var (items, totalCount) = await _quotationRepository.GetPagedByBusinessIdAsync(
            businessId, statusFilter, customerFilter, dateFrom, dateTo, searchTerm, offset, pageSize);

        // Set IsExpired on each item
        foreach (var item in items)
        {
            item.IsExpired = IsExpiredInternal(item.ValidUntil);
        }

        return new PagedResult<QuotationListDto>
        {
            Items = items,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<Quotation?> GetQuotationByIdAsync(int id)
    {
        return await _quotationRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
    }

    public async Task<List<QuotationLine>> GetQuotationLinesAsync(int quotationId)
    {
        return await _quotationLineRepository.GetByQuotationIdAsync(quotationId);
    }

    public async Task<Quotation> CreateQuotationAsync(int customerId, DateOnly? validUntil, string? notes)
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        var customer = await _customerRepository.GetByIdAndBusinessIdAsync(customerId, businessId);
        if (customer == null)
        {
            throw new ArgumentException("Customer not found or does not belong to this business");
        }

        var sequentialNumber = await _quotationRepository.GetNextSequentialNumberAsync(businessId);
        var now = DateTime.UtcNow;
        var reference = $"QUO-{now.Year}-{now.Month:D2}-{sequentialNumber:D5}";

        var quotation = new Quotation
        {
            BusinessId = businessId,
            CustomerId = customerId,
            QuotationStatusTypeId = 1, // Draft
            Reference = reference,
            ValidUntil = validUntil,
            Subtotal = 0,
            TaxAmount = 0,
            TotalAmount = 0,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var newId = await _quotationRepository.InsertAndReturnIdAsync(quotation);
        quotation.Id = newId;

        return quotation;
    }

    public async Task UpdateQuotationAsync(int quotationId, int customerId, DateOnly? validUntil, string? notes, int? quotationContactId = null, bool? isGrandTotalShown = null, string? reference = null)
    {
        var quotation = await _quotationRepository.GetByIdAndBusinessIdAsync(quotationId, _currentTenantService.CurrentBusinessId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Quotation not found");
        }

        if (quotation.QuotationStatusTypeId != 1)
        {
            throw new InvalidOperationException("Quotation can only be edited in Draft status");
        }

        var customer = await _customerRepository.GetByIdAndBusinessIdAsync(customerId, _currentTenantService.CurrentBusinessId);
        if (customer == null)
        {
            throw new ArgumentException("Customer not found or does not belong to this business");
        }

        quotation.CustomerId = customerId;
        quotation.ValidUntil = validUntil;
        quotation.Notes = notes;
        quotation.QuotationContactId = quotationContactId;
        if (isGrandTotalShown.HasValue)
        {
            quotation.IsGrandTotalShown = isGrandTotalShown.Value;
        }
        if (!string.IsNullOrWhiteSpace(reference))
        {
            quotation.Reference = reference;
        }
        quotation.UpdatedAtUtc = DateTime.UtcNow;

        await _quotationRepository.UpdateAsync(quotation);
    }

    public async Task TransitionStatusAsync(int quotationId, int newStatusId, string userId)
    {
        var quotation = await _quotationRepository.GetByIdAndBusinessIdAsync(quotationId, _currentTenantService.CurrentBusinessId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Quotation not found");
        }

        var currentStatusId = quotation.QuotationStatusTypeId;

        if (!ValidTransitionsMap.TryGetValue(currentStatusId, out var allowedTargets) || !allowedTargets.Contains(newStatusId))
        {
            var currentName = StatusNames.GetValueOrDefault(currentStatusId, "Unknown");
            var targetName = StatusNames.GetValueOrDefault(newStatusId, "Unknown");
            throw new InvalidOperationException($"Cannot transition from {currentName} to {targetName}");
        }

        // Validate at least one line when transitioning to Sent
        if (newStatusId == 2)
        {
            var lines = await _quotationLineRepository.GetByQuotationIdAsync(quotationId);
            if (lines.Count == 0)
            {
                throw new InvalidOperationException("Quotation must have at least one line item before sending");
            }
        }

        var previousStatusName = StatusNames.GetValueOrDefault(currentStatusId, "Unknown");
        var newStatusName = StatusNames.GetValueOrDefault(newStatusId, "Unknown");

        quotation.QuotationStatusTypeId = newStatusId;
        quotation.UpdatedAtUtc = DateTime.UtcNow;

        await _quotationRepository.UpdateAsync(quotation);

        // Insert audit log
        var auditLog = new AuditLog
        {
            BusinessId = quotation.BusinessId,
            UserId = userId,
            Action = "StatusTransition",
            TableName = "quotation.Quotation",
            RecordId = quotation.Id.ToString(),
            OldValues = previousStatusName,
            NewValues = newStatusName,
            Timestamp = DateTime.UtcNow
        };

        await _auditLogRepository.InsertAsync(auditLog);

        // Populate line item catalog when transitioning to Sent or Accepted (supplementary — failures do not roll back the transition)
        if (newStatusId == 2 || newStatusId == 3)
        {
            try
            {
                await _lineItemCatalogService.PopulateFromQuotationAsync(quotationId, quotation.BusinessId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to populate line item catalog for quotation {QuotationId} (BusinessId: {BusinessId}) during transition to {StatusName}. The status transition was successful.",
                    quotationId, quotation.BusinessId, newStatusName);
            }
        }
    }

    public async Task<QuotationLine> AddLineAsync(int quotationId, string description, decimal quantity, decimal unitPrice, decimal vatRate, string? referenceUrl = null, decimal discount = 0, string discountType = "Percentage", string? subtitle = null, decimal? costPrice = null, string? productCode = null, bool isReverseCharge = false, int? proposalSectionId = null, int? productPriceTierId = null)
    {
        if (isReverseCharge && vatRate > 0)
        {
            throw new ArgumentException("Reverse charge lines require 0% VAT");
        }

        if (costPrice.HasValue && costPrice.Value < 0)
        {
            throw new ArgumentException("Cost price must be zero or greater");
        }

        var quotation = await _quotationRepository.GetByIdAndBusinessIdAsync(quotationId, _currentTenantService.CurrentBusinessId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Quotation not found");
        }

        if (quotation.QuotationStatusTypeId != 1)
        {
            throw new InvalidOperationException("Quotation can only be edited in Draft status");
        }

        ValidateLineInput(description, quantity, unitPrice, vatRate);
        ValidateReferenceUrl(referenceUrl);

        var lineTotal = CalculateLineTotal(quantity, unitPrice, discount, discountType);

        var existingLines = await _quotationLineRepository.GetByQuotationIdAsync(quotationId);
        var nextSortOrder = existingLines.Count > 0 ? existingLines.Max(l => l.SortOrder) + 1 : 1;

        // Look up tier name snapshot when ProductPriceTierId is provided
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

        var line = new QuotationLine
        {
            QuotationId = quotationId,
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
            ProductCode = productCode,
            IsReverseCharge = isReverseCharge,
            ProposalSectionId = proposalSectionId,
            ProductPriceTierId = productPriceTierId,
            PriceTierName = priceTierName
        };

        await _quotationLineRepository.InsertAsync(line);

        // Auto-populate product catalog after line item persistence
        var addLineUserId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? string.Empty;
        await _productService.AutoPopulateFromLineItemAsync(
            productCode,
            description,
            unitPrice,
            vatRate,
            addLineUserId);

        await RecalculateQuotationTotalsAsync(quotation);

        return line;
    }

    public async Task UpdateLineAsync(int lineId, string description, decimal quantity, decimal unitPrice, decimal vatRate, string? referenceUrl = null, decimal discount = 0, string discountType = "Percentage", string? subtitle = null, decimal? costPrice = null, bool isReverseCharge = false)
    {
        if (isReverseCharge && vatRate > 0)
        {
            throw new ArgumentException("Reverse charge lines require 0% VAT");
        }

        if (costPrice.HasValue && costPrice.Value < 0)
        {
            throw new ArgumentException("Cost price must be zero or greater");
        }

        var line = await _quotationLineRepository.GetByIdAsync(lineId);
        if (line == null)
        {
            throw new InvalidOperationException("Line item not found");
        }

        var quotation = await _quotationRepository.GetByIdAndBusinessIdAsync(line.QuotationId, _currentTenantService.CurrentBusinessId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Quotation not found");
        }

        if (quotation.QuotationStatusTypeId != 1)
        {
            throw new InvalidOperationException("Quotation can only be edited in Draft status");
        }

        ValidateLineInput(description, quantity, unitPrice, vatRate);
        ValidateReferenceUrl(referenceUrl);

        line.Description = description;
        line.Quantity = quantity;
        line.UnitPrice = unitPrice;
        line.VatRate = vatRate;
        line.Discount = discount;
        line.DiscountType = discountType;
        line.CostPrice = costPrice;
        line.LineTotal = CalculateLineTotal(quantity, unitPrice, discount, discountType);
        line.ReferenceUrl = referenceUrl;
        line.Subtitle = subtitle;
        line.IsReverseCharge = isReverseCharge;

        await _quotationLineRepository.UpdateAsync(line);

        await RecalculateQuotationTotalsAsync(quotation);
    }

    public async Task RemoveLineAsync(int lineId)
    {
        var line = await _quotationLineRepository.GetByIdAsync(lineId);
        if (line == null)
        {
            throw new InvalidOperationException("Line item not found");
        }

        var quotation = await _quotationRepository.GetByIdAndBusinessIdAsync(line.QuotationId, _currentTenantService.CurrentBusinessId);
        if (quotation == null)
        {
            throw new InvalidOperationException("Quotation not found");
        }

        if (quotation.QuotationStatusTypeId != 1)
        {
            throw new InvalidOperationException("Quotation can only be edited in Draft status");
        }

        await _quotationLineRepository.DeleteAsync(lineId);

        await RecalculateQuotationTotalsAsync(quotation);
    }

    public bool IsExpired(Quotation quotation)
    {
        return IsExpiredInternal(quotation.ValidUntil);
    }

    public Dictionary<int, List<int>> GetValidTransitions()
    {
        return ValidTransitionsMap;
    }

    private static bool IsExpiredInternal(DateOnly? validUntil)
    {
        if (!validUntil.HasValue) return false;
        return validUntil.Value < DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private async Task RecalculateQuotationTotalsAsync(Quotation quotation)
    {
        var lines = await _quotationLineRepository.GetByQuotationIdAsync(quotation.Id);
        var sections = await _sectionRepository.GetByQuotationIdAsync(quotation.Id);

        // Build a lookup of subscription section IDs
        var subscriptionSectionIds = new HashSet<int>(
            sections.Where(s => s.ColumnConfiguration == "Subscription").Select(s => s.Id));

        // Calculate totals — annualize subscription lines (×12)
        decimal subtotal = 0;
        decimal taxAmount = 0;

        foreach (var line in lines)
        {
            var multiplier = (line.ProposalSectionId.HasValue && subscriptionSectionIds.Contains(line.ProposalSectionId.Value))
                ? 12m
                : 1m;

            subtotal += line.LineTotal * multiplier;
            taxAmount += line.LineTotal * multiplier * line.VatRate / 100m;
        }

        quotation.Subtotal = Math.Round(subtotal, 2);
        quotation.TaxAmount = Math.Round(taxAmount, 2);
        quotation.TotalAmount = quotation.Subtotal + quotation.TaxAmount;
        quotation.UpdatedAtUtc = DateTime.UtcNow;

        await _quotationRepository.UpdateAsync(quotation);
    }

    private static void ValidateLineInput(string description, decimal quantity, decimal unitPrice, decimal vatRate)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Line item description is required");
        }

        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentException("Unit price must be zero or greater");
        }

        if (vatRate < 0 || vatRate > 100)
        {
            throw new ArgumentException("VAT rate must be between 0 and 100");
        }
    }

    private static void ValidateReferenceUrl(string? referenceUrl)
    {
        if (string.IsNullOrWhiteSpace(referenceUrl))
            return;

        if (referenceUrl.Length > 2048)
            throw new ArgumentException("Reference URL must not exceed 2048 characters.");

        if (!Uri.TryCreate(referenceUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException("Reference URL must be a valid absolute URL.");

        if (uri.Scheme != "http" && uri.Scheme != "https")
            throw new ArgumentException("Reference URL must use http or https scheme.");
    }

    private static decimal CalculateLineTotal(decimal quantity, decimal unitPrice, decimal discount, string discountType)
    {
        var gross = quantity * unitPrice;

        if (discount <= 0)
            return Math.Round(gross, 2);

        if (discountType == "Fixed")
            return Math.Round(gross - discount, 2);

        // Percentage
        return Math.Round(gross * (1 - (discount / 100)), 2);
    }
}
