namespace Portal.Infrastructure.Entities.Identity;

/// <summary>
/// Represents a pending invitation token for user registration.
/// Stored in the Membership database.
/// </summary>
public class Invitation
{
    /// <summary>
    /// Auto-increment primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Invited email address.
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Target business for the invited user.
    /// </summary>
    public int BusinessId { get; set; }

    /// <summary>
    /// Unique invitation token.
    /// </summary>
    public string Token { get; set; } = null!;

    /// <summary>
    /// When the invitation was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Token expiry (CreatedAtUtc + 72 hours).
    /// </summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// Whether the invitation has been redeemed.
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// The SuperAdmin who created the invitation.
    /// </summary>
    public string CreatedByUserId { get; set; } = null!;

    /// <summary>
    /// JSON-serialized list of module permissions to apply on registration.
    /// Format: [{"Module":"customer","AccessLevel":"full"}, ...]
    /// </summary>
    public string? ModulePermissionsJson { get; set; }
}
