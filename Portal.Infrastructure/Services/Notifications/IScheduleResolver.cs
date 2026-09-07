namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Resolves the UTC time at which a message should be sent, honouring a business's optional
/// working-hours window expressed in the business's local time zone.
/// </summary>
public interface IScheduleResolver
{
    /// <summary>
    /// Returns the send time in UTC. If no working-hours window is configured, returns
    /// <paramref name="nowUtc"/> (send immediately). Otherwise, if now is inside the window
    /// (in the business's zone) returns now; if before the window, returns today's window
    /// start; if after, returns tomorrow's window start — all converted back to UTC.
    /// </summary>
    /// <param name="windowsTimeZoneId">Windows time-zone id, or null to use the default.</param>
    DateTime ResolveScheduledForUtc(
        TimeOnly? workingHoursStart,
        TimeOnly? workingHoursEnd,
        string? windowsTimeZoneId,
        DateTime nowUtc);
}
