namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// Summary KPI cards data.
/// </summary>
public class ExpenseInsightsSummaryDto
{
    public decimal TotalSpend { get; set; }
    public int CategoriesWithSpend { get; set; }
    public string? TopCategoryName { get; set; }
    public decimal AveragePerCategory { get; set; }
}
