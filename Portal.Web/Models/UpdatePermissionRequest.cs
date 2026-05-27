namespace Portal.Web.Models;

public class UpdatePermissionRequest
{
    public int UserBusinessId { get; set; }
    public string Module { get; set; } = null!;
    public string AccessLevel { get; set; } = null!;
}
