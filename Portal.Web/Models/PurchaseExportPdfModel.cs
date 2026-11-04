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

    // Name lookups (incl. inactive) resolved server-side — the raw purchase query does not populate
    // navigation properties, so the view reads names from here.
    public Dictionary<int, string> SupplierNames { get; set; } = new();
    public Dictionary<int, string> ExpenseCategoryNames { get; set; } = new();

    public string SupplierName(int? id) => id.HasValue && SupplierNames.TryGetValue(id.Value, out var n) ? n : "";
    public string ExpenseCategoryName(int? id) => id.HasValue && ExpenseCategoryNames.TryGetValue(id.Value, out var n) ? n : "";
}
