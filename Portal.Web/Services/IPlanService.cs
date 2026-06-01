using Portal.Web.Models;

namespace Portal.Web.Services;

/// <summary>
/// Provides read-only access to subscription plans for display on the registration page.
/// </summary>
public interface IPlanService
{
    /// <summary>
    /// Returns all active plans ordered by DisplayOrder ascending.
    /// </summary>
    Task<List<PlanDisplayModel>> GetActivePlansOrderedAsync();

    /// <summary>
    /// Returns a single plan by its URL slug, or null if not found or inactive.
    /// </summary>
    Task<PlanDisplayModel?> GetPlanBySlugAsync(string slug);
}
