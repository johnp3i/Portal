namespace Portal.Web.Models;

public class ToggleStatusRequest
{
    public int UserBusinessId { get; set; }
    public bool Activate { get; set; }  // true = reactivate, false = deactivate
}
