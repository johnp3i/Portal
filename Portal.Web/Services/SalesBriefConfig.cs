namespace Portal.Web.Services;

/// <summary>
/// Shared helper for the look-ahead window used by the dashboard "Today's Brief" and the pipeline
/// "Today's Actions" panel. The window (in days) is a <b>per-business</b> setting stored on
/// [portal].[BusinessProfile].SalesBriefLookAheadDays. When a business has not set a value (NULL),
/// callers fall back to <see cref="DefaultLookAheadDays"/>. The resolved value is clamped to a sane
/// range so a bad value can't produce an empty or absurd window.
/// </summary>
public static class SalesBriefConfig
{
    /// <summary>Window used when the business has not configured a value.</summary>
    public const int DefaultLookAheadDays = 7;

    // Guard rails so a bad stored value can't produce an empty or absurd window.
    public const int MinLookAheadDays = 0;
    public const int MaxLookAheadDays = 60;

    /// <summary>
    /// Resolves the effective look-ahead window from a business's configured value, falling back to
    /// the default when it is null and clamping to [MinLookAheadDays, MaxLookAheadDays].
    /// </summary>
    public static int ResolveLookAheadDays(int? businessConfiguredDays)
    {
        var days = businessConfiguredDays ?? DefaultLookAheadDays;
        if (days < MinLookAheadDays) return MinLookAheadDays;
        if (days > MaxLookAheadDays) return MaxLookAheadDays;
        return days;
    }
}
