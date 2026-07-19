namespace Portal.Infrastructure.Entities;

/// <summary>
/// A Z-Report header record representing a consolidated sales summary from a POS device for a given date or period.
/// Schema: [revenue].RevenueSummary
/// </summary>
public class RevenueSummary
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int RevenueSourceId { get; set; }

    public DateOnly SummaryDate { get; set; }

    public DateOnly? PeriodEndDate { get; set; }

    public string? ZReportNumber { get; set; }

    public decimal TotalNet { get; set; }

    public decimal TotalVat { get; set; }

    public decimal TotalGross { get; set; }

    public decimal? TotalDiscount { get; set; }

    public int? TransactionCount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public DateTime? ExportedAtUtc { get; set; }

    public int? VatSubmissionPeriodId { get; set; }

    public int? ImportSessionId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public RevenueSource RevenueSource { get; set; } = null!;

    public VatSubmissionPeriod? VatSubmissionPeriod { get; set; }

    public ICollection<RevenueSummaryLine> Lines { get; set; } = new List<RevenueSummaryLine>();
}
