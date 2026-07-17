namespace Portal.Infrastructure.Models.Receipt;

/// <summary>
/// DTO for receipt list page rows.
/// </summary>
public class ReceiptListDto
{
    public int Id { get; set; }
    public string ReceiptNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public DateTime ReceiptDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public decimal TotalAmountReceived { get; set; }
    public bool IsVoided { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public int LineCount { get; set; }
}
