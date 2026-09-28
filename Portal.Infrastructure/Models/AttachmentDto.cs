namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for returning attachment information to the UI.
/// </summary>
public class AttachmentDto
{
    public int Id { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long FileSizeBytes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string UploadedByDisplayName { get; set; } = null!;

    public bool IsOwnedByCurrentUser { get; set; }

    /// <summary>
    /// Optional advisory (non-blocking) message when this upload pushes the business to ≥80% of its
    /// storage cap. Null when there's nothing to warn about. The UI may surface it as an info toast.
    /// </summary>
    public string? StorageWarning { get; set; }
}
