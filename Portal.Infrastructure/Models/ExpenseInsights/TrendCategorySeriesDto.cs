namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// A single category's monthly data points for the trend chart.
/// </summary>
public class TrendCategorySeriesDto
{
    public string CategoryName { get; set; } = null!;
    public List<decimal> MonthlyTotals { get; set; } = new();
}
