namespace Portal.Infrastructure.Entities.Billing;

/// <summary>
/// A payment record linked to a billing invoice, tracking the monetary transaction.
/// Schema: [billing].Payment
/// </summary>
public class BillingPayment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public decimal AmountEur { get; set; }

    public string Method { get; set; } = null!;

    public DateTime PaidAtUtc { get; set; }

    public string? StripePaymentIntentId { get; set; }

    /// <summary>
    /// Payment reference number (bank transfer ref, cheque number, etc.). NULL for Stripe payments.
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// Free-text notes about the payment. NULL for Stripe payments.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// The SuperAdmin who recorded this manual payment. NULL for Stripe payments.
    /// </summary>
    public string? RecordedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public BillingInvoice BillingInvoice { get; set; } = null!;
}
