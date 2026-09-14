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

    /// <summary>
    /// Period-scoped idempotency key for once-per-period reminders (e.g. the VAT Period Due
    /// Reminder). Unlike <see cref="Daily"/>/<see cref="Weekly"/>, this does NOT vary by date, so the
    /// reminder de-dupes across the whole notice window — at most one non-Failed send per
    /// (business, assistant, period). Format: "{assistantKey}:period-{periodId}".
    /// </summary>
    public static string Period(string assistantKey, int periodId)
        => $"{assistantKey}:period-{periodId}";

    /// <summary>
    /// Recipient-scoped daily key for fan-out assistants (e.g. Task &amp; Meeting Reminder): at most
    /// one send per recipient per business-day. <paramref name="recipientToken"/> derives from the
    /// NORMALISED recipient email (not a team-member id), so two identities resolving to the same
    /// address share one key and are never double-emailed. Format: "{assistantKey}:{yyyy-MM-dd}:{token}".
    /// </summary>
    public static string RecipientDaily(string assistantKey, DateTime businessLocalNow, string recipientToken)
        => $"{assistantKey}:{businessLocalNow:yyyy-MM-dd}:{recipientToken}";
}
