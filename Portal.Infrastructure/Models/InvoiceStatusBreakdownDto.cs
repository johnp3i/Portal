namespace Portal.Infrastructure.Models;

/// <summary>
/// Invoice count breakdown by financial status for the donut chart on the home dashboard.
/// </summary>
public class InvoiceStatusBreakdownDto
{
    public int PaidCount { get; set; }
    public int PartiallyPaidCount { get; set; }
    public int UnpaidCount { get; set; }
    public int OverdueCount { get; set; }
    public int TotalCount { get; set; }
}
