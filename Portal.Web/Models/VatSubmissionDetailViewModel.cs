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
    public bool IsSubmitted { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string CurrencySymbol { get; set; } = "€";

    // Discrepancy detection: Input VAT computed by InvoiceDate vs by period assignment
    public decimal InputVatByDate { get; set; }
    public bool HasDiscrepancy => TotalInputVat != InputVatByDate;
    public int LatePurchasesIncluded { get; set; }
    public int PurchasesReportedLater { get; set; }
}
