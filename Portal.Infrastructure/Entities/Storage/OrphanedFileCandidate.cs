namespace Portal.Infrastructure.Entities.Storage;

/// <summary>
/// A physical file detected as orphaned (no live DB record references it). Upserted by the nightly
/// scan (by <see cref="RelativePath"/>) and — from Phase 4b-2 — deleted once
/// <see cref="ScheduledDeletionAtUtc"/> passes, while still Pending. Schema: [Storage].OrphanedFileCandidate.
/// </summary>
public class OrphanedFileCandidate
{
    public int Id { get; set; }

    /// <summary>Storage-root-relative, forward-slash path (matches the on-disk layout).</summary>
    public string RelativePath { get; set; } = null!;

    /// <summary>Owning business inferred from the path; null when not resolvable.</summary>
    public int? BusinessId { get; set; }

    public long FileSizeBytes { get; set; }

    public DateTime DetectedAtUtc { get; set; }
    public DateTime ScheduledDeletionAtUtc { get; set; }

    public int OrphanedFileStatusTypeId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
