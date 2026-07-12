namespace Portal.Infrastructure.Models;

/// <summary>
/// DTO for the Attachments index page — represents a single row in the all-attachments list.
/// </summary>
public class AttachmentIndexDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public string EntityType { get; set; } = null!;
    public int EntityId { get; set; }
    public string EntityReference { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public string UploadedByDisplayName { get; set; } = null!;
    public bool IsOwnedByCurrentUser { get; set; }
}

/// <summary>
/// Summary KPI data for the Attachments index page header.
/// </summary>
public class AttachmentIndexSummary
{
    public int TotalFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public int EntitiesWithFiles { get; set; }
    public int ThisMonthCount { get; set; }
}
