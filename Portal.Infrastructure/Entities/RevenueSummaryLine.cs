namespace Portal.Infrastructure.Entities;

/// <summary>
/// A VAT-rate breakdown line within a Revenue Summary. Each line represents a distinct VAT rate bucket.
/// Schema: [revenue].RevenueSummaryLine
/// </summary>
public class RevenueSummaryLine
{
    public int Id { get; set; }

    public int RevenueSummaryId { get; set; }

    public decimal VatRate { get; set; }

    public decimal NetAmount { get; set; }

    public decimal VatAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal? DiscountAmount { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public RevenueSummary RevenueSummary { get; set; } = null!;
}
