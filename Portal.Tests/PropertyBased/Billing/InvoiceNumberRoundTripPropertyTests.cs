using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Options;
using Moq;
using Portal.Infrastructure.Repositories;
using Portal.Web.Configuration;
using Portal.Web.Services.Billing;

namespace Portal.Tests.PropertyBased.Billing;

// Feature: subscription-billing-invoices, Property 2: Format/Parse round-trip

/// <summary>
/// Property-based tests for InvoiceNumberGenerator Format/Parse round-trip.
/// Verifies that for any valid PlatformCode, year, and sequence number,
/// formatting the components and then parsing the result yields identical values.
/// **Validates: Requirements 8.4**
/// </summary>
public class InvoiceNumberRoundTripPropertyTests
{
    private readonly InvoiceNumberGenerator _generator;

    public InvoiceNumberRoundTripPropertyTests()
    {
        var mockRepo = new Mock<IInvoiceSequenceRepository>();
        var mockOptions = new Mock<IOptions<InvoiceSettings>>();
        mockOptions.Setup(o => o.Value).Returns(new InvoiceSettings
        {
            PlatformCode = "BILI",
            CompanyName = "Test",
            CompanyAddress = "Test",
            CompanyCountryCode = "CY",
            CompanyVatNumber = "TEST123",
            CompanyEmail = "test@test.com"
        });

        _generator = new InvoiceNumberGenerator(mockRepo.Object, mockOptions.Object);
    }

    #region Generators

    /// <summary>
    /// Generates valid PlatformCodes: 1-10 alphanumeric characters.
    /// </summary>
    private static Gen<string> PlatformCodeGen =>
        Gen.Choose(1, 10).SelectMany(length =>
            Gen.ArrayOf(length, Gen.Elements(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
                    .ToCharArray()))
            .Select(chars => new string(chars)));

    /// <summary>
    /// Generates valid years (2020-2099).
    /// </summary>
    private static Gen<int> YearGen => Gen.Choose(2020, 2099);

    /// <summary>
    /// Generates valid sequence numbers (1-99999).
    /// </summary>
    private static Gen<int> SequenceGen => Gen.Choose(1, 99999);

    #endregion

    #region Property 2: Format/Parse round-trip

    /// <summary>
    /// For any valid PlatformCode (1-10 alphanumeric), year (2020-2099), and sequence (1-99999),
    /// Parse(Format(code, year, seq)) SHALL produce an InvoiceNumberComponents record
    /// with identical PlatformCode, Year, and Sequence values.
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FormatThenParse_YieldsIdenticalComponents()
    {
        return Prop.ForAll(
            PlatformCodeGen.ToArbitrary(),
            YearGen.ToArbitrary(),
            SequenceGen.ToArbitrary(),
            (platformCode, year, sequence) =>
            {
                var formatted = _generator.Format(platformCode, year, sequence);
                var parsed = _generator.Parse(formatted);

                var notNull = (parsed != null)
                    .Label($"Parse returned null for formatted string '{formatted}'");

                if (parsed == null)
                    return notNull;

                return notNull
                    .And((parsed.PlatformCode == platformCode)
                        .Label($"PlatformCode mismatch: expected '{platformCode}', got '{parsed.PlatformCode}' (formatted: '{formatted}')"))
                    .And((parsed.Year == year)
                        .Label($"Year mismatch: expected {year}, got {parsed.Year} (formatted: '{formatted}')"))
                    .And((parsed.Sequence == sequence)
                        .Label($"Sequence mismatch: expected {sequence}, got {parsed.Sequence} (formatted: '{formatted}')"));
            });
    }

    #endregion
}
