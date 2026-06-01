using Portal.Web.Models.Stripe;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Handles Stripe Checkout Session creation for users completing their subscription payment.
/// </summary>
public interface ICheckoutService
{
    /// <summary>
    /// Creates a Stripe Checkout Session for the user's pending registration.
    /// Returns the Stripe-hosted checkout URL for redirect, or an error result
    /// if preconditions are not met (no pending registration, already completed, plan unavailable).
    /// </summary>
    Task<CheckoutResult> CreateCheckoutSessionAsync(string userId);
}
