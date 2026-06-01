namespace Portal.Web.Models.Stripe;

public class ProvisioningRequest
{
    public string UserId { get; set; } = null!;
    public int PendingRegistrationId { get; set; }
    public int PlanId { get; set; }
    public string StripeCustomerId { get; set; } = null!;
    public string StripeSessionId { get; set; } = null!;
    public string StripeSubscriptionId { get; set; } = null!;
    public string StripePaymentIntentId { get; set; } = null!;
    public DateTime SubscriptionStart { get; set; }
    public DateTime SubscriptionEnd { get; set; }
    public decimal AmountPaid { get; set; }
    public string Currency { get; set; } = null!;
}
