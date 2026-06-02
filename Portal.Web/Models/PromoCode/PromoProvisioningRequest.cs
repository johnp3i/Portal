namespace Portal.Web.Models.PromoCode;

public class PromoProvisioningRequest
{
    public string UserId { get; set; } = null!;
    public int PendingRegistrationId { get; set; }
    public int PlanId { get; set; }
    public int PromoCodeId { get; set; }
    public int DurationMonths { get; set; }
}
