namespace Portal.Web.Services.Stripe;

/// <summary>
/// Resolves Stripe API keys for a business — checks per-business DB keys first,
/// falls back to platform-level User Secrets.
/// </summary>
public interface IStripeKeyResolutionService
{
    /// <summary>
    /// Resolves the active Stripe keys for a business.
    /// Checks per-business DB keys first, falls back to platform User Secrets.
    /// </summary>
    Task<ResolvedStripeKeys> ResolveKeysAsync(int businessId);

    /// <summary>
    /// Returns whether the business has per-business keys configured in the DB.
    /// </summary>
    Task<bool> HasBusinessKeysAsync(int businessId);
}
