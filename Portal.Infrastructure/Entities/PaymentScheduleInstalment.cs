namespace Portal.Infrastructure.Entities;

/// <summary>
/// A single planned payment within a Payment Schedule, with a target amount, optional due date,
/// and tracking of how much has been matched from actual payments.
/// Schema: [revenue].PaymentScheduleInstalment
/// </summary>
public class PaymentScheduleInstalment
{
    public int Id { get; set; }

    public int PaymentScheduleId { get; set; }

    public int SequenceNumber { get; set; }

    public decimal Amount { get; set; }

    public decimal MatchedAmount { get; set; }

    public DateOnly? DueDate { get; set; }

    public int? PaymentId { get; set; }

    public int? ParentInstalmentId { get; set; }

    public bool IsRemainder { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation properties
    public PaymentSchedule PaymentSchedule { get; set; } = null!;

    public Payment? Payment { get; set; }

    public PaymentScheduleInstalment? ParentInstalment { get; set; }
}
