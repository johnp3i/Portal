namespace Portal.Infrastructure.Entities;

/// <summary>
/// Represents a user's dismissal of an announcement in [dbo].[UserAnnouncementDismissals].
/// A unique constraint on (UserId, FeatureAnnouncementId) ensures idempotent dismissals.
/// </summary>
public class UserAnnouncementDismissal
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int FeatureAnnouncementId { get; set; }

    public DateTime DismissedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public FeatureAnnouncement FeatureAnnouncement { get; set; } = null!;
}
