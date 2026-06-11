namespace Portal.Infrastructure.Entities;

/// <summary>
/// A demo access invitation sent to a prospect, containing a magic link token
/// for auto-authenticated access to a demo business with configured permissions.
/// Schema: [portal].DemoInvitation
/// </summary>
public class DemoInvitation
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string Token { get; set; } = null!;

    public string RecipientEmail { get; set; } = null!;

    public string? RecipientName { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public string Status { get; set; } = null!; // 'sent', 'accessed', 'expired', 'revoked'

    public string CreatedByUserId { get; set; } = null!;

    public DateTime? FirstAccessedAtUtc { get; set; }

    public DateTime? LastAccessedAtUtc { get; set; }

    public int AccessCount { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ICollection<DemoInvitationPermission> Permissions { get; set; } = new List<DemoInvitationPermission>();
}
