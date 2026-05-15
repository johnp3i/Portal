namespace Portal.Infrastructure.Entities.Identity;

public class UserBusinessPermission
{
    public int Id { get; set; }
    public int UserBusinessId { get; set; }
    public string Module { get; set; } = null!;
    public string AccessLevel { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public UserBusiness UserBusiness { get; set; } = null!;
}
