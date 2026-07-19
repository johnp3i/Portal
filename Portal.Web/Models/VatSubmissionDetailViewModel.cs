namespace Portal.Web.Models;

public class VatSubmissionDetailViewModel
{
    public int SubmissionId { get; set; }
    public int PeriodId { get; set; }
    public string PeriodLabel { get; set; } = null!;
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public decimal TotalOutputVat { get; set; }
    public decimal TotalInputVat { get; set; }
    public decimal NetVatPayable { get; set; }
    public decimal InvoiceOutputVat { get; set; }
    public bool IsSubmitted { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string CurrencySymbol { get; set; } = "€";

    // Discrepancy detection: Input VAT computed by InvoiceDate vs by period assignment
    public decimal InputVatByDate { get; set; }
    public bool HasDiscrepancy => TotalInputVat != InputVatByDate;
    public int LatePurchasesIncluded { get; set; }
    public int PurchasesReportedLater { get; set; }

    // Z-Reports: External Revenue assigned to this period
    public bool IsZReportEnabled { get; set; }
    public List<ZReportDetailRow> ZReportRows { get; set; } = new();
    public decimal ZReportTotalVat { get; set; }

    // Safety warning: Z-Reports exist but feature is disabled
    public bool HasExcludedZReports { get; set; }
    public int ExcludedZReportCount { get; set; }
    public decimal ExcludedZReportVat { get; set; }
}

/// <summary>
/// Z-Report row for the VAT Detail page.
/// </summary>
public class ZReportDetailRow
{
    public string SourceName { get; set; } = null!;
    public string? ZReportNumber { get; set; }
    public string PeriodDisplay { get; set; } = null!;
    public decimal TotalVat { get; set; }
    public string AssignmentStatus { get; set; } = null!;
}
