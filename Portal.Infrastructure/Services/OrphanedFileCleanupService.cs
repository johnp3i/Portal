using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Entities.Storage;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Default <see cref="IOrphanedFileCleanupService"/> — REPORT-ONLY (Phase 4b-1). Scans the storage
/// root, flags orphans as candidates, and manages their status/config. Reads config straight from
/// <see cref="PlatformConfigRepository"/> (not the HttpContext-cached service) so it works inside
/// the background job's scope where there is no HttpContext.
/// </summary>
public class OrphanedFileCleanupService : IOrphanedFileCleanupService
{
    private const string EnabledKey = "OrphanedFileCleanupEnabled";
    private const string GraceDaysKey = "OrphanedFileGraceDays";
    private const int DefaultGraceDays = 30;

    private readonly OrphanedFileCleanupRepository _repository;
    private readonly PlatformConfigRepository _configRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrphanedFileCleanupService> _logger;

    public OrphanedFileCleanupService(
        OrphanedFileCleanupRepository repository,
        PlatformConfigRepository configRepository,
        IConfiguration configuration,
        ILogger<OrphanedFileCleanupService> logger)
    {
        _repository = repository;
        _configRepository = configRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync()
    {
        var cfg = await _configRepository.GetByKeyAsync(EnabledKey);
        return string.Equals(cfg?.Value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<int> GetGraceDaysAsync()
    {
        var cfg = await _configRepository.GetByKeyAsync(GraceDaysKey);
        return (cfg != null && int.TryParse(cfg.Value, out var days) && days > 0) ? days : DefaultGraceDays;
    }

    public async Task<OrphanScanResult> ScanAsync()
    {
        try
        {
            var basePath = _configuration["FileStorage:BasePath"];
            if (string.IsNullOrWhiteSpace(basePath) || !Directory.Exists(basePath))
            {
                _logger.LogWarning("Orphan scan skipped: FileStorage:BasePath '{BasePath}' is not configured or missing.", basePath);
                return new OrphanScanResult(0, 0, await GetGraceDaysAsync());
            }

            var graceDays = await GetGraceDaysAsync();
            var scheduledDeletionAtUtc = DateTime.UtcNow.AddDays(graceDays);

            // Build the referenced set once (all file-owning tables → on-disk relative paths).
            var referenced = OrphanedFileMatcher.BuildReferencedSet(await _repository.GetAllReferencedPathsAsync());

            var filesScanned = 0;
            var orphansFound = 0;

            foreach (var fullPath in Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories))
            {
                filesScanned++;

                // Storage-root-relative, forward-slash path (matches how DB paths are stored).
                var relative = OrphanedFileMatcher.Normalize(Path.GetRelativePath(basePath, fullPath));
                if (relative.Length == 0) continue;

                if (OrphanedFileMatcher.IsReferenced(relative, referenced))
                    continue; // live file — leave it

                // Orphan. Record/refresh a candidate. REPORT-ONLY: never deletes here.
                long size = 0;
                try { size = new FileInfo(fullPath).Length; } catch { /* size best-effort */ }

                var businessId = TryResolveBusinessId(relative);
                await _repository.UpsertPendingCandidateAsync(relative, businessId, size, scheduledDeletionAtUtc);
                orphansFound++;
            }

            _logger.LogInformation(
                "Orphan scan complete: {Scanned} files scanned, {Orphans} orphan candidate(s) recorded (grace {Grace} days). Report-only — no files deleted.",
                filesScanned, orphansFound, graceDays);

            return new OrphanScanResult(filesScanned, orphansFound, graceDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orphan scan failed.");
            throw;
        }
    }

    public Task<List<OrphanedFileCandidateRow>> GetCandidatesAsync() => _repository.GetCandidatesAsync();

    public Task<List<OrphanedFileStatusType>> GetStatusTypesAsync() => _repository.GetStatusTypesAsync();

    public Task<List<OrphanedFileDeletionLog>> GetDeletionLogAsync() => _repository.GetDeletionLogAsync();

    public Task CancelAsync(int candidateId) => _repository.SetCandidateStatusAsync(candidateId, OrphanedFileStatus.Cancelled);
    public Task PauseAsync(int candidateId) => _repository.SetCandidateStatusAsync(candidateId, OrphanedFileStatus.Paused);
    public Task ResumeAsync(int candidateId) => _repository.SetCandidateStatusAsync(candidateId, OrphanedFileStatus.Pending);

    public Task SetEnabledAsync(bool enabled) => _configRepository.UpsertAsync(EnabledKey, enabled ? "true" : "false");
    public Task SetGraceDaysAsync(int graceDays) => _configRepository.UpsertAsync(GraceDaysKey, Math.Max(1, graceDays).ToString());

    /// <summary>
    /// Infers the owning business id from the relative path. Both business-scoped layouts encode it
    /// as the FIRST segment ("{businessId}/…"); signatures use "signatures/{businessId}/…". Returns
    /// null when the first segment isn't a business id.
    /// </summary>
    private static int? TryResolveBusinessId(string relativePath)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;

        // signatures/{businessId}/...
        if (segments.Length >= 2 && segments[0].Equals("signatures", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(segments[1], out var sigBiz) ? sigBiz : (int?)null;

        // {businessId}/...
        return int.TryParse(segments[0], out var biz) ? biz : (int?)null;
    }
}
