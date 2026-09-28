namespace Portal.Infrastructure.Services;

/// <summary>
/// Resolves a business's local time zone and converts between the business's local wall-clock
/// time and UTC. The canonical "local" for a business is defined by <c>Business.TimeZoneId</c>
/// (FK to <c>[notification].[TimeZone]</c>), falling back to the platform default when unset.
///
/// Use this wherever a meeting (or any business-scoped) time is saved from, or displayed to, a
/// user in their business's local time: persist UTC, present business-local.
/// </summary>
public interface IBusinessTimeZoneService
{
    /// <summary>
    /// Resolves the <see cref="TimeZoneInfo"/> for the given business. Never throws — falls back
    /// to the platform default, then UTC, if the configured zone id is missing/unknown.
    /// </summary>
    Task<TimeZoneInfo> GetTimeZoneAsync(int businessId);

    /// <summary>
    /// Interprets <paramref name="businessLocal"/> as a wall-clock time in the business's local
    /// zone and returns the equivalent UTC instant. Used on save.
    /// </summary>
    Task<DateTime> ConvertBusinessLocalToUtcAsync(int businessId, DateTime businessLocal);

    /// <summary>
    /// Converts a UTC instant to the business's local wall-clock time. Used on read/display.
    /// </summary>
    Task<DateTime> ConvertUtcToBusinessLocalAsync(int businessId, DateTime utc);
}
