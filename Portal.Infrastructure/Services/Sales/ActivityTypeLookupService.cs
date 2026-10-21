using Microsoft.Extensions.Caching.Memory;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Cached lookup over the [sales].[ActivityType] reference table. The reference data is global and
/// changes rarely (only when an admin adds a type), so a short memory cache keeps the dropdown,
/// filters, and name resolution fast while still picking up new rows within the TTL.
/// </summary>
public class ActivityTypeLookupService : IActivityTypeLookupService
{
    private readonly ActivityTypeRepository _repository;
    private readonly IMemoryCache _cache;

    // All rows (both active and inactive), cached. Keyed once; filtering/mapping happens in-memory.
    private const string CacheKey = "sales.activitytypes.all.v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public ActivityTypeLookupService(ActivityTypeRepository repository, IMemoryCache cache)
    {
        _repository = repository;
        _cache = cache;
    }

    private async Task<List<ActivityTypeDto>> GetAllCachedAsync()
    {
        if (_cache.TryGetValue(CacheKey, out List<ActivityTypeDto>? cached) && cached is not null)
            return cached;

        var rows = await _repository.GetAllAsync(activeOnly: false);
        var list = rows.Select(t => new ActivityTypeDto
        {
            Id = t.Id,
            Name = t.Name,
            SortOrder = t.SortOrder,
            IsActive = t.IsActive
        }).ToList();

        _cache.Set(CacheKey, list, CacheTtl);
        return list;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityTypeDto>> GetAllAsync(bool activeOnly = true)
    {
        var all = await GetAllCachedAsync();
        return activeOnly ? all.Where(t => t.IsActive).ToList() : all;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<byte, string>> GetNameMapAsync()
    {
        var all = await GetAllCachedAsync();
        // Include inactive so historical activities still resolve their name.
        return all.ToDictionary(t => t.Id, t => t.Name);
    }

    /// <inheritdoc />
    public async Task<string> GetNameAsync(byte id)
    {
        var map = await GetNameMapAsync();
        return map.TryGetValue(id, out var name) ? name : "Unknown";
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(byte id)
    {
        var map = await GetNameMapAsync();
        return map.ContainsKey(id);
    }
}
