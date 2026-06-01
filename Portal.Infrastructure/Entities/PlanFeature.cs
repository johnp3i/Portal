namespace Portal.Infrastructure.Entities;

/// <summary>
/// A record associating a specific platform module with a Plan, defining which modules are accessible.
/// Schema: [dbo].PlanFeature
/// </summary>
public class PlanFeature
{
    public int Id { get; set; }

    public int PlanId { get; set; }

    public string ModuleName { get; set; } = null!;

    public bool IsIncluded { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Plan Plan { get; set; } = null!;
}
