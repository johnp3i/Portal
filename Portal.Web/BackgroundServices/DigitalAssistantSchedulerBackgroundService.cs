using Portal.Infrastructure.Services.Notifications;

namespace Portal.Web.BackgroundServices;

/// <summary>
/// In-process background service that drives the scheduled owner-facing digests (Group 3).
/// On an interval it asks <see cref="IScheduledDigestRunner"/> to find businesses whose digest
/// is due this cycle and enqueue it into the notification outbox; the existing
/// <see cref="NotificationDispatcherBackgroundService"/> then sends it. Kept separate from the
/// dispatcher: the scheduler decides WHAT/WHEN to enqueue, the dispatcher DRAINS the outbox.
/// Mirrors the dispatcher's shape: Task.Delay loop + scope-per-iteration + resilient try/catch.
/// </summary>
public class DigitalAssistantSchedulerBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DigitalAssistantSchedulerBackgroundService> _logger;
    private readonly NotificationOptions _options;

    public DigitalAssistantSchedulerBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DigitalAssistantSchedulerBackgroundService> logger,
        NotificationOptions options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableScheduler)
        {
            _logger.LogInformation("Digital Assistant digest scheduler is disabled via configuration.");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.SchedulerPollIntervalMinutes));

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
            var runner = scope.ServiceProvider.GetRequiredService<IScheduledDigestRunner>();
            await runner.RunAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Digital Assistant digest scheduler run.");
        }
    }
}
