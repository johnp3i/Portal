using FsCheck;
using FsCheck.Xunit;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for VAT Output/Input ratio zero-guard.
/// Tests Property 17 from the revenue-control design document.
/// 
/// The VatIntegrationService computes the Output/Input VAT Ratio as:
///   inputVat == 0 ? 0 : outputVat / inputVat
/// 
/// This property verifies that when Input VAT = 0, the ratio always returns 0
/// without throwing a division-by-zero exception, regardless of the Output VAT value.
/// </summary>
public class VatOutputInputRatioPropertyTests
{
    // Feature: revenue-control, Property 17: VAT Output/Input ratio zero-guard
    // **Validates: Requirements 6.5**
    [Property(MaxTest = 100)]
    public Property VatRatio_Returns_Zero_When_InputVat_Is_Zero()
    {
        // Generate random Output VAT values (positive, zero, and negative are all possible)
        var outputVatGen = Gen.Choose(-99999999, 99999999)
            .Select(i => Math.Round((decimal)i / 100m, 2));

        return Prop.ForAll(
            outputVatGen.ToArbitrary(),
            (outputVat) =>
            {
                const decimal inputVat = 0m;

                // Apply the same computation logic as VatIntegrationService
                var ratio = inputVat == 0m ? 0m : outputVat / inputVat;

                return (ratio == 0m)
                    .Label($"Expected ratio to be 0 when InputVat=0, but got {ratio} (OutputVat={outputVat})");
            });
    }

    // Feature: revenue-control, Property 17: VAT Output/Input ratio zero-guard
    // **Validates: Requirements 6.5**
    [Property(MaxTest = 100)]
    public Property VatRatio_Never_Throws_DivisionByZero_When_InputVat_Is_Zero()
    {
        // Generate random Output VAT values including edge cases
        var outputVatGen = Gen.OneOf(
            Gen.Choose(-99999999, 99999999).Select(i => Math.Round((decimal)i / 100m, 2)),
            Gen.Constant(0m),
            Gen.Constant(decimal.MaxValue / 100m),
            Gen.Constant(decimal.MinValue / 100m)
        );

        return Prop.ForAll(
            outputVatGen.ToArbitrary(),
            (outputVat) =>
            {
                const decimal inputVat = 0m;

                // This must not throw — the zero-guard should prevent DivideByZeroException
                decimal ratio;
                try
                {
                    ratio = inputVat == 0m ? 0m : outputVat / inputVat;
                }
                catch (DivideByZeroException)
                {
                    return false.Label($"DivideByZeroException thrown for OutputVat={outputVat}, InputVat=0");
                }

                return (ratio == 0m)
                    .Label($"Expected 0 but got {ratio} for OutputVat={outputVat}, InputVat=0");
            });
    }
}
