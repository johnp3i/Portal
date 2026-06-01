namespace Portal.Web.Models.Stripe;

public class BillingOverviewModel
{
    public string PlanName { get; set; } = null!;
    public string SubscriptionStatus { get; set; } = null!;
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public DateTime? NextRenewalDate { get; set; }
}
