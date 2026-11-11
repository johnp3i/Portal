namespace Portal.Infrastructure.Models;

/// <summary>
/// A single product's performance figure for one of the Product Performance charts
/// (either revenue or units sold), for the selected period.
/// </summary>
public class ProductPerformancePoint
{
    /// <summary>Product description (chart label).</summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// The measured value. For the revenue chart this is total sales value
    /// (SUM of line totals); for the units chart this is total quantity sold.
    /// </summary>
    public decimal Value { get; set; }
}

/// <summary>
/// Combined result for the collapsible "Product Performance" section: the top products
/// by revenue and by units sold over the selected period, plus the resolved period label
/// and the business currency symbol for formatting.
/// </summary>
public class ProductPerformanceResult
{
    /// <summary>Top products by revenue (issued, non-deleted invoices), highest first.</summary>
    public List<ProductPerformancePoint> TopByRevenue { get; set; } = new();

    /// <summary>Top products by units sold (issued, non-deleted invoices), highest first.</summary>
    public List<ProductPerformancePoint> TopByUnits { get; set; } = new();

    /// <summary>Human-readable label for the resolved period (e.g. "This Year (2026)").</summary>
    public string PeriodLabel { get; set; } = string.Empty;

    /// <summary>Business currency symbol for the revenue chart formatting.</summary>
    public string CurrencySymbol { get; set; } = "€";
}
