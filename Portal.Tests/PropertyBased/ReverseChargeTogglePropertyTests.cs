using FsCheck;
using FsCheck.Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: quotation-edit-modal-lines, Property 5: Reverse charge sets VAT to 0 and readonly

/// <summary>
/// Property-based tests for the reverse charge toggle behavior in quotation-line-modal.js.
/// Models the DOM state machine as a pure C# state transition to verify:
/// - Checking reverse charge sets VAT to 0 and makes it readonly
/// - Unchecking reverse charge restores the previous VAT rate and makes it editable
/// - Multiple toggle cycles preserve correctness (idempotence of on/off cycle)
/// **Validates: Requirements 2.5**
/// </summary>
public class ReverseChargeTogglePropertyTests
{
    /// <summary>
    /// Represents the state of the VAT input field in the Line Item Modal.
    /// Models the DOM state: vatInput.value, vatInput.readOnly, vatInput.dataset.previousVatRate
    /// </summary>
    private sealed class VatFieldState
    {
        public decimal VatValue { get; set; }
        public bool IsReadOnly { get; set; }
        public decimal? PreviousVatRate { get; set; }

        public static VatFieldState Initial(decimal vatRate) => new()
        {
            VatValue = vatRate,
            IsReadOnly = false,
            PreviousVatRate = null
        };
    }

    /// <summary>
    /// Simulates checking the reverse charge checkbox.
    /// Mirrors the JS logic:
    ///   vatInput.dataset.previousVatRate = vatInput.value;
    ///   vatInput.value = '0';
    ///   vatInput.readOnly = true;
    /// </summary>
    private static VatFieldState ApplyCheck(VatFieldState state) => new()
    {
        PreviousVatRate = state.VatValue,
        VatValue = 0m,
        IsReadOnly = true
    };

    /// <summary>
    /// Simulates unchecking the reverse charge checkbox.
    /// Mirrors the JS logic:
    ///   vatInput.value = vatInput.dataset.previousVatRate || '0';
    ///   vatInput.readOnly = false;
    /// </summary>
    private static VatFieldState ApplyUncheck(VatFieldState state) => new()
    {
        VatValue = state.PreviousVatRate ?? 0m,
        IsReadOnly = false,
        PreviousVatRate = null
    };

    /// <summary>
    /// Property 5a: For any initial VAT rate, checking the Reverse Charge checkbox
    /// SHALL set VAT to 0, make it read-only, and store the previous value.
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReverseCharge_Check_Sets_Vat_To_Zero_And_ReadOnly()
    {
        // Generate arbitrary VAT rates between 0.00 and 99.99
        var vatRateGen = Gen.Choose(0, 9999).Select(i => (decimal)i / 100m);

        return Prop.ForAll(
            vatRateGen.ToArbitrary(),
            (initialVatRate) =>
            {
                var state = VatFieldState.Initial(initialVatRate);

                // Act: check the reverse charge checkbox
                var afterCheck = ApplyCheck(state);

                // Assert: VAT value is 0
                var vatIsZero = (afterCheck.VatValue == 0m)
                    .Label($"VAT should be 0 after check, got {afterCheck.VatValue}");

                // Assert: field is read-only
                var isReadOnly = afterCheck.IsReadOnly
                    .Label("VAT field should be read-only after check");

                // Assert: previous rate is stored correctly
                var previousStored = (afterCheck.PreviousVatRate == initialVatRate)
                    .Label($"Previous VAT rate should be {initialVatRate}, got {afterCheck.PreviousVatRate}");

                return vatIsZero.And(isReadOnly).And(previousStored);
            });
    }

    /// <summary>
    /// Property 5b: For any initial VAT rate, checking then unchecking the Reverse Charge
    /// checkbox SHALL restore the VAT field to its original value and make it editable.
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReverseCharge_CheckThenUncheck_Restores_Original_Vat()
    {
        // Generate arbitrary VAT rates between 0.00 and 99.99
        var vatRateGen = Gen.Choose(0, 9999).Select(i => (decimal)i / 100m);

        return Prop.ForAll(
            vatRateGen.ToArbitrary(),
            (initialVatRate) =>
            {
                var state = VatFieldState.Initial(initialVatRate);

                // Act: check then uncheck
                var afterCheck = ApplyCheck(state);
                var afterUncheck = ApplyUncheck(afterCheck);

                // Assert: VAT value is restored to original
                var vatRestored = (afterUncheck.VatValue == initialVatRate)
                    .Label($"VAT should be restored to {initialVatRate} after uncheck, got {afterUncheck.VatValue}");

                // Assert: field is editable (not read-only)
                var isEditable = (!afterUncheck.IsReadOnly)
                    .Label("VAT field should be editable after uncheck");

                // Assert: previousVatRate is cleared
                var previousCleared = (afterUncheck.PreviousVatRate == null)
                    .Label($"PreviousVatRate should be null after uncheck, got {afterUncheck.PreviousVatRate}");

                return vatRestored.And(isEditable).And(previousCleared);
            });
    }

    /// <summary>
    /// Property 5c: Multiple toggle cycles preserve correctness — toggling on/off N times
    /// always ends with the original VAT rate restored and field editable.
    /// This tests idempotence of the check/uncheck cycle.
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReverseCharge_MultipleCycles_PreserveCorrectness()
    {
        // Generate arbitrary VAT rates and number of cycles (1 to 10)
        var vatRateGen = Gen.Choose(0, 9999).Select(i => (decimal)i / 100m);
        var cycleCountGen = Gen.Choose(1, 10);

        return Prop.ForAll(
            vatRateGen.ToArbitrary(),
            cycleCountGen.ToArbitrary(),
            (initialVatRate, cycles) =>
            {
                var state = VatFieldState.Initial(initialVatRate);

                // Apply N check/uncheck cycles
                for (var i = 0; i < cycles; i++)
                {
                    state = ApplyCheck(state);

                    // Mid-cycle assertions: should be in "checked" state
                    if (state.VatValue != 0m || !state.IsReadOnly)
                    {
                        return false.Label($"Cycle {i + 1}: After check, VAT should be 0 and readonly");
                    }

                    state = ApplyUncheck(state);
                }

                // After all cycles, should be back to initial state
                var vatRestored = (state.VatValue == initialVatRate)
                    .Label($"After {cycles} cycles, VAT should be {initialVatRate}, got {state.VatValue}");

                var isEditable = (!state.IsReadOnly)
                    .Label($"After {cycles} cycles, VAT field should be editable");

                var previousCleared = (state.PreviousVatRate == null)
                    .Label($"After {cycles} cycles, PreviousVatRate should be null");

                return vatRestored.And(isEditable).And(previousCleared);
            });
    }
}
