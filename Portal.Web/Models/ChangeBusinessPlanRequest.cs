namespace Portal.Web.Models;

/// <summary>
/// Request model for changing a business's subscription plan via SuperAdmin.
/// </summary>
public class ChangeBusinessPlanRequest
{
    public int BusinessId { get; set; }

    public int PlanId { get; set; }
}
