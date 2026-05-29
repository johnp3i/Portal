using Portal.Infrastructure.Entities;

namespace Portal.Web.Models;

/// <summary>
/// Model for the purchase list PDF export view.
/// </summary>
public class PurchaseExportPdfModel
{
    public List<Purchase> Purchases { get; set; } = new();
    public string CurrencySymbol { get; set; } = "€";
    public DateTime GeneratedAt { get; set; }
}
