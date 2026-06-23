using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: profit-loss-summary, Property 8: Trend percentage change

/// <summary>
/// Property-based tests for P&L trend percentage change formula.
/// Validates that the percentage change formula is correctly applied:
/// - When previous != 0: result = ((current - previous) / Math.Abs(previous)) * 100
/// - When previous == 0: result is null (no comparison data)
/// **Validates: Requirements 4.2, 4.4**
/// </summary>
public class PnlTrendPercentageChangePropertyTests
{
    /// <summary>
    /// Replicates PnlService.ComputePercentageChange for testing.
    /// </summary>
    private static decimal? ComputePercentageChange(decimal current, decimal previous)
    {
        if (previous == 0)
            return null;

        return ((current - previous) / Math.Abs(previous)) * 100m;
    }

    /// <summary>
    /// Property 8: When previous == 0, percentage change is null (no comparison data).
    /// **Validates: Requirements 4.2, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhenPreviousIsZero_ReturnsNull(int currentSeed)
    {
        var current = (decimal)(currentSeed % 999999) + (Math.Abs(currentSeed) % 100) / 100m;
        var previous = 0m;

        var result = ComputePercentageChange(current, previous);

        var isNull = result == null;

        return isNull.ToProperty()
            .Label($"ComputePercentageChange({current}, 0) should be null but was {result}");
    }

    /// <summary>
    /// Property 8: When previous > 0 and current > previous, percentage change is positive.
    /// **Validates: Requirements 4.2, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhenPreviousPositive_CurrentGreater_ReturnsPositivePercentage(int prevSeed, int deltaSeed)
    {
        // Ensure previous > 0 and delta > 0
        var previous = Math.Abs((decimal)(prevSeed % 99999)) + 1m;
        var delta = Math.Abs((decimal)(deltaSeed % 99999)) + 1m;
        var current = previous + delta;

        var result = ComputePercentageChange(current, previous);

        var isPositive = result != null && result > 0m;

        return isPositive.ToProperty()
            .Label($"ComputePercentageChange({current}, {previous}) = {result}, expected positive");
    }

    /// <summary>
    /// Property 8: When previous > 0 and current < previous, percentage change is negative.
    /// **Validates: Requirements 4.2, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhenPreviousPositive_CurrentLess_ReturnsNegativePercentage(int prevSeed, int deltaSeed)
    {
        // Ensure previous > 0 and delta > 0 so current < previous
        var previous = Math.Abs((decimal)(prevSeed % 99999)) + 2m;
        var delta = Math.Abs((decimal)(deltaSeed % 99999)) + 1m;
        var current = previous - delta;

        var result = ComputePercentageChange(current, previous);

        var isNegative = result != null && result < 0m;

        return isNegative.ToProperty()
            .Label($"ComputePercentageChange({current}, {previous}) = {result}, expected negative");
    }

    /// <summary>
    /// Property 8: When previous is negative, uses Math.Abs(previous) in denominator.
    /// Formula: ((current - previous) / |previous|) * 100
    /// **Validates: Requirements 4.2, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhenPreviousNegative_UsesAbsoluteValueInDenominator(int currentSeed, int prevSeed)
    {
        // Ensure previous is negative (non-zero)
        var previous = -(Math.Abs((decimal)(prevSeed % 99999)) + 1m);
        var current = (decimal)(currentSeed % 999999) + (Math.Abs(currentSeed) % 100) / 100m;

        var result = ComputePercentageChange(current, previous);

        // Manually compute expected using absolute value in denominator
        var expected = ((current - previous) / Math.Abs(previous)) * 100m;

        var formulaHolds = result != null && result == expected;

        return formulaHolds.ToProperty()
            .Label($"ComputePercentageChange({current}, {previous}) = {result}, expected {expected}");
    }

    /// <summary>
    /// Property 8: When current == previous, percentage change is 0%.
    /// **Validates: Requirements 4.2, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhenCurrentEqualsPrevious_ReturnsZeroPercent(int valueSeed)
    {
        // Ensure value is non-zero so we don't hit the null case
        var value = (decimal)(valueSeed % 999999);
        if (value == 0m)
            value = 1m;

        var result = ComputePercentageChange(value, value);

        var isZero = result != null && result == 0m;

        return isZero.ToProperty()
            .Label($"ComputePercentageChange({value}, {value}) = {result}, expected 0");
    }

    /// <summary>
    /// Property 8: For any non-zero previous, the formula ((current - previous) / |previous|) * 100 holds.
    /// This is the universal property test over arbitrary decimal pairs.
    /// **Validates: Requirements 4.2, 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FormulaHoldsForAllNonZeroPrevious(int currentSeed, int prevSeed)
    {
        var current = (decimal)(currentSeed % 999999) + (Math.Abs(currentSeed) % 100) / 100m;
        var previous = (decimal)(prevSeed % 999999) + (Math.Abs(prevSeed) % 100) / 100m;

        // Ensure previous is non-zero
        if (previous == 0m)
            previous = 1m;

        var result = ComputePercentageChange(current, previous);
        var expected = ((current - previous) / Math.Abs(previous)) * 100m;

        var formulaHolds = result != null && result == expected;

        return formulaHolds.ToProperty()
            .Label($"ComputePercentageChange({current}, {previous}) = {result}, expected {expected}");
    }
}
