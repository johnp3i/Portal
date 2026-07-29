namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// DTO for compliance filing attachment metadata.
/// </summary>
public class ApplicationAttachmentDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
