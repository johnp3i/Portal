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

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public BillingInvoice BillingInvoice { get; set; } = null!;
}
