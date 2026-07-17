namespace Portal.Infrastructure.Models.Receipt;

/// <summary>
/// View model for displaying a payment receipt (detail page and PDF).
/// </summary>
public class ReceiptViewModel
{
    public int Id { get; set; }
    public string ReceiptNumber { get; set; } = null!;
    public DateTime ReceiptDate { get; set; }
    public string CustomerName { get; set; } = null!;
    public string? CustomerAddress { get; set; }
    public string? CustomerEmail { get; set; }
    public decimal TotalAmountReceived { get; set; }
    public decimal OutstandingBalanceAfter { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
    public bool IsVoided { get; set; }
    public string? CurrencySymbol { get; set; }
    public decimal? CreditAmount { get; set; }

    // Business details (for header)
    public string BusinessName { get; set; } = null!;
    public string? BusinessAddress { get; set; }
    public string? BusinessPhone { get; set; }
    public string? BusinessEmail { get; set; }
    public string? BusinessVatNumber { get; set; }
    public string? BusinessLogoPath { get; set; }

    // Signature
    public string? SignatureLabel { get; set; }
    public string? SignaturePosition { get; set; }
    public string? SignatureFilePath { get; set; }

    // Line items
    public List<ReceiptLineViewModel> Lines { get; set; } = new();

    // Computed
    public bool IsFullPayment => Lines.All(l => l.InvoiceOutstandingAfter == 0);
    public string PaymentType => Lines.Count > 1 ? "Multi-Invoice Payment" : IsFullPayment ? "Payment in Full" : "Partial Payment";
}

/// <summary>
/// View model for a single line item on a receipt.
/// </summary>
public class ReceiptLineViewModel
{
    public string InvoiceNumber { get; set; } = null!;
    public decimal InvoiceTotal { get; set; }
    public decimal Amount { get; set; }
    public decimal InvoiceOutstandingBefore { get; set; }
    public decimal InvoiceOutstandingAfter { get; set; }
    public bool IsFullPayment => InvoiceOutstandingAfter == 0;
}
