namespace Portal.Infrastructure.Entities;

/// <summary>
/// A record tracking a promo code redemption by a user for a specific business.
/// Schema: [dbo].PromoCodeRedemption
/// </summary>
public class PromoCodeRedemption
{
    public int Id { get; set; }

    public int PromoCodeId { get; set; }

    public string UserId { get; set; } = null!;

    public int BusinessId { get; set; }

    public DateTime RedeemedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public PromoCode PromoCode { get; set; } = null!;

    public Business Business { get; set; } = null!;
}
