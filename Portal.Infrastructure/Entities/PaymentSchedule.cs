namespace Portal.Infrastructure.Entities;

/// <summary>
/// A structured instalment plan attached to an invoice, defining how the outstanding balance
/// will be collected across multiple instalments over time.
/// Schema: [revenue].PaymentSchedule
/// </summary>
public class PaymentSchedule
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public int InvoiceId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedByUserId { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;

    public Invoice Invoice { get; set; } = null!;

    public ICollection<PaymentScheduleInstalment> Instalments { get; set; } = new List<PaymentScheduleInstalment>();

    public ICollection<PaymentScheduleHistory> History { get; set; } = new List<PaymentScheduleHistory>();
}
