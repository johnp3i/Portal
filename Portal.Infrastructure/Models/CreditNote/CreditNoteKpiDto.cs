namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for credit note KPI card values.
/// </summary>
public class CreditNoteKpiDto
{
    public int TotalIssuedCount { get; set; }
    public decimal TotalValue { get; set; }
    public int PendingApplicationCount { get; set; }
}
