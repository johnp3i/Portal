namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for system log KPI card values.
/// </summary>
public class SystemLogKpiCounts
{
    public int ErrorCount24h { get; set; }
    public int WarningCount24h { get; set; }
    public int TotalToday { get; set; }
}
