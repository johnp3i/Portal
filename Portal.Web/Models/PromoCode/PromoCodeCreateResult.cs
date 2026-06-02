namespace Portal.Web.Models.PromoCode;

public class PromoCodeCreateResult
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? ErrorMessage { get; set; }
}
