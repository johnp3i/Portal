namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// Request model for budget limit updates.
/// </summary>
public class UpdateBudgetLimitRequest
{
    public int ExpenseCategoryId { get; set; }
    public decimal? PeriodLimitEur { get; set; }
}
