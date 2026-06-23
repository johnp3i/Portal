using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: expense-categorisation-insights, Property 7: Month-over-month variance computation

/// <summary>
/// Property-based tests for ExpenseInsightsService.ComputeVariance (private static method).
/// Validates the Month-over-month variance computation for all specified cases:
/// - "N/A" when hasPreviousData is false
/// - "New" when previousMonthSpend == 0 AND currentMonthSpend > 0
/// - "—" when both are 0
/// - "-100.0" when currentMonthSpend == 0 AND previousMonthSpend > 0
/// - Otherwise: ((current - previous) / previous) × 100 rounded to 1 decimal place, formatted as "F1"
/// **Validates: Requirements 9.1, 9.4, 9.5, 9.6, 9.7**
/// </summary>
public class ExpenseInsightsMoMVariancePropertyTests
{
    private readonly MethodInfo _computeVarianceMethod;

    public ExpenseInsightsMoMVariancePropertyTests()
    {
        _computeVarianceMethod = typeof(ExpenseInsightsService).GetMethod(
            "ComputeVariance",
            BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    #region Helpers

    /// <summary>
    /// Invokes the private static ComputeVariance method via reflection.
    /// </summary>
    private string InvokeComputeVariance(decimal currentSpend, decimal previousSpend, bool hasPreviousData)
    {
        return (string)_computeVarianceMethod.Invoke(null, new object[] { currentSpend, previousSpend, hasPreviousData })!;
    }

    /// <summary>
    /// Generates a non-negative decimal from a seed value. Range: 0 to 999,999.99.
    /// </summary>
    private static decimal GenerateNonNegativeDecimal(int seed)
    {
        return Math.Abs(seed % 100_000_000) / 100m; // 0.00 to 999,999.99
    }

    /// <summary>
    /// Generates a strictly positive decimal from a seed value. Range: 0.01 to 999,999.99.
    /// </summary>
    private static decimal GeneratePositiveDecimal(int seed)
    {
        return (Math.Abs(seed % 99_999_999) + 1) / 100m; // 0.01 to 999,999.99
    }

    #endregion

    #region Case 1: hasPreviousData is false → "N/A"

    /// <summary>
    /// When hasPreviousData is false, ComputeVariance always returns "N/A"
    /// regardless of currentSpend and previousSpend values.
    /// **Validates: Requirements 9.1, 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoPreviousData_AlwaysReturnsNA(PositiveInt currentSeed, PositiveInt previousSeed)
    {
        var currentSpend = GenerateNonNegativeDecimal(currentSeed.Get);
        var previousSpend = GenerateNonNegativeDecimal(previousSeed.Get);

        var result = InvokeComputeVariance(currentSpend, previousSpend, hasPreviousData: false);

        return (result == "N/A")
            .ToProperty()
            .Label($"Current={currentSpend}, Previous={previousSpend}, hasPreviousData=false, Result=\"{result}\"");
    }

    #endregion

    #region Case 2: previousSpend == 0 AND currentSpend > 0 → "New"

    /// <summary>
    /// When hasPreviousData is true, previousSpend is 0, and currentSpend > 0,
    /// ComputeVariance returns "New".
    /// **Validates: Requirements 9.1, 9.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PreviousZeroCurrentPositive_ReturnsNew(PositiveInt currentSeed)
    {
        var currentSpend = GeneratePositiveDecimal(currentSeed.Get);

        var result = InvokeComputeVariance(currentSpend, previousSpend: 0m, hasPreviousData: true);

        return (result == "New")
            .ToProperty()
            .Label($"Current={currentSpend}, Previous=0, hasPreviousData=true, Result=\"{result}\"");
    }

    #endregion

    #region Case 3: both are 0 → "—"

    /// <summary>
    /// When hasPreviousData is true and both currentSpend and previousSpend are 0,
    /// ComputeVariance returns "—" (em dash).
    /// **Validates: Requirements 9.1, 9.6**
    /// </summary>
    [Fact]
    public void BothZero_ReturnsEmDash()
    {
        var result = InvokeComputeVariance(currentSpend: 0m, previousSpend: 0m, hasPreviousData: true);

        Assert.Equal("—", result);
    }

    #endregion

    #region Case 4: currentSpend == 0 AND previousSpend > 0 → "-100.0"

    /// <summary>
    /// When hasPreviousData is true, currentSpend is 0, and previousSpend > 0,
    /// ComputeVariance returns "-100.0".
    /// **Validates: Requirements 9.1, 9.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CurrentZeroPreviousPositive_ReturnsMinus100(PositiveInt previousSeed)
    {
        var previousSpend = GeneratePositiveDecimal(previousSeed.Get);

        var result = InvokeComputeVariance(currentSpend: 0m, previousSpend, hasPreviousData: true);

        return (result == "-100.0")
            .ToProperty()
            .Label($"Current=0, Previous={previousSpend}, hasPreviousData=true, Result=\"{result}\"");
    }

    #endregion

    #region Case 5: Normal case → ((current - previous) / previous) × 100, rounded to 1dp, formatted "F1"

    /// <summary>
    /// When hasPreviousData is true and both currentSpend > 0 and previousSpend > 0,
    /// ComputeVariance returns the percentage change formula: ((current - previous) / previous) × 100
    /// rounded to 1 decimal place and formatted as "F1".
    /// **Validates: Requirements 9.1, 9.4, 9.5, 9.6, 9.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NormalCase_ReturnsCorrectPercentageVariance(PositiveInt currentSeed, PositiveInt previousSeed)
    {
        var currentSpend = GeneratePositiveDecimal(currentSeed.Get);
        var previousSpend = GeneratePositiveDecimal(previousSeed.Get);

        var result = InvokeComputeVariance(currentSpend, previousSpend, hasPreviousData: true);

        var expectedVariance = Math.Round(((currentSpend - previousSpend) / previousSpend) * 100m, 1);
        var expectedResult = expectedVariance.ToString("F1");

        return (result == expectedResult)
            .ToProperty()
            .Label($"Current={currentSpend}, Previous={previousSpend}, " +
                   $"ExpectedVariance={expectedResult}, ActualResult=\"{result}\"");
    }

    /// <summary>
    /// When current equals previous (both > 0), the variance is always "0.0".
    /// **Validates: Requirements 9.1, 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EqualSpend_ReturnsZeroVariance(PositiveInt spendSeed)
    {
        var spend = GeneratePositiveDecimal(spendSeed.Get);

        var result = InvokeComputeVariance(spend, spend, hasPreviousData: true);

        return (result == "0.0")
            .ToProperty()
            .Label($"Current={spend}, Previous={spend}, Result=\"{result}\"");
    }

    /// <summary>
    /// When current is double the previous spend, the variance is always "100.0".
    /// **Validates: Requirements 9.1, 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DoubleSpend_Returns100Variance(PositiveInt spendSeed)
    {
        var previousSpend = GeneratePositiveDecimal(spendSeed.Get);
        var currentSpend = previousSpend * 2;

        var result = InvokeComputeVariance(currentSpend, previousSpend, hasPreviousData: true);

        return (result == "100.0")
            .ToProperty()
            .Label($"Current={currentSpend}, Previous={previousSpend}, Result=\"{result}\"");
    }

    /// <summary>
    /// When current is half the previous spend, the variance is always "-50.0".
    /// **Validates: Requirements 9.1, 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property HalfSpend_ReturnsMinus50Variance(PositiveInt spendSeed)
    {
        // Use even numbers to avoid rounding issues with halving
        var previousSpend = GeneratePositiveDecimal(spendSeed.Get) * 2;
        var currentSpend = previousSpend / 2;

        var result = InvokeComputeVariance(currentSpend, previousSpend, hasPreviousData: true);

        return (result == "-50.0")
            .ToProperty()
            .Label($"Current={currentSpend}, Previous={previousSpend}, Result=\"{result}\"");
    }

    #endregion
}
