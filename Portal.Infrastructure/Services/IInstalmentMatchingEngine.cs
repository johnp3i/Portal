namespace Portal.Infrastructure.Services;

/// <summary>
/// Pure computation engine that determines how a payment should be allocated
/// across eligible instalments following priority rules.
/// No I/O, no side effects — returns allocation instructions only.
/// </summary>
public interface IInstalmentMatchingEngine
{
    /// <summary>
    /// Allocates a payment amount across eligible instalments following priority rules.
    /// Priority: Due (2) → Overdue (3) → Pending (1), then by SequenceNumber ascending.
    /// Returns allocation instructions without performing any I/O.
    /// </summary>
    MatchResult AllocatePayment(decimal paymentAmount, List<InstalmentMatchCandidate> candidates);
}

/// <summary>
/// Represents a candidate instalment eligible for payment matching.
/// </summary>
public class InstalmentMatchCandidate
{
    public int InstalmentId { get; set; }
    public decimal Amount { get; set; }
    public decimal AlreadyMatched { get; set; }
    public int ComputedStatusId { get; set; }
    public int SequenceNumber { get; set; }
}

/// <summary>
/// The result of a payment allocation operation containing all allocations
/// and an optional remainder instalment for partial fills.
/// </summary>
public class MatchResult
{
    public List<MatchAllocation> Allocations { get; set; } = new();
    public RemainderInstalment? Remainder { get; set; }
}

/// <summary>
/// A single allocation of payment to an instalment.
/// </summary>
public class MatchAllocation
{
    public int InstalmentId { get; set; }
    public decimal AllocatedAmount { get; set; }
    public bool IsFullyPaid { get; set; }
}

/// <summary>
/// Created when a payment partially satisfies an instalment, representing
/// the remaining gap that still needs to be collected.
/// </summary>
public class RemainderInstalment
{
    public int ParentInstalmentId { get; set; }
    public decimal Amount { get; set; }
}
