namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for creating an invoice line.
/// </summary>
public class CreateInvoiceLineDto
{
    public string Description { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal Discount { get; set; }
    public string DiscountType { get; set; } = "Percentage";
    public decimal? CostPrice { get; set; }
    public string? ReferenceUrl { get; set; }
    public string? Subtitle { get; set; }
    public int? SectionIndex { get; set; }
    public string? ProductCode { get; set; }
}
