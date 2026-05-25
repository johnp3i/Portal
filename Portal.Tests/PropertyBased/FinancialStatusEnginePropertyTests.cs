using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 4: Deterministic financial status computation

/// <summary>
/// Property-based tests for FinancialStatusEngine.DetermineFinancialStatus.
/// Validates that the pure function deterministically computes the correct financial status
/// for all combinations of inputs per the decision tree.
/// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**
/// </summary>
public class FinancialStatusEnginePropertyTests
{
    // Financial Status Type IDs
    private const int StatusUnpaid = 1;
    private const int StatusPartiallyPaid = 2;
    private const int StatusPaid = 3;
    private const int StatusOverdue = 4;
    private const int StatusWrittenOff = 5;

    /// <summary>
    /// Creates a FinancialStatusEngine instance for testing the pure function.
    /// The repositories are null since DetermineFinancialStatus is a pure function
    /// that doesn't use them.
    /// </summary>
    private static FinancialStatusEngine CreateEngine()
    {
        return new FinancialStatusEngine(null!, null!);
    }

    /// <summary>
    /// Computes the expected financial status based on the decision tree from the design document.
    /// This is the oracle function that the property test verifies against.
    /// </summary>
    private static int ComputeExpectedStatus(
        decimal totalAmount, decimal outstandingBalance,
        bool hasValidPayments, DateOnly dueDate, int currentStatusId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Rule 1: WrittenOff is always preserved
        if (currentStatusId == StatusWrittenOff)
            return StatusWrittenOff;

        // Rule 2: Fully paid
        if (outstandingBalance == 0 && hasValidPayments)
            return StatusPaid;

        // Rule 3: Overdue
        if (outstandingBalance > 0 && dueDate < today)
            return StatusOverdue;

        // Rule 4: Partially paid
        if (outstandingBalance > 0 && hasValidPayments && dueDate >= today)
            return StatusPartiallyPaid;

        // Rule 5: Unpaid
        if (outstandingBalance == totalAmount && dueDate >= today)
            return StatusUnpaid;

        // Default: Unpaid
        return StatusUnpaid;
    }

    #region Property 4: Deterministic financial status computation

    /// <summary>
    /// Property 4: Deterministic financial status computation
    /// For any valid combination of totalAmount, outstandingBalance, hasValidPayments,
    /// dueDate, and currentStatusId, the engine returns the correct status per the decision tree.
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DetermineFinancialStatus_MatchesDecisionTree(
        PositiveInt totalAmountSeed,
        PositiveInt paidAmountSeed,
        bool hasValidPayments,
        int dueDateOffsetDays,
        byte statusSeed)
    {
        // Generate totalAmount: positive decimal between 1 and 100000
        var totalAmount = (totalAmountSeed.Get % 100000 + 1) * 1.00m;

        // Generate outstandingBalance: between 0 and totalAmount
        var paidPortion = paidAmountSeed.Get % ((int)totalAmount + 1);
        var outstandingBalance = totalAmount - paidPortion;

        // Ensure consistency: if outstandingBalance == 0, there must be valid payments
        // If outstandingBalance == totalAmount, hasValidPayments should be false for Unpaid scenario
        var effectiveHasValidPayments = hasValidPayments;
        if (outstandingBalance == 0)
            effectiveHasValidPayments = true; // Can't be fully paid without payments
        if (outstandingBalance == totalAmount && !hasValidPayments)
            effectiveHasValidPayments = false; // No payments means full amount outstanding

        // Generate dueDate: offset from today between -365 and +365 days
        var offset = (dueDateOffsetDays % 731) - 365;
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offset);

        // Generate currentStatusId: 1-5
        var currentStatusId = (statusSeed % 5) + 1;

        var engine = CreateEngine();

        var actual = engine.DetermineFinancialStatus(
            totalAmount, outstandingBalance, effectiveHasValidPayments, dueDate, currentStatusId);

        var expected = ComputeExpectedStatus(
            totalAmount, outstandingBalance, effectiveHasValidPayments, dueDate, currentStatusId);

        return (actual == expected).ToProperty()
            .Label($"totalAmount={totalAmount}, outstanding={outstandingBalance}, " +
                   $"hasPayments={effectiveHasValidPayments}, dueDate={dueDate}, " +
                   $"currentStatus={currentStatusId} => expected={expected}, actual={actual}");
    }

    #endregion

    #region Individual Decision Tree Branch Tests

    /// <summary>
    /// WrittenOff status is always preserved regardless of other inputs.
    /// **Validates: Requirement 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WrittenOff_AlwaysPreserved(
        PositiveInt totalAmountSeed,
        PositiveInt outstandingSeed,
        bool hasValidPayments,
        int dueDateOffset)
    {
        var totalAmount = (totalAmountSeed.Get % 100000 + 1) * 1.00m;
        var outstandingBalance = (outstandingSeed.Get % ((int)totalAmount + 1)) * 1.00m;
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(dueDateOffset % 731 - 365);

        var engine = CreateEngine();
        var result = engine.DetermineFinancialStatus(
            totalAmount, outstandingBalance, hasValidPayments, dueDate, StatusWrittenOff);

        return (result == StatusWrittenOff).ToProperty()
            .Label($"WrittenOff should be preserved but got {result}");
    }

    /// <summary>
    /// When outstanding balance is zero and valid payments exist, status is Paid.
    /// **Validates: Requirement 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ZeroBalance_WithPayments_ReturnsPaid(
        PositiveInt totalAmountSeed,
        byte statusSeed)
    {
        var totalAmount = (totalAmountSeed.Get % 100000 + 1) * 1.00m;
        var outstandingBalance = 0m;
        var hasValidPayments = true;
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30); // future date
        // Any non-WrittenOff status
        var currentStatusId = (statusSeed % 4) + 1; // 1-4

        var engine = CreateEngine();
        var result = engine.DetermineFinancialStatus(
            totalAmount, outstandingBalance, hasValidPayments, dueDate, currentStatusId);

        return (result == StatusPaid).ToProperty()
            .Label($"Expected Paid(3) but got {result} for currentStatus={currentStatusId}");
    }

    /// <summary>
    /// When outstanding balance > 0 and due date is in the past, status is Overdue.
    /// **Validates: Requirement 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PositiveBalance_PastDue_ReturnsOverdue(
        PositiveInt totalAmountSeed,
        PositiveInt outstandingSeed,
        bool hasValidPayments,
        PositiveInt daysOverdue,
        byte statusSeed)
    {
        var totalAmount = (totalAmountSeed.Get % 100000 + 1) * 1.00m;
        // Ensure outstanding > 0
        var outstandingBalance = Math.Max(1m, (outstandingSeed.Get % (int)totalAmount) * 1.00m);
        // Due date in the past (at least 1 day ago)
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(daysOverdue.Get % 365 + 1));
        var currentStatusId = (statusSeed % 4) + 1; // 1-4 (non-WrittenOff)

        var engine = CreateEngine();
        var result = engine.DetermineFinancialStatus(
            totalAmount, outstandingBalance, hasValidPayments, dueDate, currentStatusId);

        return (result == StatusOverdue).ToProperty()
            .Label($"Expected Overdue(4) but got {result} for outstanding={outstandingBalance}, dueDate={dueDate}");
    }

    /// <summary>
    /// When outstanding balance > 0, valid payments exist, and due date is today or future, status is PartiallyPaid.
    /// **Validates: Requirement 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PositiveBalance_WithPayments_NotOverdue_ReturnsPartiallyPaid(
        PositiveInt totalAmountSeed,
        PositiveInt paidSeed,
        PositiveInt futureDays,
        byte statusSeed)
    {
        var totalAmount = (totalAmountSeed.Get % 100000 + 1) * 1.00m;
        // Ensure outstanding > 0 but less than totalAmount (partial payment made)
        var paidAmount = Math.Max(1m, (paidSeed.Get % ((int)totalAmount)) * 1.00m);
        var outstandingBalance = totalAmount - paidAmount;
        if (outstandingBalance <= 0) outstandingBalance = 1m; // ensure positive

        var hasValidPayments = true;
        // Due date today or in the future
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(futureDays.Get % 365);
        var currentStatusId = (statusSeed % 4) + 1; // 1-4 (non-WrittenOff)

        var engine = CreateEngine();
        var result = engine.DetermineFinancialStatus(
            totalAmount, outstandingBalance, hasValidPayments, dueDate, currentStatusId);

        return (result == StatusPartiallyPaid).ToProperty()
            .Label($"Expected PartiallyPaid(2) but got {result} for outstanding={outstandingBalance}, " +
                   $"totalAmount={totalAmount}, dueDate={dueDate}");
    }

    /// <summary>
    /// When outstanding balance equals totalAmount, no valid payments, and due date is today or future, status is Unpaid.
    /// **Validates: Requirement 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FullBalance_NoPayments_NotOverdue_ReturnsUnpaid(
        PositiveInt totalAmountSeed,
        PositiveInt futureDays,
        byte statusSeed)
    {
        var totalAmount = (totalAmountSeed.Get % 100000 + 1) * 1.00m;
        var outstandingBalance = totalAmount; // full amount outstanding
        var hasValidPayments = false;
        // Due date today or in the future
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(futureDays.Get % 365);
        var currentStatusId = (statusSeed % 4) + 1; // 1-4 (non-WrittenOff)

        var engine = CreateEngine();
        var result = engine.DetermineFinancialStatus(
            totalAmount, outstandingBalance, hasValidPayments, dueDate, currentStatusId);

        return (result == StatusUnpaid).ToProperty()
            .Label($"Expected Unpaid(1) but got {result} for totalAmount={totalAmount}, dueDate={dueDate}");
    }

    #endregion
}
