using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using Portal.Web.Controllers;
using Xunit;

namespace Portal.Tests.Unit.Properties;

// Feature: purchase-classification-enhancements, Property 5: CSV PurchaseType Resolver

/// <summary>
/// Property-based tests for the CSV PurchaseType resolver function.
/// Tests Property 5 from the purchase-classification-enhancements design document.
///
/// Property 5: CSV PurchaseType Resolver
/// - "Asset" (any case) → 1
/// - "Stock" (any case) → 2
/// - "Expense" (any case) → 3
/// - empty/null → 3 (default to Expense)
/// - unrecognised → null (invalid)
/// **Validates: Requirements 6.6**
/// </summary>
public class CsvPurchaseTypeResolverPropertyTests
{
    // Feature: purchase-classification-enhancements, Property 5: CSV PurchaseType Resolver
    // **Validates: Requirements 6.6**

    #region Reflection Helper

    /// <summary>
    /// Invokes the private static ResolvePurchaseTypeId method on PurchaseController via reflection.
    /// </summary>
    private static int? InvokeResolvePurchaseTypeId(string? purchaseTypeName)
    {
        var method = typeof(PurchaseController).GetMethod(
            "ResolvePurchaseTypeId",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
            throw new InvalidOperationException("ResolvePurchaseTypeId method not found on PurchaseController.");

        return (int?)method.Invoke(null, new object?[] { purchaseTypeName });
    }

    #endregion

    #region Generators

    /// <summary>
    /// Generates case variations of a known string (e.g., "Asset" → "ASSET", "asset", "AsSeT").
    /// </summary>
    private static Gen<string> CaseVariationGen(string baseValue)
    {
        return Gen.ArrayOf(baseValue.Length,
            Gen.Elements(true, false))
            .Select(upperFlags =>
            {
                var chars = new char[baseValue.Length];
                for (int i = 0; i < baseValue.Length; i++)
                {
                    chars[i] = upperFlags[i]
                        ? char.ToUpperInvariant(baseValue[i])
                        : char.ToLowerInvariant(baseValue[i]);
                }
                return new string(chars);
            });
    }

    /// <summary>
    /// Generates strings that are NOT recognised purchase type names.
    /// Excludes "asset", "stock", "expense" (case-insensitive) and empty/whitespace strings.
    /// </summary>
    private static Gen<string> UnrecognisedStringGen()
    {
        return Gen.Choose(3, 20).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements(
                'A', 'B', 'C', 'D', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                'N', 'O', 'P', 'Q', 'R', 'U', 'V', 'W', 'X', 'Y', 'Z',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', '_'))
            .Select(chars => new string(chars)))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Where(s =>
            {
                var lower = s.Trim().ToLowerInvariant();
                return lower != "asset" && lower != "stock" && lower != "expense";
            });
    }

    #endregion

    #region Property 5: "Asset" (any case) → 1

    // Feature: purchase-classification-enhancements, Property 5: CSV PurchaseType Resolver
    // **Validates: Requirements 6.6**
    [Property(MaxTest = 100)]
    public Property AssetCaseVariation_ResolvesTo1()
    {
        return Prop.ForAll(
            CaseVariationGen("asset").ToArbitrary(),
            (input) =>
            {
                var result = InvokeResolvePurchaseTypeId(input);
                return (result == 1).ToProperty()
                    .Label($"Input='{input}': Expected 1, Got {result}");
            });
    }

    #endregion

    #region Property 5: "Stock" (any case) → 2

    // Feature: purchase-classification-enhancements, Property 5: CSV PurchaseType Resolver
    // **Validates: Requirements 6.6**
    [Property(MaxTest = 100)]
    public Property StockCaseVariation_ResolvesTo2()
    {
        return Prop.ForAll(
            CaseVariationGen("stock").ToArbitrary(),
            (input) =>
            {
                var result = InvokeResolvePurchaseTypeId(input);
                return (result == 2).ToProperty()
                    .Label($"Input='{input}': Expected 2, Got {result}");
            });
    }

    #endregion

    #region Property 5: "Expense" (any case) → 3

    // Feature: purchase-classification-enhancements, Property 5: CSV PurchaseType Resolver
    // **Validates: Requirements 6.6**
    [Property(MaxTest = 100)]
    public Property ExpenseCaseVariation_ResolvesTo3()
    {
        return Prop.ForAll(
            CaseVariationGen("expense").ToArbitrary(),
            (input) =>
            {
                var result = InvokeResolvePurchaseTypeId(input);
                return (result == 3).ToProperty()
                    .Label($"Input='{input}': Expected 3, Got {result}");
            });
    }

    #endregion

    #region Property 5: empty/null → 3 (default to Expense)

    // Feature: purchase-classification-enhancements, Property 5: CSV PurchaseType Resolver
    // **Validates: Requirements 6.6**
    [Property(MaxTest = 100)]
    public Property NullOrEmpty_DefaultsTo3()
    {
        var emptyInputGen = Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Constant<string?>(""),
            Gen.Constant<string?>(" "),
            Gen.Constant<string?>("  "),
            Gen.Constant<string?>("\t"),
            Gen.Constant<string?>("\n"),
            Gen.Constant<string?>(" \t "));

        return Prop.ForAll(
            emptyInputGen.ToArbitrary(),
            (input) =>
            {
                var result = InvokeResolvePurchaseTypeId(input);
                return (result == 3).ToProperty()
                    .Label($"Input='{input ?? "null"}': Expected 3, Got {result}");
            });
    }

    #endregion

    #region Property 5: unrecognised → null

    // Feature: purchase-classification-enhancements, Property 5: CSV PurchaseType Resolver
    // **Validates: Requirements 6.6**
    [Property(MaxTest = 100)]
    public Property UnrecognisedString_ReturnsNull()
    {
        return Prop.ForAll(
            UnrecognisedStringGen().ToArbitrary(),
            (input) =>
            {
                var result = InvokeResolvePurchaseTypeId(input);
                return (result == null).ToProperty()
                    .Label($"Input='{input}': Expected null, Got {result}");
            });
    }

    #endregion
}
