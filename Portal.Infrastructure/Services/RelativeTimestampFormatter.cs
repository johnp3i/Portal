namespace Portal.Infrastructure.Services;

/// <summary>
/// Formats UTC timestamps into human-readable relative strings.
/// Static utility — no DI needed.
/// </summary>
public static class RelativeTimestampFormatter
{
    /// <summary>
    /// Formats a UTC timestamp relative to the current time (or provided reference time).
    /// </summary>
    public static string Format(DateTime timestampUtc, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var diff = now - timestampUtc;

        if (diff.TotalSeconds < 60)
            return "Just now";

        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} min ago";

        if (diff.TotalHours < 24)
        {
            var hours = (int)diff.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        // Check if it's yesterday (calendar day comparison)
        var yesterday = now.Date.AddDays(-1);
        if (timestampUtc.Date == yesterday)
            return $"Yesterday at {timestampUtc:HH:mm}";

        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} days ago";

        return timestampUtc.ToString("dd MMM yyyy");
    }
}
