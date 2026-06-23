namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// Trend data for the 12-month line chart.
/// </summary>
public class ExpenseInsightsTrendDto
{
    public List<string> MonthLabels { get; set; } = new();
    public List<TrendCategorySeriesDto> Series { get; set; } = new();
    public bool HasSufficientData { get; set; }
}
