using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Handles Z-Report bulk import from CSV files.
/// </summary>
public interface IZReportImportService
{
    /// <summary>
    /// Parses a CSV file, groups rows by DateFrom+DateTo+ZNumber, validates, and detects duplicates.
    /// Returns a preview for user confirmation.
    /// </summary>
    Task<ServiceResult<ZReportImportPreview>> ParseAndPreviewAsync(Stream fileStream, string fileName, int revenueSourceId);

    /// <summary>
    /// Confirms the import and bulk-inserts RevenueSummary + RevenueSummaryLine records in a transaction.
    /// </summary>
    Task<ServiceResult<ZReportImportResult>> ConfirmImportAsync(ZReportImportPreview preview, List<int>? excludeGroupIndexes = null);
}

/// <summary>
/// Preview result from parsing a Z-Report CSV file.
/// </summary>
public class ZReportImportPreview
{
    public string FileName { get; set; } = null!;
    public int RevenueSourceId { get; set; }
    public string RevenueSourceName { get; set; } = null!;
    public int TotalCsvRows { get; set; }
    public int TotalGroups { get; set; }
    public int ValidGroups { get; set; }
    public int DuplicateCount { get; set; }
    public List<ZReportImportGroup> Groups { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// A grouped Z-Report (one RevenueSummary) from the CSV import.
/// </summary>
public class ZReportImportGroup
{
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public string? ZReportNumber { get; set; }
    public DateTime? ExportDate { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalDiscount { get; set; }
    public bool IsDuplicate { get; set; }
    public int? DuplicateOfId { get; set; }
    public bool ImportDuplicateAnyway { get; set; }
    public List<ZReportImportLine> Lines { get; set; } = new();
}

/// <summary>
/// A single VAT rate line within a grouped Z-Report.
/// </summary>
public class ZReportImportLine
{
    public decimal VatRate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
}

/// <summary>
/// Result after confirming a Z-Report bulk import.
/// </summary>
public class ZReportImportResult
{
    public int ImportedCount { get; set; }
    public decimal TotalGross { get; set; }
}
