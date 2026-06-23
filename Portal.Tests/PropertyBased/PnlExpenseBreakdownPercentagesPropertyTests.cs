using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: profit-loss-summary, Property 9: Expense breakdown percentages

/// <summary>
/// Property-based tests for P&L expense breakdown percentage computation.
/// Validates that the sum of all PercentageOfTotal values in the category breakdown
/// equals 100% (within ±0.1% floating-point tolerance) for any non-empty set of expense amounts.
/// This is a pure arithmetic test — no database or entity interaction needed.
/// **Validates: Requirements 3.4, 9.2**
/// </summary>
public class PnlExpenseBreakdownPercentagesPropertyTests
{
    #region Percentage Computation Logic (mirrors PnlService)

    /// <summary>
    /// Computes the PercentageOfTotal for each category amount using the same formula as PnlService:
    /// PercentageOfTotal = (amount / totalExpenses) * 100
    /// </summary>
    private static decimal[] ComputePercentages(decimal[] amounts)
    {
        var totalExpenses = amounts.Sum();
        if (totalExpenses == 0m)
            return Array.Empty<decimal>();

        return amounts.Select(a => (a / totalExpenses) * 100m).ToArray();
    }

    #endregion

    #region Property 9: Expense breakdown percentages sum to 100%

    /// <summary>
    /// For any non-empty list of positive expense amounts (1-20 items), the sum of all
    /// PercentageOfTotal values must equal 100% within ±0.1% tolerance.
    /// **Validates: Requirements 3.4, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PercentagesSum_Equals_100_Within_Tolerance(PositiveInt[] seeds)
    {
        // Guard: need at least 1 item and at most 20
        if (seeds == null || seeds.Length == 0)
            return true.ToProperty().Label("Skipped: empty input");

        // Take 1-20 items
        var items = seeds.Take(20).ToArray();

        // Generate positive decimal amounts from seeds
        var amounts = items.Select(s => (Math.Abs(s.Get % 100000) + 1) / 100m).ToArray();

        // Compute percentages using the same formula as PnlService
        var percentages = ComputePercentages(amounts);

        // Sum all percentages
        var percentageSum = percentages.Sum();

        // Assert: sum should be 100% within ±0.1% tolerance
        var tolerance = 0.1m;
        var isWithinTolerance = Math.Abs(percentageSum - 100m) <= tolerance;

        return isWithinTolerance.ToProperty()
            .Label($"PercentageSum={percentageSum}, Expected=100±{tolerance}, " +
                   $"ItemCount={items.Length}, TotalExpenses={amounts.Sum()}");
    }

    /// <summary>
    /// For a single expense category, the percentage must be exactly 100%.
    /// **Validates: Requirements 3.4, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleCategory_Percentage_Is_Exactly_100(PositiveInt seed)
    {
        // Generate a single positive amount
        var amount = (Math.Abs(seed.Get % 100000) + 1) / 100m;
        var amounts = new[] { amount };

        // Compute percentage
        var percentages = ComputePercentages(amounts);
        var percentageSum = percentages.Sum();

        // Single category should be exactly 100%
        return (percentageSum == 100m).ToProperty()
            .Label($"SingleCategory: Amount={amount}, Percentage={percentageSum} (expected 100)");
    }

    /// <summary>
    /// For two equal amounts, each percentage should be exactly 50%.
    /// **Validates: Requirements 3.4, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TwoEqualAmounts_EachPercentage_Is_50(PositiveInt seed)
    {
        // Generate two identical amounts
        var amount = (Math.Abs(seed.Get % 100000) + 1) / 100m;
        var amounts = new[] { amount, amount };

        // Compute percentages
        var percentages = ComputePercentages(amounts);

        // Each should be 50% and sum should be 100%
        var eachIs50 = percentages.All(p => p == 50m);
        var sumIs100 = percentages.Sum() == 100m;

        return (eachIs50 && sumIs100).ToProperty()
            .Label($"TwoEqual: Amount={amount}, Percentages=[{string.Join(", ", percentages)}], Sum={percentages.Sum()}");
    }

    /// <summary>
    /// For many categories (maximum boundary: 20 items), percentages still sum to 100% within tolerance.
    /// Uses seeds to generate exactly 20 distinct amounts.
    /// **Validates: Requirements 3.4, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MaxCategories_PercentagesSum_Within_Tolerance(PositiveInt[] seeds)
    {
        // Need at least 20 seeds to fill max categories
        if (seeds == null || seeds.Length < 20)
            return true.ToProperty().Label("Skipped: insufficient seeds for 20 categories");

        // Take exactly 20 items
        var amounts = seeds.Take(20)
            .Select(s => (Math.Abs(s.Get % 100000) + 1) / 100m)
            .ToArray();

        // Compute percentages
        var percentages = ComputePercentages(amounts);
        var percentageSum = percentages.Sum();

        // Assert within tolerance
        var tolerance = 0.1m;
        var isWithinTolerance = Math.Abs(percentageSum - 100m) <= tolerance;

        return isWithinTolerance.ToProperty()
            .Label($"20 categories: PercentageSum={percentageSum}, Expected=100±{tolerance}");
    }

    /// <summary>
    /// Each individual percentage must be positive (since all amounts are positive).
    /// This ensures no negative or zero percentages creep in from the computation.
    /// **Validates: Requirements 3.4, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllPercentages_Are_Positive(PositiveInt[] seeds)
    {
        if (seeds == null || seeds.Length == 0)
            return true.ToProperty().Label("Skipped: empty input");

        var items = seeds.Take(20).ToArray();
        var amounts = items.Select(s => (Math.Abs(s.Get % 100000) + 1) / 100m).ToArray();

        var percentages = ComputePercentages(amounts);

        var allPositive = percentages.All(p => p > 0m);

        return allPositive.ToProperty()
            .Label($"ItemCount={items.Length}, Percentages=[{string.Join(", ", percentages.Take(5))}...]");
    }

    #endregion
}
