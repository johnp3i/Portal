namespace Portal.Infrastructure.Services;

/// <summary>
/// Cross-module search service that queries multiple entity types in parallel.
/// </summary>
public interface IGlobalSearchService
{
    /// <summary>
    /// Searches across all permitted entity types for the given query.
    /// </summary>
    /// <param name="query">The search string (minimum 2 characters).</param>
    /// <param name="businessId">The current tenant's business ID for isolation.</param>
    /// <param name="permittedModules">Module keys the user has access to.</param>
    Task<GlobalSearchResultDto> SearchAsync(string query, int businessId, HashSet<string> permittedModules);
}
