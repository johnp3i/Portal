namespace Portal.Infrastructure.Services;

/// <summary>
/// Pure computation engine that determines the current status of an instalment
/// based on its due date, instalment amount, and matched payment total.
/// No I/O, no side effects.
/// </summary>
public interface IInstalmentStatusEngine
{
    /// <summary>
    /// Determines the instalment status based on due date, current date, and payment state.
    /// Pure function — no side effects.
    /// </summary>
    /// <param name="dueDate">The instalment's due date, or null if not yet assigned.</param>
    /// <param name="instalmentAmount">The target amount for this instalment.</param>
    /// <param name="matchedTotal">The total amount of payments matched to this instalment.</param>
    /// <returns>
    /// Status type ID: Pending (1), Due (2), Overdue (3), Paid (4), PartiallyPaid (5).
    /// </returns>
    int DetermineStatus(DateOnly? dueDate, decimal instalmentAmount, decimal matchedTotal);
}
