using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Options;
using Moq;
using Portal.Infrastructure.Repositories;
using Portal.Web.Configuration;
using Portal.Web.Services.Billing;

namespace Portal.Tests.PropertyBased.Billing;

// Feature: subscription-billing-invoices, Property 3: Parse rejects malformed input

/// <summary>
/// Property-based tests for InvoiceNumberGenerator.Parse rejecting malformed input.
/// Generates random strings that do NOT conform to the invoice number pattern
/// ^([A-Za-z0-9]{1,10})-INV-(\d{4})-(\d{4,})$ and asserts Parse returns null.
/// **Validates: Requirements 8.3**
/// </summary>
public class InvoiceNumberParseRejectPropertyTests
{
    private static readonly InvoiceNumberGenerator Generator;

    static InvoiceNumberParseRejectPropertyTests()
    {
        var mockRepo = new Mock<IInvoiceSequenceRepository>();
        var settings = Options.Create(new InvoiceSettings { PlatformCode = "BILI" });
        Generator = new InvoiceNumberGenerator(mockRepo.Object, settings);
    }

    #region Generators

    /// <summary>
    /// Generates strings missing the -INV- separator entirely.
    /// Examples: "ABC-2024-0001", "BILI2024-0001", "random text"
    /// </summary>
    private static Gen<string> MissingInvSeparatorGen =>
        Gen.OneOf(
            // Platform code with wrong separator
            from code in Gen.Elements("BILI", "ABC", "Test1")
            from year in Gen.Choose(2020, 2099)
            from seq in Gen.Choose(1, 9999)
            select $"{code}-{year}-{seq:D4}",
            // No separators at all
            Arb.Default.NonEmptyString().Generator.Select(s => s.Get.Replace("-INV-", "")),
            // Wrong keyword instead of INV
            from code in Gen.Elements("BILI", "XYZ", "A1B2")
            from keyword in Gen.Elements("INVOICE", "inv", "Inv", "REC", "NUM", "XXX")
            from year in Gen.Choose(2020, 2099)
            from seq in Gen.Choose(1, 9999)
            select $"{code}-{keyword}-{year}-{seq:D4}"
        );

    /// <summary>
    /// Generates strings with a non-numeric year component (not exactly 4 digits).
    /// Examples: "BILI-INV-20X4-0001", "BILI-INV-99-0001", "BILI-INV-ABCD-0001"
    /// </summary>
    private static Gen<string> NonNumericYearGen =>
        Gen.OneOf(
            // Year with letters mixed in
            from code in Gen.Elements("BILI", "TEST", "A1")
            from badYear in Gen.Elements("20X4", "ABCD", "2O24", "20.4", "year")
            from seq in Gen.Choose(1, 9999)
            select $"{code}-INV-{badYear}-{seq:D4}",
            // Year too short (less than 4 digits)
            from code in Gen.Elements("BILI", "XY")
            from shortYear in Gen.Choose(10, 999)
            from seq in Gen.Choose(1, 9999)
            select $"{code}-INV-{shortYear}-{seq:D4}",
            // Year too long (more than 4 digits)
            from code in Gen.Elements("BILI", "AB")
            from longYear in Gen.Choose(10000, 99999)
            from seq in Gen.Choose(1, 9999)
            select $"{code}-INV-{longYear}-{seq:D4}"
        );

    /// <summary>
    /// Generates strings with a sequence that is too short (fewer than 4 digits).
    /// Examples: "BILI-INV-2024-1", "BILI-INV-2024-12", "BILI-INV-2024-123"
    /// </summary>
    private static Gen<string> TooShortSequenceGen =>
        from code in Gen.Elements("BILI", "TEST", "XYZ")
        from year in Gen.Choose(2020, 2099)
        from seq in Gen.Choose(1, 999) // 1 to 3 digits when formatted without padding
        select $"{code}-INV-{year}-{seq}";

    /// <summary>
    /// Generates strings with special characters in the platform code position.
    /// Examples: "BI!I-INV-2024-0001", "B@LI-INV-2024-0001", "BI LI-INV-2024-0001"
    /// </summary>
    private static Gen<string> SpecialCharsPlatformCodeGen =>
        from specialCode in Gen.Elements(
            "BI!I", "B@LI", "BI LI", "BI#I", "BI$I", "BI%I",
            "BILI.", "BI&LI", "BI*LI", "BI(LI", "BI)LI",
            "BI+LI", "BI=LI", "BI/LI", "BI\\LI", "BI:LI")
        from year in Gen.Choose(2020, 2099)
        from seq in Gen.Choose(1, 9999)
        select $"{specialCode}-INV-{year}-{seq:D4}";

    /// <summary>
    /// Generates strings with platform code exceeding the 10 character maximum.
    /// Examples: "ABCDEFGHIJK-INV-2024-0001" (11 chars)
    /// </summary>
    private static Gen<string> TooLongPlatformCodeGen =>
        from length in Gen.Choose(11, 20)
        from chars in Gen.ArrayOf(length, Gen.Elements(
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
            'K', 'L', 'M', 'N', '1', '2', '3', '4', '5', '6'))
        from year in Gen.Choose(2020, 2099)
        from seq in Gen.Choose(1, 9999)
        select $"{new string(chars)}-INV-{year}-{seq:D4}";

    /// <summary>
    /// Generates empty, whitespace, or null-like strings.
    /// </summary>
    private static Gen<string> EmptyOrWhitespaceGen =>
        Gen.Elements("", " ", "  ", "\t", "\n", "   \t\n  ");

    /// <summary>
    /// Combined generator for all malformed invoice number patterns.
    /// </summary>
    private static Arbitrary<string> MalformedInvoiceNumberArb =>
        Gen.OneOf(
            MissingInvSeparatorGen,
            NonNumericYearGen,
            TooShortSequenceGen,
            SpecialCharsPlatformCodeGen,
            TooLongPlatformCodeGen,
            EmptyOrWhitespaceGen
        ).ToArbitrary();

    #endregion

    /// <summary>
    /// For any string that does not conform to the invoice number pattern
    /// {AlphaNumeric(1-10)}-INV-{4digits}-{4+digits}, the Parse method SHALL return null.
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Parse_RejectsMalformedInput()
    {
        return Prop.ForAll(
            MalformedInvoiceNumberArb,
            malformedInput =>
            {
                var result = Generator.Parse(malformedInput);

                return (result == null)
                    .Label($"Parse should return null for malformed input '{malformedInput}' but returned: {result}");
            });
    }
}
