namespace Portal.Infrastructure.Entities;

/// <summary>
/// A VAT return submission record for a specific period.
/// Schema: [vat].VatSubmission
/// </summary>
public class VatSubmission
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int VatSubmissionPeriodId { get; set; }

    public decimal TotalOutputVat { get; set; }

    public decimal TotalInputVat { get; set; }

    public decimal NetVatPayable { get; set; }

    public bool IsSubmitted { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public VatSubmissionPeriod VatSubmissionPeriod { get; set; } = null!;
}
