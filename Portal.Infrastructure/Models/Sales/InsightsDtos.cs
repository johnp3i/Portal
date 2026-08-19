namespace Portal.Infrastructure.Models.Sales;

/// <summary>
/// All computed metrics for the Insights page.
/// </summary>
public class InsightsMetricsDto
{
    public int NewLeadsCount { get; set; }
    public decimal? ResponseSlaPercentage { get; set; }
    public decimal? DemoConversionRate { get; set; }
    public decimal? ProposalConversionRate { get; set; }
    public decimal? WinRate { get; set; }
    public List<RevenueBreakdownDto> RevenueByProduct { get; set; } = new();
    public List<RevenueBreakdownDto> RevenueBySource { get; set; } = new();
    public double? AverageSalesCycleDays { get; set; }
}

/// <summary>
/// Revenue breakdown row for product or source grouping.
/// </summary>
public class RevenueBreakdownDto
{
    public string Name { get; set; } = null!;
    public decimal TotalRevenue { get; set; }
    public decimal Percentage { get; set; }
}

/// <summary>
/// Conversion rates grouped together.
/// </summary>
public class ConversionRatesDto
{
    public decimal? DemoConversionRate { get; set; }
    public decimal? ProposalConversionRate { get; set; }
    public decimal? WinRate { get; set; }
}
