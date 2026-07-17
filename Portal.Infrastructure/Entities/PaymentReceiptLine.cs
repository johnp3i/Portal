namespace Portal.Infrastructure.Entities;

/// <summary>
/// A single line item on a payment receipt, linking the receipt to a specific invoice payment.
/// For global payments, there are multiple lines (one per child allocation).
/// Schema: [revenue].PaymentReceiptLine
/// </summary>
public class PaymentReceiptLine
{
    public int Id { get; set; }
    public int PaymentReceiptId { get; set; }
    public int PaymentId { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal InvoiceTotal { get; set; }
    public decimal InvoiceOutstandingBefore { get; set; }
    public decimal InvoiceOutstandingAfter { get; set; }
}
