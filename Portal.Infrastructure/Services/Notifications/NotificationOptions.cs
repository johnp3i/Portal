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

    /// <summary>
    /// Days after a VAT period's end date by which the return must be filed. The schema stores
    /// no filing deadline, so the Financial Snapshot derives it as PeriodEndDate + this offset.
    /// Default 40 (Cyprus: VAT is due by the 10th day of the second month after period end,
    /// ~40 days). Adjust per jurisdiction.
    /// </summary>
    public int VatFilingOffsetDays { get; set; } = 40;

    /// <summary>
    /// Only surface the VAT-deadline line when the filing deadline is within this many days
    /// (avoids nagging about a deadline that is months away). Default 21.
    /// </summary>
    public int VatDeadlineNoticeDays { get; set; } = 21;
    public int FailureAlertThreshold { get; set; } = 20;
    public int FailureAlertWindowHours { get; set; } = 24;
    public string DefaultTimeZoneWindowsId { get; set; } = "UTC";
    public string BrandingFooterUrl { get; set; } = "https://www.3inventors.com";
}
