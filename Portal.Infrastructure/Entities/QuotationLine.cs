namespace Portal.Infrastructure.Entities;

/// <summary>
/// An individual priced item within a Quotation.
/// Schema: [quotation].QuotationLine
/// </summary>
public class QuotationLine
{
    public int Id { get; set; }

    public int QuotationId { get; set; }

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

    public int? ProposalSectionId { get; set; }

    public string? Subtitle { get; set; }

    // Navigation properties
    public Quotation Quotation { get; set; } = null!;

    public ProposalSection? ProposalSection { get; set; }
}
