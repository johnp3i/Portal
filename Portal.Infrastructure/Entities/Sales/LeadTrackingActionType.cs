namespace Portal.Infrastructure.Entities.Sales;

/// <summary>
/// Lookup table for lead tracking action types. Schema: [sales].[LeadTrackingActionType]
/// </summary>
public class LeadTrackingActionType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
