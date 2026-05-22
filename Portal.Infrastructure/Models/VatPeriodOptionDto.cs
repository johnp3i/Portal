namespace Portal.Infrastructure.Models;

/// <summary>
/// Lightweight DTO representing an unsubmitted VAT period available for invoice reassignment.
/// </summary>
public class VatPeriodOptionDto
{
    public int Id { get; set; }
    public string PeriodLabel { get; set; } = null!;
}
