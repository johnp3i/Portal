namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Lookup: defines where a lead originated (Website, Referral, Event, etc.).
/// Schema: [sales].[LeadSourceType]
/// </summary>
public class LeadSourceType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }
}
