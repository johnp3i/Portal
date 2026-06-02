namespace Portal.Infrastructure.Entities;

/// <summary>
/// A key-value platform-wide configuration setting stored in [dbo].[PlatformConfig].
/// Used for feature flags and platform settings (e.g., ShowPromoCodeField, TrialBadgeText).
/// </summary>
public class PlatformConfig
{
    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime LastModifiedAtUtc { get; set; }
}
