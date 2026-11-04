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
    /// <summary>The raw payment-method id, carried so the Edit Payment modal can pre-select the current method.</summary>
    public int PaymentMethodTypeId { get; set; }
    public int? ParentPaymentId { get; set; }
    public string? InvoiceNumber { get; set; }
    /// <summary>The numeric Id of the invoice this payment is allocated to (null for unallocated/standalone credit payments).</summary>
    public int? InvoiceId { get; set; }
    public bool IsAutoAllocated { get; set; }
}
