namespace Portal.Infrastructure.Entities;

/// <summary>
/// A point-in-time HTML snapshot of an invoice shared with a customer via a secure link.
/// Schema: [invoice].InvoiceShare
/// </summary>
public class InvoiceShare
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public int BusinessId { get; set; }

    public string ShareToken { get; set; } = null!;

    public string SnapshotHtml { get; set; } = null!;

    public string CustomerEmail { get; set; } = null!;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public bool IsActive { get; set; }

    // Navigation properties
    public Invoice Invoice { get; set; } = null!;

    public Business Business { get; set; } = null!;
}
