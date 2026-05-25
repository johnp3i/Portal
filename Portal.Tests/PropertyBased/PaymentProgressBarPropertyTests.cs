using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 18: Payment progress bar percentage correctness

/// <summary>
/// Property-based tests for the payment progress bar percentage computation.
/// Verifies that for any invoice with TotalAmount > 0, the payment progress percentage
/// equals (TotalPaid / TotalAmount) × 100, clamped between 0 and 100.
/// **Validates: Requirements 10.4**
/// </summary>
public class PaymentProgressBarPropertyTests
{
    #region Property 18: Payment progress bar percentage correctness

    /// <summary>
    /// Property 18: For any TotalAmount > 0 and any TotalPaid >= 0,
    /// the progress percentage is always between 0 and 100 inclusive.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProgressPercentage_AlwaysClampedBetween0And100(
        PositiveInt totalAmountSeed,
        NonNegativeInt totalPaidSeed)
    {
        // Generate TotalAmount > 0 (between 0.01 and 999999.99)
        var totalAmount = (totalAmountSeed.Get % 99999900 + 1) / 100m;

        // Generate TotalPaid >= 0 (can exceed TotalAmount to test clamping)
        var totalPaid = totalPaidSeed.Get / 100m;

        var percentage = RevenueCalculations.ComputeProgressPercentage(totalAmount, totalPaid);

        return (percentage >= 0m && percentage <= 100m).ToProperty()
            .Label($"Expected 0 <= percentage <= 100, got {percentage} " +
                   $"for totalAmount={totalAmount}, totalPaid={totalPaid}");
    }

    /// <summary>
    /// Property 18: For any TotalAmount > 0 and TotalPaid between 0 and TotalAmount,
    /// the percentage equals Math.Round((TotalPaid / TotalAmount) * 100, 1).
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProgressPercentage_MatchesFormula_WhenWithinBounds(
        PositiveInt totalAmountSeed,
        PositiveInt paidFractionSeed)
    {
        // Generate TotalAmount > 0
        var totalAmount = (totalAmountSeed.Get % 99999900 + 1) / 100m;

        // Generate TotalPaid between 0 and TotalAmount (inclusive)
        var fraction = (paidFractionSeed.Get % 10001) / 10000m; // 0.0000 to 1.0000
        var totalPaid = Math.Round(totalAmount * fraction, 2);

        var percentage = RevenueCalculations.ComputeProgressPercentage(totalAmount, totalPaid);

        // Expected: (TotalPaid / TotalAmount) * 100, rounded to 1 decimal
        var expected = Math.Round(totalPaid / totalAmount * 100, 1);

        return (percentage == expected).ToProperty()
            .Label($"Expected {expected}%, got {percentage}% " +
                   $"for totalAmount={totalAmount}, totalPaid={totalPaid}");
    }

    /// <summary>
    /// Property 18: When TotalPaid exceeds TotalAmount, percentage is clamped to 100.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProgressPercentage_ClampedAt100_WhenOverpaid(
        PositiveInt totalAmountSeed,
        PositiveInt excessSeed)
    {
        // Generate TotalAmount > 0
        var totalAmount = (totalAmountSeed.Get % 99999900 + 1) / 100m;

        // Generate TotalPaid > TotalAmount (overpayment scenario)
        var excess = (excessSeed.Get % 100000 + 1) / 100m;
        var totalPaid = totalAmount + excess;

        var percentage = RevenueCalculations.ComputeProgressPercentage(totalAmount, totalPaid);

        return (percentage == 100m).ToProperty()
            .Label($"Expected 100% when overpaid, got {percentage}% " +
                   $"for totalAmount={totalAmount}, totalPaid={totalPaid}");
    }

    /// <summary>
    /// Property 18: When TotalPaid is zero, percentage is 0.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProgressPercentage_IsZero_WhenNothingPaid(
        PositiveInt totalAmountSeed)
    {
        var totalAmount = (totalAmountSeed.Get % 99999900 + 1) / 100m;
        var totalPaid = 0m;

        var percentage = RevenueCalculations.ComputeProgressPercentage(totalAmount, totalPaid);

        return (percentage == 0m).ToProperty()
            .Label($"Expected 0% when nothing paid, got {percentage}% for totalAmount={totalAmount}");
    }

    /// <summary>
    /// Property 18: When TotalPaid equals TotalAmount, percentage is 100.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProgressPercentage_Is100_WhenFullyPaid(
        PositiveInt totalAmountSeed)
    {
        var totalAmount = (totalAmountSeed.Get % 99999900 + 1) / 100m;
        var totalPaid = totalAmount;

        var percentage = RevenueCalculations.ComputeProgressPercentage(totalAmount, totalPaid);

        return (percentage == 100m).ToProperty()
            .Label($"Expected 100% when fully paid, got {percentage}% for totalAmount={totalAmount}");
    }

    /// <summary>
    /// Property 18: When TotalAmount is zero or negative, percentage is 0.
    /// This guards against division by zero.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ProgressPercentage_IsZero_WhenTotalAmountNotPositive(
        NonNegativeInt totalPaidSeed,
        int totalAmountSeed)
    {
        // Generate TotalAmount <= 0
        var totalAmount = -(Math.Abs(totalAmountSeed) % 100000) / 100m;
        var totalPaid = totalPaidSeed.Get / 100m;

        var percentage = RevenueCalculations.ComputeProgressPercentage(totalAmount, totalPaid);

        return (percentage == 0m).ToProperty()
            .Label($"Expected 0% for non-positive totalAmount={totalAmount}, got {percentage}%");
    }

    #endregion
}
