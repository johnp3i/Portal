namespace Portal.Infrastructure.Entities;

/// <summary>
/// A calculated time range representing a single VAT reporting period.
/// Schema: [vat].VatSubmissionPeriod
/// </summary>
public class VatSubmissionPeriod
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public DateOnly PeriodStartDate { get; set; }

    public DateOnly PeriodEndDate { get; set; }

    public string PeriodLabel { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public ICollection<VatSubmission> VatSubmissions { get; set; } = new List<VatSubmission>();

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
