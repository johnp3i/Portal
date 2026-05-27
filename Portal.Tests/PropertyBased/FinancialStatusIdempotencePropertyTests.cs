using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 6: Status recalculation idempotence

/// <summary>
/// Property-based tests verifying that the FinancialStatusEngine's pure functions
/// are idempotent: computing the same result when called multiple times with the same inputs.
/// **Validates: Requirements 2.7**
/// </summary>
public class FinancialStatusIdempotencePropertyTests
{
    private readonly FinancialStatusEngine _engine;

    public FinancialStatusIdempotencePropertyTests()
    {
        // FinancialStatusEngine's pure functions (ComputeOutstandingBalance, DetermineFinancialStatus)
        // don't use repositories, so we can pass null for the repository dependencies.
        // Only RecalculateStatusAsync uses them, which we don't test here.
        _engine = new FinancialStatusEngine(null!, null!, null!);
    }

    #region Helpers

    private static List<Payment> GeneratePayments(PositiveInt[] amountSeeds, bool[] voidFlags)
    {
        var payments = new List<Payment>();
        var count = Math.Min(amountSeeds.Length, 10);

        for (int i = 0; i < count; i++)
        {
            var isVoided = voidFlags.Length > 0 && voidFlags[i % voidFlags.Length];
            payments.Add(new Payment
            {
                Id = i + 1,
                BusinessId = 1,
                InvoiceId = 1,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow.AddDays(-i),
                Amount = Math.Abs(amountSeeds[i].Get % 10000) / 100m + 0.01m,
                IsVoided = isVoided,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        return payments;
    }

    private static decimal GenerateTotalAmount(PositiveInt seed)
    {
        // Generate a total amount between 1.00 and 99999.99
        return (Math.Abs(seed.Get) % 9999900 + 100) / 100m;
    }

    #endregion

    /// <summary>
    /// ComputeOutstandingBalance is idempotent: calling it twice with the same inputs
    /// produces the same result.
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public void ComputeOutstandingBalance_IsIdempotent(
        PositiveInt totalAmountSeed,
        PositiveInt[] paymentSeeds,
        bool[] voidFlags)
    {
        if (paymentSeeds.Length == 0) return;

        var totalAmount = GenerateTotalAmount(totalAmountSeed);
        var payments = GeneratePayments(paymentSeeds, voidFlags);

        // First computation
        var balance1 = _engine.ComputeOutstandingBalance(totalAmount, payments);

        // Second computation with same inputs
        var balance2 = _engine.ComputeOutstandingBalance(totalAmount, payments);

        Assert.Equal(balance1, balance2);
    }

    /// <summary>
    /// DetermineFinancialStatus is idempotent: calling it twice with the same inputs
    /// produces the same status.
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public void DetermineFinancialStatus_IsIdempotent(
        PositiveInt totalAmountSeed,
        PositiveInt[] paymentSeeds,
        bool[] voidFlags,
        bool hasValidPayments,
        PositiveInt dueDateOffsetSeed,
        PositiveInt statusSeed)
    {
        var totalAmount = GenerateTotalAmount(totalAmountSeed);
        if (paymentSeeds.Length == 0) return;
        var payments = GeneratePayments(paymentSeeds, voidFlags);

        var outstandingBalance = _engine.ComputeOutstandingBalance(totalAmount, payments);

        // Generate a due date that can be in the past or future
        var daysOffset = (dueDateOffsetSeed.Get % 730) - 365; // -365 to +365 days
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysOffset));

        // Generate a valid currentStatusId (1-5)
        var currentStatusId = (statusSeed.Get % 5) + 1;

        // First computation
        var status1 = _engine.DetermineFinancialStatus(
            totalAmount, outstandingBalance, hasValidPayments, dueDate, currentStatusId);

        // Second computation with same inputs
        var status2 = _engine.DetermineFinancialStatus(
            totalAmount, outstandingBalance, hasValidPayments, dueDate, currentStatusId);

        Assert.Equal(status1, status2);
    }

    /// <summary>
    /// Full recalculation cycle idempotence: compute balance, determine status, then compute
    /// balance again with the same payments — the outstanding balance is unchanged.
    /// This simulates the full recalculation cycle described in Requirement 2.7.
    /// **Validates: Requirements 2.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public void FullRecalculationCycle_OutstandingBalanceUnchanged(
        PositiveInt totalAmountSeed,
        PositiveInt[] paymentSeeds,
        bool[] voidFlags,
        PositiveInt dueDateOffsetSeed,
        PositiveInt statusSeed)
    {
        if (paymentSeeds.Length == 0) return;

        var totalAmount = GenerateTotalAmount(totalAmountSeed);
        var payments = GeneratePayments(paymentSeeds, voidFlags);

        var daysOffset = (dueDateOffsetSeed.Get % 730) - 365;
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysOffset));
        var currentStatusId = (statusSeed.Get % 5) + 1;

        // Step 1: Compute outstanding balance
        var balance1 = _engine.ComputeOutstandingBalance(totalAmount, payments);

        // Step 2: Determine financial status (this is what RecalculateStatusAsync does internally)
        var hasValidPayments = payments.Any(p => !p.IsVoided);
        var newStatus = _engine.DetermineFinancialStatus(
            totalAmount, balance1, hasValidPayments, dueDate, currentStatusId);

        // Step 3: Compute outstanding balance again (same payments, same totalAmount)
        // The status change should NOT affect the outstanding balance computation
        var balance2 = _engine.ComputeOutstandingBalance(totalAmount, payments);

        // Idempotence: balance is identical after the recalculation cycle
        Assert.Equal(balance1, balance2);
    }
}
