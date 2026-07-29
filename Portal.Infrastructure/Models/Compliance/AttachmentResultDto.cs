namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// Result DTO returned after a successful compliance attachment upload.
/// </summary>
public class AttachmentResultDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
}
