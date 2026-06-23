namespace Portal.Web.Models;

public class GrantPermissionRequest
{
    public string UserId { get; set; } = null!;
    public string Module { get; set; } = null!;
    public string AccessLevel { get; set; } = null!;
}
