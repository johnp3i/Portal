using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Single source of truth for prospect-activity types, backed by the [sales].[ActivityType]
/// reference table (replaces the former hardcoded in-code dictionaries). Results are briefly cached
/// so newly-inserted types appear across the app without a restart.
/// </summary>
public interface IActivityTypeLookupService
{
    /// <summary>All types for pickers/filters. <paramref name="activeOnly"/> hides inactive rows.</summary>
    Task<IReadOnlyList<ActivityTypeDto>> GetAllAsync(bool activeOnly = true);

    /// <summary>Id -> Name map for display resolution (includes inactive, so history still resolves).</summary>
    Task<IReadOnlyDictionary<byte, string>> GetNameMapAsync();

    /// <summary>Resolves a type id to its name, or "Unknown" if the id is not found.</summary>
    Task<string> GetNameAsync(byte id);

    /// <summary>True if the id exists (any active state) — used to validate incoming requests.</summary>
    Task<bool> ExistsAsync(byte id);
}
