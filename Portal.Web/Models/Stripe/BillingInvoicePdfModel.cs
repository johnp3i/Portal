namespace Portal.Web.Models.Stripe;

/// <summary>
/// View model used for rendering the billing invoice PDF, containing company header,
/// business details, invoice data, line items, and payment information.
/// </summary>
public class BillingInvoicePdfModel
{
    // 3 Inventors company header
    public string CompanyName { get; set; } = "3 Inventors";
    public string CompanyAddress { get; set; } = "3 Inventors Ltd";
    public string? CompanyLogoUrl { get; set; }

    // Subscribing business details
    public string BusinessName { get; set; } = string.Empty;
    public string? VatNumber { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    // Invoice details
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    // Line items
    public List<BillingInvoiceLineItem> LineItems { get; set; } = new();

    // Totals
    public decimal Subtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }

    // Payment info
    public string? PaymentMethod { get; set; }
    public DateTime? PaymentDate { get; set; }
}

/// <summary>
/// A single line item on a billing invoice PDF.
/// </summary>
public class BillingInvoiceLineItem
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}
