namespace Portal.Infrastructure.Entities;

/// <summary>
/// A named grouping of quotation lines within a proposal with configurable column display.
/// Schema: [quotation].ProposalSection
/// </summary>
public class ProposalSection
{
    public int Id { get; set; }

    public int QuotationId { get; set; }

    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public string ColumnConfiguration { get; set; } = null!;

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public string SectionType { get; set; } = "LineItems";

    public bool IsEmphasized { get; set; }

    public string? AccentColor { get; set; }

    public string? Label { get; set; }

    public bool IsTotalsTableShown { get; set; }

    public bool IsHalfWidth { get; set; }

    // Navigation properties
    public Quotation Quotation { get; set; } = null!;

    public ICollection<QuotationLine> QuotationLines { get; set; } = new List<QuotationLine>();
}
