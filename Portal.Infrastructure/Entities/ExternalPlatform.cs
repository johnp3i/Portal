namespace Portal.Infrastructure.Entities;

/// <summary>
/// An external system that produces sales for a Business (e.g., another 3 Inventors platform,
/// an online store). Identified by its invoice PlatformCode (matches the {PlatformCode} segment
/// of the {PlatformCode}-INV-{yyyy}-{NNNN} invoice-number format). Distinct from RevenueSource,
/// which represents a POS device/register.
/// Schema: [revenue].ExternalPlatform
/// </summary>
public class ExternalPlatform
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string Name { get; set; } = null!;

    public string PlatformCode { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ICollection<ExternalSalesRecord> ExternalSalesRecords { get; set; } = new List<ExternalSalesRecord>();
}
