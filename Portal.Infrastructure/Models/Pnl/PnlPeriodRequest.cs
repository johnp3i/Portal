namespace Portal.Infrastructure.Models;

/// <summary>
/// Request model for specifying a P&amp;L reporting period.
/// </summary>
public class PnlPeriodRequest
{
    public PnlPeriodType PeriodType { get; set; }
    public DateOnly? CustomStartDate { get; set; }
    public DateOnly? CustomEndDate { get; set; }
}
