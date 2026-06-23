namespace Portal.Infrastructure.Models;

/// <summary>
/// Year-over-year trend comparison data for P&amp;L figures.
/// Percentage change values are null when the previous period value was zero.
/// </summary>
public class PnlTrendDto
{
    public decimal PreviousRevenue { get; set; }
    public decimal PreviousCogs { get; set; }
    public decimal PreviousGrossProfit { get; set; }
    public decimal PreviousOperatingExpenses { get; set; }
    public decimal PreviousNetProfit { get; set; }

    /// <summary>Percentage change in revenue. Null when previous revenue was zero.</summary>
    public decimal? RevenueChange { get; set; }

    /// <summary>Percentage change in COGS. Null when previous COGS was zero.</summary>
    public decimal? CogsChange { get; set; }

    /// <summary>Percentage change in gross profit. Null when previous gross profit was zero.</summary>
    public decimal? GrossProfitChange { get; set; }

    /// <summary>Percentage change in operating expenses. Null when previous was zero.</summary>
    public decimal? OperatingExpensesChange { get; set; }

    /// <summary>Percentage change in net profit. Null when previous net profit was zero.</summary>
    public decimal? NetProfitChange { get; set; }
}
