using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Models;

/// <summary>
/// Result of a demo invitation creation — includes the invitation entity
/// and a flag indicating whether the email was successfully delivered.
/// </summary>
public class DemoInvitationCreateResult
{
    public DemoInvitation Invitation { get; set; } = null!;

    /// <summary>
    /// True if the invitation email was sent successfully. False if email delivery failed
    /// (the invitation is still persisted — use Resend to retry).
    /// </summary>
    public bool IsEmailSent { get; set; }
}
