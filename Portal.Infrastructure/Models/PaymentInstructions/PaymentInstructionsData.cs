namespace Portal.Infrastructure.Models;

/// <summary>
/// Data required to display bank transfer details in the payment instructions modal.
/// </summary>
public class PaymentInstructionsData
{
    public string BusinessName { get; set; } = null!;
    public string BankName { get; set; } = null!;
    public string Iban { get; set; } = null!;
    public string PayeeName { get; set; } = null!;
    public string? SwiftBic { get; set; }
    public decimal OutstandingAmount { get; set; }
    public string CurrencySymbol { get; set; } = null!;
    public DateOnly DueDate { get; set; }
    public string TransferReference { get; set; } = null!;
}
