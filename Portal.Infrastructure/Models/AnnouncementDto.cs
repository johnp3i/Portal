namespace Portal.Infrastructure.Models;

/// <summary>
/// DTO returned to user-facing components (panel + banner).
/// </summary>
public class AnnouncementDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string DetailHtml { get; set; } = string.Empty;
    public string? ModuleKey { get; set; }
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }
    public string? TargetPlanTier { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public bool IsDismissed { get; set; }

    /// <summary>
    /// True if both CtaLabel and CtaUrl are non-empty.
    /// </summary>
    public bool HasCta => !string.IsNullOrEmpty(CtaLabel) && !string.IsNullOrEmpty(CtaUrl);
}
