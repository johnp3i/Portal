namespace Portal.Infrastructure.Entities;

/// <summary>
/// A configured module permission for a demo invitation, defining what access level
/// the demo session user has for a specific platform module.
/// Schema: [portal].DemoInvitationPermission
/// </summary>
public class DemoInvitationPermission
{
    public int Id { get; set; }

    public int DemoInvitationId { get; set; }

    public string Module { get; set; } = null!;

    public string AccessLevel { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public DemoInvitation DemoInvitation { get; set; } = null!;
}
