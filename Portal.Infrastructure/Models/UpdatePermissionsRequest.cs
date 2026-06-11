namespace Portal.Infrastructure.Models;

public class UpdatePermissionsRequest
{
    public int InvitationId { get; set; }
    public List<ModulePermissionEntry> Permissions { get; set; } = new();
}
