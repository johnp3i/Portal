using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models.Storage;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Aggregates logical storage usage for the per-business Storage tab and the SuperAdmin platform
/// Storage page. Read-only (Phase 1). Signatures are reported with a count but no size.
/// </summary>
public class StorageUsageService : IStorageUsageService
{
    private readonly StorageUsageRepository _repository;
    private readonly PortalDbContext _context;
    private readonly IMemoryCache _cache;

    /// <summary>How long a business's storage status is cached for the ambient signals.</summary>
    private static readonly TimeSpan StatusCacheDuration = TimeSpan.FromMinutes(2);

    public StorageUsageService(StorageUsageRepository repository, PortalDbContext context, IMemoryCache cache)
    {
        _repository = repository;
        _context = context;
        _cache = cache;
    }

    public async Task<BusinessStorageDto> GetBusinessStorageAsync(int businessId)
    {
        try
        {
            var attachments = await _repository.GetAttachmentUsageAsync(businessId);
            var compliance = await _repository.GetComplianceUsageAsync(businessId);
            var logos = await _repository.GetLogoUsageAsync(businessId);
            var signatureCount = await _repository.GetSignatureCountAsync(businessId);
            var byType = await _repository.GetAttachmentsByTypeAsync(businessId);

            var dto = new BusinessStorageDto
            {
                TotalBytes = attachments.Bytes + compliance.Bytes + logos.Bytes,
                Categories = new List<StorageCategoryDto>
                {
                    new() { Name = "Document attachments", Files = attachments.Files, Bytes = attachments.Bytes },
                    new() { Name = "Compliance attachments", Files = compliance.Files, Bytes = compliance.Bytes },
                    new() { Name = "Logos", Files = logos.Files, Bytes = logos.Bytes },
                    new() { Name = "Signatures", Files = signatureCount, Bytes = 0, NotCountedYet = true }
                },
                AttachmentsByType = byType
                    .Select(t => new StorageTypeDto
                    {
                        RecordType = FriendlyType(t.EntityType),
                        Files = t.Files,
                        Bytes = t.Bytes
                    })
                    .ToList()
            };

            // Resolve the business's storage cap from its active plan (Subscriptions → Plans,
            // mirroring PlanCheckService.GetCurrentPlanNameAsync). StorageLimitMb NULL = unlimited.
            var limitMb = await _context.Subscriptions
                .Where(s => s.BusinessId == businessId
                    && (s.Status == "active" || s.Status == "trialing" || s.Status == "past_due"))
                .Join(_context.Plans,
                    s => s.PlanId,
                    p => p.Id,
                    (s, p) => p.StorageLimitMb)
                .FirstOrDefaultAsync();

            if (limitMb.HasValue && limitMb.Value > 0)
                dto.LimitBytes = limitMb.Value * 1024L * 1024L;

            return dto;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<AdminStoragePageDto> GetPlatformStorageAsync(string? search, string? planFilter, string sortBy, string sortDir)
    {
        try
        {
            // 1. All non-demo businesses + plan/status (mirrors BusinessInsightsRepository join).
            var businesses = await (
                from business in _context.Businesses
                where !business.IsDemoAccount
                join businessPlan in _context.BusinessPlans on business.Id equals businessPlan.BusinessId into bpGroup
                from bp in bpGroup.DefaultIfEmpty()
                join plan in _context.Plans on bp.PlanId equals plan.Id into planGroup
                from p in planGroup.DefaultIfEmpty()
                select new
                {
                    business.Id,
                    business.Name,
                    PlanName = p != null ? p.Name : "No Plan",
                    Status = bp != null ? bp.Status : "unknown",
                    StorageLimitMb = p != null ? p.StorageLimitMb : null
                }
            ).ToListAsync();

            // 2. Per-business bytes per category, across all tenants (raw SQL → not query-filtered).
            var attachmentBytes = await _repository.GetAllAttachmentBytesAsync();
            var complianceBytes = await _repository.GetAllComplianceBytesAsync();
            var logoBytes = await _repository.GetAllLogoBytesAsync();

            // 3. Assemble rows.
            var rows = businesses.Select(b => new AdminStorageRowDto
            {
                BusinessId = b.Id,
                BusinessName = b.Name,
                PlanName = b.PlanName,
                Status = b.Status,
                AttachmentBytes = attachmentBytes.TryGetValue(b.Id, out var a) ? a : 0L,
                ComplianceBytes = complianceBytes.TryGetValue(b.Id, out var c) ? c : 0L,
                LogoBytes = logoBytes.TryGetValue(b.Id, out var l) ? l : 0L,
                LimitBytes = b.StorageLimitMb.HasValue && b.StorageLimitMb.Value > 0
                    ? b.StorageLimitMb.Value * 1024L * 1024L
                    : (long?)null
            }).ToList();

            // 4. Filters.
            if (!string.IsNullOrWhiteSpace(search))
                rows = rows.Where(r => r.BusinessName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(planFilter))
                rows = rows.Where(r => string.Equals(r.PlanName, planFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            // 5. Sort (name or size, asc or desc).
            var sortField = (sortBy ?? "size").ToLowerInvariant();
            var descending = !string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
            rows = sortField switch
            {
                "name" => (descending
                    ? rows.OrderByDescending(r => r.BusinessName)
                    : rows.OrderBy(r => r.BusinessName)).ToList(),
                _ => (descending
                    ? rows.OrderByDescending(r => r.TotalBytes)
                    : rows.OrderBy(r => r.TotalBytes)).ToList()
            };

            // 6. Platform summary (computed over ALL businesses with files, pre-filter for accuracy).
            var withFiles = rows.Where(r => r.TotalBytes > 0).ToList();
            var summary = new AdminStorageSummaryDto
            {
                TotalBytes = rows.Sum(r => r.TotalBytes),
                BusinessesWithFiles = withFiles.Count,
                LargestBusinessBytes = withFiles.Count > 0 ? withFiles.Max(r => r.TotalBytes) : 0L,
                AverageBytes = withFiles.Count > 0 ? (long)withFiles.Average(r => r.TotalBytes) : 0L
            };

            return new AdminStoragePageDto
            {
                Rows = rows,
                Summary = summary,
                SearchTerm = search,
                PlanFilter = planFilter,
                SortBy = sortField,
                SortDir = descending ? "desc" : "asc"
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<(long UsedBytes, long? LimitBytes)> GetUsageAndLimitAsync(int businessId)
    {
        try
        {
            // Current logical usage = SUM of live rows across the measured categories.
            var attachments = await _repository.GetAttachmentUsageAsync(businessId);
            var compliance = await _repository.GetComplianceUsageAsync(businessId);
            var logos = await _repository.GetLogoUsageAsync(businessId);
            var usedBytes = attachments.Bytes + compliance.Bytes + logos.Bytes;

            // Resolve the plan cap (Subscriptions → Plans), identical to GetBusinessStorageAsync.
            // StorageLimitMb NULL/0 = unlimited (no cap).
            var limitMb = await _context.Subscriptions
                .Where(s => s.BusinessId == businessId
                    && (s.Status == "active" || s.Status == "trialing" || s.Status == "past_due"))
                .Join(_context.Plans,
                    s => s.PlanId,
                    p => p.Id,
                    (s, p) => p.StorageLimitMb)
                .FirstOrDefaultAsync();

            long? limitBytes = (limitMb.HasValue && limitMb.Value > 0)
                ? limitMb.Value * 1024L * 1024L
                : null;

            return (usedBytes, limitBytes);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<StorageStatusDto> GetStatusAsync(int businessId)
    {
        try
        {
            // Cached briefly per business — this is called on every page render (sidebar badge),
            // so we avoid re-running the usage sums each request. A recently-freed business may
            // see the badge/banner linger until the cache expires (acceptable for an advisory).
            var cacheKey = $"storage-status-{businessId}";
            if (_cache.TryGetValue(cacheKey, out StorageStatusDto? cached) && cached != null)
                return cached;

            var (usedBytes, limitBytes) = await GetUsageAndLimitAsync(businessId);
            var status = new StorageStatusDto { UsedBytes = usedBytes, LimitBytes = limitBytes };

            _cache.Set(cacheKey, status, StatusCacheDuration);
            return status;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>Maps a stored EntityType to a friendly label for the "by record type" table.</summary>
    private static string FriendlyType(string entityType) => entityType switch
    {
        "Purchase" => "Purchase invoices",
        "Invoice" => "Invoices",
        "Quotation" => "Quotations",
        "Supplier" => "Suppliers",
        "Customer" => "Customers",
        "Payment" => "Payments",
        "CreditNote" => "Credit notes",
        "RevenueSummary" => "Z-Reports",
        _ => entityType
    };
}
