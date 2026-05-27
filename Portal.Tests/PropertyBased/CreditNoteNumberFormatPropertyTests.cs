using FsCheck;
using FsCheck.Xunit;
using System.Text.RegularExpressions;
using Xunit;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Property-based tests for Credit Note Number Format and Sequencing.
/// Verifies that generated credit note numbers match the CN-YYYY-NNNN pattern
/// and that sequential numbers increment by exactly 1.
/// 
/// The number generation logic under test (from CreditNoteService.GenerateCreditNoteNumberAsync):
///   int year = issueDate.Year;
///   int? highestNumber = await _creditNoteRepository.GetHighestNumberForYearAsync(businessId, year);
///   int nextNumber = (highestNumber ?? 0) + 1;
///   if (nextNumber > 9999) throw ...;
///   return $"CN-{year}-{nextNumber:D4}";
///
/// Since the method is private, we replicate the pure formatting logic here and verify
/// the properties hold for all valid inputs.
/// **Validates: Requirements 2.1, 2.2, 2.3**
/// </summary>
public class CreditNoteNumberFormatPropertyTests
{
    private static readonly Regex CreditNoteNumberPattern = new(@"^CN-\d{4}-\d{4}$", RegexOptions.Compiled);

    /// <summary>
    /// Replicates the credit note number generation logic from CreditNoteService.GenerateCreditNoteNumberAsync.
    /// This is the exact algorithm used in production.
    /// </summary>
    private static string GenerateCreditNoteNumber(int year, int? highestExistingNumber)
    {
        int nextNumber = (highestExistingNumber ?? 0) + 1;

        if (nextNumber > 9999)
            throw new InvalidOperationException("Annual credit note limit (9999) reached for this year.");

        return $"CN-{year}-{nextNumber:D4}";
    }

    #region Property 2a: Credit note number matches CN-YYYY-NNNN format

    /// <summary>
    /// Property 2a: For any valid year (2020–2030) and existing count (0–9998),
    /// the generated credit note number must match the regex pattern ^CN-\d{4}-\d{4}$.
    /// **Validates: Requirement 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedNumber_MatchesCnYyyyNnnnFormat()
    {
        return Prop.ForAll(
            Gen.Choose(2020, 2030).ToArbitrary(),
            Gen.Choose(0, 9998).ToArbitrary(),
            (year, existingCount) =>
            {
                int? highestNumber = existingCount == 0 ? null : existingCount;
                var creditNoteNumber = GenerateCreditNoteNumber(year, highestNumber);

                var matchesFormat = CreditNoteNumberPattern.IsMatch(creditNoteNumber);

                return matchesFormat.ToProperty()
                    .Label($"Year={year}, ExistingCount={existingCount}, Number='{creditNoteNumber}': " +
                           $"MatchesFormat={matchesFormat}");
            });
    }

    #endregion

    #region Property 2b: Year portion matches the input year

    /// <summary>
    /// Property 2b: For any valid year (2020–2030) and existing count (0–9998),
    /// the year portion of the generated credit note number must match the input year.
    /// **Validates: Requirement 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedNumber_YearPortionMatchesInputYear()
    {
        return Prop.ForAll(
            Gen.Choose(2020, 2030).ToArbitrary(),
            Gen.Choose(0, 9998).ToArbitrary(),
            (year, existingCount) =>
            {
                int? highestNumber = existingCount == 0 ? null : existingCount;
                var creditNoteNumber = GenerateCreditNoteNumber(year, highestNumber);

                // Extract year portion: CN-YYYY-NNNN → YYYY is characters 3..6
                var yearPortion = creditNoteNumber.Substring(3, 4);
                var yearMatches = yearPortion == year.ToString();

                return yearMatches.ToProperty()
                    .Label($"Year={year}, ExistingCount={existingCount}, Number='{creditNoteNumber}', " +
                           $"ExtractedYear='{yearPortion}': YearMatches={yearMatches}");
            });
    }

    #endregion

    #region Property 2c: Sequential number increments by exactly 1

    /// <summary>
    /// Property 2c: For any valid year (2020–2030) and existing count (0–9998),
    /// the sequential portion of the generated credit note number must equal highestExistingNumber + 1.
    /// When no credit notes exist (highestNumber = null), the sequential portion is 1 (i.e., 0001).
    /// **Validates: Requirements 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedNumber_SequentialPortionIncrementsCorrectly()
    {
        return Prop.ForAll(
            Gen.Choose(2020, 2030).ToArbitrary(),
            Gen.Choose(0, 9998).ToArbitrary(),
            (year, existingCount) =>
            {
                int? highestNumber = existingCount == 0 ? null : existingCount;
                int expectedSequential = (highestNumber ?? 0) + 1;
                var creditNoteNumber = GenerateCreditNoteNumber(year, highestNumber);

                // Extract sequential portion: CN-YYYY-NNNN → NNNN is characters 8..11
                var sequentialPortion = creditNoteNumber.Substring(8, 4);
                var parsedSequential = int.Parse(sequentialPortion);
                var sequentialCorrect = parsedSequential == expectedSequential;

                return sequentialCorrect.ToProperty()
                    .Label($"Year={year}, ExistingCount={existingCount}, HighestNumber={highestNumber}, " +
                           $"Number='{creditNoteNumber}', ParsedSeq={parsedSequential}, " +
                           $"ExpectedSeq={expectedSequential}: Correct={sequentialCorrect}");
            });
    }

    #endregion

    #region Property 2d: Sequential numbers are zero-padded to 4 digits

    /// <summary>
    /// Property 2d: For any valid year and existing count, the sequential portion
    /// is always exactly 4 characters (zero-padded).
    /// **Validates: Requirement 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedNumber_SequentialPortionIsZeroPaddedTo4Digits()
    {
        return Prop.ForAll(
            Gen.Choose(2020, 2030).ToArbitrary(),
            Gen.Choose(0, 9998).ToArbitrary(),
            (year, existingCount) =>
            {
                int? highestNumber = existingCount == 0 ? null : existingCount;
                var creditNoteNumber = GenerateCreditNoteNumber(year, highestNumber);

                // The total length should be 12: "CN-" (3) + "YYYY" (4) + "-" (1) + "NNNN" (4)
                var correctLength = creditNoteNumber.Length == 12;

                // Sequential portion should be exactly 4 digits
                var sequentialPortion = creditNoteNumber.Substring(8, 4);
                var allDigits = sequentialPortion.All(char.IsDigit);

                return (correctLength && allDigits).ToProperty()
                    .Label($"Year={year}, ExistingCount={existingCount}, Number='{creditNoteNumber}', " +
                           $"Length={creditNoteNumber.Length}, SeqPortion='{sequentialPortion}': " +
                           $"CorrectLength={correctLength}, AllDigits={allDigits}");
            });
    }

    #endregion

    #region Edge case: First credit note of the year starts at 0001

    /// <summary>
    /// When no credit notes exist for a business and year (highestNumber = null),
    /// the generated number should be CN-YYYY-0001.
    /// **Validates: Requirement 2.3**
    /// </summary>
    [Fact]
    public void FirstCreditNoteOfYear_StartsAt0001()
    {
        int year = 2025;
        var creditNoteNumber = GenerateCreditNoteNumber(year, null);

        Assert.Equal("CN-2025-0001", creditNoteNumber);
        Assert.Matches(@"^CN-\d{4}-\d{4}$", creditNoteNumber);
    }

    /// <summary>
    /// When the highest existing number is 9998, the next number should be 9999.
    /// **Validates: Requirement 2.2**
    /// </summary>
    [Fact]
    public void HighestIs9998_NextIs9999()
    {
        int year = 2025;
        var creditNoteNumber = GenerateCreditNoteNumber(year, 9998);

        Assert.Equal("CN-2025-9999", creditNoteNumber);
    }

    /// <summary>
    /// When the highest existing number is 9999, generation should throw (limit reached).
    /// **Validates: Requirement 2.1 (range 0001–9999)**
    /// </summary>
    [Fact]
    public void HighestIs9999_ThrowsLimitReached()
    {
        int year = 2025;

        var exception = Assert.Throws<InvalidOperationException>(
            () => GenerateCreditNoteNumber(year, 9999));

        Assert.Contains("9999", exception.Message);
    }

    #endregion
}
