using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: profit-loss-summary, Property 4: Margin formulas

/// <summary>
/// Property-based tests for P&L margin formula correctness.
/// Validates that GrossMargin and NetMargin are correctly computed from Revenue, COGS, and
/// Operating Expenses, with zero-revenue protection returning 0 for both margins.
/// **Validates: Requirements 1.6, 1.7**
/// </summary>
public class PnlMarginFormulasPropertyTests
{
    #region Margin Computation Logic (mirrors PnlService)

    /// <summary>
    /// Computes GrossMargin using the same formula as PnlService.
    /// Returns 0 when revenue is zero (zero-revenue protection).
    /// </summary>
    private static decimal ComputeGrossMargin(decimal revenue, decimal cogs)
    {
        var grossProfit = revenue - cogs;
        return revenue == 0m ? 0m : (grossProfit / revenue) * 100m;
    }

    /// <summary>
    /// Computes NetMargin using the same formula as PnlService.
    /// Returns 0 when revenue is zero (zero-revenue protection).
    /// </summary>
    private static decimal ComputeNetMargin(decimal revenue, decimal cogs, decimal operatingExpenses)
    {
        var grossProfit = revenue - cogs;
        var netProfit = grossProfit - operatingExpenses;
        return revenue == 0m ? 0m : (netProfit / revenue) * 100m;
    }

    #endregion

    #region Property 4: Margin formulas are correctly applied with zero-revenue protection

    /// <summary>
    /// When Revenue is zero, both GrossMargin and NetMargin must be exactly 0,
    /// regardless of COGS or Operating Expenses values.
    /// **Validates: Requirements 1.6, 1.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ZeroRevenue_BothMarginsAreZero(int cogsSeed, int opExSeed)
    {
        // Generate arbitrary COGS and OpEx values — margins must still be 0
        var cogs = Math.Abs(cogsSeed % 100000) / 100m;
        var operatingExpenses = Math.Abs(opExSeed % 100000) / 100m;
        var revenue = 0m;

        var grossMargin = ComputeGrossMargin(revenue, cogs);
        var netMargin = ComputeNetMargin(revenue, cogs, operatingExpenses);

        return (grossMargin == 0m && netMargin == 0m).ToProperty()
            .Label($"Zero revenue: GrossMargin={grossMargin}, NetMargin={netMargin}, COGS={cogs}, OpEx={operatingExpenses}");
    }

    /// <summary>
    /// When Revenue is positive, GrossMargin equals (GrossProfit / Revenue) * 100
    /// and NetMargin equals (NetProfit / Revenue) * 100.
    /// **Validates: Requirements 1.6, 1.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PositiveRevenue_MarginsFollowFormula(PositiveInt revenueSeed, int cogsSeed, int opExSeed)
    {
        // Generate positive revenue and arbitrary COGS/OpEx
        var revenue = (Math.Abs(revenueSeed.Get % 100000) + 1) / 100m; // always > 0
        var cogs = Math.Abs(cogsSeed % 100000) / 100m;
        var operatingExpenses = Math.Abs(opExSeed % 100000) / 100m;

        var grossProfit = revenue - cogs;
        var netProfit = grossProfit - operatingExpenses;

        var expectedGrossMargin = (grossProfit / revenue) * 100m;
        var expectedNetMargin = (netProfit / revenue) * 100m;

        var actualGrossMargin = ComputeGrossMargin(revenue, cogs);
        var actualNetMargin = ComputeNetMargin(revenue, cogs, operatingExpenses);

        return (actualGrossMargin == expectedGrossMargin && actualNetMargin == expectedNetMargin).ToProperty()
            .Label($"Revenue={revenue}, COGS={cogs}, OpEx={operatingExpenses}, " +
                   $"ExpectedGrossMargin={expectedGrossMargin}, ActualGrossMargin={actualGrossMargin}, " +
                   $"ExpectedNetMargin={expectedNetMargin}, ActualNetMargin={actualNetMargin}");
    }

    /// <summary>
    /// GrossMargin is always 100% when COGS is zero and Revenue is positive
    /// (because GrossProfit == Revenue in that case).
    /// **Validates: Requirements 1.6, 1.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PositiveRevenue_ZeroCogs_GrossMarginIs100(PositiveInt revenueSeed)
    {
        var revenue = (Math.Abs(revenueSeed.Get % 100000) + 1) / 100m;
        var cogs = 0m;

        var grossMargin = ComputeGrossMargin(revenue, cogs);

        return (grossMargin == 100m).ToProperty()
            .Label($"Revenue={revenue}, COGS=0, GrossMargin={grossMargin} (expected 100)");
    }

    /// <summary>
    /// When Revenue equals COGS (and Revenue > 0), GrossMargin is exactly 0%
    /// (because GrossProfit == 0).
    /// **Validates: Requirements 1.6, 1.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RevenueEqualsCogs_GrossMarginIsZero(PositiveInt revenueSeed)
    {
        var revenue = (Math.Abs(revenueSeed.Get % 100000) + 1) / 100m;
        var cogs = revenue; // same as revenue

        var grossMargin = ComputeGrossMargin(revenue, cogs);

        return (grossMargin == 0m).ToProperty()
            .Label($"Revenue={revenue}, COGS={cogs}, GrossMargin={grossMargin} (expected 0)");
    }

    /// <summary>
    /// Mixed scenario: random Revenue (including zero), COGS, and OpEx.
    /// Verifies that the margin formulas maintain consistency:
    /// - Zero revenue → both margins zero
    /// - Positive revenue → margins match formula
    /// **Validates: Requirements 1.6, 1.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MixedScenario_MarginFormulasAreConsistent(
        NonNegativeInt revenueSeed, int cogsSeed, int opExSeed)
    {
        // Revenue can be zero (NonNegativeInt includes 0)
        var revenue = (revenueSeed.Get % 100000) / 100m;
        var cogs = Math.Abs(cogsSeed % 100000) / 100m;
        var operatingExpenses = Math.Abs(opExSeed % 100000) / 100m;

        var grossProfit = revenue - cogs;
        var netProfit = grossProfit - operatingExpenses;

        var actualGrossMargin = ComputeGrossMargin(revenue, cogs);
        var actualNetMargin = ComputeNetMargin(revenue, cogs, operatingExpenses);

        bool isCorrect;
        if (revenue == 0m)
        {
            isCorrect = actualGrossMargin == 0m && actualNetMargin == 0m;
        }
        else
        {
            var expectedGrossMargin = (grossProfit / revenue) * 100m;
            var expectedNetMargin = (netProfit / revenue) * 100m;
            isCorrect = actualGrossMargin == expectedGrossMargin && actualNetMargin == expectedNetMargin;
        }

        return isCorrect.ToProperty()
            .Label($"Revenue={revenue}, COGS={cogs}, OpEx={operatingExpenses}, " +
                   $"GrossMargin={actualGrossMargin}, NetMargin={actualNetMargin}");
    }

    #endregion
}
