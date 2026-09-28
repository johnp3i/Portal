namespace Portal.Infrastructure.Models.Compliance;

/// <summary>
/// Result DTO returned after a successful compliance attachment upload.
/// </summary>
public class AttachmentResultDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Optional advisory (non-blocking) message when this upload pushes the business to ≥80% of its
    /// storage cap. Null when there's nothing to warn about. The UI may surface it as an info toast.
    /// </summary>
    public string? StorageWarning { get; set; }
}
