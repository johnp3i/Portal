using Microsoft.AspNetCore.Http;
using Portal.Infrastructure.Repositories;

namespace Portal.Web.Services;

/// <summary>
/// Reads and writes platform configuration values from [dbo].[PlatformConfig].
/// Caches values in HttpContext.Items for the lifetime of the current HTTP request
/// to avoid repeated database queries within a single request.
/// </summary>
public class PlatformConfigService : IPlatformConfigService
{
    private readonly PlatformConfigRepository _repository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private const string CacheKeyPrefix = "PlatformConfig_";

    public PlatformConfigService(
        PlatformConfigRepository repository,
        IHttpContextAccessor httpContextAccessor)
    {
        _repository = repository;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<string?> GetValueAsync(string key)
    {
        try
        {
            var cacheKey = CacheKeyPrefix + key;
            var items = _httpContextAccessor.HttpContext?.Items;

            // Check request-scoped cache first
            if (items != null && items.ContainsKey(cacheKey))
            {
                return items[cacheKey] as string;
            }

            // Cache miss — query the repository
            var config = await _repository.GetByKeyAsync(key);
            var value = config?.Value;

            // Cache the result in HttpContext.Items (even null to avoid repeated DB calls)
            if (items != null)
            {
                items[cacheKey] = value;
            }

            return value;
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetValueAsync(string key, string value)
    {
        try
        {
            await _repository.UpsertAsync(key, value);

            // Invalidate the request-scoped cache entry
            var cacheKey = CacheKeyPrefix + key;
            var items = _httpContextAccessor.HttpContext?.Items;

            if (items != null && items.ContainsKey(cacheKey))
            {
                items.Remove(cacheKey);
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
}
