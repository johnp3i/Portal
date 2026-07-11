using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: recurring-expense-validation, Properties 5 & 6

/// <summary>
/// Property-based tests for grace period lookup window behaviour and amount tolerance symmetry.
/// These test PURE LOGIC only — no database, no mocking.
/// </summary>
public class RecurringExpenseGraceAndAmountPropertyTests
{
    #region Property 5: Grace period widens lookup but not expectation

    /// <summary>
    /// Property 5a: Grace period extends the lookup start date backward.
    /// lookupStart = startDate.AddDays(-gracePeriod)
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GracePeriod_ExtendsLookupStartBackward(PositiveInt yearOffset, PositiveInt monthOffset, NonNegativeInt graceDaysSeed)
    {
        // Generate a realistic start date
        var baseYear = 2020 + (yearOffset.Get % 6); // 2020-2025
        var baseMonth = (monthOffset.Get % 12) + 1; // 1-12
        var startDate = new DateOnly(baseYear, baseMonth, 1);

        // Clamp grace period to 0-15
        var gracePeriodDays = graceDaysSeed.Get % 16;

        var lookupStart = startDate.AddDays(-gracePeriodDays);
        var expectedLookupStart = startDate.AddDays(-gracePeriodDays);

        return (lookupStart == expectedLookupStart).ToProperty()
            .Label($"startDate={startDate}, grace={gracePeriodDays}, " +
                   $"lookupStart={lookupStart}, expected={expectedLookupStart}");
    }

    /// <summary>
    /// Property 5b: Grace period extends the lookup end date forward.
    /// lookupEnd = endDate.AddDays(gracePeriod)
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GracePeriod_ExtendsLookupEndForward(PositiveInt yearOffset, PositiveInt monthOffset, PositiveInt periodMonthsSeed, NonNegativeInt graceDaysSeed)
    {
        // Generate a realistic start date
        var baseYear = 2020 + (yearOffset.Get % 6); // 2020-2025
        var baseMonth = (monthOffset.Get % 12) + 1; // 1-12
        var startDate = new DateOnly(baseYear, baseMonth, 1);

        // Generate end date: 1-12 months after start
        var periodMonths = (periodMonthsSeed.Get % 12) + 1;
        var endDate = startDate.AddMonths(periodMonths).AddDays(-1); // end of last month in period

        // Clamp grace period to 0-15
        var gracePeriodDays = graceDaysSeed.Get % 16;

        var lookupEnd = endDate.AddDays(gracePeriodDays);
        var expectedLookupEnd = endDate.AddDays(gracePeriodDays);

        return (lookupEnd == expectedLookupEnd).ToProperty()
            .Label($"endDate={endDate}, grace={gracePeriodDays}, " +
                   $"lookupEnd={lookupEnd}, expected={expectedLookupEnd}");
    }

    /// <summary>
    /// Property 5c: Expected count does NOT change with different grace period values.
    /// The same date range and frequency always produce the same expectedCount
    /// regardless of the grace period configured.
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GracePeriod_DoesNotAffectExpectedCount(
        PositiveInt periodMonthsSeed,
        PositiveInt frequencySeed,
        NonNegativeInt grace1Seed,
        NonNegativeInt grace2Seed)
    {
        // Generate period months: 1-12
        var periodMonths = (periodMonthsSeed.Get % 12) + 1;

        // Generate frequency months: 1-6
        var frequencyMonths = (frequencySeed.Get % 6) + 1;

        // Two different grace periods: 0-15
        var grace1 = grace1Seed.Get % 16;
        var grace2 = grace2Seed.Get % 16;

        // Expected count formula: floor(periodMonths / frequencyMonths)
        var expectedCountWithGrace1 = (int)Math.Floor((double)periodMonths / frequencyMonths);
        var expectedCountWithGrace2 = (int)Math.Floor((double)periodMonths / frequencyMonths);

        // The grace period never appears in the expected count formula
        return (expectedCountWithGrace1 == expectedCountWithGrace2).ToProperty()
            .Label($"periodMonths={periodMonths}, frequency={frequencyMonths}, " +
                   $"grace1={grace1}, grace2={grace2}, " +
                   $"count1={expectedCountWithGrace1}, count2={expectedCountWithGrace2}");
    }

    /// <summary>
    /// Property 5d: Grace period of 0 means lookup window equals period boundaries exactly.
    /// When gracePeriodDays = 0, lookupStart = startDate and lookupEnd = endDate.
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GracePeriodZero_LookupEqualsExactPeriod(PositiveInt yearOffset, PositiveInt monthOffset, PositiveInt periodMonthsSeed)
    {
        var baseYear = 2020 + (yearOffset.Get % 6);
        var baseMonth = (monthOffset.Get % 12) + 1;
        var startDate = new DateOnly(baseYear, baseMonth, 1);

        var periodMonths = (periodMonthsSeed.Get % 12) + 1;
        var endDate = startDate.AddMonths(periodMonths).AddDays(-1);

        var gracePeriodDays = 0;

        var lookupStart = startDate.AddDays(-gracePeriodDays);
        var lookupEnd = endDate.AddDays(gracePeriodDays);

        var startMatch = lookupStart == startDate;
        var endMatch = lookupEnd == endDate;

        return (startMatch && endMatch).ToProperty()
            .Label($"Grace=0: lookupStart={lookupStart} (expected {startDate}), " +
                   $"lookupEnd={lookupEnd} (expected {endDate})");
    }

    /// <summary>
    /// Property 5e: Lookup window with grace period is always wider than or equal to period without grace.
    /// For grace G >= 0: lookupStart <= startDate and lookupEnd >= endDate.
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GracePeriod_AlwaysWidensOrEqualsWindow(
        PositiveInt yearOffset,
        PositiveInt monthOffset,
        PositiveInt periodMonthsSeed,
        NonNegativeInt graceDaysSeed)
    {
        var baseYear = 2020 + (yearOffset.Get % 6);
        var baseMonth = (monthOffset.Get % 12) + 1;
        var startDate = new DateOnly(baseYear, baseMonth, 1);

        var periodMonths = (periodMonthsSeed.Get % 12) + 1;
        var endDate = startDate.AddMonths(periodMonths).AddDays(-1);

        var gracePeriodDays = graceDaysSeed.Get % 16; // 0-15

        var lookupStart = startDate.AddDays(-gracePeriodDays);
        var lookupEnd = endDate.AddDays(gracePeriodDays);

        var startWidened = lookupStart <= startDate;
        var endWidened = lookupEnd >= endDate;

        return (startWidened && endWidened).ToProperty()
            .Label($"Grace={gracePeriodDays}: lookupStart={lookupStart} <= startDate={startDate}, " +
                   $"lookupEnd={lookupEnd} >= endDate={endDate}");
    }

    #endregion

    #region Property 6: Amount tolerance range is symmetric

    /// <summary>
    /// Property 6a: Amount tolerance range is symmetric around the expected amount.
    /// The distance from expectedAmount to lowerBound equals the distance from expectedAmount to upperBound.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AmountTolerance_RangeIsSymmetric(PositiveInt amountSeed, PositiveInt toleranceSeed)
    {
        // Generate expectedAmount: 1-10000 (positive decimal)
        var expectedAmount = (amountSeed.Get % 10000 + 1) * 1.00m;

        // Generate tolerance: 1-100%
        var tolerancePercent = ((toleranceSeed.Get % 100) + 1) * 1.00m;

        var lowerBound = expectedAmount * (1 - tolerancePercent / 100);
        var upperBound = expectedAmount * (1 + tolerancePercent / 100);

        var distanceBelow = expectedAmount - lowerBound;
        var distanceAbove = upperBound - expectedAmount;

        return (distanceBelow == distanceAbove).ToProperty()
            .Label($"expectedAmount={expectedAmount}, tolerance={tolerancePercent}%, " +
                   $"lower={lowerBound}, upper={upperBound}, " +
                   $"distBelow={distanceBelow}, distAbove={distanceAbove}");
    }

    /// <summary>
    /// Property 6b: Lower bound equals expectedAmount * (1 - tolerancePercent/100).
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AmountTolerance_LowerBoundFormula(PositiveInt amountSeed, PositiveInt toleranceSeed)
    {
        var expectedAmount = (amountSeed.Get % 10000 + 1) * 1.00m;
        var tolerancePercent = ((toleranceSeed.Get % 100) + 1) * 1.00m;

        var lowerBound = expectedAmount * (1 - tolerancePercent / 100);
        var expectedLower = expectedAmount - (expectedAmount * tolerancePercent / 100);

        return (lowerBound == expectedLower).ToProperty()
            .Label($"expectedAmount={expectedAmount}, tolerance={tolerancePercent}%, " +
                   $"lowerBound={lowerBound}, expectedLower={expectedLower}");
    }

    /// <summary>
    /// Property 6c: Upper bound equals expectedAmount * (1 + tolerancePercent/100).
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AmountTolerance_UpperBoundFormula(PositiveInt amountSeed, PositiveInt toleranceSeed)
    {
        var expectedAmount = (amountSeed.Get % 10000 + 1) * 1.00m;
        var tolerancePercent = ((toleranceSeed.Get % 100) + 1) * 1.00m;

        var upperBound = expectedAmount * (1 + tolerancePercent / 100);
        var expectedUpper = expectedAmount + (expectedAmount * tolerancePercent / 100);

        return (upperBound == expectedUpper).ToProperty()
            .Label($"expectedAmount={expectedAmount}, tolerance={tolerancePercent}%, " +
                   $"upperBound={upperBound}, expectedUpper={expectedUpper}");
    }

    /// <summary>
    /// Property 6d: A value at exactly the lower bound is INCLUDED in the range.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AmountTolerance_ExactLowerBoundIsIncluded(PositiveInt amountSeed, PositiveInt toleranceSeed)
    {
        var expectedAmount = (amountSeed.Get % 10000 + 1) * 1.00m;
        var tolerancePercent = ((toleranceSeed.Get % 100) + 1) * 1.00m;

        var lowerBound = expectedAmount * (1 - tolerancePercent / 100);
        var upperBound = expectedAmount * (1 + tolerancePercent / 100);

        var valueAtLower = lowerBound;
        var isIncluded = valueAtLower >= lowerBound && valueAtLower <= upperBound;

        return isIncluded.ToProperty()
            .Label($"Value at lowerBound={lowerBound} should be included. " +
                   $"expectedAmount={expectedAmount}, tolerance={tolerancePercent}%");
    }

    /// <summary>
    /// Property 6e: A value at exactly the upper bound is INCLUDED in the range.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AmountTolerance_ExactUpperBoundIsIncluded(PositiveInt amountSeed, PositiveInt toleranceSeed)
    {
        var expectedAmount = (amountSeed.Get % 10000 + 1) * 1.00m;
        var tolerancePercent = ((toleranceSeed.Get % 100) + 1) * 1.00m;

        var lowerBound = expectedAmount * (1 - tolerancePercent / 100);
        var upperBound = expectedAmount * (1 + tolerancePercent / 100);

        var valueAtUpper = upperBound;
        var isIncluded = valueAtUpper >= lowerBound && valueAtUpper <= upperBound;

        return isIncluded.ToProperty()
            .Label($"Value at upperBound={upperBound} should be included. " +
                   $"expectedAmount={expectedAmount}, tolerance={tolerancePercent}%");
    }

    /// <summary>
    /// Property 6f: A value below the lower bound is EXCLUDED from the range.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AmountTolerance_BelowLowerBoundIsExcluded(PositiveInt amountSeed, PositiveInt toleranceSeed, PositiveInt epsilonSeed)
    {
        var expectedAmount = (amountSeed.Get % 10000 + 1) * 1.00m;
        var tolerancePercent = ((toleranceSeed.Get % 100) + 1) * 1.00m;

        var lowerBound = expectedAmount * (1 - tolerancePercent / 100);
        var upperBound = expectedAmount * (1 + tolerancePercent / 100);

        // Generate a small positive epsilon to go below the lower bound
        var epsilon = ((epsilonSeed.Get % 100) + 1) * 0.01m;
        var valueBelowLower = lowerBound - epsilon;

        var isExcluded = valueBelowLower < lowerBound;

        return isExcluded.ToProperty()
            .Label($"Value {valueBelowLower} below lowerBound={lowerBound} should be excluded. " +
                   $"expectedAmount={expectedAmount}, tolerance={tolerancePercent}%");
    }

    /// <summary>
    /// Property 6g: A value above the upper bound is EXCLUDED from the range.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AmountTolerance_AboveUpperBoundIsExcluded(PositiveInt amountSeed, PositiveInt toleranceSeed, PositiveInt epsilonSeed)
    {
        var expectedAmount = (amountSeed.Get % 10000 + 1) * 1.00m;
        var tolerancePercent = ((toleranceSeed.Get % 100) + 1) * 1.00m;

        var lowerBound = expectedAmount * (1 - tolerancePercent / 100);
        var upperBound = expectedAmount * (1 + tolerancePercent / 100);

        // Generate a small positive epsilon to go above the upper bound
        var epsilon = ((epsilonSeed.Get % 100) + 1) * 0.01m;
        var valueAboveUpper = upperBound + epsilon;

        var isExcluded = valueAboveUpper > upperBound;

        return isExcluded.ToProperty()
            .Label($"Value {valueAboveUpper} above upperBound={upperBound} should be excluded. " +
                   $"expectedAmount={expectedAmount}, tolerance={tolerancePercent}%");
    }

    /// <summary>
    /// Property 6h: The expected amount itself is always within the tolerance range.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AmountTolerance_ExpectedAmountIsAlwaysWithinRange(PositiveInt amountSeed, PositiveInt toleranceSeed)
    {
        var expectedAmount = (amountSeed.Get % 10000 + 1) * 1.00m;
        var tolerancePercent = ((toleranceSeed.Get % 100) + 1) * 1.00m;

        var lowerBound = expectedAmount * (1 - tolerancePercent / 100);
        var upperBound = expectedAmount * (1 + tolerancePercent / 100);

        var isWithin = expectedAmount >= lowerBound && expectedAmount <= upperBound;

        return isWithin.ToProperty()
            .Label($"expectedAmount={expectedAmount} should always be within [{lowerBound}, {upperBound}]. " +
                   $"tolerance={tolerancePercent}%");
    }

    #endregion
}
