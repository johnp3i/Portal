using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.ExpenseInsights;

namespace Portal.Web.Models;

/// <summary>
/// View model for the Expense Insights Index page.
/// </summary>
public class ExpenseInsightsViewModel
{
    public ExpenseInsightsDto InsightsData { get; set; } = null!;
    public ExpenseInsightsTrendDto TrendData { get; set; } = null!;
    public List<ExpenseCategoryLimitViewModel> BudgetConfig { get; set; } = new();
    public string CurrencySymbol { get; set; } = "€";
    public PnlPeriodType SelectedPeriod { get; set; } = PnlPeriodType.CurrentMonth;
}
