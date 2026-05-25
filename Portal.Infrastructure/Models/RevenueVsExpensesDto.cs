namespace Portal.Infrastructure.Models;

/// <summary>
/// Monthly revenue vs expenses data point for the bar chart on the home dashboard.
/// </summary>
public class RevenueVsExpensesDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = null!;
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
}
