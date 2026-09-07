namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Bound from the "Notifications" configuration section. Shared by the producer, dispatcher,
/// and admin alerting.
/// </summary>
public class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool EnableDispatcher { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 50;
    public int DefaultMaxRetries { get; set; } = 5;

    // --- Scheduled digests (Group 3) ---
    /// <summary>Master switch for the digest scheduler background service.</summary>
    public bool EnableScheduler { get; set; } = true;
    /// <summary>How often the digest scheduler wakes to check for due digests.</summary>
    public int SchedulerPollIntervalMinutes { get; set; } = 15;
    public int FailureAlertThreshold { get; set; } = 20;
    public int FailureAlertWindowHours { get; set; } = 24;
    public string DefaultTimeZoneWindowsId { get; set; } = "UTC";
    public string BrandingFooterUrl { get; set; } = "https://www.3inventors.com";
}
