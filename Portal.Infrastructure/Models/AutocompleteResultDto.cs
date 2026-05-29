namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for product autocomplete search results.
/// Combines results from the Product catalog and historical InvoiceLine/QuotationLine records.
/// </summary>
public class AutocompleteResultDto
{
    /// <summary>
    /// The source of the result: "Product", "Invoice", or "Quotation".
    /// </summary>
    public string Source { get; set; } = null!;

    /// <summary>
    /// The product code (if available from the source record).
    /// </summary>
    public string? ProductCode { get; set; }

    /// <summary>
    /// The product or line item description.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// The unit/selling price.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// The VAT rate (if available).
    /// </summary>
    public decimal? VatRate { get; set; }

    /// <summary>
    /// The cost price (if available).
    /// </summary>
    public decimal? CostPrice { get; set; }

    /// <summary>
    /// The supplier name (if a supplier is associated with the product).
    /// </summary>
    public string? SupplierName { get; set; }

    /// <summary>
    /// The relevant date for sorting: LastUsedDate for products, InvoiceDate for invoice lines, CreatedAtUtc for quotation lines.
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// The product type name (Services or Goods) derived from the product's ProductTypeId.
    /// Null when the product has no ProductTypeId assigned.
    /// </summary>
    public string? ProductTypeName { get; set; }
}
