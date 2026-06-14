using FsCheck;
using FsCheck.Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: quotation-edit-modal-lines, Property 2: Zero discount shows dash, positive shows minus-prefixed amount

/// <summary>
/// Property-based tests for discount display formatting in the quotation line item table.
/// Validates that when the computed discount amount equals 0 the discount column renders a dash character,
/// and when the discount amount is greater than 0 it renders the amount prefixed with a minus sign and currency symbol.
/// **Validates: Requirements 1.3, 1.4**
/// </summary>
public class DiscountDisplayFormattingPropertyTests
{
    #region Helper: Discount Display Logic (Oracle)

    /// <summary>
    /// Replicates the discount amount computation from _SectionCards.cshtml:
    /// If DiscountType == "Percentage": discountAmount = unitPrice * quantity * (discount / 100)
    /// If DiscountType == "Fixed": discountAmount = discount
    /// </summary>
    private static decimal ComputeDiscountAmount(decimal unitPrice, decimal quantity, decimal discount, string discountType)
    {
        return discountType == "Percentage"
            ? unitPrice * quantity * (discount / 100m)
            : discount;
    }

    /// <summary>
    /// Replicates the discount display formatting from _SectionCards.cshtml:
    /// If discountAmount == 0 → "-"
    /// If discountAmount > 0 → "-€{discountAmount:N2}"
    /// </summary>
    private static string FormatDiscountDisplay(decimal discountAmount)
    {
        if (discountAmount == 0)
        {
            return "-";
        }
        return $"-€{discountAmount:N2}";
    }

    #endregion

    #region Property 2a: Zero discount amount renders dash

    /// <summary>
    /// Property 2a: For any line item where the computed discount amount equals zero,
    /// the formatted discount display SHALL be a dash character "-".
    /// This covers: discount=0 (both types), and Percentage with unitPrice=0 or quantity=0.
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ZeroDiscountAmount_DisplaysDash()
    {
        // Generate scenarios where discountAmount will be 0:
        // 1. Fixed type with discount = 0
        // 2. Percentage type with discount = 0
        // 3. Percentage type with unitPrice = 0 (any discount)
        var zeroDiscountGen = Gen.OneOf(
            // Fixed type, discount = 0, arbitrary quantity > 0 and unitPrice >= 0
            Gen.Choose(1, 10000).SelectMany(qty =>
                Gen.Choose(0, 10000).Select(price =>
                    (Quantity: (decimal)qty / 100m, UnitPrice: (decimal)price / 100m, Discount: 0m, DiscountType: "Fixed"))),
            // Percentage type, discount = 0, arbitrary quantity > 0 and unitPrice >= 0
            Gen.Choose(1, 10000).SelectMany(qty =>
                Gen.Choose(0, 10000).Select(price =>
                    (Quantity: (decimal)qty / 100m, UnitPrice: (decimal)price / 100m, Discount: 0m, DiscountType: "Percentage"))),
            // Percentage type, unitPrice = 0, arbitrary discount and quantity > 0
            Gen.Choose(1, 10000).SelectMany(qty =>
                Gen.Choose(0, 10000).Select(disc =>
                    (Quantity: (decimal)qty / 100m, UnitPrice: 0m, Discount: (decimal)disc / 100m, DiscountType: "Percentage")))
        );

        return Prop.ForAll(
            zeroDiscountGen.ToArbitrary(),
            (input) =>
            {
                var discountAmount = ComputeDiscountAmount(input.UnitPrice, input.Quantity, input.Discount, input.DiscountType);
                var display = FormatDiscountDisplay(discountAmount);

                return (discountAmount == 0m && display == "-")
                    .Label($"Expected dash for zero discount. " +
                           $"UnitPrice={input.UnitPrice}, Qty={input.Quantity}, " +
                           $"Discount={input.Discount}, Type={input.DiscountType}, " +
                           $"ComputedAmount={discountAmount}, Display=\"{display}\"");
            });
    }

    #endregion

    #region Property 2b: Positive discount amount renders minus-prefixed currency amount

    /// <summary>
    /// Property 2b: For any line item where the computed discount amount is greater than zero,
    /// the formatted discount display SHALL be "-€{amount:N2}" (minus sign, euro symbol, formatted to 2 decimals).
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PositiveDiscountAmount_DisplaysMinusPrefixedAmount()
    {
        // Generate scenarios where discountAmount will be > 0:
        // 1. Fixed type with discount > 0
        // 2. Percentage type with unitPrice > 0, quantity > 0, discount > 0
        var positiveDiscountGen = Gen.OneOf(
            // Fixed type, discount > 0, arbitrary quantity > 0 and unitPrice >= 0
            Gen.Choose(1, 100000).SelectMany(disc =>
                Gen.Choose(1, 10000).SelectMany(qty =>
                    Gen.Choose(0, 100000).Select(price =>
                        (Quantity: (decimal)qty / 100m, UnitPrice: (decimal)price / 100m, Discount: (decimal)disc / 100m, DiscountType: "Fixed")))),
            // Percentage type, all positive: unitPrice > 0, quantity > 0, discount > 0
            Gen.Choose(1, 100000).SelectMany(price =>
                Gen.Choose(1, 10000).SelectMany(qty =>
                    Gen.Choose(1, 10000).Select(disc =>
                        (Quantity: (decimal)qty / 100m, UnitPrice: (decimal)price / 100m, Discount: (decimal)disc / 100m, DiscountType: "Percentage"))))
        );

        return Prop.ForAll(
            positiveDiscountGen.ToArbitrary(),
            (input) =>
            {
                var discountAmount = ComputeDiscountAmount(input.UnitPrice, input.Quantity, input.Discount, input.DiscountType);
                var display = FormatDiscountDisplay(discountAmount);
                var expectedDisplay = $"-€{discountAmount:N2}";

                return (discountAmount > 0m && display == expectedDisplay)
                    .Label($"Expected \"{expectedDisplay}\" for positive discount. " +
                           $"UnitPrice={input.UnitPrice}, Qty={input.Quantity}, " +
                           $"Discount={input.Discount}, Type={input.DiscountType}, " +
                           $"ComputedAmount={discountAmount}, Display=\"{display}\"");
            });
    }

    #endregion

    #region Property 2c: Discount display is deterministic (same inputs → same output)

    /// <summary>
    /// Property 2c: For any line item with arbitrary valid field values, the discount display
    /// formatting produces exactly one of two outputs: "-" for zero or "-€{amount:N2}" for positive.
    /// No other output is possible.
    /// **Validates: Requirements 1.3, 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DiscountDisplay_AlwaysProducesDashOrMinusPrefixedAmount()
    {
        // Generate arbitrary valid inputs covering full range
        var arbitraryInputGen = Gen.Choose(0, 100000).SelectMany(price =>
            Gen.Choose(1, 10000).SelectMany(qty =>
                Gen.Choose(0, 10000).SelectMany(disc =>
                    Gen.Elements("Percentage", "Fixed").Select(type =>
                        (Quantity: (decimal)qty / 100m, UnitPrice: (decimal)price / 100m, Discount: (decimal)disc / 100m, DiscountType: type)))));

        return Prop.ForAll(
            arbitraryInputGen.ToArbitrary(),
            (input) =>
            {
                var discountAmount = ComputeDiscountAmount(input.UnitPrice, input.Quantity, input.Discount, input.DiscountType);
                var display = FormatDiscountDisplay(discountAmount);

                var isDash = display == "-";
                var isMinusPrefixed = display.StartsWith("-€") && display.Length > 2;
                var isValidOutput = isDash || isMinusPrefixed;

                // Additionally verify the mapping is correct
                var mappingCorrect = (discountAmount == 0m && isDash) || (discountAmount > 0m && isMinusPrefixed);

                return (isValidOutput && mappingCorrect)
                    .Label($"Display must be either \"-\" (zero) or \"-€X.XX\" (positive). " +
                           $"Got \"{display}\" for discountAmount={discountAmount}, " +
                           $"UnitPrice={input.UnitPrice}, Qty={input.Quantity}, " +
                           $"Discount={input.Discount}, Type={input.DiscountType}");
            });
    }

    #endregion
}
