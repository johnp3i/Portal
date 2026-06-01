namespace Portal.Web.Models.Stripe;

public class ProvisioningResult
{
    public bool Success { get; set; }
    public int? BusinessId { get; set; }
    public string? ErrorMessage { get; set; }
}
