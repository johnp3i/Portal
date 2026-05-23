namespace Portal.Infrastructure.Models;

/// <summary>
/// View model for the Supplier Dashboard page, containing all data needed to render
/// KPI cards, charts, and the paginated purchases table for a single supplier.
/// </summary>
public class SupplierDashboardViewModel
{
    // Supplier info
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public DateTime CollaborationSince { get; set; }
    public bool IsActive { get; set; }
    public string CurrencySymbol { get; set; } = "€";

    // Period filter
    public int? SelectedPeriodId { get; set; }
    public List<VatPeriodOption> Periods { get; set; } = new();

    // KPIs
    public decimal TotalSpend { get; set; }
    public int TotalPurchases { get; set; }
    public decimal AverageMonthlySpend { get; set; }

    // Spend Share Chart (donut)
    public List<SpendShareSlice> SpendShareData { get; set; } = new();

    // Monthly Spend Chart (bar)
    public List<MonthlySpendBar> MonthlySpendData { get; set; } = new();

    // Period Spend Chart (bar)
    public List<PeriodSpendBar> PeriodSpendData { get; set; } = new();

    // Purchase filter state (for form preservation and pagination links)
    public string? FilterDescription { get; set; }
    public int? FilterCategoryId { get; set; }
    public DateOnly? FilterDateFrom { get; set; }
    public DateOnly? FilterDateTo { get; set; }

    // Category dropdown options
    public List<ExpenseCategoryOption> ExpenseCategories { get; set; } = new();

    // Purchases Table
    public List<PurchaseTableRow> Purchases { get; set; } = new();
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
}

/// <summary>
/// Represents a VAT period option in the period filter dropdown.
/// </summary>
public class VatPeriodOption
{
    public int Id { get; set; }
    public string Label { get; set; } = null!;
}

/// <summary>
/// Represents a single slice in the Spend Share donut chart.
/// </summary>
public class SpendShareSlice
{
    public string SupplierName { get; set; } = null!;
    public decimal Amount { get; set; }
    public bool IsCurrentSupplier { get; set; }
}

/// <summary>
/// Represents a single bar in the Monthly Spend bar chart.
/// </summary>
public class MonthlySpendBar
{
    /// <summary>Abbreviated month name, e.g. "Mar", "Apr".</summary>
    public string MonthLabel { get; set; } = null!;
    /// <summary>Calendar year for this bar, e.g. 2025.</summary>
    public int Year { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>
/// Represents a single bar in the Per-Period Spend bar chart.
/// </summary>
public class PeriodSpendBar
{
    public int PeriodId { get; set; }
    public string PeriodLabel { get; set; } = null!;
    public decimal Amount { get; set; }
    /// <summary>True when this period matches the currently selected period filter.</summary>
    public bool IsSelected { get; set; }
}

/// <summary>
/// Represents a single row in the Purchases table.
/// </summary>
public class PurchaseTableRow
{
    public DateOnly InvoiceDate { get; set; }
    public string Description { get; set; } = null!;
    public string Category { get; set; } = null!;
    public decimal AmountExcludingVat { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// Lightweight DTO for populating the expense category dropdown in the purchase filter panel.
/// </summary>
public class ExpenseCategoryOption
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}
