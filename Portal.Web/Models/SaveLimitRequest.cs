namespace Portal.Web.Models;

public class SaveLimitRequest
{
    public int ExpenseCategoryId { get; set; }
    public decimal? AnnualLimitEur { get; set; }
    public decimal? PeriodLimitEur { get; set; }
}
