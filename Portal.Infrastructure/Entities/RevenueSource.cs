namespace Portal.Infrastructure.Entities;

/// <summary>
/// A configurable label representing where external revenue comes from (e.g., POS device, register, online store).
/// Schema: [revenue].RevenueSource
/// </summary>
public class RevenueSource
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ICollection<RevenueSummary> RevenueSummaries { get; set; } = new List<RevenueSummary>();
}
