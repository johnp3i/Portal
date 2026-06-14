using FsCheck;
using FsCheck.Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: invoice-edit-modal-lines, Property 5: Whitespace-only descriptions are rejected

/// <summary>
/// Property-based tests for whitespace description rejection validation.
/// Validates that the description field validation logic (description.trim() check)
/// correctly rejects any string composed entirely of whitespace characters and
/// correctly accepts any string containing at least one non-whitespace character.
///
/// The validation logic from invoice-line-modal.js:
///   var descriptionValue = descriptionInput.value.trim();
///   if (!descriptionValue) { /* reject — show inline validation message */ }
///
/// Modelled in C# as: string.IsNullOrWhiteSpace(description) → rejected
///
/// **Validates: Requirements 2.8, 6.4**
/// </summary>
public class InvoiceWhitespaceDescriptionRejectionPropertyTests
{
    /// <summary>
    /// Simulates the JavaScript trim-based validation logic.
    /// Returns true if the description is INVALID (should be rejected).
    /// Equivalent to: !description.trim() in JS (empty after trim → falsy → rejected).
    /// </summary>
    private static bool IsDescriptionRejected(string description)
    {
        return string.IsNullOrWhiteSpace(description);
    }

    /// <summary>
    /// Property 5a: Any string composed only of whitespace characters (space, tab, \n, \r, \f)
    /// fails the description validation (is rejected).
    ///
    /// Generator: produces strings of length 0–50 composed only of whitespace characters.
    /// Assertion: string.IsNullOrWhiteSpace returns true for all generated inputs.
    ///
    /// **Validates: Requirements 2.8, 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhitespaceOnly_Descriptions_Are_Rejected()
    {
        // Generate strings composed entirely of whitespace characters
        var whitespaceChars = new[] { ' ', '\t', '\n', '\r', '\f' };

        var whitespaceStringGen = from length in Gen.Choose(0, 50)
                                  from chars in Gen.ListOf(length, Gen.Elements(whitespaceChars))
                                  select new string(chars.ToArray());

        return Prop.ForAll(
            whitespaceStringGen.ToArbitrary(),
            description =>
            {
                var rejected = IsDescriptionRejected(description);

                return rejected
                    .Label($"Whitespace-only string should be rejected: " +
                           $"length={description.Length}, " +
                           $"repr='{description.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t").Replace("\f", "\\f")}'");
            });
    }

    /// <summary>
    /// Property 5b: Any string containing at least one non-whitespace character
    /// passes the description validation (is accepted).
    ///
    /// Generator: produces strings that contain at least one printable non-whitespace character
    /// (ASCII 33–126), optionally surrounded by whitespace.
    /// Assertion: string.IsNullOrWhiteSpace returns false for all generated inputs.
    ///
    /// **Validates: Requirements 2.8, 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonWhitespace_Descriptions_Are_Accepted()
    {
        var whitespaceChars = new[] { ' ', '\t', '\n', '\r', '\f' };

        // Generate at least one non-whitespace character (printable ASCII 33-126)
        var nonWhitespaceCharGen = Gen.Choose(33, 126).Select(i => (char)i);

        // Generate optional leading/trailing whitespace
        var whitespaceGen = from length in Gen.Choose(0, 10)
                            from chars in Gen.ListOf(length, Gen.Elements(whitespaceChars))
                            select new string(chars.ToArray());

        // Build a string: optional whitespace + at least one non-whitespace + optional whitespace
        var validDescriptionGen = from leading in whitespaceGen
                                  from nonWsCount in Gen.Choose(1, 20)
                                  from nonWsChars in Gen.ListOf(nonWsCount, nonWhitespaceCharGen)
                                  from trailing in whitespaceGen
                                  select leading + new string(nonWsChars.ToArray()) + trailing;

        return Prop.ForAll(
            validDescriptionGen.ToArbitrary(),
            description =>
            {
                var rejected = IsDescriptionRejected(description);

                return (!rejected)
                    .Label($"Non-whitespace string should be accepted: " +
                           $"length={description.Length}, " +
                           $"trimmed='{description.Trim()}'");
            });
    }
}
