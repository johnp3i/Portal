namespace Portal.Infrastructure.Entities.Billing;

/// <summary>
/// A subscription record tracking a business's active plan, billing period, and lifecycle status.
/// Schema: [billing].Subscription
/// </summary>
public class Subscription
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int PlanId { get; set; }

    public string Status { get; set; } = null!;

    public string? StripeSubscriptionId { get; set; }

    public DateTime CurrentPeriodStart { get; set; }

    public DateTime CurrentPeriodEnd { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public bool IsGraceAccessUsed { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Plan Plan { get; set; } = null!;
}
