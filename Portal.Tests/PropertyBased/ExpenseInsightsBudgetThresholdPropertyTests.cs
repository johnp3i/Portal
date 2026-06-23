using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Models.ExpenseInsights;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: expense-categorisation-insights, Property 5: Budget status threshold classification

/// <summary>
/// Property-based tests for ExpenseInsightsService.ComputeBudgetStatus.
/// Validates that budget status classification matches the spec:
/// - "Exceeded" when limit is not null AND spend >= limit (ratio >= 1.0)
/// - "Approaching" when limit is not null AND spend >= 0.8 * limit AND spend &lt; limit (ratio >= 0.8 and &lt; 1.0)
/// - "Within Limit" when limit is not null AND spend &lt; 0.8 * limit (ratio &lt; 0.8)
/// - "No Limit" when limit is null or &lt;= 0
/// **Validates: Requirements 7.1, 7.2, 7.3**
/// </summary>
public class ExpenseInsightsBudgetThresholdPropertyTests
{
    private readonly MethodInfo _computeBudgetStatusMethod;

    public ExpenseInsightsBudgetThresholdPropertyTests()
    {
        _computeBudgetStatusMethod = typeof(ExpenseInsightsService).GetMethod(
            "ComputeBudgetStatus",
            BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    #region Helpers

    /// <summary>
    /// Invokes the private static ComputeBudgetStatus method via reflection.
    /// </summary>
    private BudgetStatus InvokeComputeBudgetStatus(decimal spend, decimal? limit)
    {
        return (BudgetStatus)_computeBudgetStatusMethod.Invoke(null, new object[] { spend, limit })!;
    }

    /// <summary>
    /// Generates a non-negative decimal from a seed value.
    /// </summary>
    private static decimal GenerateNonNegativeDecimal(int seed)
    {
        return Math.Abs(seed) * 0.01m;
    }

    /// <summary>
    /// Generates a positive decimal (> 0) from a seed value for use as a limit.
    /// </summary>
    private static decimal GeneratePositiveDecimal(int seed)
    {
        return (Math.Abs(seed) % 999_999) + 0.01m;
    }

    #endregion

    #region NoLimit — null or <= 0

    /// <summary>
    /// When limit is null, ComputeBudgetStatus always returns NoLimit regardless of spend.
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullLimit_AlwaysReturnsNoLimit(PositiveInt spendSeed)
    {
        var spend = GenerateNonNegativeDecimal(spendSeed.Get);

        var result = InvokeComputeBudgetStatus(spend, null);

        return (result == BudgetStatus.NoLimit)
            .ToProperty()
            .Label($"Spend={spend}, Limit=null, Result={result}");
    }

    /// <summary>
    /// When limit is zero or negative, ComputeBudgetStatus returns NoLimit regardless of spend.
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ZeroOrNegativeLimit_AlwaysReturnsNoLimit(PositiveInt spendSeed, PositiveInt limitSeed)
    {
        var spend = GenerateNonNegativeDecimal(spendSeed.Get);
        // Generate a limit that is zero or negative
        var limit = -(Math.Abs(limitSeed.Get) % 10000) * 0.01m; // 0 or negative

        var result = InvokeComputeBudgetStatus(spend, limit);

        return (result == BudgetStatus.NoLimit)
            .ToProperty()
            .Label($"Spend={spend}, Limit={limit}, Result={result}");
    }

    #endregion

    #region Exceeded — ratio >= 1.0

    /// <summary>
    /// When spend >= limit (ratio >= 1.0), ComputeBudgetStatus returns Exceeded.
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpendAtOrAboveLimit_ReturnsExceeded(PositiveInt limitSeed, NonNegativeInt extraSeed)
    {
        var limit = GeneratePositiveDecimal(limitSeed.Get);
        // Spend is at or above the limit
        var spend = limit + (extraSeed.Get * 0.01m);

        var result = InvokeComputeBudgetStatus(spend, limit);

        return (result == BudgetStatus.Exceeded)
            .ToProperty()
            .Label($"Spend={spend}, Limit={limit}, Ratio={spend / limit:F4}, Result={result}");
    }

    /// <summary>
    /// Boundary: when spend is exactly equal to limit (ratio = 1.0), returns Exceeded.
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpendExactlyEqualsLimit_ReturnsExceeded(PositiveInt limitSeed)
    {
        var limit = GeneratePositiveDecimal(limitSeed.Get);
        var spend = limit; // ratio = exactly 1.0

        var result = InvokeComputeBudgetStatus(spend, limit);

        return (result == BudgetStatus.Exceeded)
            .ToProperty()
            .Label($"Spend={spend}, Limit={limit}, Ratio=1.0, Result={result}");
    }

    #endregion

    #region Approaching — ratio >= 0.8 and < 1.0

    /// <summary>
    /// When spend >= 0.8 * limit and spend &lt; limit, ComputeBudgetStatus returns Approaching.
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpendInApproachingRange_ReturnsApproaching(PositiveInt limitSeed, PositiveInt fractionSeed)
    {
        var limit = GeneratePositiveDecimal(limitSeed.Get);
        var lowerBound = 0.8m * limit;
        // Generate spend between [0.8*limit, limit) — exclusive of limit
        var range = limit - lowerBound; // = 0.2 * limit
        if (range <= 0) range = 0.01m;
        var fraction = (Math.Abs(fractionSeed.Get) % 1000) / 1000.0m; // 0.000 to 0.999
        var spend = lowerBound + (fraction * range * 0.999m); // Stay below limit

        // Guard: ensure spend is within [0.8*limit, limit)
        if (spend < lowerBound) spend = lowerBound;
        if (spend >= limit) spend = limit - 0.01m;
        if (spend < lowerBound) return true.ToProperty().Label("Degenerate case skipped");

        var result = InvokeComputeBudgetStatus(spend, limit);

        return (result == BudgetStatus.Approaching)
            .ToProperty()
            .Label($"Spend={spend}, Limit={limit}, Ratio={spend / limit:F4}, Result={result}");
    }

    /// <summary>
    /// Boundary: when spend is exactly 0.8 * limit (ratio = 0.8), returns Approaching.
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpendExactly80Percent_ReturnsApproaching(PositiveInt limitSeed)
    {
        var limit = GeneratePositiveDecimal(limitSeed.Get);
        var spend = 0.8m * limit; // ratio = exactly 0.8

        var result = InvokeComputeBudgetStatus(spend, limit);

        return (result == BudgetStatus.Approaching)
            .ToProperty()
            .Label($"Spend={spend}, Limit={limit}, Ratio=0.8, Result={result}");
    }

    #endregion

    #region WithinLimit — ratio < 0.8

    /// <summary>
    /// When spend &lt; 0.8 * limit (ratio &lt; 0.8), ComputeBudgetStatus returns WithinLimit.
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpendBelowApproachingThreshold_ReturnsWithinLimit(PositiveInt limitSeed, NonNegativeInt fractionSeed)
    {
        var limit = GeneratePositiveDecimal(limitSeed.Get);
        var upperBound = 0.8m * limit;
        // Generate spend in range [0, 0.8*limit)
        var fraction = (Math.Abs(fractionSeed.Get) % 1000) / 1000.0m; // 0.000 to 0.999
        var spend = fraction * upperBound * 0.999m; // Stay below 0.8 * limit

        if (spend >= upperBound) spend = upperBound - 0.01m;
        if (spend < 0) spend = 0;

        var result = InvokeComputeBudgetStatus(spend, limit);

        return (result == BudgetStatus.WithinLimit)
            .ToProperty()
            .Label($"Spend={spend}, Limit={limit}, Ratio={spend / limit:F4}, Result={result}");
    }

    /// <summary>
    /// When spend is zero and limit is positive, ComputeBudgetStatus returns WithinLimit.
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ZeroSpend_WithPositiveLimit_ReturnsWithinLimit(PositiveInt limitSeed)
    {
        var limit = GeneratePositiveDecimal(limitSeed.Get);
        var spend = 0m;

        var result = InvokeComputeBudgetStatus(spend, limit);

        return (result == BudgetStatus.WithinLimit)
            .ToProperty()
            .Label($"Spend=0, Limit={limit}, Ratio=0, Result={result}");
    }

    #endregion

    #region Exhaustive Classification

    /// <summary>
    /// For any non-negative spend and any positive limit, the classification always matches
    /// the spec's threshold rules. This is the master property covering all branches.
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClassificationAlwaysMatchesSpec(PositiveInt spendSeed, PositiveInt limitSeed)
    {
        var spend = GenerateNonNegativeDecimal(spendSeed.Get);
        var limit = GeneratePositiveDecimal(limitSeed.Get);

        var result = InvokeComputeBudgetStatus(spend, limit);

        var ratio = spend / limit;
        BudgetStatus expected;
        if (ratio >= 1.0m)
            expected = BudgetStatus.Exceeded;
        else if (ratio >= 0.8m)
            expected = BudgetStatus.Approaching;
        else
            expected = BudgetStatus.WithinLimit;

        return (result == expected)
            .ToProperty()
            .Label($"Spend={spend}, Limit={limit}, Ratio={ratio:F4}, Expected={expected}, Actual={result}");
    }

    /// <summary>
    /// For any non-negative spend with a nullable limit (null, zero, negative, or positive),
    /// the classification matches the full spec including the NoLimit branch.
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FullClassificationIncludingNoLimit(PositiveInt spendSeed, int limitSelector, PositiveInt limitValueSeed)
    {
        var spend = GenerateNonNegativeDecimal(spendSeed.Get);

        // Generate limit: null (selector % 3 == 0), zero/negative (selector % 3 == 1), positive (selector % 3 == 2)
        decimal? limit = (Math.Abs(limitSelector) % 3) switch
        {
            0 => null,
            1 => -(Math.Abs(limitValueSeed.Get) % 10000) * 0.01m, // zero or negative
            _ => GeneratePositiveDecimal(limitValueSeed.Get)        // positive
        };

        var result = InvokeComputeBudgetStatus(spend, limit);

        BudgetStatus expected;
        if (limit == null || limit <= 0)
        {
            expected = BudgetStatus.NoLimit;
        }
        else
        {
            var ratio = spend / limit.Value;
            if (ratio >= 1.0m)
                expected = BudgetStatus.Exceeded;
            else if (ratio >= 0.8m)
                expected = BudgetStatus.Approaching;
            else
                expected = BudgetStatus.WithinLimit;
        }

        return (result == expected)
            .ToProperty()
            .Label($"Spend={spend}, Limit={limit?.ToString() ?? "null"}, Expected={expected}, Actual={result}");
    }

    #endregion
}
