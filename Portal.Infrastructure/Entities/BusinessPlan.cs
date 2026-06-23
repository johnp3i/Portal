namespace Portal.Infrastructure.Entities;

/// <summary>
/// The association between a Business (tenant) and their active Plan, including subscription lifecycle dates.
/// Schema: [dbo].BusinessPlan
/// </summary>
public class BusinessPlan
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int PlanId { get; set; }

    public DateTime StartDateUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }

    public bool IsActive { get; set; }

    public string Status { get; set; } = "active";

    public DateTime? TrialEndsAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Plan Plan { get; set; } = null!;
}
