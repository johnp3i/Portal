namespace Portal.Web.Models;

/// <summary>
/// Request model for changing a business subscription's status via SuperAdmin.
/// </summary>
public class ChangeSubscriptionStatusRequest
{
    public int BusinessPlanId { get; set; }

    public string Status { get; set; } = null!;
}
