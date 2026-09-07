namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Computes the UTC send time from a business's working-hours window and time zone.
/// Falls back to a default time zone (and to UTC) when the business zone is unknown/invalid,
/// so scheduling never throws.
/// </summary>
public class ScheduleResolver : IScheduleResolver
{
    private readonly string _defaultWindowsTimeZoneId;

    public ScheduleResolver(string defaultWindowsTimeZoneId)
    {
        _defaultWindowsTimeZoneId = defaultWindowsTimeZoneId;
    }

    public DateTime ResolveScheduledForUtc(
        TimeOnly? workingHoursStart,
        TimeOnly? workingHoursEnd,
        string? windowsTimeZoneId,
        DateTime nowUtc)
    {
        // No window configured → send immediately.
        if (workingHoursStart is null || workingHoursEnd is null)
            return nowUtc;

        var tz = ResolveTimeZone(windowsTimeZoneId);

        var start = workingHoursStart.Value;
        var end = workingHoursEnd.Value;

        // "Now" in the business's local time.
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var localTimeOfDay = TimeOnly.FromDateTime(localNow);

        DateTime localTarget;

        // Handle a normal same-day window (start <= end). An inverted window (overnight)
        // is treated as "no restriction" defensively — send now — rather than guessing.
        if (start <= end)
        {
            if (localTimeOfDay >= start && localTimeOfDay <= end)
            {
                // Inside the window — send now.
                return nowUtc;
            }
            if (localTimeOfDay < start)
            {
                // Before the window — schedule for today's start.
                localTarget = localNow.Date.Add(start.ToTimeSpan());
            }
            else
            {
                // After the window — schedule for tomorrow's start.
                localTarget = localNow.Date.AddDays(1).Add(start.ToTimeSpan());
            }
        }
        else
        {
            // Overnight/inverted window — do not delay.
            return nowUtc;
        }

        // Convert the local target back to UTC.
        var unspecified = DateTime.SpecifyKind(localTarget, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }

    private TimeZoneInfo ResolveTimeZone(string? windowsTimeZoneId)
    {
        foreach (var candidate in new[] { windowsTimeZoneId, _defaultWindowsTimeZoneId, "UTC" })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (Exception ex)
            {
                // Try the next fallback.
            }
        }
        return TimeZoneInfo.Utc;
    }
}
