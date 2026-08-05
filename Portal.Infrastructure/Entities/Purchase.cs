namespace Portal.Infrastructure.Entities;

/// <summary>
/// An expense entry representing money spent by the Business, with VAT tracking.
/// Schema: [purchase].Purchase
/// </summary>
public class Purchase
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int SupplierId { get; set; }

    public int ExpenseCategoryId { get; set; }

    public int PurchaseOriginTypeId { get; set; }

    public int PurchaseTypeId { get; set; }

    public string? InvoiceNumber { get; set; }

    public DateOnly InvoiceDate { get; set; }

    public string? Description { get; set; }

    public decimal AmountExcludingVat { get; set; }

    public decimal VatAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Country { get; set; }

    public string? Notes { get; set; }

    public bool IsCancelled { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public int? VatSubmissionPeriodId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public int? PayslipPeriodId { get; set; }

    public string? CancelledByUserId { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Supplier Supplier { get; set; } = null!;

    public ExpenseCategory ExpenseCategory { get; set; } = null!;

    public PurchaseOriginType PurchaseOriginType { get; set; } = null!;

    public PurchaseType PurchaseType { get; set; } = null!;

    public VatSubmissionPeriod? VatSubmissionPeriod { get; set; }
}
