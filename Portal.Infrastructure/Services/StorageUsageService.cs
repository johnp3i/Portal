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

    // ── Active-plan statuses that count as "in force" for cap resolution. Kept in one place so
    //    the per-business tab, the enforcement path, and the SuperAdmin page all agree. ──────────
    private static readonly string[] ActivePlanStatuses = { "active", "trialing", "past_due" };

    /// <summary>MB → bytes; null/0 MB = unlimited (no cap).</summary>
    private static long? MbToBytes(int? limitMb) =>
        (limitMb.HasValue && limitMb.Value > 0) ? limitMb.Value * 1024L * 1024L : (long?)null;

    /// <summary>Cache key for a business's ambient storage status.</summary>
    private static string StatusCacheKey(int businessId) => $"storage-status-{businessId}";

    /// <summary>
    /// Single source of truth for a business's storage cap in bytes (Subscriptions → Plans, active
    /// statuses only). Returns null when unlimited or when the business has no active subscription.
    /// </summary>
    private async Task<long?> ResolveLimitBytesAsync(int businessId)
    {
        var limitMb = await _context.Subscriptions
            .Where(s => s.BusinessId == businessId && ActivePlanStatuses.Contains(s.Status))
            .Join(_context.Plans, s => s.PlanId, p => p.Id, (s, p) => p.StorageLimitMb)
            .FirstOrDefaultAsync();

        return MbToBytes(limitMb);
    }

    /// <summary>
    /// Per-business storage caps (bytes) for ALL businesses with an active subscription, resolved
    /// from the SAME source as the per-business/enforcement path. One grouped query (no N+1) so the
    /// SuperAdmin page can never show a cap that differs from what's actually enforced.
    /// </summary>
    private async Task<Dictionary<int, long?>> ResolveAllLimitBytesAsync()
    {
        var rows = await _context.Subscriptions
            .Where(s => ActivePlanStatuses.Contains(s.Status))
            .Join(_context.Plans, s => s.PlanId, p => p.Id,
                (s, p) => new { s.BusinessId, p.StorageLimitMb })
            .ToListAsync();

        // If a business somehow has multiple active subscriptions, take the first deterministically.
        return rows
            .GroupBy(r => r.BusinessId)
            .ToDictionary(g => g.Key, g => MbToBytes(g.First().StorageLimitMb));
    }

    public async Task<BusinessStorageDto> GetBusinessStorageAsync(int businessId)
    {
        try
        {
            var attachments = await _repository.GetAttachmentUsageAsync(businessId);
            var compliance = await _repository.GetComplianceUsageAsync(businessId);
            var logos = await _repository.GetLogoUsageAsync(businessId);
            var signatures = await _repository.GetSignatureUsageAsync(businessId);
            var byType = await _repository.GetAttachmentsByTypeAsync(businessId);

            var dto = new BusinessStorageDto
            {
                TotalBytes = attachments.Bytes + compliance.Bytes + logos.Bytes + signatures.Bytes,
                Categories = new List<StorageCategoryDto>
                {
                    new() { Name = "Document attachments", Files = attachments.Files, Bytes = attachments.Bytes },
                    new() { Name = "Compliance attachments", Files = compliance.Files, Bytes = compliance.Bytes },
                    new() { Name = "Logos", Files = logos.Files, Bytes = logos.Bytes },
                    new() { Name = "Signatures", Files = signatures.Files, Bytes = signatures.Bytes }
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

            dto.LimitBytes = await ResolveLimitBytesAsync(businessId);

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
            // 1. All non-demo businesses + plan/status for DISPLAY (BusinessPlans join). The plan
            //    name/status shown here is advisory; the enforced cap comes from step 1b so it can
            //    never disagree with the per-business tab or the upload enforcement.
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
                    Status = bp != null ? bp.Status : "unknown"
                }
            ).ToListAsync();

            // 1b. Enforced storage caps from the SAME active-Subscriptions → Plans source used by the
            //     per-business tab and the upload enforcer (single grouped query, no N+1).
            var limitBytesByBusiness = await ResolveAllLimitBytesAsync();

            // 2. Per-business bytes per category, across all tenants (raw SQL → not query-filtered).
            var attachmentBytes = await _repository.GetAllAttachmentBytesAsync();
            var complianceBytes = await _repository.GetAllComplianceBytesAsync();
            var logoBytes = await _repository.GetAllLogoBytesAsync();
            var signatureBytes = await _repository.GetAllSignatureBytesAsync();

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
                SignatureBytes = signatureBytes.TryGetValue(b.Id, out var sg) ? sg : 0L,
                LimitBytes = limitBytesByBusiness.TryGetValue(b.Id, out var lim) ? lim : (long?)null
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
            var signatures = await _repository.GetSignatureUsageAsync(businessId);
            var usedBytes = attachments.Bytes + compliance.Bytes + logos.Bytes + signatures.Bytes;

            var limitBytes = await ResolveLimitBytesAsync(businessId);

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
            var cacheKey = StatusCacheKey(businessId);
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

    public void InvalidateStatus(int businessId)
    {
        _cache.Remove(StatusCacheKey(businessId));
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
