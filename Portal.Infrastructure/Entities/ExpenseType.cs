namespace Portal.Infrastructure.Entities;

/// <summary>
/// Lookup table classifying the type of an Expense Category.
/// Schema: [purchase].ExpenseType
/// Values: Services (1), Goods (2)
/// </summary>
public class ExpenseType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
