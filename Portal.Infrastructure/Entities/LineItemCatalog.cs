namespace Portal.Infrastructure.Entities;

/// <summary>
/// A reusable line item template automatically populated from quotation transitions.
/// Schema: [quotation].LineItemCatalog
/// </summary>
public class LineItemCatalog
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string Description { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public decimal VatRate { get; set; }

    public string? ReferenceUrl { get; set; }

    public decimal Discount { get; set; }

    public string DiscountType { get; set; } = "Percentage";

    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;
}
