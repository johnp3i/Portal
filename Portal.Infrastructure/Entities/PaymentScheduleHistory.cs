namespace Portal.Infrastructure.Entities;

/// <summary>
/// An audit record capturing a single modification to a Payment Schedule or its instalments,
/// including the field changed, old and new values, and the identity of the user who made the change.
/// Schema: [revenue].PaymentScheduleHistory
/// </summary>
public class PaymentScheduleHistory
{
    public int Id { get; set; }

    public int PaymentScheduleId { get; set; }

    public string FieldChanged { get; set; } = null!;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string ChangedByUserId { get; set; } = null!;

    public DateTime ChangedAtUtc { get; set; }

    // Navigation properties
    public PaymentSchedule PaymentSchedule { get; set; } = null!;
}
