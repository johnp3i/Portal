namespace Portal.Infrastructure.Models;

/// <summary>
/// Expense category breakdown item for the P&amp;L report.
/// </summary>
public class PnlCategoryBreakdownDto
{
    public int ExpenseCategoryId { get; set; }
    public string CategoryName { get; set; } = null!;

    /// <summary>"Services" or "Goods".</summary>
    public string ExpenseTypeName { get; set; } = null!;

    /// <summary>2 = Stock (COGS), 3 = Expense (OpEx).</summary>
    public int PurchaseTypeId { get; set; }

    /// <summary>"Stock" or "Expense".</summary>
    public string PurchaseTypeName { get; set; } = null!;

    public decimal TotalAmount { get; set; }

    /// <summary>Percentage of total (COGS + OpEx) combined.</summary>
    public decimal PercentageOfTotal { get; set; }
}
