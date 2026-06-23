namespace Portal.Web.Models;

/// <summary>
/// View model representing a single expense category's budget configuration and current spend status.
/// </summary>
public class ExpenseCategoryLimitViewModel
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal? CurrentLimit { get; set; }
    public decimal CurrentSpend { get; set; }
    public string BudgetStatus { get; set; } = "No Limit";
}
