using Portal.Infrastructure.Entities.Storage;

namespace Portal.Web.Models;

/// <summary>View model for the SuperAdmin "Upcoming for deletion" page (Phase 4b-1, report-only).</summary>
public class StorageCleanupViewModel
{
    public bool CleanupEnabled { get; set; }

    /// <summary>Whether DESTRUCTIVE deletion is switched on (separate from detection).</summary>
    public bool DeletionEnabled { get; set; }

    /// <summary>How many candidates are due for deletion right now (past their scheduled date).</summary>
    public int DueCount { get; set; }

    /// <summary>Total size of the due candidates.</summary>
    public long DueBytes { get; set; }

    public int GraceDays { get; set; }

    public string DueSizeDisplay => Portal.Infrastructure.Models.Storage.StorageFormat.Bytes(DueBytes);

    /// <summary>Status legend rows (Name + Description) pulled from the DB, not hard-coded.</summary>
    public List<OrphanedFileStatusType> StatusTypes { get; set; } = new();

    public List<StorageCleanupCandidateItem> Candidates { get; set; } = new();

    /// <summary>Display name of the time zone the dates on this page are shown in.</summary>
    public string TimeZoneLabel { get; set; } = "UTC";
}

/// <summary>One candidate row with dates already converted to the display (local) time zone.</summary>
public class StorageCleanupCandidateItem
{
    public int Id { get; set; }
    public string RelativePath { get; set; } = null!;
    public int? BusinessId { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime DetectedAtLocal { get; set; }
    public DateTime ScheduledDeletionAtLocal { get; set; }
    public int StatusTypeId { get; set; }
    public string StatusName { get; set; } = null!;
    public string StatusDescription { get; set; } = null!;

    public string SizeDisplay => Portal.Infrastructure.Models.Storage.StorageFormat.Bytes(FileSizeBytes);
    public string BusinessDisplay => BusinessId?.ToString() ?? "—";

    /// <summary>Pending candidates can be paused or cancelled; paused/cancelled can be resumed.</summary>
    public bool CanPauseOrCancel => StatusTypeId == OrphanedFileStatus.Pending;
    public bool CanResume => StatusTypeId == OrphanedFileStatus.Paused || StatusTypeId == OrphanedFileStatus.Cancelled;
}
