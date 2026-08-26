namespace Portal.Infrastructure.Models;

/// <summary>
/// Detailed totals breakdown for invoices and quotations,
/// exposing both gross and net subtotals for UI display.
/// </summary>
public class DocumentTotalsBreakdown
{
    /// <summary>Sum of (Quantity × UnitPrice) for all normal lines (before per-line discounts).</summary>
    public decimal GrossSubtotal { get; set; }

    /// <summary>Sum of LineTotal for all normal lines (after per-line discounts). This matches the document's Subtotal.</summary>
    public decimal NetSubtotal { get; set; }

    /// <summary>Aggregate per-line discounts: GrossSubtotal - NetSubtotal.</summary>
    public decimal LineDiscounts { get; set; }

    /// <summary>Invoice/quotation-level discount (positive value, e.g. 50.00 means 50 off in currency).</summary>
    public decimal InvoiceDiscount { get; set; }

    /// <summary>NetSubtotal - InvoiceDiscount.</summary>
    public decimal NetAmount { get; set; }

    /// <summary>VAT computed on normal lines after their per-line discounts (aggregate-rounded).</summary>
    public decimal Vat { get; set; }

    /// <summary>NetAmount + Vat. Final amount payable.</summary>
    public decimal Total { get; set; }

    /// <summary>Discount type if adjustment line exists: "Percentage" or "Fixed".</summary>
    public string? DiscountType { get; set; }

    /// <summary>The raw discount value entered by the user.</summary>
    public decimal? DiscountValue { get; set; }

    /// <summary>Whether an adjustment line currently exists.</summary>
    public bool HasInvoiceDiscount { get; set; }

    /// <summary>Whether any per-line discounts exist.</summary>
    public bool HasLineDiscounts { get; set; }

    /// <summary>The document's currency code (e.g. "EUR", "GBP", "USD").</summary>
    public string CurrencyCode { get; set; } = "EUR";
}
