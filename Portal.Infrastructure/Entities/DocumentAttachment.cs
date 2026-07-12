namespace Portal.Infrastructure.Entities;

/// <summary>
/// Represents a file attachment metadata record associated with a business entity.
/// Schema: [document].DocumentAttachment
/// </summary>
public class DocumentAttachment
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public string FileName { get; set; } = null!;

    public string OriginalFileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public string StoragePath { get; set; } = null!;

    public long FileSizeBytes { get; set; }

    public string UploadedByUserId { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business? Business { get; set; }
}
