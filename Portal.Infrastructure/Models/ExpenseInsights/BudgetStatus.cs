namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// Budget status enumeration for threshold alerts.
/// </summary>
public enum BudgetStatus
{
    NoLimit,
    WithinLimit,
    Approaching,
    Exceeded
}
