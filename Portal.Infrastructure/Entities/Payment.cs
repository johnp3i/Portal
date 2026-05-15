namespace Portal.Infrastructure.Entities;

/// <summary>
/// A monetary transaction recorded against an Invoice. Payments are never physically deleted;
/// voiding sets IsVoided = 1 (soft-delete pattern).
/// Schema: [revenue].Payment
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int InvoiceId { get; set; }

    public int PaymentMethodTypeId { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }

    public bool IsVoided { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedByUserId { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Invoice Invoice { get; set; } = null!;

    public PaymentMethodType PaymentMethodType { get; set; } = null!;
}
