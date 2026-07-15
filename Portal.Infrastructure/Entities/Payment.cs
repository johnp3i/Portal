namespace Portal.Infrastructure.Entities;

/// <summary>
/// A monetary transaction recorded against an Invoice. Payments are never physically deleted;
/// voiding sets IsVoided = 1 (soft-delete pattern).
/// Supports parent-child model for global payment allocation:
///   - Parent payment: InvoiceId=NULL, CustomerId set, children reference via ParentPaymentId
///   - Child payment (allocation): InvoiceId set, ParentPaymentId set, IsAutoAllocated=true for FIFO
///   - Per-invoice payment (legacy): InvoiceId set, ParentPaymentId=NULL
/// Schema: [revenue].Payment
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    /// <summary>
    /// Nullable for parent (global) payments. Set for child allocations and per-invoice payments.
    /// </summary>
    public int? InvoiceId { get; set; }

    /// <summary>
    /// Self-referencing FK — links child allocations to their parent payment.
    /// NULL for parent payments and standalone per-invoice payments.
    /// </summary>
    public int? ParentPaymentId { get; set; }

    /// <summary>
    /// True for system-created FIFO allocations. False for manual selections and per-invoice payments.
    /// </summary>
    public bool IsAutoAllocated { get; set; }

    /// <summary>
    /// FK to Customer — set on parent (global) payments. NULL for children and per-invoice payments.
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// Unallocated overpayment remainder stored on the parent payment.
    /// </summary>
    public decimal CreditAmount { get; set; }

    public int PaymentMethodTypeId { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public bool IsVoided { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedByUserId { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Invoice? Invoice { get; set; }

    public Customer? Customer { get; set; }

    public Payment? ParentPayment { get; set; }

    public ICollection<Payment> ChildAllocations { get; set; } = new List<Payment>();

    public PaymentMethodType PaymentMethodType { get; set; } = null!;
}
