using Portal.Infrastructure.Entities;

namespace Portal.Web.Models;

public class PurchaseListViewModel
{
    public List<Purchase> Purchases { get; set; } = new();
    public List<Supplier> Suppliers { get; set; } = new();
    public List<ExpenseCategory> ExpenseCategories { get; set; } = new();
    public List<PurchaseOriginType> OriginTypes { get; set; } = new();
    public List<PurchaseType> PurchaseTypes { get; set; } = new();
    public List<VatSubmissionPeriod> VatPeriods { get; set; } = new();
    public string CurrencySymbol { get; set; } = "â‚¬";

    // Attachment counts keyed by purchase Id, batch-loaded once in the controller so the list
    // view can render the paperclip badge without a per-row DB query (avoids an N+1 that made the
    // table render progressively over several seconds).
    public Dictionary<int, int> AttachmentCounts { get; set; } = new();

    // Name lookups for the CURRENT page, resolved server-side including inactive suppliers/categories
    // (the active-only service load left inactive ones blank). Keyed by the FK id.
    public Dictionary<int, string> SupplierNames { get; set; } = new();
    public Dictionary<int, string> ExpenseCategoryNames { get; set; } = new();

    // Server-side pagination state.
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)System.Math.Ceiling((double)TotalCount / PageSize) : 0;

    // Aggregate KPIs over the FULL filtered set (not just the current page).
    public Portal.Infrastructure.Models.PurchaseListAggregates Aggregates { get; set; } = new();

    // Convenience display helpers for the current page rows.
    public string SupplierName(int? id) => id.HasValue && SupplierNames.TryGetValue(id.Value, out var n) ? n : "";
    public string ExpenseCategoryName(int? id) => id.HasValue && ExpenseCategoryNames.TryGetValue(id.Value, out var n) ? n : "";

    // Filter state
    public int? SupplierId { get; set; }
    public int? ExpenseCategoryId { get; set; }
    public int? PurchaseOriginTypeId { get; set; }
    public int? PurchaseTypeId { get; set; }
    public int? VatPeriodId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; }

    // When true, payroll-generated purchases (PayslipPeriodId != null) are included in the list.
    // Default false so they are hidden; the toggle in the filter panel flips it.
    public bool IncludePayroll { get; set; }

    // Soft-gate teaser visibility
    public bool ShowExpenseInsightsTeaser { get; set; }
}
