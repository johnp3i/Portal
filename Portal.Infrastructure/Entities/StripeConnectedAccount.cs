namespace Portal.Infrastructure.Entities;

/// <summary>
/// Represents a business's Stripe Connect linked account.
/// Schema: [stripe].ConnectedAccount
/// </summary>
public class StripeConnectedAccount
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string StripeAccountId { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime ConnectedAtUtc { get; set; }

    public DateTime? DisconnectedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public Business Business { get; set; } = null!;
}
