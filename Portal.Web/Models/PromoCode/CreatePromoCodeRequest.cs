namespace Portal.Web.Models.PromoCode;

public class CreatePromoCodeRequest
{
    public int DurationMonths { get; set; }
    public int MaxRedemptions { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? BoundEmail { get; set; }
}
