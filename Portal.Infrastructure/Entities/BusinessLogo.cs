namespace Portal.Infrastructure.Entities;

/// <summary>
/// A logo image uploaded to a business's logo library for use in proposals.
/// Schema: [portal].BusinessLogo
/// </summary>
public class BusinessLogo
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string DisplayName { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long FileSizeBytes { get; set; }

    public string PublicUrl { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public bool IsPrimary { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;
}
