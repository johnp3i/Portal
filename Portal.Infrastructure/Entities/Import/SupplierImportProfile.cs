namespace Portal.Infrastructure.Entities.Import;

/// <summary>
/// Default values stored against a supplier to auto-populate fields during import.
/// Schema: [import].SupplierImportProfile
/// </summary>
public class SupplierImportProfile
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int SupplierId { get; set; }

    public int? DefaultExpenseCategoryId { get; set; }

    public int? DefaultPurchaseOriginTypeId { get; set; }

    public string? DefaultCountry { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    // Navigation properties
    public Business? Business { get; set; }

    public Supplier? Supplier { get; set; }

    public ExpenseCategory? DefaultExpenseCategory { get; set; }

    public PurchaseOriginType? DefaultPurchaseOriginType { get; set; }
}
