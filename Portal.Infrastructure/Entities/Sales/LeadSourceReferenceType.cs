namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Lookup: defines the specific channel within a lead source (Facebook, LinkedIn, etc.).
/// Schema: [sales].[LeadSourceReferenceType]
/// </summary>
public class LeadSourceReferenceType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }
}
