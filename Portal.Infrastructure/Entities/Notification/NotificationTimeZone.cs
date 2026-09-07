namespace Portal.Infrastructure.Entities.Notification;

/// <summary>
/// A supported time zone for per-business working-hours scheduling.
/// Schema: [notification].TimeZone (seeded reference). Class name is NotificationTimeZone
/// to avoid clashing with System.TimeZone.
/// </summary>
public class NotificationTimeZone
{
    public int Id { get; set; }

    public string DisplayName { get; set; } = null!;

    /// <summary>Windows time-zone id (e.g. "GTB Standard Time") for TimeZoneInfo on Windows hosting.</summary>
    public string WindowsId { get; set; } = null!;

    /// <summary>IANA id (e.g. "Europe/Nicosia") for portability.</summary>
    public string IanaId { get; set; } = null!;
}
