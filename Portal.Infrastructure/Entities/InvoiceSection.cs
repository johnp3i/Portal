namespace Portal.Infrastructure.Entities;

/// <summary>
/// A named grouping of invoice lines within an invoice with configurable column display.
/// Schema: [invoice].InvoiceSection
/// </summary>
public class InvoiceSection
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public string ColumnConfiguration { get; set; } = null!;

    public string SectionType { get; set; } = "LineItems";

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public bool IsEmphasized { get; set; }

    public string? AccentColor { get; set; }

    public string? Label { get; set; }

    public bool IsTotalsTableShown { get; set; }

    // Navigation properties
    public Invoice Invoice { get; set; } = null!;

    public ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();
}
