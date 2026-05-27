namespace Portal.Infrastructure.Entities.Identity;

public class UserBusiness
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int BusinessId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsOwner { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
