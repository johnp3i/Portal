using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Options;
using Moq;
using Portal.Infrastructure.Repositories;
using Portal.Web.Configuration;
using Portal.Web.Services.Billing;

namespace Portal.Tests.PropertyBased.Billing;

// Feature: subscription-billing-invoices, Property 1: Invoice number format validity

/// <summary>
/// Property-based tests for InvoiceNumberGenerator.Format method.
/// Verifies that for any valid PlatformCode (1-10 alphanumeric chars), year (2020-2099),
/// and sequence (1-99999), the Format method produces a string matching the expected pattern
/// and that the components correspond to the input values.
/// **Validates: Requirements 1.1, 8.1**
/// </summary>
public class InvoiceNumberFormatPropertyTests
{
    private static readonly Regex InvoiceNumberPattern =
        new(@"^[A-Za-z0-9]{1,10}-INV-\d{4}-\d{4,}$", RegexOptions.Compiled);

    private static readonly InvoiceNumberGenerator Generator = CreateGenerator();

    private static InvoiceNumberGenerator CreateGenerator()
    {
        var mockRepo = new Mock<IInvoiceSequenceRepository>();
        var settings = Options.Create(new InvoiceSettings { PlatformCode = "TEST" });
        return new InvoiceNumberGenerator(mockRepo.Object, settings);
    }

    #region Generators

    /// <summary>
    /// Generates random alphanumeric PlatformCodes between 1 and 10 characters.
    /// </summary>
    private static Gen<string> PlatformCodeGen =>
        Gen.Choose(1, 10).SelectMany(length =>
            Gen.ArrayOf(length, Gen.Elements(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
                    .ToCharArray()))
            .Select(chars => new string(chars)));

    /// <summary>
    /// Generates random years between 2020 and 2099.
    /// </summary>
    private static Gen<int> YearGen => Gen.Choose(2020, 2099);

    /// <summary>
    /// Generates random sequence numbers between 1 and 99999.
    /// </summary>
    private static Gen<int> SequenceGen => Gen.Choose(1, 99999);

    #endregion

    /// <summary>
    /// For any valid PlatformCode, year, and sequence number, the Format method SHALL produce
    /// a string matching the pattern ^[A-Za-z0-9]{1,10}-INV-\d{4}-\d{4,}$.
    /// **Validates: Requirements 1.1, 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Format_ProducesValidPatternForAnyValidInput()
    {
        return Prop.ForAll(
            PlatformCodeGen.ToArbitrary(),
            YearGen.ToArbitrary(),
            SequenceGen.ToArbitrary(),
            (platformCode, year, sequence) =>
            {
                var result = Generator.Format(platformCode, year, sequence);

                return InvoiceNumberPattern.IsMatch(result)
                    .Label($"Result '{result}' does not match pattern ^[A-Za-z0-9]{{1,10}}-INV-\\d{{4}}-\\d{{4,}}$");
            });
    }

    /// <summary>
    /// For any valid PlatformCode, year, and sequence number, the formatted result SHALL contain
    /// the exact PlatformCode as the first component (before -INV-).
    /// **Validates: Requirements 1.1, 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Format_PlatformCodeComponentMatchesInput()
    {
        return Prop.ForAll(
            PlatformCodeGen.ToArbitrary(),
            YearGen.ToArbitrary(),
            SequenceGen.ToArbitrary(),
            (platformCode, year, sequence) =>
            {
                var result = Generator.Format(platformCode, year, sequence);
                var parts = result.Split("-INV-");

                return (parts[0] == platformCode)
                    .Label($"PlatformCode component '{parts[0]}' does not match input '{platformCode}'");
            });
    }

    /// <summary>
    /// For any valid PlatformCode, year, and sequence number, the formatted result SHALL contain
    /// the exact 4-digit year as the year component.
    /// **Validates: Requirements 1.1, 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Format_YearComponentMatchesInput()
    {
        return Prop.ForAll(
            PlatformCodeGen.ToArbitrary(),
            YearGen.ToArbitrary(),
            SequenceGen.ToArbitrary(),
            (platformCode, year, sequence) =>
            {
                var result = Generator.Format(platformCode, year, sequence);
                var afterInv = result.Split("-INV-")[1];
                var yearStr = afterInv.Split('-')[0];
                var parsedYear = int.Parse(yearStr);

                return (parsedYear == year)
                    .Label($"Year component '{yearStr}' (parsed: {parsedYear}) does not match input year {year}");
            });
    }

    /// <summary>
    /// For any valid PlatformCode, year, and sequence number, the formatted result SHALL contain
    /// the correct sequence value as the last component (zero-padded to at least 4 digits).
    /// **Validates: Requirements 1.1, 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Format_SequenceComponentMatchesInput()
    {
        return Prop.ForAll(
            PlatformCodeGen.ToArbitrary(),
            YearGen.ToArbitrary(),
            SequenceGen.ToArbitrary(),
            (platformCode, year, sequence) =>
            {
                var result = Generator.Format(platformCode, year, sequence);
                var afterInv = result.Split("-INV-")[1];
                var sequenceStr = afterInv.Split('-')[1];
                var parsedSequence = int.Parse(sequenceStr);

                return (parsedSequence == sequence)
                    .Label($"Sequence component '{sequenceStr}' (parsed: {parsedSequence}) does not match input sequence {sequence}")
                    .And((sequenceStr.Length >= 4)
                        .Label($"Sequence component '{sequenceStr}' should be at least 4 digits but has {sequenceStr.Length} digits"));
            });
    }
}
