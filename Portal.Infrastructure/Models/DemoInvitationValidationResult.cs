using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Models;

public class DemoInvitationValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorReason { get; set; } // "invalid", "expired", "revoked"
    public DemoInvitation? Invitation { get; set; }
}
