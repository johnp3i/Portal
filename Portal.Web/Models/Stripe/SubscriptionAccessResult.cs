namespace Portal.Web.Models.Stripe;

public class SubscriptionAccessResult
{
    public bool HasActiveSubscription { get; set; }
    public bool IsGraceAccess { get; set; }
    public string SubscriptionStatus { get; set; } = null!;
    public string PlanName { get; set; } = null!;
    public HashSet<string> IncludedModules { get; set; } = new();
}
