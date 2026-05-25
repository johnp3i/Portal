namespace Portal.Infrastructure.Models;

/// <summary>
/// Input data transfer object for recording a payment against an invoice.
/// </summary>
public class RecordPaymentDto
{
    public int InvoiceId { get; set; }
    public int PaymentMethodTypeId { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}
