using System.Globalization;

namespace Portal.Infrastructure.Services.Notifications;

/// <summary>
/// Deterministic per-cycle idempotency key for scheduled digests, stored verbatim on
/// <c>OutboxMessage.CycleKey</c> (no hashing). Format: "{assistantKey}:{ISOYear}-W{ISOWeek}".
/// Computed from the business-local date so a business's "week" aligns with its time zone.
/// </summary>
public static class DigestCycleKey
{
    /// <summary>Builds the weekly cycle key for the given assistant and business-local date.</summary>
    public static string Weekly(string assistantKey, DateTime businessLocalNow)
    {
        var date = businessLocalNow.Date;
        // ISO-8601 week + ISO year (the ISO year can differ from the calendar year near Jan 1).
        var isoWeek = ISOWeek.GetWeekOfYear(date);
        var isoYear = ISOWeek.GetYear(date);
        return $"{assistantKey}:{isoYear:D4}-W{isoWeek:D2}";
    }

    /// <summary>
    /// Builds the daily cycle key for the given assistant and business-local date.
    /// Format: "{assistantKey}:{yyyy-MM-dd}". The outbox cycle-dedup then guarantees at most one
    /// send per business per day.
    /// </summary>
    public static string Daily(string assistantKey, DateTime businessLocalNow)
        => $"{assistantKey}:{businessLocalNow:yyyy-MM-dd}";
}
