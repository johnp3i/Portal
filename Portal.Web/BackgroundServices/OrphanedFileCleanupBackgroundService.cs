using Portal.Infrastructure.Services;

namespace Portal.Web.BackgroundServices;

/// <summary>
/// Nightly orphaned-file job. Runs once a day at a configurable UTC time. Two independent switches:
/// detection (PlatformConfig OrphanedFileCleanupEnabled) records/refreshes candidates; deletion
/// (PlatformConfig OrphanedFileDeletionEnabled) removes files past their scheduled date and logs
/// them. Both ship disabled, so a full detection cycle can be watched before anything is deleted.
/// Mirrors PaymentReminderBackgroundService.
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
                "Nightly orphan scan: {Scanned} scanned, {Orphans} candidate(s) recorded (grace {Grace}d).",
                result.FilesScanned, result.OrphansFound, result.GraceDays);

            // Destructive deletion is a SEPARATE switch (OrphanedFileDeletionEnabled). Detection can
            // run for weeks populating the report before deletion is ever turned on. Only when the
            // deletion switch is on do we remove files past their scheduled date.
            if (!await service.IsDeletionEnabledAsync())
            {
                _logger.LogInformation("Orphaned-file deletion is disabled (OrphanedFileDeletionEnabled). Detection-only this run.");
                return;
            }

            var deletion = await service.RunCleanupAsync();
            _logger.LogInformation(
                "Nightly orphan deletion: {Deleted} deleted, {Skipped} skipped, {Bytes} bytes freed.",
                deletion.Deleted, deletion.Skipped, deletion.BytesFreed);
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
