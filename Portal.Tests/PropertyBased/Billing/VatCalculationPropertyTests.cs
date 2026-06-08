using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Portal.Web.Services.Billing;

namespace Portal.Tests.PropertyBased.Billing;

// Feature: subscription-billing-invoices, Property 5: VAT calculation correctness

/// <summary>
/// Property-based tests for VatCalculationService.Calculate.
/// Verifies correct VAT rate selection, VatAmount = netAmount × rate,
/// GrossAmount = netAmount + VatAmount, and reverse charge flag for all country/VAT number scenarios.
/// **Validates: Requirements 9.1, 9.2, 9.3, 9.4**
/// </summary>
public class VatCalculationPropertyTests
{
    private static readonly VatCalculationService Service =
        new(NullLogger<VatCalculationService>.Instance);

    /// <summary>
    /// EU member state codes (27 members as of current EU composition).
    /// </summary>
    private static readonly string[] EuMemberStates =
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR",
        "DE", "GR", "HU", "IE", "IT", "LV", "LT", "LU", "MT", "NL",
        "PL", "PT", "RO", "SK", "SI", "ES", "SE"
    };

    /// <summary>
    /// EU member states excluding Cyprus (for reverse charge scenarios).
    /// </summary>
    private static readonly string[] EuNonCyprus =
        EuMemberStates.Where(c => c != "CY").ToArray();

    /// <summary>
    /// Non-EU country codes for testing export scenarios.
    /// </summary>
    private static readonly string[] NonEuCountries =
    {
        "US", "GB", "CH", "NO", "AU", "JP", "CA", "BR", "IN", "CN",
        "KR", "SG", "NZ", "IL", "AE", "ZA", "MX", "TR", "RU", "UA"
    };

    #region Generators

    /// <summary>
    /// Generates positive net amounts (0.01 to 999,999.99).
    /// </summary>
    private static Gen<decimal> PositiveAmountGen =>
        Gen.Choose(1, 99999999).Select(i => (decimal)i / 100m);

    /// <summary>
    /// Generates a non-empty VAT registration number.
    /// </summary>
    private static Gen<string> VatNumberGen =>
        Gen.Elements("CY12345678X", "DE123456789", "FR12345678901", "IT12345678901",
            "NL123456789B01", "ES12345678A", "BE0123456789", "AT12345678",
            "PL1234567890", "IE1234567A");

    /// <summary>
    /// Generates null or empty VAT numbers.
    /// </summary>
    private static Gen<string?> EmptyVatNumberGen =>
        Gen.Elements<string?>(null, "", "  ");

    #endregion

    #region Property 5a: Cyprus (CY) → 19% VAT

    /// <summary>
    /// For any positive net amount with country "CY", the VAT rate SHALL be 19%,
    /// IsReverseCharge SHALL be false, VatAmount = netAmount × 0.19, GrossAmount = netAmount + VatAmount.
    /// **Validates: Requirements 9.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CyprusCustomer_AppliesStandardVatRate()
    {
        return Prop.ForAll(
            PositiveAmountGen.ToArbitrary(),
            Arb.From(Gen.Elements<string?>(null, "", "CY99999999X", "CY12345678Z")),
            (netAmount, vatNumber) =>
            {
                var result = Service.Calculate(netAmount, "CY", vatNumber);

                var expectedVatAmount = netAmount * 0.19m;
                var expectedGross = netAmount + expectedVatAmount;

                return (result.VatRate == 0.19m)
                    .Label($"VatRate should be 0.19 but was {result.VatRate}")
                    .And((result.VatAmount == expectedVatAmount)
                        .Label($"VatAmount should be {expectedVatAmount} but was {result.VatAmount}"))
                    .And((result.GrossAmount == expectedGross)
                        .Label($"GrossAmount should be {expectedGross} but was {result.GrossAmount}"))
                    .And((!result.IsReverseCharge)
                        .Label("IsReverseCharge should be false for CY"));
            });
    }

    #endregion

    #region Property 5b: EU non-CY with VAT number → 0% reverse charge

    /// <summary>
    /// For any positive net amount with an EU country (not CY) and a non-empty VAT number,
    /// the VAT rate SHALL be 0% with IsReverseCharge = true, VatAmount = 0, GrossAmount = netAmount.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EuNonCyprusWithVatNumber_AppliesReverseCharge()
    {
        return Prop.ForAll(
            PositiveAmountGen.ToArbitrary(),
            Arb.From(Gen.Elements(EuNonCyprus)),
            Arb.From(VatNumberGen),
            (netAmount, country, vatNumber) =>
            {
                var result = Service.Calculate(netAmount, country, vatNumber);

                return (result.VatRate == 0m)
                    .Label($"VatRate should be 0 but was {result.VatRate} for country={country}")
                    .And((result.IsReverseCharge)
                        .Label($"IsReverseCharge should be true for EU country {country} with VAT number"))
                    .And((result.VatAmount == 0m)
                        .Label($"VatAmount should be 0 but was {result.VatAmount}"))
                    .And((result.GrossAmount == netAmount)
                        .Label($"GrossAmount should equal netAmount ({netAmount}) but was {result.GrossAmount}"));
            });
    }

    #endregion

    #region Property 5c: EU non-CY without VAT number → 19%

    /// <summary>
    /// For any positive net amount with an EU country (not CY) and null/empty VAT number,
    /// the VAT rate SHALL be 19%, IsReverseCharge = false.
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EuNonCyprusWithoutVatNumber_AppliesStandardVatRate()
    {
        return Prop.ForAll(
            PositiveAmountGen.ToArbitrary(),
            Arb.From(Gen.Elements(EuNonCyprus)),
            Arb.From(EmptyVatNumberGen),
            (netAmount, country, vatNumber) =>
            {
                var result = Service.Calculate(netAmount, country, vatNumber);

                var expectedVatAmount = netAmount * 0.19m;
                var expectedGross = netAmount + expectedVatAmount;

                return (result.VatRate == 0.19m)
                    .Label($"VatRate should be 0.19 but was {result.VatRate} for EU country {country} without VAT number")
                    .And((!result.IsReverseCharge)
                        .Label($"IsReverseCharge should be false for EU country {country} without VAT number"))
                    .And((result.VatAmount == expectedVatAmount)
                        .Label($"VatAmount should be {expectedVatAmount} but was {result.VatAmount}"))
                    .And((result.GrossAmount == expectedGross)
                        .Label($"GrossAmount should be {expectedGross} but was {result.GrossAmount}"));
            });
    }

    #endregion

    #region Property 5d: Non-EU country → 0% no reverse charge

    /// <summary>
    /// For any positive net amount with a non-EU country code,
    /// the VAT rate SHALL be 0% with IsReverseCharge = false, VatAmount = 0, GrossAmount = netAmount.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonEuCountry_AppliesZeroVat()
    {
        return Prop.ForAll(
            PositiveAmountGen.ToArbitrary(),
            Arb.From(Gen.Elements(NonEuCountries)),
            Arb.From(Gen.Elements<string?>(null, "", "GB123456789", "US12-3456789", "CH123456")),
            (netAmount, country, vatNumber) =>
            {
                var result = Service.Calculate(netAmount, country, vatNumber);

                return (result.VatRate == 0m)
                    .Label($"VatRate should be 0 but was {result.VatRate} for non-EU country {country}")
                    .And((!result.IsReverseCharge)
                        .Label($"IsReverseCharge should be false for non-EU country {country}"))
                    .And((result.VatAmount == 0m)
                        .Label($"VatAmount should be 0 but was {result.VatAmount}"))
                    .And((result.GrossAmount == netAmount)
                        .Label($"GrossAmount should equal netAmount ({netAmount}) but was {result.GrossAmount}"));
            });
    }

    #endregion

    #region Property 5e: Amount arithmetic invariant (all scenarios)

    /// <summary>
    /// For any positive net amount, any country code, and any VAT number:
    /// VatAmount = netAmount × VatRate AND GrossAmount = netAmount + VatAmount.
    /// This property tests the arithmetic invariant across ALL scenarios.
    /// **Validates: Requirements 9.1, 9.2, 9.3, 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AmountArithmetic_AlwaysCorrect()
    {
        var countryGen = Gen.OneOf(
            Gen.Constant<string?>("CY"),
            Gen.Elements(EuNonCyprus).Select(c => (string?)c),
            Gen.Elements(NonEuCountries).Select(c => (string?)c),
            Gen.Constant<string?>(null)
        );

        var vatNumberGen = Gen.OneOf(
            VatNumberGen.Select(v => (string?)v),
            EmptyVatNumberGen
        );

        return Prop.ForAll(
            PositiveAmountGen.ToArbitrary(),
            Arb.From(countryGen),
            Arb.From(vatNumberGen),
            (netAmount, country, vatNumber) =>
            {
                var result = Service.Calculate(netAmount, country, vatNumber);

                var expectedVatAmount = netAmount * result.VatRate;
                var expectedGross = netAmount + result.VatAmount;

                return (result.VatAmount == expectedVatAmount)
                    .Label($"VatAmount ({result.VatAmount}) should equal netAmount ({netAmount}) × VatRate ({result.VatRate}) = {expectedVatAmount}")
                    .And((result.GrossAmount == expectedGross)
                        .Label($"GrossAmount ({result.GrossAmount}) should equal netAmount ({netAmount}) + VatAmount ({result.VatAmount}) = {expectedGross}"));
            });
    }

    #endregion
}
