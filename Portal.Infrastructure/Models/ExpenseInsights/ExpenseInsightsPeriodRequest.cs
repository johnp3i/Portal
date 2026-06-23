using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// Request model for expense insights period queries.
/// </summary>
public class ExpenseInsightsPeriodRequest
{
    public PnlPeriodType PeriodType { get; set; }
    public DateOnly? CustomStartDate { get; set; }
    public DateOnly? CustomEndDate { get; set; }
}
