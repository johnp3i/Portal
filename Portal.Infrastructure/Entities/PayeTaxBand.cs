namespace Portal.Infrastructure.Entities;

/// <summary>
/// Represents a progressive income tax band for PAYE calculation.
/// Country-specific with year-based effective ranges.
/// Schema: [payroll].PayeTaxBand
/// </summary>
public class PayeTaxBand
{
    public int Id { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal Rate { get; set; }
    public int EffectiveFromYear { get; set; }
    public int? EffectiveToYear { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
