namespace Portal.Infrastructure.Entities;

/// <summary>
/// A digital signature image managed at the business level.
/// Can be attached to receipts and other documents.
/// Schema: [portal].Signature
/// </summary>
public class Signature
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public string Label { get; set; } = null!;
    public string? Position { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public string UploadedByUserId { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
