namespace Portal.Web.Services.Stripe;

/// <summary>
/// Processes incoming Stripe webhook events, verifying signatures and routing
/// to the appropriate handler based on event type.
/// </summary>
public interface IWebhookProcessingService
{
    /// <summary>
    /// Verifies the Stripe signature, checks idempotency, and processes the webhook event.
    /// Returns an HTTP-appropriate status code for the webhook response.
    /// </summary>
    Task<int> ProcessEventAsync(string json, string signatureHeader);
}
