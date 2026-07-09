namespace Portal.Infrastructure.Models.CashFlow;

public class OutflowCategoryDto
{
    public int ExpenseCategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public decimal AverageMonthlyAmount { get; set; }
    public int MonthsOfData { get; set; }
}
