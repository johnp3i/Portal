using Portal.Infrastructure.Entities.Storage;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>Result of a scan pass (report-only in Phase 4b-1 — nothing is deleted).</summary>
public sealed record OrphanScanResult(int FilesScanned, int OrphansFound, int GraceDays);

/// <summary>
/// Orphaned-file cleanup (Phase 4b). REPORT-ONLY in 4b-1: scans the storage root, records orphan
/// candidates with a scheduled-deletion date, and exposes admin actions (cancel/pause/resume,
/// config). The actual file deletion is Phase 4b-2. Shared by the nightly background job and the
/// admin controller.
/// </summary>
public interface IOrphanedFileCleanupService
{
    /// <summary>Whether the scheduled cleanup job is enabled (PlatformConfig OrphanedFileCleanupEnabled).</summary>
    Task<bool> IsEnabledAsync();

    /// <summary>The grace period in days (PlatformConfig OrphanedFileGraceDays; default 30).</summary>
    Task<int> GetGraceDaysAsync();

    /// <summary>
    /// Walks the storage root, compares every physical file against the live DB-referenced set, and
    /// upserts a Pending candidate (with ScheduledDeletionAtUtc = now + grace) for each orphan.
    /// Report-only — never deletes. Safe to run repeatedly (idempotent upsert; cancelled/paused
    /// rows are preserved).
    /// </summary>
    Task<OrphanScanResult> ScanAsync();

    /// <summary>Current candidates (joined to their status name/description) for the admin page.</summary>
    Task<List<OrphanedFileCandidateRow>> GetCandidatesAsync();

    /// <summary>Status types (id/name/description) for the DB-driven status legend.</summary>
    Task<List<OrphanedFileStatusType>> GetStatusTypesAsync();

    /// <summary>Deletion audit log (source for the deletion report). Empty until Phase 4b-2.</summary>
    Task<List<OrphanedFileDeletionLog>> GetDeletionLogAsync();

    Task CancelAsync(int candidateId);
    Task PauseAsync(int candidateId);
    /// <summary>Resume a paused/cancelled candidate back to Pending.</summary>
    Task ResumeAsync(int candidateId);

    Task SetEnabledAsync(bool enabled);
    Task SetGraceDaysAsync(int graceDays);
}
