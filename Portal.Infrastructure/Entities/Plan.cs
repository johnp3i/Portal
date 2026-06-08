namespace Portal.Infrastructure.Entities;

/// <summary>
/// A subscription tier record defining pricing, billing cycle, user limits, and metadata.
/// Schema: [dbo].Plan
/// </summary>
public class Plan
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public decimal MonthlyPriceEur { get; set; }

    public decimal? AnnualPriceEur { get; set; }

    public int MaxUsers { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public string? Description { get; set; }

    public string? StripeProductId { get; set; }

    public string? StripePriceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties
    public ICollection<PlanFeature> PlanFeatures { get; set; } = new List<PlanFeature>();

    public ICollection<BusinessPlan> BusinessPlans { get; set; } = new List<BusinessPlan>();
}
