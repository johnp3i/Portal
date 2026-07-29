namespace Portal.Web.Services.Stripe;

/// <summary>
/// Represents the resolved set of Stripe API keys for a business,
/// regardless of whether they came from the database or User Secrets.
/// </summary>
public class ResolvedStripeKeys
{
    public string? ConnectClientId { get; set; }
    public string? SecretKey { get; set; }
    public string? ConnectWebhookSecret { get; set; }
    public string ConnectOAuthRedirectUri { get; set; } = null!;

    /// <summary>True if keys were loaded from the per-business database, false if from User Secrets.</summary>
    public bool IsFromDatabase { get; set; }

    /// <summary>True if all three keys are present and non-empty.</summary>
    public bool IsComplete =>
        !string.IsNullOrEmpty(ConnectClientId) &&
        !string.IsNullOrEmpty(SecretKey) &&
        !string.IsNullOrEmpty(ConnectWebhookSecret);
}
