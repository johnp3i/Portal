using Portal.Infrastructure.Models;

namespace Portal.Web.Models;

/// <summary>
/// Model for the invoice list PDF export view.
/// </summary>
public class InvoiceExportPdfModel
{
    public List<InvoiceListDto> Invoices { get; set; } = new();
    public string CurrencySymbol { get; set; } = "€";
    public DateTime GeneratedAt { get; set; }
}
