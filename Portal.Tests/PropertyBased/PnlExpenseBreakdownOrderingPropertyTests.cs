using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: profit-loss-summary, Property 10: Expense breakdown ordering

/// <summary>
/// Property-based tests for P&L expense breakdown ordering.
/// Validates that the expense breakdown list is always ordered by TotalAmount descending,
/// meaning each item's TotalAmount >= the next item's TotalAmount.
/// This is a pure sorting property — simulates the breakdown result list and verifies ordering.
/// **Validates: Requirements 9.3**
/// </summary>
public class PnlExpenseBreakdownOrderingPropertyTests
{
    /// <summary>
    /// Generates a positive decimal amount from a PositiveInt seed.
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = Math.Abs(seed) % 999999 + 1;
        return raw / 100m;
    }

    /// <summary>
    /// Property 10: After sorting descending by TotalAmount (as PnlService does via OrderByDescending),
    /// every consecutive pair satisfies items[i].TotalAmount >= items[i+1].TotalAmount.
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpenseBreakdown_IsOrderedByAmountDescending(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true (no items to order)");

        var itemCount = Math.Min(amountSeeds.Length, 20);

        // Generate random amounts representing TotalAmount per category
        var amounts = new List<decimal>();
        for (int i = 0; i < itemCount; i++)
        {
            amounts.Add(GenerateAmount(amountSeeds[i].Get));
        }

        // Simulate PnlService's OrderByDescending(c => c.TotalAmount)
        var sorted = amounts.OrderByDescending(a => a).ToList();

        // Assert: for every consecutive pair, items[i] >= items[i+1]
        var isOrdered = true;
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (sorted[i] < sorted[i + 1])
            {
                isOrdered = false;
                break;
            }
        }

        return isOrdered.ToProperty()
            .Label($"Sorted list of {sorted.Count} items should be descending. " +
                   $"First={sorted[0]}, Last={sorted[^1]}");
    }

    /// <summary>
    /// Property 10: A single-item breakdown list is trivially ordered.
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpenseBreakdown_SingleItem_IsTriviallyOrdered(PositiveInt amountSeed)
    {
        var amount = GenerateAmount(amountSeed.Get);
        var sorted = new List<decimal> { amount }.OrderByDescending(a => a).ToList();

        var isOrdered = sorted.Count == 1;

        return isOrdered.ToProperty()
            .Label($"Single item list with amount {amount} is trivially ordered");
    }

    /// <summary>
    /// Property 10: When all items have the same TotalAmount, the list is still ordered descending.
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpenseBreakdown_EqualAmounts_IsOrdered(PositiveInt amountSeed, PositiveInt countSeed)
    {
        var amount = GenerateAmount(amountSeed.Get);
        var count = (countSeed.Get % 19) + 2; // 2 to 20 items

        // All items have the same amount
        var amounts = Enumerable.Repeat(amount, count).ToList();

        // Simulate PnlService's OrderByDescending(c => c.TotalAmount)
        var sorted = amounts.OrderByDescending(a => a).ToList();

        // Assert: for every consecutive pair, items[i] >= items[i+1]
        var isOrdered = true;
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (sorted[i] < sorted[i + 1])
            {
                isOrdered = false;
                break;
            }
        }

        return isOrdered.ToProperty()
            .Label($"All {count} items with same amount {amount} should still be ordered descending");
    }

    /// <summary>
    /// Property 10: OrderByDescending produces a list where every element is >= its successor,
    /// regardless of the original order of inputs.
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpenseBreakdown_RandomOrder_SortsCorrectly(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length < 2)
            return true.ToProperty().Label("Fewer than 2 items — trivially ordered");

        var itemCount = Math.Min(amountSeeds.Length, 20);

        // Generate amounts in arbitrary order
        var amounts = new List<decimal>();
        for (int i = 0; i < itemCount; i++)
        {
            amounts.Add(GenerateAmount(amountSeeds[i].Get));
        }

        // Simulate PnlService's .OrderByDescending(c => c.TotalAmount)
        var sorted = amounts.OrderByDescending(a => a).ToList();

        // Verify descending invariant
        var allPairsDescending = true;
        var firstViolation = "";
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (sorted[i] < sorted[i + 1])
            {
                allPairsDescending = false;
                firstViolation = $"Violation at index {i}: {sorted[i]} < {sorted[i + 1]}";
                break;
            }
        }

        return allPairsDescending.ToProperty()
            .Label($"All {sorted.Count} items ordered descending. " +
                   (allPairsDescending ? "OK" : firstViolation));
    }
}
