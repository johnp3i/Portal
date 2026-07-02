using Portal.Infrastructure.Services;

namespace Portal.Web.BackgroundServices;

/// <summary>
/// Daily background job that evaluates all eligible businesses for payment reminders.
/// Runs at a configurable time (default 06:00 UTC), processes businesses sequentially,
/// and is resilient to individual business failures.
/// </summary>
public class PaymentReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentReminderBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public PaymentReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentReminderBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if background job is enabled
        var isEnabled = _configuration.GetValue<bool>("PaymentReminders:EnableBackgroundJob", true);
        if (!isEnabled)
        {
            _logger.LogInformation("Payment reminder background job is disabled via configuration.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateDelayUntilNextRun();
            _logger.LogInformation("Payment reminder job will next run in {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunDailyEvaluationAsync(stoppingToken);
        }
    }

    private async Task RunDailyEvaluationAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting daily payment reminder evaluation at {Time}", DateTime.UtcNow);

        try
        {
            List<int> businessIds;
            using (var scope = _scopeFactory.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IPaymentReminderService>();
                businessIds = await service.GetEligibleBusinessIdsAsync();
            }

            _logger.LogInformation("Found {Count} eligible businesses for reminder evaluation", businessIds.Count);

            foreach (var businessId in businessIds)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    using var bizScope = _scopeFactory.CreateScope();
                    var bizService = bizScope.ServiceProvider.GetRequiredService<IPaymentReminderService>();
                    var result = await bizService.EvaluateAndSendAsync(businessId, DateOnly.FromDateTime(DateTime.UtcNow));

                    _logger.LogInformation(
                        "Reminder evaluation complete for BusinessId={BusinessId}: {Evaluated} evaluated, {Sent} sent, {Failed} failed",
                        businessId, result.InvoicesEvaluated, result.RemindersSent, result.RemindersFailed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reminder evaluation failed for BusinessId={BusinessId}", businessId);
                    // Continue processing remaining businesses
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in daily payment reminder evaluation");
        }

        _logger.LogInformation("Daily payment reminder evaluation completed at {Time}", DateTime.UtcNow);
    }

    private TimeSpan CalculateDelayUntilNextRun()
    {
        var scheduledTimeStr = _configuration.GetValue<string>("PaymentReminders:ScheduledTimeUtc", "06:00");
        var scheduledTime = TimeOnly.Parse(scheduledTimeStr!);

        var now = DateTime.UtcNow;
        var todayScheduled = now.Date.Add(scheduledTime.ToTimeSpan());

        if (todayScheduled > now)
        {
            return todayScheduled - now;
        }

        // Already past today's time, schedule for tomorrow
        return todayScheduled.AddDays(1) - now;
    }
}
