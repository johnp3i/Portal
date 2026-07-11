namespace Portal.Web.Models.RecurringExpense;

public class SaveRecurringRuleRequest
{
    public int? Id { get; set; }
    public int SupplierId { get; set; }
    public int? ExpenseCategoryId { get; set; }
    public int FrequencyMonths { get; set; }
    public decimal? ExpectedAmount { get; set; }
    public decimal? AmountTolerancePercent { get; set; }
    public int GracePeriodDays { get; set; }
    public string Description { get; set; } = string.Empty;
}
