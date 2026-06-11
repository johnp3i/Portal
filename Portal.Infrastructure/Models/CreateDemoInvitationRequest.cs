namespace Portal.Infrastructure.Models;

public class CreateDemoInvitationRequest
{
    public int BusinessId { get; set; }
    public string RecipientEmail { get; set; } = null!;
    public string? RecipientName { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public List<ModulePermissionEntry> Permissions { get; set; } = new();
}
