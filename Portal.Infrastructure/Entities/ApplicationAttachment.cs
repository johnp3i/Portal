namespace Portal.Infrastructure.Entities;

/// <summary>
/// Represents a submission evidence attachment for a compliance filing.
/// Schema: [compliance].ApplicationAttachment
/// </summary>
public class ApplicationAttachment
{
    public int Id { get; set; }

    public int BusinessApplicationId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
