namespace Portal.Infrastructure.Entities.Billing;

/// <summary>
/// A billing invoice record for a subscription payment period.
/// Schema: [billing].Invoice
/// </summary>
public class BillingInvoice
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string? StripeInvoiceId { get; set; }

    public decimal AmountEur { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? PaidAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ICollection<BillingPayment> BillingPayments { get; set; } = new List<BillingPayment>();
}
