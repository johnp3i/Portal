namespace Portal.Infrastructure.Entities;

/// <summary>
/// A formal receipt document confirming receipt of payment.
/// One receipt per payment event. Global payments produce one receipt with multiple line items.
/// Schema: [revenue].PaymentReceipt
/// </summary>
public class PaymentReceipt
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string ReceiptNumber { get; set; } = null!;
    public int CustomerId { get; set; }
    public int PaymentId { get; set; }
    public DateTime ReceiptDate { get; set; }
    public decimal TotalAmountReceived { get; set; }
    public decimal OutstandingBalanceAfter { get; set; }
    public int PaymentMethodTypeId { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
    public int? SignatureId { get; set; }
    public bool IsVoided { get; set; }
    public string CreatedByUserId { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public ICollection<PaymentReceiptLine> Lines { get; set; } = new List<PaymentReceiptLine>();
}
