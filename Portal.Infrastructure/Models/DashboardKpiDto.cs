namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for revenue dashboard KPI card values.
/// </summary>
public class DashboardKpiDto
{
    public decimal OutstandingReceivables { get; set; }
    public int OutstandingInvoiceCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public int OverdueInvoiceCount { get; set; }
    public decimal PaidThisMonth { get; set; }
    public int PaidThisMonthCount { get; set; }
    public decimal PartiallyPaidAmount { get; set; }
    public int PartiallyPaidCount { get; set; }
}
