namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// A supplier row within a category expansion.
/// </summary>
public class TopSupplierDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public decimal TotalSpend { get; set; }
    public decimal PercentageOfCategory { get; set; }
}
