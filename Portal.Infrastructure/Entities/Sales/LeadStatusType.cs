namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Lookup: defines pipeline stages for lead progression (New, Contacted, Won, etc.).
/// Schema: [sales].[LeadStatusType]
/// </summary>
public class LeadStatusType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int DisplayOrder { get; set; }

    public string? Colour { get; set; }

    public bool IsTerminal { get; set; }
}
