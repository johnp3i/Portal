namespace Portal.Infrastructure.Entities;

/// <summary>
/// A point-in-time HTML snapshot of a quotation shared with a customer via a secure link.
/// Schema: [quotation].ProposalShare
/// </summary>
public class ProposalShare
{
    public int Id { get; set; }

    public int QuotationId { get; set; }

    public int BusinessId { get; set; }

    public string ShareToken { get; set; } = null!;

    public string SnapshotHtml { get; set; } = null!;

    public string CustomerEmail { get; set; } = null!;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public bool IsActive { get; set; }

    // Navigation properties
    public Quotation Quotation { get; set; } = null!;

    public Business Business { get; set; } = null!;
}
