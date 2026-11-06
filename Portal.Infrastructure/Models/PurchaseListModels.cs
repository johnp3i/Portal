namespace Portal.Infrastructure.Models;

/// <summary>
/// All filter + sort + paging inputs for the purchases list, passed as one object so the
/// controller, service, and repository stay in sync as filters evolve.
/// </summary>
public class PurchaseListFilter
{
    public int? SupplierId { get; set; }
    public int? ExpenseCategoryId { get; set; }
    public int? PurchaseOriginTypeId { get; set; }
    public int? PurchaseTypeId { get; set; }
    public int? VatPeriodId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? SearchTerm { get; set; }
    /// <summary>recent (default) | date_desc | date_asc | amount_desc | amount_asc.</summary>
    public string? SortBy { get; set; }

    /// <summary>When false (default), payroll-generated purchases (PayslipPeriodId != null) are
    /// excluded from the list, counts, aggregates and exports. When true they are included.</summary>
    public bool IncludePayroll { get; set; }
}

/// <summary>
/// Aggregate KPIs for the purchases list, computed over the FULL filtered set in SQL so the cards
/// stay correct regardless of which page is shown. Cancelled purchases are excluded from the money
/// and origin figures (they are not real spend), matching the previous in-view computation.
/// </summary>
public class PurchaseListAggregates
{
    public int TotalCount { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal TotalVat { get; set; }
    public int OutdatedCount { get; set; }
    public int DomesticCount { get; set; }
    public int EuRcCount { get; set; }
    public int EuPaidCount { get; set; }
    public int NonEuCount { get; set; }
}
