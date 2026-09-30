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
    private const string DeletionEnabledKey = "OrphanedFileDeletionEnabled";
    private const string GraceDaysKey = "OrphanedFileGraceDays";
    private const int DefaultGraceDays = 30;

    private readonly OrphanedFileCleanupRepository _repository;
    private readonly PlatformConfigRepository _configRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrphanedFileCleanupService> _logger;

    public OrphanedFileCleanupService(
        OrphanedFileCleanupRepository repository,
        PlatformConfigRepository configRepository,
        IFileStorageService fileStorage,
        IConfiguration configuration,
        ILogger<OrphanedFileCleanupService> logger)
    {
        _repository = repository;
        _configRepository = configRepository;
        _fileStorage = fileStorage;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync()
    {
        var cfg = await _configRepository.GetByKeyAsync(EnabledKey);
        return string.Equals(cfg?.Value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> IsDeletionEnabledAsync()
    {
        var cfg = await _configRepository.GetByKeyAsync(DeletionEnabledKey);
        return string.Equals(cfg?.Value, "true", StringComparison.OrdinalIgnoreCase);
    }

    public Task SetDeletionEnabledAsync(bool enabled) =>
        _configRepository.UpsertAsync(DeletionEnabledKey, enabled ? "true" : "false");

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

    public async Task<OrphanDeletionPreview> PreviewDeletionAsync()
    {
        var due = await _repository.GetDueCandidatesAsync(DateTime.UtcNow);
        return new OrphanDeletionPreview(due.Count, due.Sum(d => d.FileSizeBytes));
    }

    public async Task<OrphanDeletionResult> RunCleanupAsync()
    {
        try
        {
            var due = await _repository.GetDueCandidatesAsync(DateTime.UtcNow);
            if (due.Count == 0)
                return new OrphanDeletionResult(0, 0, 0);

            // Re-build the referenced set NOW (not at scan time). A file that became referenced
            // again between the scan and this pass must NOT be deleted — this re-verification is the
            // core safety guard of the destructive path.
            var referenced = OrphanedFileMatcher.BuildReferencedSet(await _repository.GetAllReferencedPathsAsync());

            var deleted = 0;
            var skipped = 0;
            long bytesFreed = 0;

            foreach (var candidate in due)
            {
                // Safety re-check: still orphaned?
                if (OrphanedFileMatcher.IsReferenced(candidate.RelativePath, referenced))
                {
                    // It's referenced again — spare it and return it to a live state. Resume to
                    // Pending so a later scan re-evaluates it (it will simply not be flagged while
                    // it stays referenced).
                    _logger.LogInformation(
                        "Orphan deletion skipped for '{Path}': now referenced by a live record.", candidate.RelativePath);
                    await _repository.SetCandidateStatusAsync(candidate.Id, OrphanedFileStatus.Pending);
                    skipped++;
                    continue;
                }

                try
                {
                    var existed = await _fileStorage.ExistsAsync(candidate.RelativePath);

                    // No-op if already gone; DeleteAsync is idempotent.
                    await _fileStorage.DeleteAsync(candidate.RelativePath);

                    var reason = existed ? "Orphaned file removed by cleanup" : "Orphaned file already absent on disk";
                    await _repository.InsertDeletionLogAsync(
                        candidate.RelativePath, candidate.BusinessId, candidate.FileSizeBytes, reason, DateTime.UtcNow);
                    await _repository.SetCandidateStatusAsync(candidate.Id, OrphanedFileStatus.Deleted);

                    if (existed) bytesFreed += candidate.FileSizeBytes;
                    deleted++;
                }
                catch (Exception ex)
                {
                    // One bad file must not abort the batch; leave it Pending to retry next pass.
                    _logger.LogError(ex, "Failed to delete orphaned file '{Path}'. Leaving it Pending.", candidate.RelativePath);
                    skipped++;
                }
            }

            _logger.LogInformation(
                "Orphan deletion pass complete: {Deleted} deleted, {Skipped} skipped, {Bytes} bytes freed.",
                deleted, skipped, bytesFreed);

            return new OrphanDeletionResult(deleted, skipped, bytesFreed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orphan deletion pass failed.");
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
