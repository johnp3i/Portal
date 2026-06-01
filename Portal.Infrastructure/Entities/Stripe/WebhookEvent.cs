namespace Portal.Infrastructure.Entities.Stripe;

/// <summary>
/// Records processed Stripe webhook events for idempotency and audit purposes.
/// Schema: [stripe].WebhookEvent
/// </summary>
public class WebhookEvent
{
    public int Id { get; set; }

    public string EventId { get; set; } = null!;

    public string Type { get; set; } = null!;

    public DateTime? ProcessedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
