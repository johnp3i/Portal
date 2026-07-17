namespace Portal.Infrastructure.Models.Receipt;

/// <summary>
/// View model for displaying a signature in the management UI and selection dropdowns.
/// </summary>
public class SignatureViewModel
{
    public int Id { get; set; }
    public string Label { get; set; } = null!;
    public string? Position { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string UploadedByDisplayName { get; set; } = null!;
}
