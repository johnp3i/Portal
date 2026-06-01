namespace Portal.Infrastructure.Entities.Stripe;

/// <summary>
/// Maps a Portal Business to a Stripe Customer Id for payment correlation.
/// Schema: [stripe].Customer
/// </summary>
public class StripeCustomer
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string StripeCustomerId { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}
