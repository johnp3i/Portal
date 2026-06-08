namespace Portal.Infrastructure.Entities;

/// <summary>
/// A configured spending threshold for an expense category within a business.
/// Schema: [purchase].ExpenseCategoryLimit
/// </summary>
public class ExpenseCategoryLimit
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int ExpenseCategoryId { get; set; }

    public decimal? AnnualLimitEur { get; set; }

    public decimal? PeriodLimitEur { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ExpenseCategory ExpenseCategory { get; set; } = null!;
}
