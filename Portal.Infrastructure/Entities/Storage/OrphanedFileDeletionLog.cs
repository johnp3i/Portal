namespace Portal.Infrastructure.Entities.Storage;

/// <summary>
/// Permanent audit record of a file removed by the cleanup (populated in Phase 4b-2). The admin
/// "deletion report" reads from here. Schema: [Storage].OrphanedFileDeletionLog.
/// </summary>
public class OrphanedFileDeletionLog
{
    public int Id { get; set; }
    public string RelativePath { get; set; } = null!;
    public int? BusinessId { get; set; }
    public long FileSizeBytes { get; set; }
    public string Reason { get; set; } = null!;
    public DateTime DeletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
