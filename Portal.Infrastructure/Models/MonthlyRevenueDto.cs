namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for a monthly revenue data point used in dashboard charts.
/// </summary>
public class MonthlyRevenueDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = null!;
    public decimal Amount { get; set; }
    public bool IncludesPosRevenue { get; set; }
}
