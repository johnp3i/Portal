namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for VAT liability per period used in the dashboard chart.
/// </summary>
public class VatPeriodLiabilityDto
{
    public string PeriodLabel { get; set; } = null!;
    public decimal OutputVat { get; set; }
    public decimal InputVat { get; set; }
    public decimal NetPayable { get; set; }
}
