namespace Portal.Infrastructure.Models;

/// <summary>
/// DTO for the admin management list.
/// </summary>
public class AdminAnnouncementDto
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

    /// <summary>
    /// Computed status: "Active", "Expired", "Scheduled", "Inactive"
    /// </summary>
    public string Status
    {
        get
        {
            if (!IsActive) return "Inactive";
            if (ExpiresAtUtc.HasValue && ExpiresAtUtc.Value < DateTime.UtcNow) return "Expired";
            if (PublishedAtUtc > DateTime.UtcNow) return "Scheduled";
            return "Active";
        }
    }
}
