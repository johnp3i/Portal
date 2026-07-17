namespace Portal.Infrastructure.Entities;

/// <summary>
/// A token-based share link for a payment receipt, allowing public access.
/// Same pattern as InvoiceShare — token, HTML snapshot, expiry.
/// Schema: [revenue].PaymentReceiptShare
/// </summary>
public class PaymentReceiptShare
{
    public int Id { get; set; }
    public int PaymentReceiptId { get; set; }
    public int BusinessId { get; set; }
    public string ShareToken { get; set; } = null!;
    public string SnapshotHtml { get; set; } = null!;
    public string CustomerEmail { get; set; } = null!;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = null!;
}
