namespace Portal.Infrastructure.Models;

public class CategorySpendingProgress
{
    public int ExpenseCategoryId { get; set; }
    public decimal? AnnualLimitEur { get; set; }
    public decimal? AnnualSpent { get; set; }
    public int? AnnualYear { get; set; }
    public decimal? PeriodLimitEur { get; set; }
    public decimal? PeriodSpent { get; set; }
    public string? PeriodLabel { get; set; }
}
