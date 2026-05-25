namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for paired monthly totals of invoiced versus collected amounts.
/// </summary>
public class InvoicedVsCollectedDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = null!;
    public decimal InvoicedAmount { get; set; }
    public decimal CollectedAmount { get; set; }
}
