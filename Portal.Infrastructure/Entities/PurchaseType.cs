namespace Portal.Infrastructure.Entities;

/// <summary>
/// Lookup table classifying the type of a Purchase.
/// Schema: [purchase].PurchaseType
/// Values: Asset (1), Stock (2), Expense (3)
/// </summary>
public class PurchaseType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
