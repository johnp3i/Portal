namespace Portal.Web.Services;

/// <summary>
/// Provides access to platform-wide configuration values stored in [dbo].[PlatformConfig].
/// Values are cached for the duration of the HTTP request to avoid repeated database queries.
/// </summary>
public interface IPlatformConfigService
{
    /// <summary>
    /// Returns the configuration value for the specified key using case-insensitive lookup.
    /// Returns null if the key does not exist.
    /// </summary>
    Task<string?> GetValueAsync(string key);

    /// <summary>
    /// Inserts or updates a configuration value for the specified key.
    /// Invalidates the request-scoped cache entry for the key.
    /// </summary>
    Task SetValueAsync(string key, string value);
}
