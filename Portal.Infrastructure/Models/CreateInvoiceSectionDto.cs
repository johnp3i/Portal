namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for creating an invoice section.
/// </summary>
public class CreateInvoiceSectionDto
{
    public string Name { get; set; } = null!;
    public string ColumnConfiguration { get; set; } = "OneTime";
    public string SectionType { get; set; } = "LineItems";
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public bool IsEmphasized { get; set; }
    public string? AccentColor { get; set; }
    public string? Label { get; set; }
    public bool IsTotalsTableShown { get; set; }
}
