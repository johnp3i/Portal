namespace Portal.Infrastructure.Entities;

/// <summary>
/// Tracks a Stripe Checkout Session created for an invoice payment.
/// Used for idempotency (unique StripeSessionId) and fee transparency.
/// Schema: [stripe].CheckoutSession
/// </summary>
public class StripeCheckoutSession
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int InvoiceId { get; set; }

    public string StripeSessionId { get; set; } = null!;

    public decimal Amount { get; set; }

    public decimal? StripeFeeAmount { get; set; }

    public decimal? NetAmount { get; set; }

    public string Currency { get; set; } = "EUR";

    public string Status { get; set; } = "pending";

    public string? StripePaymentIntentId { get; set; }

    public string? StripeChargeId { get; set; }

    public int? PaymentId { get; set; }

    public string? CustomerName { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    // Navigation
    public Business Business { get; set; } = null!;

    public Invoice Invoice { get; set; } = null!;
}
