namespace Portal.Infrastructure.Entities;

/// <summary>
/// An individual priced item within an Invoice.
/// Schema: [invoice].InvoiceLine
/// </summary>
public class InvoiceLine
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public string Description { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal VatRate { get; set; }

    public decimal Discount { get; set; }

    public string DiscountType { get; set; } = "Percentage";

    public decimal? CostPrice { get; set; }

    public decimal LineTotal { get; set; }

    public int SortOrder { get; set; }

    public string? ReferenceUrl { get; set; }

    public string? Subtitle { get; set; }

    public int? InvoiceSectionId { get; set; }

    // Navigation properties
    public Invoice Invoice { get; set; } = null!;

    public InvoiceSection? InvoiceSection { get; set; }
}
