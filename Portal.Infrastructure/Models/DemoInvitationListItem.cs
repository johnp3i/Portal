namespace Portal.Infrastructure.Models;

public class DemoInvitationListItem
{
    public int Id { get; set; }
    public string RecipientEmail { get; set; } = null!;
    public string? RecipientName { get; set; }
    public string BusinessName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
    public int AccessCount { get; set; }
    public DateTime? FirstAccessedAtUtc { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
