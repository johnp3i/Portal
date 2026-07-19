using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Handles Sales Invoice bulk import from CSV files into ExternalSalesRecord.
/// </summary>
public interface ISalesImportService
{
    Task<ServiceResult<SalesImportPreview>> ParseAndPreviewAsync(Stream fileStream, string fileName, int? revenueSourceId);
    Task<ServiceResult<SalesImportResult>> ConfirmImportAsync(SalesImportPreview preview, List<int>? excludeRowIndexes = null);
}

public class SalesImportPreview
{
    public string FileName { get; set; } = null!;
    public int? RevenueSourceId { get; set; }
    public string? RevenueSourceName { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int DuplicateCount { get; set; }
    public decimal BatchTotal { get; set; }
    public List<SalesImportRow> Rows { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class SalesImportRow
{
    public DateOnly TransactionDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Description { get; set; }
    public string? PaymentMethod { get; set; }
    public bool IsDuplicate { get; set; }
    public bool IsValid { get; set; } = true;
    public string? ValidationError { get; set; }
}

public class SalesImportResult
{
    public int ImportedCount { get; set; }
    public decimal TotalAmount { get; set; }
}
