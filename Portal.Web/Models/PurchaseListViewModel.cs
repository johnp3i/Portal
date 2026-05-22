using Portal.Infrastructure.Entities;

namespace Portal.Web.Models;

public class PurchaseListViewModel
{
    public List<Purchase> Purchases { get; set; } = new();
    public List<Supplier> Suppliers { get; set; } = new();
    public List<ExpenseCategory> ExpenseCategories { get; set; } = new();
    public List<PurchaseOriginType> OriginTypes { get; set; } = new();

    // Filter state
    public int? SupplierId { get; set; }
    public int? ExpenseCategoryId { get; set; }
    public int? PurchaseOriginTypeId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? SearchTerm { get; set; }
}
