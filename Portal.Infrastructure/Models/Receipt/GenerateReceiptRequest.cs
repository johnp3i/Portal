namespace Portal.Infrastructure.Models.Receipt;

/// <summary>
/// Request model for generating a payment receipt.
/// </summary>
public class GenerateReceiptRequest
{
    public int PaymentId { get; set; }
    public int? SignatureId { get; set; }
    public string? Notes { get; set; }
}
