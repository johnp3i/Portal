namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides access to the current tenant's BusinessId for use in EF Core global query filters.
/// </summary>
public interface ICurrentTenantService
{
    /// <summary>
    /// Gets the current tenant's BusinessId resolved from authentication claims.
    /// Returns 0 if no valid BusinessId is found (global query filter will return zero results).
    /// </summary>
    int CurrentBusinessId { get; }
}
