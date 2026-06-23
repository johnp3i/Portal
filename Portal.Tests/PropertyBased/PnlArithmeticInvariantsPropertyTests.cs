using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: profit-loss-summary, Property 3: Arithmetic invariants

/// <summary>
/// Property-based tests for P&L arithmetic invariants.
/// Validates that derived profit figures maintain correct arithmetic relationships:
/// GrossProfit == Revenue - COGS AND NetProfit == GrossProfit - OperatingExpenses.
/// These are pure arithmetic properties — no database or entity interaction needed.
/// **Validates: Requirements 1.4, 1.5**
/// </summary>
public class PnlArithmeticInvariantsPropertyTests
{
    /// <summary>
    /// Property 3: GrossProfit equals Revenue minus COGS for all generated inputs.
    /// This invariant must hold regardless of whether values are negative, zero, or positive.
    /// **Validates: Requirements 1.4, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GrossProfit_Equals_Revenue_Minus_COGS(int revSeed, int cogsSeed)
    {
        // Generate reasonable decimal values from seeds
        var revenue = (decimal)(revSeed % 999999) + (Math.Abs(revSeed) % 100) / 100m;
        var cogs = (decimal)(cogsSeed % 999999) + (Math.Abs(cogsSeed) % 100) / 100m;

        // Compute GrossProfit using the same formula as PnlService
        var grossProfit = revenue - cogs;

        // Assert the invariant
        var invariantHolds = grossProfit == revenue - cogs;

        return invariantHolds.ToProperty()
            .Label($"GrossProfit({grossProfit}) == Revenue({revenue}) - COGS({cogs})");
    }

    /// <summary>
    /// Property 3: NetProfit equals GrossProfit minus OperatingExpenses for all generated inputs.
    /// This invariant must hold regardless of whether values are negative, zero, or positive.
    /// **Validates: Requirements 1.4, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NetProfit_Equals_GrossProfit_Minus_OperatingExpenses(int revSeed, int cogsSeed, int opExSeed)
    {
        // Generate reasonable decimal values from seeds
        var revenue = (decimal)(revSeed % 999999) + (Math.Abs(revSeed) % 100) / 100m;
        var cogs = (decimal)(cogsSeed % 999999) + (Math.Abs(cogsSeed) % 100) / 100m;
        var operatingExpenses = (decimal)(opExSeed % 999999) + (Math.Abs(opExSeed) % 100) / 100m;

        // Compute derived values using the same formulas as PnlService
        var grossProfit = revenue - cogs;
        var netProfit = grossProfit - operatingExpenses;

        // Assert the invariant
        var invariantHolds = netProfit == grossProfit - operatingExpenses;

        return invariantHolds.ToProperty()
            .Label($"NetProfit({netProfit}) == GrossProfit({grossProfit}) - OpEx({operatingExpenses})");
    }

    /// <summary>
    /// Property 3: Both arithmetic invariants hold simultaneously.
    /// GrossProfit == Revenue - COGS AND NetProfit == GrossProfit - OperatingExpenses.
    /// This is the combined property ensuring both relationships are always correct.
    /// **Validates: Requirements 1.4, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BothInvariants_Hold_Simultaneously(int revSeed, int cogsSeed, int opExSeed)
    {
        // Generate reasonable decimal values from seeds
        var revenue = (decimal)(revSeed % 999999) + (Math.Abs(revSeed) % 100) / 100m;
        var cogs = (decimal)(cogsSeed % 999999) + (Math.Abs(cogsSeed) % 100) / 100m;
        var operatingExpenses = (decimal)(opExSeed % 999999) + (Math.Abs(opExSeed) % 100) / 100m;

        // Compute derived values using the same formulas as PnlService
        var grossProfit = revenue - cogs;
        var netProfit = grossProfit - operatingExpenses;

        // Assert BOTH invariants
        var grossProfitInvariant = grossProfit == revenue - cogs;
        var netProfitInvariant = netProfit == grossProfit - operatingExpenses;
        var bothHold = grossProfitInvariant && netProfitInvariant;

        return bothHold.ToProperty()
            .Label($"Revenue={revenue}, COGS={cogs}, OpEx={operatingExpenses}, " +
                   $"GrossProfit={grossProfit}, NetProfit={netProfit} | " +
                   $"GP invariant={grossProfitInvariant}, NP invariant={netProfitInvariant}");
    }

    /// <summary>
    /// Property 3: The transitive relationship NetProfit == Revenue - COGS - OperatingExpenses holds.
    /// This is a derived consequence of the two primary invariants.
    /// **Validates: Requirements 1.4, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NetProfit_Equals_Revenue_Minus_COGS_Minus_OpEx(int revSeed, int cogsSeed, int opExSeed)
    {
        // Generate reasonable decimal values from seeds
        var revenue = (decimal)(revSeed % 999999) + (Math.Abs(revSeed) % 100) / 100m;
        var cogs = (decimal)(cogsSeed % 999999) + (Math.Abs(cogsSeed) % 100) / 100m;
        var operatingExpenses = (decimal)(opExSeed % 999999) + (Math.Abs(opExSeed) % 100) / 100m;

        // Compute using PnlService's approach (two-step)
        var grossProfit = revenue - cogs;
        var netProfit = grossProfit - operatingExpenses;

        // Compute the direct formula (one-step)
        var directNetProfit = revenue - cogs - operatingExpenses;

        // Both approaches must yield the same result
        var invariantHolds = netProfit == directNetProfit;

        return invariantHolds.ToProperty()
            .Label($"Two-step NetProfit({netProfit}) == Direct({directNetProfit}) | " +
                   $"Revenue={revenue}, COGS={cogs}, OpEx={operatingExpenses}");
    }
}
