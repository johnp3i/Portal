namespace Portal.Infrastructure.Models;

/// <summary>
/// Represents a single row parsed from a CSV purchase import file,
/// including resolved IDs and validation state.
/// </summary>
public class CsvPurchaseRowDto
{
    public int RowNumber { get; set; }

    public DateOnly InvoiceDate { get; set; }

    public string? InvoiceNumber { get; set; }

    public string SupplierName { get; set; } = null!;

    public string ExpenseCategoryName { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal AmountExcludingVat { get; set; }

    public decimal VatAmount { get; set; }

    public string PurchaseOriginType { get; set; } = "Domestic";

    public string? Country { get; set; }

    public string? Notes { get; set; }

    // Resolved IDs (after matching against active records)
    public int? ResolvedSupplierId { get; set; }

    public int? ResolvedExpenseCategoryId { get; set; }

    public int? ResolvedPurchaseOriginTypeId { get; set; }

    // Validation state
    public bool IsValid { get; set; }

    public string? ErrorMessage { get; set; }
}
