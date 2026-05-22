namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for a single purchase row in the VAT submission detail breakdown.
/// </summary>
public class VatPurchaseBreakdownDto
{
    public int Id { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Description { get; set; }
    public string SupplierName { get; set; } = null!;
    public DateOnly InvoiceDate { get; set; }
    public string CategoryName { get; set; } = null!;

    /// <summary>
    /// True when the purchase was explicitly assigned to this period via VatSubmissionPeriodId.
    /// False when it was included via date-range fallback.
    /// </summary>
    public bool IsExplicitAssignment { get; set; }

    /// <summary>
    /// 1 = Domestic, 2 = EU Reverse Charge, 3 = Non-EU
    /// </summary>
    public int PurchaseOriginTypeId { get; set; }

    public decimal VatAmount { get; set; }
}
