using Portal.Infrastructure.Models.Storage;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Read-only storage usage reporting (Phase 1 — visibility only, no enforcement).
/// Logical usage = SUM of live rows' FileSizeBytes. Signatures are surfaced as "not counted yet".
/// </summary>
public interface IStorageUsageService
{
    /// <summary>Per-business storage summary for the My Business → Storage tab.</summary>
    Task<BusinessStorageDto> GetBusinessStorageAsync(int businessId);

    /// <summary>Platform-wide storage per business for the SuperAdmin Storage page.</summary>
    Task<AdminStoragePageDto> GetPlatformStorageAsync(string? search, string? planFilter, string sortBy, string sortDir);

    /// <summary>
    /// Lightweight accessor for storage enforcement (Phase 3): a business's current logical
    /// usage in bytes and its plan storage cap in bytes (null = unlimited / no cap). Cheaper
    /// than <see cref="GetBusinessStorageAsync"/> — it skips the category and by-type breakdowns.
    /// </summary>
    Task<(long UsedBytes, long? LimitBytes)> GetUsageAndLimitAsync(int businessId);

    /// <summary>
    /// Cached storage status for ambient signals (sidebar badge + dashboard banner). Wraps
    /// <see cref="GetUsageAndLimitAsync"/> in a short per-business memory cache so it can be called
    /// on every page render (the sidebar) without re-running the usage sums each request.
    /// </summary>
    Task<StorageStatusDto> GetStatusAsync(int businessId);

    /// <summary>
    /// Evicts the cached storage status for a business. Call after a file upload or delete so the
    /// badge/banner reflect the new usage immediately instead of lingering for up to the cache TTL.
    /// </summary>
    void InvalidateStatus(int businessId);
}
