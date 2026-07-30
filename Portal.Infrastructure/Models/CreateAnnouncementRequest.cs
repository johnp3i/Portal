namespace Portal.Infrastructure.Models;

/// <summary>
/// Request model for creating a new feature announcement.
/// </summary>
public class CreateAnnouncementRequest
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string DetailHtml { get; set; } = string.Empty;
    public string? ModuleKey { get; set; }
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }
    public string? TargetPlanTier { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime PublishedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
