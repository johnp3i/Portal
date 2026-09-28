using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Services.Notifications;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Default <see cref="IBusinessTimeZoneService"/>. Resolves the business time zone via the
/// established two-hop lookup (Business.TimeZoneId -> NotificationTimeZone.WindowsId ->
/// TimeZoneInfo), mirroring ScheduledDigestRunner / ScheduleResolver. Never throws.
/// </summary>
public class BusinessTimeZoneService : IBusinessTimeZoneService
{
    private readonly PortalDbContext _dbContext;
    private readonly NotificationOptions _options;
    private readonly ILogger<BusinessTimeZoneService> _logger;

    public BusinessTimeZoneService(
        PortalDbContext dbContext,
        NotificationOptions options,
        ILogger<BusinessTimeZoneService> logger)
    {
        _dbContext = dbContext;
        _options = options;
        _logger = logger;
    }

    public async Task<TimeZoneInfo> GetTimeZoneAsync(int businessId)
    {
        var timeZoneId = await _dbContext.Businesses
            .AsNoTracking()
            .Where(b => b.Id == businessId)
            .Select(b => b.TimeZoneId)
            .FirstOrDefaultAsync();

        string? windowsId = null;
        if (timeZoneId != null)
        {
            windowsId = await _dbContext.NotificationTimeZones
                .AsNoTracking()
                .Where(tz => tz.Id == timeZoneId)
                .Select(tz => tz.WindowsId)
                .FirstOrDefaultAsync();
        }

        windowsId ??= _options.DefaultTimeZoneWindowsId;

        foreach (var candidate in new[] { windowsId, _options.DefaultTimeZoneWindowsId, "UTC" })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Unknown time zone '{WindowsId}' for BusinessId={BusinessId}; trying next fallback.",
                    candidate, businessId);
            }
        }

        return TimeZoneInfo.Utc;
    }

    public async Task<DateTime> ConvertBusinessLocalToUtcAsync(int businessId, DateTime businessLocal)
    {
        var tz = await GetTimeZoneAsync(businessId);
        // Treat the incoming value as a wall-clock time in the business's zone (ignore any Kind).
        var unspecified = DateTime.SpecifyKind(businessLocal, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }

    public async Task<DateTime> ConvertUtcToBusinessLocalAsync(int businessId, DateTime utc)
    {
        var tz = await GetTimeZoneAsync(businessId);
        var asUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, tz);
    }
}
