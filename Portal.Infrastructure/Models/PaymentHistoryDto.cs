namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for a payment history row displayed on the invoice detail view.
/// </summary>
public class PaymentHistoryDto
{
    public int Id { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethodName { get; set; } = null!;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public bool IsVoided { get; set; }
}
