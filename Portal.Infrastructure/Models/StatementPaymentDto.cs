namespace Portal.Infrastructure.Models;

/// <summary>
/// Internal query result representing a payment within the statement period.
/// </summary>
public class StatementPaymentDto
{
    public int Id { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string PaymentMethodName { get; set; } = string.Empty;
    public int? ParentPaymentId { get; set; }
    public string? InvoiceNumber { get; set; }
    public bool IsAutoAllocated { get; set; }
}
