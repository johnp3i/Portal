namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object containing the full P&amp;L summary for a given period.
/// </summary>
public class PnlSummaryDto
{
    /// <summary>Period start date.</summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>Period end date.</summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>Total revenue from non-voided payments in the period.</summary>
    public decimal Revenue { get; set; }

    /// <summary>Cost of Goods Sold (PurchaseTypeId == 2).</summary>
    public decimal Cogs { get; set; }

    /// <summary>Gross Profit = Revenue - COGS.</summary>
    public decimal GrossProfit { get; set; }

    /// <summary>Operating Expenses (PurchaseTypeId == 3).</summary>
    public decimal OperatingExpenses { get; set; }

    /// <summary>Net Profit = GrossProfit - OperatingExpenses.</summary>
    public decimal NetProfit { get; set; }

    /// <summary>Gross Margin percentage (0–100 scale). Zero when Revenue is zero.</summary>
    public decimal GrossMargin { get; set; }

    /// <summary>Net Margin percentage (0–100 scale). Zero when Revenue is zero.</summary>
    public decimal NetMargin { get; set; }

    /// <summary>Year-over-year trend comparison. Null if no comparison data available.</summary>
    public PnlTrendDto? Trend { get; set; }

    /// <summary>Expense breakdown by category, ordered by amount descending.</summary>
    public List<PnlCategoryBreakdownDto> CategoryBreakdown { get; set; } = new();

    /// <summary>Indicates whether any financial data exists for the period.</summary>
    public bool HasData { get; set; }
}
