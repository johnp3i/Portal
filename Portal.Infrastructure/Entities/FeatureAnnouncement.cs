namespace Portal.Infrastructure.Entities;

/// <summary>
/// Represents a feature announcement record in [dbo].[FeatureAnnouncements].
/// Used by the What's New system to communicate newly released features to Portal users.
/// </summary>
public class FeatureAnnouncement
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string DetailHtml { get; set; } = string.Empty;

    public string? ModuleKey { get; set; }

    public string? CtaLabel { get; set; }

    public string? CtaUrl { get; set; }

    public string? TargetPlanTier { get; set; }

    public bool IsActive { get; set; }

    public DateTime PublishedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
