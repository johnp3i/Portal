namespace Portal.Infrastructure.Models.Import;

/// <summary>
/// A row extracted from the uploaded file before validation.
/// </summary>
public class ParsedRow
{
    public int RowNumber { get; set; }

    public DateOnly? InvoiceDate { get; set; }

    public string? InvoiceNumber { get; set; }

    public string? Description { get; set; }

    public decimal? AmountExcludingVat { get; set; }

    public decimal? VatAmount { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? PurchaseOriginTypeName { get; set; }

    public int? PurchaseOriginTypeId { get; set; }

    public string? Country { get; set; }

    public string? Notes { get; set; }

    public string? ExpenseCategoryName { get; set; }

    public int? ExpenseCategoryId { get; set; }

    public int? VatSubmissionPeriodId { get; set; }

    /// <summary>Raw values keyed by source column name/index for debugging.</summary>
    public Dictionary<string, string> RawValues { get; set; } = new();
}
