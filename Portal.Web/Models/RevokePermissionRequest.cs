namespace Portal.Web.Models;

public class RevokePermissionRequest
{
    public string UserId { get; set; } = null!;
    public string Module { get; set; } = null!;
}
