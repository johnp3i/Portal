namespace Portal.Infrastructure.Entities;

/// <summary>
/// A promotional code that grants a free trial subscription to a prospect.
/// Schema: [dbo].PromoCode
/// </summary>
public class PromoCode
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public int DurationMonths { get; set; }

    public int MaxRedemptions { get; set; }

    public int CurrentRedemptions { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public string? BoundEmail { get; set; }

    public bool IsRevoked { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public ICollection<PromoCodeRedemption> Redemptions { get; set; } = new List<PromoCodeRedemption>();
}
