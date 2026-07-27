namespace Portal.Infrastructure.Models;

/// <summary>
/// Summary statistics displayed as cards at the top of the Business Insights admin page.
/// </summary>
public class BusinessInsightSummaryDto
{
    public int TotalBusinesses { get; set; }
    public int ConfirmedAccounts { get; set; }
    public int ActiveLast30Days { get; set; }
    public int OnTrial { get; set; }
}
