namespace Portal.Infrastructure.Models.ExpenseInsights;

/// <summary>
/// Top-level response containing all insights data for a period.
/// </summary>
public class ExpenseInsightsDto
{
    public ExpenseInsightsSummaryDto Summary { get; set; } = null!;
    public List<ExpenseCategoryBreakdownDto> Categories { get; set; } = new();
    public ExpenseInsightsDateRange Period { get; set; } = null!;
    public int BudgetExceededCount { get; set; }
    public int BudgetApproachingCount { get; set; }
    public bool HasData { get; set; }

    public static ExpenseInsightsDto Empty() => new()
    {
        Summary = new ExpenseInsightsSummaryDto(),
        Categories = new List<ExpenseCategoryBreakdownDto>(),
        Period = new ExpenseInsightsDateRange(),
        HasData = false
    };
}
