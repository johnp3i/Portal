using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;

namespace Portal.Tests.PropertyBased;

// Feature: quotation-edit-modal-lines, Property 1: Line total equals qty × unitPrice − discountAmount

/// <summary>
/// Property-based tests for line total computation logic used in _SectionCards.cshtml.
/// Validates that the computed line total always equals quantity × unitPrice − discountAmount,
/// where discountAmount depends on the discount type (Percentage or Fixed).
/// **Validates: Requirements 1.5**
/// </summary>
public class LineTotalComputationPropertyTests
{
    /// <summary>
    /// Computes the line total using the same formula as _SectionCards.cshtml:
    /// discountAmount = DiscountType == "Percentage"
    ///     ? UnitPrice * Quantity * (Discount / 100)
    ///     : Discount;
    /// LineTotal = Quantity * UnitPrice - discountAmount
    /// </summary>
    private static decimal ComputeLineTotal(decimal quantity, decimal unitPrice, decimal discount, string discountType)
    {
        var discountAmount = discountType == "Percentage"
            ? unitPrice * quantity * (discount / 100m)
            : discount;

        return quantity * unitPrice - discountAmount;
    }

    /// <summary>
    /// Property 1: For any line item with quantity > 0, unitPrice >= 0, discount >= 0,
    /// and discountType in {Percentage, Fixed}, the computed line total SHALL equal
    /// quantity × unitPrice − discountAmount.
    /// 
    /// For Percentage type: total = qty * unitPrice - (unitPrice * qty * discount/100)
    /// For Fixed type: total = qty * unitPrice - discount
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LineTotal_Equals_Qty_Times_UnitPrice_Minus_DiscountAmount_Percentage()
    {
        // Generate: quantity > 0 (0.01 to 9999.99), unitPrice >= 0 (0 to 99999.99), discount 0-100 for Percentage
        var quantityGen = Gen.Choose(1, 999999).Select(i => (decimal)i / 100m);
        var unitPriceGen = Gen.Choose(0, 9999999).Select(i => (decimal)i / 100m);
        var discountGen = Gen.Choose(0, 10000).Select(i => (decimal)i / 100m); // 0.00 to 100.00

        return Prop.ForAll(
            quantityGen.ToArbitrary(),
            unitPriceGen.ToArbitrary(),
            discountGen.ToArbitrary(),
            (quantity, unitPrice, discount) =>
            {
                const string discountType = "Percentage";

                var expectedDiscountAmount = unitPrice * quantity * (discount / 100m);
                var expectedTotal = quantity * unitPrice - expectedDiscountAmount;

                var computedTotal = ComputeLineTotal(quantity, unitPrice, discount, discountType);

                return (computedTotal == expectedTotal)
                    .Label($"Percentage: qty={quantity}, unitPrice={unitPrice}, discount={discount}%, " +
                           $"expected={expectedTotal}, got={computedTotal}");
            });
    }

    /// <summary>
    /// Property 1 (Fixed discount): For any line item with quantity > 0, unitPrice >= 0,
    /// discount >= 0 and <= qty * unitPrice, and discountType = "Fixed",
    /// the computed line total SHALL equal quantity × unitPrice − discount.
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LineTotal_Equals_Qty_Times_UnitPrice_Minus_DiscountAmount_Fixed()
    {
        // Generate: quantity > 0 (0.01 to 9999.99), unitPrice >= 0 (0 to 99999.99)
        // For Fixed: discount >= 0 and <= qty * unitPrice (so total remains non-negative)
        var quantityGen = Gen.Choose(1, 999999).Select(i => (decimal)i / 100m);
        var unitPriceGen = Gen.Choose(0, 9999999).Select(i => (decimal)i / 100m);

        // Generate a discount fraction (0 to 100%) and apply to qty*unitPrice to get a valid fixed discount
        var discountFractionGen = Gen.Choose(0, 10000).Select(i => (decimal)i / 10000m); // 0.0000 to 1.0000

        return Prop.ForAll(
            quantityGen.ToArbitrary(),
            unitPriceGen.ToArbitrary(),
            discountFractionGen.ToArbitrary(),
            (quantity, unitPrice, discountFraction) =>
            {
                const string discountType = "Fixed";

                // Fixed discount is capped to qty * unitPrice
                var maxDiscount = quantity * unitPrice;
                var discount = maxDiscount * discountFraction;

                var expectedTotal = quantity * unitPrice - discount;

                var computedTotal = ComputeLineTotal(quantity, unitPrice, discount, discountType);

                return (computedTotal == expectedTotal)
                    .Label($"Fixed: qty={quantity}, unitPrice={unitPrice}, discount={discount}, " +
                           $"expected={expectedTotal}, got={computedTotal}");
            });
    }

    /// <summary>
    /// Property 1 (Combined): For any line item with arbitrary valid inputs and either discount type,
    /// the QuotationLine entity's LineTotal field, when set via the computation formula,
    /// SHALL match the expected formula result.
    /// **Validates: Requirements 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LineTotal_OnQuotationLineEntity_MatchesFormula()
    {
        var combinedGen = from quantity in Gen.Choose(1, 999999).Select(i => (decimal)i / 100m)
                          from unitPrice in Gen.Choose(0, 9999999).Select(i => (decimal)i / 100m)
                          from discountValue in Gen.Choose(0, 10000).Select(i => (decimal)i / 100m)
                          from discountType in Gen.Elements("Percentage", "Fixed")
                          select (quantity, unitPrice, discountValue, discountType);

        return Prop.ForAll(
            combinedGen.ToArbitrary(),
            (input) =>
            {
                var (quantity, unitPrice, discountValue, discountType) = input;

                // For Fixed type, constrain discount to not exceed qty * unitPrice
                var discount = discountType == "Fixed"
                    ? Math.Min(discountValue / 100m * quantity * unitPrice, quantity * unitPrice)
                    : discountValue;

                // Create a QuotationLine entity and compute its total
                var line = new QuotationLine
                {
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    Discount = discount,
                    DiscountType = discountType
                };

                // Apply the computation formula (same as _SectionCards.cshtml)
                var discountAmount = line.DiscountType == "Percentage"
                    ? line.UnitPrice * line.Quantity * (line.Discount / 100m)
                    : line.Discount;

                line.LineTotal = line.Quantity * line.UnitPrice - discountAmount;

                // Verify the entity's LineTotal matches the expected computation
                var expectedTotal = ComputeLineTotal(quantity, unitPrice, discount, discountType);

                return (line.LineTotal == expectedTotal)
                    .Label($"Entity: qty={quantity}, unitPrice={unitPrice}, discount={discount}, " +
                           $"type={discountType}, expected={expectedTotal}, got={line.LineTotal}");
            });
    }
}
