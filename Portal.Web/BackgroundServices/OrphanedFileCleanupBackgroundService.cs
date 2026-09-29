using Portal.Infrastructure.Services;

namespace Portal.Web.BackgroundServices;

/// <summary>
/// Nightly orphaned-file scan (Phase 4b-1 — REPORT-ONLY: records candidates, never deletes). Runs
/// once a day at a configurable UTC time; each run is a no-op unless the SuperAdmin has enabled the
/// cleanup (PlatformConfig OrphanedFileCleanupEnabled = 'true'). Mirrors PaymentReminderBackgroundService.
/// </summary>
public class OrphanedFileCleanupBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrphanedFileCleanupBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public OrphanedFileCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrphanedFileCleanupBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateDelayUntilNextRun();
            _logger.LogInformation("Orphaned-file cleanup scan will next run in {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunScanAsync(stoppingToken);
        }
    }

    private async Task RunScanAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrphanedFileCleanupService>();

            // Master switch: the scan only runs when a SuperAdmin has enabled it. Ships disabled.
            if (!await service.IsEnabledAsync())
            {
                _logger.LogInformation("Orphaned-file cleanup is disabled (OrphanedFileCleanupEnabled). Skipping scan.");
                return;
            }

            var result = await service.ScanAsync();
            _logger.LogInformation(
                "Nightly orphan scan: {Scanned} scanned, {Orphans} candidate(s) recorded (grace {Grace}d). Report-only.",
                result.FilesScanned, result.OrphansFound, result.GraceDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nightly orphaned-file cleanup scan failed.");
        }
    }

    private TimeSpan CalculateDelayUntilNextRun()
    {
        var scheduledTimeStr = _configuration.GetValue<string>("OrphanedFileCleanup:ScheduledTimeUtc", "03:00");
        var scheduledTime = TimeOnly.Parse(scheduledTimeStr!);

        var now = DateTime.UtcNow;
        var todayScheduled = now.Date.Add(scheduledTime.ToTimeSpan());

        return todayScheduled > now
            ? todayScheduled - now
            : todayScheduled.AddDays(1) - now;
    }
}
