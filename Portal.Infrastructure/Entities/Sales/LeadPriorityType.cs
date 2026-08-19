namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Lookup: defines lead priority levels (Hot, Warm, Cold).
/// Schema: [sales].[LeadPriorityType]
/// </summary>
public class LeadPriorityType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public string Colour { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}
