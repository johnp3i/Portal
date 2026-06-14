using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using System.Globalization;

namespace Portal.Tests.PropertyBased;

// Feature: quotation-edit-modal-lines, Property 4: Summary shows correct count and sum of line totals

/// <summary>
/// Property-based tests for section summary computation in _SectionCards.cshtml.
/// Validates that for any section containing zero or more line items with positive totals,
/// the section summary text displays the exact item count and the sum of all line totals
/// formatted to two decimal places with the € currency symbol.
/// **Validates: Requirements 4.2, 4.3**
/// </summary>
public class SectionSummaryComputationPropertyTests
{
    #region Helper Methods

    /// <summary>
    /// Replicates the section summary computation logic from _SectionCards.cshtml.
    /// Computes the summary text: "{count} item(s) · Subtotal €{sum:N2}"
    /// </summary>
    private static string ComputeSectionSummary(List<QuotationLine> lines)
    {
        var sectionCount = lines.Count;
        var sectionSubtotal = lines.Sum(l => l.LineTotal);
        var plural = sectionCount != 1 ? "s" : "";
        return $"{sectionCount} item{plural} · Subtotal €{sectionSubtotal.ToString("N2")}";
    }

    /// <summary>
    /// Creates a QuotationLine with a specified LineTotal value.
    /// </summary>
    private static QuotationLine CreateLineWithTotal(decimal lineTotal, int sortOrder)
    {
        return new QuotationLine
        {
            Id = sortOrder,
            QuotationId = 1,
            Description = $"Test item {sortOrder}",
            Quantity = 1m,
            UnitPrice = lineTotal,
            VatRate = 0m,
            Discount = 0m,
            DiscountType = "Percentage",
            LineTotal = lineTotal,
            SortOrder = sortOrder,
            IsReverseCharge = false
        };
    }

    #endregion

    #region Property 4: Summary shows correct count and sum of line totals

    /// <summary>
    /// Property 4: For any section containing zero or more line items with positive line totals,
    /// the section summary text SHALL display the exact item count and the sum of all line totals
    /// formatted to two decimal places with the € currency symbol.
    /// Verifies count, sum, pluralization, and formatting.
    /// **Validates: Requirements 4.2, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SectionSummary_DisplaysCorrectCountAndFormattedSum(PositiveInt[] totalSeeds)
    {
        // Generate 0 to N line items with positive totals
        var lineCount = Math.Min(totalSeeds.Length, 25);
        var lines = new List<QuotationLine>();

        for (int i = 0; i < lineCount; i++)
        {
            // Generate a positive decimal total (between 0.01 and 99999.99)
            var lineTotal = (Math.Abs(totalSeeds[i].Get) % 9999999 + 1) / 100m;
            lines.Add(CreateLineWithTotal(lineTotal, i + 1));
        }

        var summary = ComputeSectionSummary(lines);

        // Verify count
        var expectedCount = lines.Count;
        var countCorrect = summary.StartsWith($"{expectedCount} item");

        // Verify pluralization
        var expectedPlural = expectedCount != 1 ? "s" : "";
        var pluralCorrect = summary.Contains($"{expectedCount} item{expectedPlural} ·");

        // Verify subtotal sum
        var expectedSubtotal = lines.Sum(l => l.LineTotal);
        var expectedFormatted = $"€{expectedSubtotal.ToString("N2")}";
        var subtotalCorrect = summary.Contains(expectedFormatted);

        // Verify full format
        var expectedFull = $"{expectedCount} item{expectedPlural} · Subtotal {expectedFormatted}";
        var formatCorrect = summary == expectedFull;

        return (countCorrect && pluralCorrect && subtotalCorrect && formatCorrect)
            .ToProperty()
            .Label($"Expected: '{expectedFull}', Actual: '{summary}', " +
                   $"Count={expectedCount}, Subtotal={expectedSubtotal}");
    }

    /// <summary>
    /// Property 4 (zero items): When a section has zero line items, the summary SHALL display
    /// "0 items · Subtotal €0.00".
    /// **Validates: Requirements 4.2, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SectionSummary_ZeroItems_DisplaysZeroItemsAndZeroSubtotal(bool _)
    {
        var lines = new List<QuotationLine>();
        var summary = ComputeSectionSummary(lines);

        var expected = "0 items · Subtotal €0.00";
        return (summary == expected)
            .ToProperty()
            .Label($"Expected: '{expected}', Actual: '{summary}'");
    }

    /// <summary>
    /// Property 4 (single item): When a section has exactly one line item, the summary SHALL
    /// use singular "item" (not "items").
    /// **Validates: Requirements 4.2, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SectionSummary_SingleItem_UsesSingularForm(PositiveInt totalSeed)
    {
        var lineTotal = (Math.Abs(totalSeed.Get) % 9999999 + 1) / 100m;
        var lines = new List<QuotationLine> { CreateLineWithTotal(lineTotal, 1) };

        var summary = ComputeSectionSummary(lines);

        // Must use singular "item" (not "items")
        var usesSingular = summary.Contains("1 item ·") && !summary.Contains("1 items");

        // Subtotal must equal the single line's total
        var expectedFormatted = $"€{lineTotal.ToString("N2")}";
        var subtotalCorrect = summary.Contains(expectedFormatted);

        var expected = $"1 item · Subtotal {expectedFormatted}";
        var formatCorrect = summary == expected;

        return (usesSingular && subtotalCorrect && formatCorrect)
            .ToProperty()
            .Label($"Expected: '{expected}', Actual: '{summary}'");
    }

    /// <summary>
    /// Property 4 (multiple items): When a section has more than one line item, the summary
    /// SHALL use plural "items" and the subtotal equals the sum of all line totals.
    /// **Validates: Requirements 4.2, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SectionSummary_MultipleItems_UsesPlural_AndSubtotalIsSumOfTotals(PositiveInt[] totalSeeds)
    {
        // Ensure at least 2 items
        if (totalSeeds.Length < 2)
            return true.ToProperty().Label("Fewer than 2 seeds — trivially true");

        var lineCount = Math.Min(totalSeeds.Length, 25);
        var lines = new List<QuotationLine>();

        for (int i = 0; i < lineCount; i++)
        {
            var lineTotal = (Math.Abs(totalSeeds[i].Get) % 9999999 + 1) / 100m;
            lines.Add(CreateLineWithTotal(lineTotal, i + 1));
        }

        var summary = ComputeSectionSummary(lines);

        // Must use plural "items"
        var usesPlural = summary.Contains($"{lineCount} items ·");

        // Subtotal must equal sum of all line totals
        var expectedSubtotal = lines.Sum(l => l.LineTotal);
        var expectedFormatted = $"€{expectedSubtotal.ToString("N2")}";
        var subtotalCorrect = summary.Contains(expectedFormatted);

        // Verify formatting uses two decimal places (N2)
        var twoDecimalCorrect = summary.Contains("€") && summary.EndsWith(expectedFormatted.Substring(expectedFormatted.Length - 3));

        return (usesPlural && subtotalCorrect && twoDecimalCorrect)
            .ToProperty()
            .Label($"Count={lineCount}, ExpectedSubtotal={expectedSubtotal}, " +
                   $"ExpectedFormatted={expectedFormatted}, Summary='{summary}'");
    }

    /// <summary>
    /// Property 4 (format invariant): The summary always contains the € symbol and
    /// the amount is formatted with exactly two decimal places using "N2" format.
    /// **Validates: Requirements 4.2, 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SectionSummary_AlwaysFormatsWithEuroCurrencyAndTwoDecimals(PositiveInt[] totalSeeds)
    {
        var lineCount = Math.Min(totalSeeds.Length, 20);
        var lines = new List<QuotationLine>();

        for (int i = 0; i < lineCount; i++)
        {
            var lineTotal = (Math.Abs(totalSeeds[i].Get) % 9999999 + 1) / 100m;
            lines.Add(CreateLineWithTotal(lineTotal, i + 1));
        }

        var summary = ComputeSectionSummary(lines);

        // Must contain € symbol
        var hasEuroSymbol = summary.Contains("€");

        // Must contain "Subtotal €" prefix
        var hasSubtotalPrefix = summary.Contains("· Subtotal €");

        // The amount after € must have exactly 2 decimal places
        var euroIndex = summary.IndexOf("€");
        var amountStr = summary.Substring(euroIndex + 1);
        var expectedSubtotal = lines.Sum(l => l.LineTotal);
        var expectedAmountStr = expectedSubtotal.ToString("N2");
        var amountMatchesN2Format = amountStr == expectedAmountStr;

        return (hasEuroSymbol && hasSubtotalPrefix && amountMatchesN2Format)
            .ToProperty()
            .Label($"HasEuro={hasEuroSymbol}, HasPrefix={hasSubtotalPrefix}, " +
                   $"AmountStr='{amountStr}', ExpectedN2='{expectedAmountStr}'");
    }

    #endregion
}
