namespace Portal.Infrastructure.Services;

/// <summary>
/// Pure computation engine that allocates a payment across eligible instalments.
/// No I/O, no injected dependencies — stateless and deterministic.
/// 
/// Matching priority: Due (2) → Overdue (3) → Pending (1), then SequenceNumber ASC.
/// </summary>
public class InstalmentMatchingEngine : IInstalmentMatchingEngine
{
    // Status type IDs matching [revenue].[PaymentScheduleInstalmentStatusType]
    private const int StatusPending = 1;
    private const int StatusDue = 2;
    private const int StatusOverdue = 3;

    /// <inheritdoc />
    public MatchResult AllocatePayment(decimal paymentAmount, List<InstalmentMatchCandidate> candidates)
    {
        var result = new MatchResult();

        if (paymentAmount <= 0 || candidates == null || candidates.Count == 0)
            return result;

        // Sort candidates by priority: Due (1st) → Overdue (2nd) → Pending (3rd) → others (4th),
        // then by SequenceNumber ascending within each group.
        var sorted = candidates
            .OrderBy(c => GetStatusPriority(c.ComputedStatusId))
            .ThenBy(c => c.SequenceNumber)
            .ToList();

        var remaining = paymentAmount;

        foreach (var candidate in sorted)
        {
            if (remaining <= 0)
                break;

            var balance = candidate.Amount - candidate.AlreadyMatched;

            if (balance <= 0)
                continue;

            if (remaining >= balance)
            {
                // Full allocation — payment covers the entire remaining balance of this instalment
                result.Allocations.Add(new MatchAllocation
                {
                    InstalmentId = candidate.InstalmentId,
                    AllocatedAmount = balance,
                    IsFullyPaid = true
                });
                remaining -= balance;
            }
            else
            {
                // Partial allocation — payment is less than the remaining balance
                result.Allocations.Add(new MatchAllocation
                {
                    InstalmentId = candidate.InstalmentId,
                    AllocatedAmount = remaining,
                    IsFullyPaid = false
                });

                // Create a remainder instalment for the gap
                result.Remainder = new RemainderInstalment
                {
                    ParentInstalmentId = candidate.InstalmentId,
                    Amount = balance - remaining
                };

                remaining = 0;
            }
        }

        return result;
    }

    /// <summary>
    /// Maps ComputedStatusId to sort priority.
    /// Due (2) = priority 1, Overdue (3) = priority 2, Pending (1) = priority 3, all others = priority 4.
    /// </summary>
    private static int GetStatusPriority(int computedStatusId)
    {
        return computedStatusId switch
        {
            StatusDue => 1,
            StatusOverdue => 2,
            StatusPending => 3,
            _ => 4
        };
    }
}
