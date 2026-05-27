namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object representing a user's permission for a single portal module.
/// PermissionId is null when no UserBusinessPermission record exists yet for the module.
/// </summary>
public class UserModulePermissionDto
{
    public int? PermissionId { get; set; }
    public string Module { get; set; } = null!;
    public string AccessLevel { get; set; } = null!;
    public bool IsActive { get; set; }
}
