using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using Portal.Web.Controllers;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: purchase-classification-enhancements, Property 4: CSV Origin Type Resolver

/// <summary>
/// Property-based tests for the ResolvePurchaseOriginTypeId function in PurchaseController.
/// Any case variation of "EuPaid"/"eu paid"/"eupaid" → 4; existing mappings preserved; unknown strings → null.
/// **Validates: Requirements 1.9, 6.5**
/// </summary>
public class CsvOriginTypeResolverPropertyTests
{
    #region Reflection Helper

    /// <summary>
    /// Invokes the private static ResolvePurchaseOriginTypeId method on PurchaseController via reflection.
    /// </summary>
    private static int? InvokeResolvePurchaseOriginTypeId(string? input)
    {
        var method = typeof(PurchaseController).GetMethod(
            "ResolvePurchaseOriginTypeId",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
            throw new InvalidOperationException("ResolvePurchaseOriginTypeId method not found on PurchaseController.");

        return (int?)method.Invoke(null, new object?[] { input });
    }

    #endregion

    #region Generators

    /// <summary>
    /// Generates case variations of a given string by randomly toggling each character's case.
    /// </summary>
    private static Gen<string> CaseVariationGen(string baseString)
    {
        return Gen.ArrayOf(baseString.Length, Gen.Elements(true, false))
            .Select(flags =>
            {
                var chars = new char[baseString.Length];
                for (int i = 0; i < baseString.Length; i++)
                {
                    chars[i] = flags[i] ? char.ToUpper(baseString[i]) : char.ToLower(baseString[i]);
                }
                return new string(chars);
            });
    }

    /// <summary>
    /// Generates case variations of EU Paid strings: "eupaid" and "eu paid".
    /// </summary>
    private static Gen<string> EuPaidVariationGen()
    {
        return Gen.OneOf(
            CaseVariationGen("eupaid"),
            CaseVariationGen("eu paid"));
    }

    /// <summary>
    /// Generates case variations of Domestic string: "domestic".
    /// </summary>
    private static Gen<string> DomesticVariationGen()
    {
        return CaseVariationGen("domestic");
    }

    /// <summary>
    /// Generates case variations of EU Reverse Charge strings.
    /// </summary>
    private static Gen<string> EuReverseChargeVariationGen()
    {
        return Gen.OneOf(
            CaseVariationGen("eureversecharge"),
            CaseVariationGen("eu reverse charge"),
            CaseVariationGen("eurc"),
            CaseVariationGen("eu rc"));
    }

    /// <summary>
    /// Generates case variations of Non-EU strings.
    /// </summary>
    private static Gen<string> NonEuVariationGen()
    {
        return Gen.OneOf(
            CaseVariationGen("noneu"),
            CaseVariationGen("non-eu"),
            CaseVariationGen("non eu"));
    }

    /// <summary>
    /// Generates random strings that do NOT match any known origin type mapping.
    /// Uses characters that won't accidentally form valid mappings.
    /// </summary>
    private static Gen<string> UnknownStringGen()
    {
        return Gen.Choose(3, 20).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements(
                'x', 'y', 'z', 'q', 'w', '0', '1', '2', '3', '4', '5',
                '6', '7', '8', '9', '#', '@', '!', '%', '^'))
            .Select(chars => new string(chars)))
            .Where(s => !string.IsNullOrWhiteSpace(s));
    }

    #endregion

    #region Property 4a: EU Paid case variations resolve to 4

    /// <summary>
    /// Property 4a: Any case variation of "EuPaid" or "eu paid" resolves to PurchaseOriginTypeId 4.
    /// **Validates: Requirements 1.9, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EuPaidCaseVariation_ResolvesTo4()
    {
        return Prop.ForAll(
            EuPaidVariationGen().ToArbitrary(),
            (input) =>
            {
                var result = InvokeResolvePurchaseOriginTypeId(input);
                return (result == 4).ToProperty()
                    .Label($"Input='{input}': Expected=4, Got={result}");
            });
    }

    #endregion

    #region Property 4b: Domestic case variations resolve to 1

    /// <summary>
    /// Property 4b: Any case variation of "Domestic" resolves to PurchaseOriginTypeId 1.
    /// **Validates: Requirements 1.9, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DomesticCaseVariation_ResolvesTo1()
    {
        return Prop.ForAll(
            DomesticVariationGen().ToArbitrary(),
            (input) =>
            {
                var result = InvokeResolvePurchaseOriginTypeId(input);
                return (result == 1).ToProperty()
                    .Label($"Input='{input}': Expected=1, Got={result}");
            });
    }

    #endregion

    #region Property 4c: EU Reverse Charge case variations resolve to 2

    /// <summary>
    /// Property 4c: Any case variation of "EuReverseCharge"/"eu reverse charge"/"eurc"/"eu rc" resolves to PurchaseOriginTypeId 2.
    /// **Validates: Requirements 1.9, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EuReverseChargeCaseVariation_ResolvesTo2()
    {
        return Prop.ForAll(
            EuReverseChargeVariationGen().ToArbitrary(),
            (input) =>
            {
                var result = InvokeResolvePurchaseOriginTypeId(input);
                return (result == 2).ToProperty()
                    .Label($"Input='{input}': Expected=2, Got={result}");
            });
    }

    #endregion

    #region Property 4d: Non-EU case variations resolve to 3

    /// <summary>
    /// Property 4d: Any case variation of "NonEu"/"non-eu"/"non eu" resolves to PurchaseOriginTypeId 3.
    /// **Validates: Requirements 1.9, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonEuCaseVariation_ResolvesTo3()
    {
        return Prop.ForAll(
            NonEuVariationGen().ToArbitrary(),
            (input) =>
            {
                var result = InvokeResolvePurchaseOriginTypeId(input);
                return (result == 3).ToProperty()
                    .Label($"Input='{input}': Expected=3, Got={result}");
            });
    }

    #endregion

    #region Property 4e: Unknown strings resolve to null

    /// <summary>
    /// Property 4e: Any string that does not match a known origin type name resolves to null.
    /// **Validates: Requirements 1.9, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnknownString_ResolvesToNull()
    {
        return Prop.ForAll(
            UnknownStringGen().ToArbitrary(),
            (input) =>
            {
                var result = InvokeResolvePurchaseOriginTypeId(input);
                return (result == null).ToProperty()
                    .Label($"Input='{input}': Expected=null, Got={result}");
            });
    }

    #endregion
}
