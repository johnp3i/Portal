using Portal.Web.Models.Stripe;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Provides subscription status and plan feature access information for a business.
/// Results are cached for the HTTP request lifetime to avoid repeated database queries.
/// </summary>
public interface ISubscriptionPlanService
{
    /// <summary>
    /// Gets the subscription status and plan features for a business.
    /// Results are cached for the duration of the HTTP request.
    /// </summary>
    Task<SubscriptionAccessResult> GetAccessAsync(int businessId);
}
