namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for a recent payment row displayed on the revenue dashboard.
/// </summary>
public class RecentPaymentDto
{
    public int Id { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string PaymentMethodName { get; set; } = null!;
    public decimal Amount { get; set; }
    public bool IsFullPayment { get; set; }
}
