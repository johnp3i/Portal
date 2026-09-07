using Portal.Infrastructure.Repositories.Notification;
using Portal.Infrastructure.Services.Notifications;
using Portal.Web.Services.Email;

namespace Portal.Web.Services.Notifications;

/// <summary>
/// Alerts administrators when notification deliveries fail beyond a configured threshold
/// within a rolling window. Throttled via a durable timestamp in PlatformConfig so a delivery
/// outage produces a single summary alert rather than one per failed message.
///
/// The throttle timestamp is persisted in [dbo].[PlatformConfig] (key
/// <see cref="LastAlertConfigKey"/>) rather than in-memory, so it survives application restarts
/// and is shared across instances in a multi-instance deployment (previously a static field
/// that reset on restart and did not coordinate across processes).
///
/// Channels: (1) email to the configured PlatformConfig recipient list; (2) a high-severity
/// system log entry surfaced in the SuperAdmin SystemLogs viewer (the existing in-app surface).
/// A dedicated real-time SignalR admin toast is a documented future enhancement (needs a hub
/// + an always-connected admin client), out of scope for Phase 1.
/// </summary>
public class NotificationAdminAlertService : INotificationAdminAlertService
{
    public const string RecipientsConfigKey = "AdminNotificationRecipients";

    /// <summary>PlatformConfig key holding the UTC timestamp (ISO-8601 round-trip) of the last alert.</summary>
    public const string LastAlertConfigKey = "NotificationLastFailureAlertUtc";

    private readonly NotificationOutboxRepository _outboxRepository;
    private readonly IPlatformConfigService _platformConfigService;
    private readonly IEmailSender _emailSender;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationAdminAlertService> _logger;

    public NotificationAdminAlertService(
        NotificationOutboxRepository outboxRepository,
        IPlatformConfigService platformConfigService,
        IEmailSender emailSender,
        NotificationOptions options,
        ILogger<NotificationAdminAlertService> logger)
    {
        _outboxRepository = outboxRepository;
        _platformConfigService = platformConfigService;
        _emailSender = emailSender;
        _options = options;
        _logger = logger;
    }

    public async Task CheckAndAlertAsync()
    {
        try
        {
            var windowHours = Math.Max(1, _options.FailureAlertWindowHours);
            var sinceUtc = DateTime.UtcNow.AddHours(-windowHours);

            var failedCount = await _outboxRepository.CountFailedSinceAsync(sinceUtc);
            if (failedCount < _options.FailureAlertThreshold)
                return;

            // Throttle: only one alert per window. The last-alert timestamp is persisted in
            // PlatformConfig so it survives restarts and is shared across instances.
            var lastAlertUtc = await ReadLastAlertUtcAsync();
            if (lastAlertUtc.HasValue && DateTime.UtcNow - lastAlertUtc.Value < TimeSpan.FromHours(windowHours))
                return;

            // Claim this window by recording the alert time BEFORE sending, so a concurrent
            // pass (or the next poll) sees the updated timestamp and does not re-alert.
            await _platformConfigService.SetValueAsync(LastAlertConfigKey, DateTime.UtcNow.ToString("o"));

            // In-app surface: high-severity log (visible in the SystemLogs admin viewer).
            _logger.LogError(
                "Notification delivery alert: {FailedCount} message(s) failed in the last {WindowHours}h (threshold {Threshold}).",
                failedCount, windowHours, _options.FailureAlertThreshold);

            // Email channel.
            var recipientsRaw = await _platformConfigService.GetValueAsync(RecipientsConfigKey);
            if (string.IsNullOrWhiteSpace(recipientsRaw))
            {
                _logger.LogWarning("No {Key} configured — skipping admin failure email.", RecipientsConfigKey);
                return;
            }

            var recipients = recipientsRaw
                .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .Where(r => r.Length > 0)
                .Distinct()
                .ToList();

            var subject = $"[Portal] Notification delivery failures: {failedCount} in {windowHours}h";
            var body = BuildAlertHtml(failedCount, windowHours);

            foreach (var recipient in recipients)
            {
                try
                {
                    await _emailSender.SendEmailAsync(recipient, subject, body, EmailDepartmentEnum.Notifications);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send admin failure alert to {Recipient}.", recipient);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification admin alert check failed.");
        }
    }

    /// <summary>
    /// Reads the last-alert timestamp from PlatformConfig. Returns null when unset or unparseable
    /// (treated as "never alerted"), so a bad value fails safe toward alerting rather than
    /// silently suppressing.
    /// </summary>
    private async Task<DateTime?> ReadLastAlertUtcAsync()
    {
        var raw = await _platformConfigService.GetValueAsync(LastAlertConfigKey);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        _logger.LogWarning(
            "Could not parse {Key} value '{Raw}' as a timestamp; treating as never-alerted.",
            LastAlertConfigKey, raw);
        return null;
    }

    private static string BuildAlertHtml(int failedCount, int windowHours)
        => $@"<!DOCTYPE html><html><body style=""font-family:'Segoe UI',sans-serif;color:#0B1B28;"">
            <h2 style=""color:#C24A4A;"">Notification delivery failures</h2>
            <p><strong>{failedCount}</strong> notification message(s) failed to send in the last <strong>{windowHours} hour(s)</strong>.</p>
            <p>Check the mail server / SMTP configuration and the notification outbox in the Portal.
            Messages that exhausted their retries are marked <em>Failed</em>.</p>
            </body></html>";
}
