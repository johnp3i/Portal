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
}
