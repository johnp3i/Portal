namespace Portal.Web.Configuration
{
    public class StripeSettings
    {
        public const string SectionName = "Stripe_BILI";

        public string SecretKey { get; set; } = null!;
        public string PublishableKey { get; set; } = null!;
        public string WebhookSigningSecret { get; set; } = null!;
        public string? DefaultTaxRateId { get; set; }
        public string? BaseUrl { get; set; }

        // Stripe Connect settings
        public string? ConnectClientId { get; set; }
        public string? ConnectWebhookSecret { get; set; }
        public string? ConnectOAuthRedirectUri { get; set; }
    }
}
