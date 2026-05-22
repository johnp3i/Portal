namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for VAT period reassignment impact preview.
/// </summary>
public class ReassignmentImpactDto
{
    public string InvoiceNumber { get; set; } = null!;
    public decimal TaxAmount { get; set; }
    public string SourcePeriodLabel { get; set; } = null!;
    public string TargetPeriodLabel { get; set; } = null!;
    public decimal SourcePeriodProjectedOutputVat { get; set; }
    public decimal TargetPeriodProjectedOutputVat { get; set; }
    public string CurrencySymbol { get; set; } = "€";
}
