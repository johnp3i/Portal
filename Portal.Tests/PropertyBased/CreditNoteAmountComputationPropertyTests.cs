using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Property-based tests for CreditNoteService.ComputeAmounts.
/// Validates the Amount Computation Chain: Subtotal, TaxAmount, and TotalAmount
/// are correctly computed from any set of valid credit note lines.
/// **Validates: Requirements 1.6, 1.7, 1.8, 1.9**
/// </summary>
public class CreditNoteAmountComputationPropertyTests
{
    /// <summary>
    /// Generates a single valid CreateCreditNoteLineDto with:
    /// - Quantity: 0.0001 to 999,999
    /// - UnitPrice: 0.01 to 999,999,999.99
    /// - VatRate: 0 to 100
    /// </summary>
    private static Gen<CreateCreditNoteLineDto> ValidLineGen()
    {
        return from quantityInt in Gen.Choose(1, 999999)
               from quantityFrac in Gen.Choose(0, 9999)
               from unitPriceInt in Gen.Choose(0, 999999)
               from unitPriceCents in Gen.Choose(1, 99)
               from vatRateInt in Gen.Choose(0, 100)
               let quantity = (decimal)quantityInt + (decimal)quantityFrac / 10000m
               let unitPrice = (decimal)unitPriceInt + (decimal)unitPriceCents / 100m
               let vatRate = (decimal)vatRateInt
               select new CreateCreditNoteLineDto
               {
                   Description = "Test line",
                   Quantity = quantity,
                   UnitPrice = unitPrice,
                   VatRate = vatRate
               };
    }

    /// <summary>
    /// Generates a non-empty list of valid credit note lines (1 to 10 lines).
    /// </summary>
    private static Gen<List<CreateCreditNoteLineDto>> ValidLinesGen()
    {
        return from count in Gen.Choose(1, 10)
               from lines in Gen.ListOf(count, ValidLineGen())
               select lines.ToList();
    }

    /// <summary>
    /// Property 1: Amount Computation Chain
    /// For any set of valid credit note lines, verify:
    /// - Subtotal = sum(Quantity × UnitPrice) for all lines
    /// - TaxAmount = sum(LineTotal × VatRate / 100) for each line
    /// - TotalAmount = Subtotal + TaxAmount
    /// **Validates: Requirements 1.6, 1.7, 1.8, 1.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputeAmounts_SubtotalEqualsSumOfLineTotals()
    {
        return Prop.ForAll(
            ValidLinesGen().ToArbitrary(),
            lines =>
            {
                var (subtotal, taxAmount, totalAmount) = CreditNoteService.ComputeAmounts(lines);

                // Requirement 1.6: Each line total = Quantity × UnitPrice
                // Requirement 1.7: Subtotal = sum of all line totals
                decimal expectedSubtotal = 0m;
                foreach (var line in lines)
                {
                    expectedSubtotal += line.Quantity * line.UnitPrice;
                }

                // Requirement 1.8: TaxAmount = sum(LineTotal × VatRate / 100) for each line
                decimal expectedTaxAmount = 0m;
                foreach (var line in lines)
                {
                    decimal lineTotal = line.Quantity * line.UnitPrice;
                    expectedTaxAmount += lineTotal * line.VatRate / 100m;
                }

                // Requirement 1.9: TotalAmount = Subtotal + TaxAmount
                decimal expectedTotalAmount = expectedSubtotal + expectedTaxAmount;

                var subtotalCorrect = subtotal == expectedSubtotal;
                var taxCorrect = taxAmount == expectedTaxAmount;
                var totalCorrect = totalAmount == expectedTotalAmount;

                return (subtotalCorrect && taxCorrect && totalCorrect)
                    .Label($"Subtotal: expected={expectedSubtotal}, actual={subtotal}, match={subtotalCorrect} | " +
                           $"TaxAmount: expected={expectedTaxAmount}, actual={taxAmount}, match={taxCorrect} | " +
                           $"TotalAmount: expected={expectedTotalAmount}, actual={totalAmount}, match={totalCorrect}");
            });
    }

    /// <summary>
    /// Property 1 (supplementary): TotalAmount always equals Subtotal + TaxAmount.
    /// This verifies the additive relationship independently.
    /// **Validates: Requirement 1.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputeAmounts_TotalAlwaysEqualsSubtotalPlusTax()
    {
        return Prop.ForAll(
            ValidLinesGen().ToArbitrary(),
            lines =>
            {
                var (subtotal, taxAmount, totalAmount) = CreditNoteService.ComputeAmounts(lines);

                return (totalAmount == subtotal + taxAmount)
                    .Label($"TotalAmount ({totalAmount}) should equal Subtotal ({subtotal}) + TaxAmount ({taxAmount})");
            });
    }

    /// <summary>
    /// Property 1 (supplementary): Subtotal is positive for valid inputs.
    /// Since Quantity > 0 and UnitPrice > 0, subtotal must be positive.
    /// **Validates: Requirement 1.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComputeAmounts_SubtotalIsPositive()
    {
        return Prop.ForAll(
            ValidLinesGen().ToArbitrary(),
            lines =>
            {
                var (subtotal, _, _) = CreditNoteService.ComputeAmounts(lines);

                return (subtotal > 0m)
                    .Label($"Subtotal ({subtotal}) should be positive for valid inputs with positive quantity and unit price");
            });
    }
}
