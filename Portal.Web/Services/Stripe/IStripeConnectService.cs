using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Service interface for Stripe Connect integration — OAuth onboarding,
/// Checkout Session creation, and webhook processing.
/// </summary>
public interface IStripeConnectService
{
    // ─── Onboarding ──────────────────────────────────────────────────────

    /// <summary>
    /// Generates the Stripe OAuth URL for the business owner to connect their account.
    /// </summary>
    Task<string> GetOAuthConnectUrlAsync(int businessId);

    /// <summary>
    /// Exchanges the OAuth authorization code for a connected account ID and stores it.
    /// </summary>
    Task<ServiceResult> CompleteOAuthAsync(int businessId, string authorizationCode, string state);

    /// <summary>
    /// Disconnects the business from Stripe (soft-delete).
    /// </summary>
    Task<ServiceResult> DisconnectAsync(int businessId);

    /// <summary>
    /// Checks whether a business has an active Stripe connected account.
    /// </summary>
    Task<bool> IsConnectedAsync(int businessId);

    /// <summary>
    /// Gets the Stripe connected account ID for a business, or null if not connected.
    /// </summary>
    Task<string?> GetConnectedAccountIdAsync(int businessId);

    // ─── Checkout ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Stripe Checkout Session for the invoice's outstanding balance.
    /// Returns the Checkout Session URL for the customer to be redirected to.
    /// </summary>
    Task<ServiceResult<string>> CreateCheckoutSessionAsync(int invoiceId, int businessId, string successUrl, string cancelUrl, string? customerName);

    // ─── Webhook ─────────────────────────────────────────────────────────

    /// <summary>
    /// Processes a checkout.session.completed event — creates Payment record, updates status.
    /// </summary>
    Task<ServiceResult> HandleCheckoutCompletedAsync(string stripeSessionId, string paymentIntentId);

    /// <summary>
    /// Processes a checkout.session.expired event — marks session as expired.
    /// </summary>
    Task HandleCheckoutExpiredAsync(string stripeSessionId);

    // ─── Card Payments View ──────────────────────────────────────────────

    /// <summary>
    /// Gets completed checkout sessions for the Card Payments view.
    /// </summary>
    Task<List<StripeCheckoutSession>> GetCompletedSessionsAsync(int businessId, DateTime? fromUtc, DateTime? toUtc);
}
