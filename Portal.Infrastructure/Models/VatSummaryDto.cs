namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for the current VAT period summary displayed on the home dashboard.
/// </summary>
public class VatSummaryDto
{
    public decimal TotalOutputVat { get; set; }
    public decimal TotalInputVat { get; set; }
    public decimal NetVatPayable { get; set; }
    public string PeriodLabel { get; set; } = null!;
    public bool HasData { get; set; }
}
