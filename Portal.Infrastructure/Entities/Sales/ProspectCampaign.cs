namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// A named prospecting push for one product, with a weekly cold-call target and a date range.
/// Contains prospects being researched/approached before they enter the lead pipeline.
/// Schema: [sales].[ProspectCampaign]
/// </summary>
public class ProspectCampaign
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>Optional FK to [sales].[Product] (SalesProduct) — the product this campaign promotes.</summary>
    public int? SalesProductId { get; set; }

    public string? Description { get; set; }

    public string? Market { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int WeeklyCallTarget { get; set; }

    public string? OwnerUserId { get; set; }

    /// <summary>1 Draft, 2 Active, 3 Closed.</summary>
    public byte Status { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public SalesProduct? SalesProduct { get; set; }

    public ICollection<Prospect> Prospects { get; set; } = new List<Prospect>();
}
