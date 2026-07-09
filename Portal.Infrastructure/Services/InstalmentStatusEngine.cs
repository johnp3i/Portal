namespace Portal.Infrastructure.Services;

/// <summary>
/// Pure computation engine that determines the current status of an instalment
/// based on its due date, instalment amount, and matched payment total.
/// Uses TimeProvider for testability of date-dependent logic.
/// </summary>
public class InstalmentStatusEngine : IInstalmentStatusEngine
{
    // Instalment Status Type IDs (matching [revenue].PaymentScheduleInstalmentStatusType)
    private const int StatusPending = 1;
    private const int StatusDue = 2;
    private const int StatusOverdue = 3;
    private const int StatusPaid = 4;
    private const int StatusPartiallyPaid = 5;

    private readonly TimeProvider _timeProvider;

    public InstalmentStatusEngine(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public int DetermineStatus(DateOnly? dueDate, decimal instalmentAmount, decimal matchedTotal)
    {
        // Priority 1: Fully paid — matched total covers the instalment amount
        if (matchedTotal >= instalmentAmount)
            return StatusPaid;

        // Priority 2: Partially paid — some payment received but not full amount
        if (matchedTotal > 0 && matchedTotal < instalmentAmount)
            return StatusPartiallyPaid;

        // Priority 3-6: No payments matched — status depends on due date vs today
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().DateTime);

        // No due date assigned → Pending
        if (dueDate == null)
            return StatusPending;

        // Due date in the future → Pending
        if (dueDate > today)
            return StatusPending;

        // Due date is today → Due
        if (dueDate == today)
            return StatusDue;

        // Due date in the past → Overdue
        return StatusOverdue;
    }
}
