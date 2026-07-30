namespace Portal.Infrastructure.Models;

/// <summary>
/// Data transfer object for a recent payment row displayed on the revenue dashboard.
/// </summary>
public class RecentPaymentDto
{
    public int Id { get; set; }
    public int PaymentId => Id;
    public DateTime PaymentDateUtc { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string PaymentMethodName { get; set; } = null!;
    public decimal Amount { get; set; }
    public bool IsFullPayment { get; set; }
    public bool IsVoided { get; set; }

    /// <summary>
    /// True when PaymentDateUtc is in the future — payment is recorded but not yet counted toward paid totals.
    /// </summary>
    public bool IsUpcoming { get; set; }

    /// <summary>
    /// True when the payment has been matched to a payment schedule instalment.
    /// </summary>
    public bool IsScheduled { get; set; }

    /// <summary>
    /// Payment reference code (e.g., cheque number, Stripe charge ID). Null if not provided.
    /// </summary>
    public string? Reference { get; set; }
}
