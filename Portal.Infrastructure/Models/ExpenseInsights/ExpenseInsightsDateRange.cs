namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// Resolved date range for expense insights queries.
/// </summary>
public class ExpenseInsightsDateRange
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
