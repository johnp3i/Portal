namespace Portal.Web.Models.PromoCode;

public class PromoCodeListItem
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int DurationMonths { get; set; }
    public int CurrentRedemptions { get; set; }
    public int MaxRedemptions { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string? BoundEmail { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
