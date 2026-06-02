namespace Portal.Web.Models.PromoCode;

public class PromoCodeValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public int? PromoCodeId { get; set; }
    public int? DurationMonths { get; set; }
}
