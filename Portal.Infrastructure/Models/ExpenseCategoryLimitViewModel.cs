namespace Portal.Infrastructure.Models;

public class ExpenseCategoryLimitViewModel
{
    public int ExpenseCategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal? AnnualLimitEur { get; set; }
    public decimal? PeriodLimitEur { get; set; }
}
