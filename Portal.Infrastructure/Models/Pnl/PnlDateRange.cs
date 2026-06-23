namespace Portal.Infrastructure.Models;

/// <summary>
/// Represents a resolved date range for P&amp;L period queries.
/// </summary>
public class PnlDateRange
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
