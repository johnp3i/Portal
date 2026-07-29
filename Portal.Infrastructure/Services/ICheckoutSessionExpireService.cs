namespace Portal.Infrastructure.Services;

/// <summary>
/// Expires pending Stripe Checkout Sessions when an invoice becomes fully paid.
/// Prevents overpayment from customers who still have checkout pages open.
/// </summary>
public interface ICheckoutSessionExpireService
{
    /// <summary>
    /// Attempts to expire all pending checkout sessions for the specified invoice.
    /// Runs gracefully — never throws, logs warnings on failure.
    /// </summary>
    /// <param name="invoiceId">The invoice that became fully paid.</param>
    /// <param name="businessId">The business owning the invoice.</param>
    /// <param name="excludeSessionId">Optional Stripe session ID to exclude (e.g., the session that just completed via webhook).</param>
    Task TryExpirePendingSessionsAsync(int invoiceId, int businessId, string? excludeSessionId = null);
}
