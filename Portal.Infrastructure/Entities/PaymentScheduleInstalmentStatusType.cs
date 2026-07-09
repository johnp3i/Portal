namespace Portal.Infrastructure.Entities;

/// <summary>
/// Reference table defining the possible status values for a payment schedule instalment.
/// Values: Pending (1), Due (2), Overdue (3), Paid (4), PartiallyPaid (5).
/// Schema: [revenue].PaymentScheduleInstalmentStatusType
/// </summary>
public class PaymentScheduleInstalmentStatusType
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
