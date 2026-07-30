namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for a payment history row displayed on the invoice detail view.
/// </summary>
public class PaymentHistoryDto
{
    public int Id { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public bool IsVoided { get; set; }

    /// <summary>
    /// True if this payment was created by the FIFO allocation engine.
    /// </summary>
    public bool IsAutoAllocated { get; set; }

    /// <summary>
    /// Set when this payment is a child allocation — references the parent payment Id.
    /// </summary>
    public int? ParentPaymentId { get; set; }

    /// <summary>
    /// The parent payment's reference (for display: "Auto-allocated from Payment [REF]").
    /// </summary>
    public string? ParentReference { get; set; }

    /// <summary>
    /// True when PaymentDateUtc is in the future — payment is recorded but not yet counted toward paid totals.
    /// </summary>
    public bool IsUpcoming { get; set; }
}
