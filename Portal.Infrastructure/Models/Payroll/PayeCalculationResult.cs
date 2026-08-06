namespace Portal.Infrastructure.Models.Payroll;

/// <summary>
/// Result of the PAYE progressive tax calculation.
/// Contains the computed monthly PAYE amount and per-band breakdown.
/// </summary>
public class PayeCalculationResult
{
    public bool IsValid { get; set; }
    public string? ValidationError { get; set; }
    public decimal AnnualProjectedIncome { get; set; }
    public decimal AnnualTax { get; set; }
    public decimal MonthlyPaye { get; set; }
    public decimal EffectiveRate { get; set; }
    public decimal TopMarginalRate { get; set; }
    public List<PayeBandBreakdown> BandBreakdowns { get; set; } = new();
}

/// <summary>
/// Breakdown of tax contribution from a single progressive band.
/// </summary>
public class PayeBandBreakdown
{
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal Rate { get; set; }
    public decimal TaxableAmountInBand { get; set; }
    public decimal TaxForBand { get; set; }
}
