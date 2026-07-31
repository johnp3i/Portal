namespace Portal.Infrastructure.Entities;

/// <summary>
/// A recurring expense rule defining expected purchase frequency and optional amount for a supplier.
/// Schema: [purchase].SupplierRecurringRule
/// </summary>
public class SupplierRecurringRule
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int SupplierId { get; set; }

    public int? ExpenseCategoryId { get; set; }

    public int FrequencyMonths { get; set; }

    public decimal? ExpectedAmount { get; set; }

    public decimal? AmountTolerancePercent { get; set; }

    public int GracePeriodDays { get; set; }

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Supplier Supplier { get; set; } = null!;

    public ExpenseCategory? ExpenseCategory { get; set; }
}
