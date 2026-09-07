namespace Portal.Infrastructure.Models;

/// <summary>
/// Lean P&amp;L figures for a business + date range, computed tenant-lessly (explicit businessId,
/// no current-tenant dependency). Produced by <see cref="Services.IPnlService.ComputeSnapshotAsync"/>
/// for the scheduled Weekly Financial Snapshot digest. Omits the trend/category/margin breakdown
/// of <see cref="PnlSummaryDto"/> — the digest only needs the headline figures.
/// </summary>
public class PnlSnapshotDto
{
    /// <summary>Period start date.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Period end date.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Revenue collected (non-voided payments) in the period.</summary>
    public decimal Revenue { get; set; }

    /// <summary>Cost of Goods Sold (PurchaseTypeId == 2) in the period.</summary>
    public decimal Cogs { get; set; }

    /// <summary>Operating Expenses (PurchaseTypeId == 3) in the period.</summary>
    public decimal OperatingExpenses { get; set; }

    /// <summary>Gross Profit = Revenue - COGS.</summary>
    public decimal GrossProfit { get; set; }

    /// <summary>Net Profit = GrossProfit - OperatingExpenses.</summary>
    public decimal NetProfit { get; set; }

    /// <summary>True when any figure is non-zero (used to select the "all clear" variant).</summary>
    public bool HasData { get; set; }
}
