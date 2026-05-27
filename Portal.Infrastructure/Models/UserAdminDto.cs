namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object representing a business user for the admin user list.
/// </summary>
public class UserAdminDto
{
    public int UserBusinessId { get; set; }
    public string UserId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime? LastLoginUtc { get; set; }
}
