using Portal.Infrastructure.Repositories.Notification;
using Portal.Infrastructure.Services.Notifications;
using Portal.Web.Services.Email;
using Portal.Web.Services.Notifications;

namespace Portal.Web.BackgroundServices;

/// <summary>
/// In-process background service that drains the notification outbox. Polls on a configurable
/// interval, sends each due message via IEmailSender, and records success/retry/failure.
/// Runs across all businesses (no tenant context). Mirrors PaymentReminderBackgroundService:
/// BackgroundService + Task.Delay loop + IServiceScopeFactory scope-per-iteration + resilient
/// per-message try/catch so one poison message never stops the batch.
/// </summary>
public class NotificationDispatcherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDispatcherBackgroundService> _logger;
    private readonly NotificationOptions _options;

    public NotificationDispatcherBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationDispatcherBackgroundService> logger,
        NotificationOptions options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableDispatcher)
        {
            _logger.LogInformation("Notification dispatcher is disabled via configuration.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var outboxRepository = scope.ServiceProvider.GetRequiredService<NotificationOutboxRepository>();
            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            var due = await outboxRepository.GetDuePendingAsync(_options.BatchSize, DateTime.UtcNow);
            if (due.Count == 0)
                return;

            _logger.LogInformation("Notification dispatcher processing {Count} due message(s).", due.Count);

            foreach (var message in due)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    await emailSender.SendEmailAsync(
                        message.RecipientEmail,
                        message.Subject,
                        message.BodyHtml,
                        EmailDepartmentEnum.Notifications,
                        message.ReplyToEmail);

                    await outboxRepository.MarkSentAsync(message.Id);
                }
                catch (Exception ex)
                {
                    // Retry cadence: a failed message stays Pending and is retried on the NEXT
                    // poll (fixed interval — no exponential backoff), consuming one retry per
                    // poll. With the default 60s interval and MaxRetries=5, a persistently
                    // failing message reaches Failed after ~5 polls (~5 minutes). ScheduledForUtc
                    // is intentionally left unchanged so the message remains due.
                    var error = ex.Message;
                    if (message.RetryCount + 1 >= message.MaxRetries)
                    {
                        await outboxRepository.MarkFailedAsync(message.Id, error);
                        _logger.LogError(ex,
                            "Notification message {Id} failed permanently after {Retries} attempt(s).",
                            message.Id, message.RetryCount + 1);
                    }
                    else
                    {
                        await outboxRepository.MarkRetryAsync(message.Id, error);
                        _logger.LogWarning(ex,
                            "Notification message {Id} send failed (attempt {Attempt}); will retry.",
                            message.Id, message.RetryCount + 1);
                    }
                }
            }

            // After the batch, check whether persistent failures warrant an admin alert.
            try
            {
                var alertService = scope.ServiceProvider.GetRequiredService<INotificationAdminAlertService>();
                await alertService.CheckAndAlertAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification admin failure-alert check failed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in notification dispatcher run.");
        }
    }
}
