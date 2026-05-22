namespace Portal.Infrastructure.Entities;

/// <summary>
/// Lookup table classifying the geographic origin of a Purchase.
/// Schema: [purchase].PurchaseOriginType
/// Values: Domestic (1), EuReverseCharge (2), NonEu (3)
/// </summary>
public class PurchaseOriginType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
