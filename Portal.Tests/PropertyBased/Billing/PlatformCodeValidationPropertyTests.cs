using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Options;
using Moq;
using Portal.Infrastructure.Repositories;
using Portal.Web.Configuration;
using Portal.Web.Services.Billing;
using Xunit;

namespace Portal.Tests.PropertyBased.Billing;

// Feature: subscription-billing-invoices, Property 4: PlatformCode validation rejects invalid codes

/// <summary>
/// Property-based tests for PlatformCode validation in InvoiceNumberGenerator.
/// For any string that is null, empty, or contains at least one non-alphanumeric character,
/// GenerateNextAsync SHALL throw InvalidOperationException.
/// **Validates: Requirements 1.6**
/// </summary>
public class PlatformCodeValidationPropertyTests
{
    private readonly Mock<IInvoiceSequenceRepository> _mockRepository = new();

    private InvoiceNumberGenerator CreateGenerator(string? platformCode)
    {
        var settings = new InvoiceSettings
        {
            PlatformCode = platformCode!,
            CompanyName = "Test",
            CompanyAddress = "Test Address",
            CompanyCountryCode = "CY",
            CompanyVatNumber = "CY12345678X",
            CompanyEmail = "test@test.com"
        };

        var options = Options.Create(settings);
        return new InvoiceNumberGenerator(_mockRepository.Object, options);
    }

    #region Generators

    /// <summary>
    /// Generates strings containing at least one non-alphanumeric character.
    /// Includes special characters, whitespace, punctuation, and unicode.
    /// </summary>
    private static Gen<string> InvalidPlatformCodeGen =>
        Gen.OneOf(
            // Strings with special characters mixed in
            Gen.Elements(
                "BILI!", "AB@C", "A B", "TEST#1", "INV-01", "A.B", "PLAT/CODE",
                "CODE\t", "\nABC", "A&B", "TEST+1", "X=Y", "A,B", "CODE;1",
                " ", "  ", "\t", "AB CD", "BILI_01", "A(B)", "[CODE]", "{X}",
                "A\\B", "A|B", "A<B", "A>B", "A?B", "A*B", "A^B", "A~B"),
            // Strings longer than 10 characters (exceed max length)
            Gen.Elements(
                "ABCDEFGHIJK", "TOOLONGCODE1", "EXCESSIVECOD"),
            // Strings with unicode/non-ASCII
            Gen.Elements(
                "BILÍcode", "TËST", "CÖD", "日本語", "ΚΩΔΙΚ", "БІLI")
        );

    /// <summary>
    /// Generates null or empty strings.
    /// </summary>
    private static Gen<string?> NullOrEmptyGen =>
        Gen.Elements<string?>(null, "");

    #endregion

    /// <summary>
    /// For any null or empty PlatformCode, GenerateNextAsync SHALL throw InvalidOperationException.
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullOrEmptyPlatformCode_ThrowsInvalidOperationException()
    {
        return Prop.ForAll(
            Arb.From(NullOrEmptyGen),
            platformCode =>
            {
                var generator = CreateGenerator(platformCode);

                var exception = Assert.ThrowsAsync<InvalidOperationException>(
                    () => generator.GenerateNextAsync(DateTime.UtcNow)).Result;

                return (exception != null)
                    .Label($"Expected InvalidOperationException for PlatformCode='{platformCode ?? "(null)"}'");
            });
    }

    /// <summary>
    /// For any string containing at least one non-alphanumeric character or exceeding 10 chars,
    /// GenerateNextAsync SHALL throw InvalidOperationException.
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidCharactersPlatformCode_ThrowsInvalidOperationException()
    {
        return Prop.ForAll(
            Arb.From(InvalidPlatformCodeGen),
            platformCode =>
            {
                var generator = CreateGenerator(platformCode);

                var exception = Assert.ThrowsAsync<InvalidOperationException>(
                    () => generator.GenerateNextAsync(DateTime.UtcNow)).Result;

                return (exception != null)
                    .Label($"Expected InvalidOperationException for PlatformCode='{platformCode}'");
            });
    }

    /// <summary>
    /// For any invalid PlatformCode, the repository SHALL NOT be called
    /// (validation rejects before reaching the database).
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvalidPlatformCode_DoesNotCallRepository()
    {
        var allInvalidGen = Gen.OneOf(
            NullOrEmptyGen,
            InvalidPlatformCodeGen.Select(s => (string?)s)
        );

        return Prop.ForAll(
            Arb.From(allInvalidGen),
            platformCode =>
            {
                var mockRepo = new Mock<IInvoiceSequenceRepository>();
                var settings = new InvoiceSettings
                {
                    PlatformCode = platformCode!,
                    CompanyName = "Test",
                    CompanyAddress = "Test Address",
                    CompanyCountryCode = "CY",
                    CompanyVatNumber = "CY12345678X",
                    CompanyEmail = "test@test.com"
                };
                var options = Options.Create(settings);
                var generator = new InvoiceNumberGenerator(mockRepo.Object, options);

                try
                {
                    generator.GenerateNextAsync(DateTime.UtcNow).GetAwaiter().GetResult();
                }
                catch (InvalidOperationException)
                {
                    // Expected
                }

                mockRepo.Verify(
                    r => r.IncrementAndGetAsync(It.IsAny<int>()),
                    Times.Never);

                return true.ToProperty();
            });
    }
}
