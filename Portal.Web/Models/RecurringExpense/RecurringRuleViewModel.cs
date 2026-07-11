namespace Portal.Web.Models.RecurringExpense;

public class RecurringRuleViewModel
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int? ExpenseCategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int FrequencyMonths { get; set; }
    public string FrequencyLabel { get; set; } = string.Empty;
    public decimal? ExpectedAmount { get; set; }
    public decimal? AmountTolerancePercent { get; set; }
    public int GracePeriodDays { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
