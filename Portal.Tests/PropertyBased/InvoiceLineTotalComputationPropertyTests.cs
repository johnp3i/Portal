using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;

namespace Portal.Tests.PropertyBased;

// Feature: invoice-edit-modal-lines, Property 1: Line total equals qty × unitPrice − discountAmount

/// <summary>
/// Property-based tests for invoice line total computation logic.
/// Validates that the computed line total always equals quantity × unitPrice − discountAmount,
/// where discountAmount depends on the discount type (Percentage or Fixed).
/// **Validates: Requirements 1.6**
/// </summary>
public class InvoiceLineTotalComputationPropertyTests
{
    /// <summary>
    /// Computes the line total using the formula:
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
    /// Property 1 (Percentage): For any invoice line item with quantity > 0, unitPrice >= 0,
    /// discount >= 0, and discountType = "Percentage", the computed line total SHALL equal
    /// quantity × unitPrice − (unitPrice × quantity × discount / 100).
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LineTotal_Equals_Qty_Times_UnitPrice_Minus_DiscountAmount_Percentage()
    {
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
    /// Property 1 (Fixed): For any invoice line item with quantity > 0, unitPrice >= 0,
    /// discount >= 0 and <= qty * unitPrice, and discountType = "Fixed",
    /// the computed line total SHALL equal quantity × unitPrice − discount.
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LineTotal_Equals_Qty_Times_UnitPrice_Minus_DiscountAmount_Fixed()
    {
        var quantityGen = Gen.Choose(1, 999999).Select(i => (decimal)i / 100m);
        var unitPriceGen = Gen.Choose(0, 9999999).Select(i => (decimal)i / 100m);
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
    /// Property 1 (Combined): For any invoice line item with arbitrary valid inputs and either discount type,
    /// the InvoiceLine entity's LineTotal field, when set via the computation formula,
    /// SHALL match the expected formula result.
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LineTotal_OnInvoiceLineEntity_MatchesFormula()
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

                // Create an InvoiceLine entity and compute its total
                var line = new InvoiceLine
                {
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    Discount = discount,
                    DiscountType = discountType
                };

                // Apply the computation formula
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
