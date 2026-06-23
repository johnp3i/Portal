namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// A single category row in the breakdown table.
/// </summary>
public class ExpenseCategoryBreakdownDto
{
    public int ExpenseCategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public string ExpenseTypeName { get; set; } = null!;
    public decimal TotalSpend { get; set; }
    public decimal PercentageOfTotal { get; set; }
    public string Variance { get; set; } = "\u2014";
    public decimal? VarianceValue { get; set; }
    public decimal? BudgetLimit { get; set; }
    public string BudgetStatus { get; set; } = "No Limit";
    public List<TopSupplierDto> TopSuppliers { get; set; } = new();
}
