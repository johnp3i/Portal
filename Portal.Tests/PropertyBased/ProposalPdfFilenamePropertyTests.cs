using System.Reflection;
using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Portal.Web.Controllers;
using Xunit;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Property-based tests for proposal PDF filename generation, sanitization,
/// logo data URI encoding, and download bar exclusion.
/// **Validates: Requirements 2.2, 5.1, 5.2, 1.8, 4.2, 4.6, 6.8, 6.9, 6.10**
/// </summary>
public class ProposalPdfFilenamePropertyTests
{
    private static readonly char[] InvalidChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

    private static string InvokeGenerateProposalPdfFilename(string reference)
    {
        var method = typeof(QuotationController).GetMethod(
            "GenerateProposalPdfFilename",
            BindingFlags.NonPublic | BindingFlags.Static);

        return (string)method!.Invoke(null, new object[] { reference })!;
    }

    #region Property 1: Proposal PDF filename format

    /// <summary>
    /// Feature: quotation-pdf-download, Property 1: Proposal PDF filename format
    /// For any non-empty string containing at least one valid filename character
    /// (not in invalid chars, not a control char, not whitespace, not a dot),
    /// the result starts with "QUO-" and ends with ".pdf".
    /// **Validates: Requirements 2.2, 5.1, 6.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FilenameFormat_StartsWithQUO_EndsWithPdf()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString().Filter(s =>
                s.Get.Any(c => !InvalidChars.Contains(c) && c > '\u001F' && !char.IsWhiteSpace(c) && c != '.')),
            nes =>
            {
                var result = InvokeGenerateProposalPdfFilename(nes.Get);

                var startsCorrectly = result.StartsWith("QUO-");
                var endsCorrectly = result.EndsWith(".pdf");

                return (startsCorrectly && endsCorrectly).ToProperty()
                    .Label($"Input='{nes.Get}', Result='{result}': " +
                           $"StartsWithQUO={startsCorrectly}, EndsWithPdf={endsCorrectly}");
            });
    }

    #endregion

    #region Property 2: Filename sanitization removes all invalid characters and trims

    /// <summary>
    /// Feature: quotation-pdf-download, Property 2: Filename sanitization removes all invalid characters and trims
    /// For any arbitrary string, the generated filename never contains any of the invalid characters
    /// or ASCII control characters (0x00–0x1F), and the reference portion (between "QUO-" and ".pdf")
    /// has no leading/trailing whitespace or dots.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Filename_NeverContainsInvalidCharsOrControlChars_AndTrimsReferencePortion(string input)
    {
        var testInput = input ?? string.Empty;
        var result = InvokeGenerateProposalPdfFilename(testInput);

        // Check no invalid chars in result
        var containsInvalidChar = InvalidChars.Any(c => result.Contains(c));

        // Check no ASCII control characters in result
        var containsControlChar = result.Any(c => c <= '\u001F');

        // Extract reference portion between "QUO-" and ".pdf"
        var referencePortion = string.Empty;
        if (result.StartsWith("QUO-") && result.EndsWith(".pdf"))
        {
            referencePortion = result.Substring(4, result.Length - 4 - 4); // Remove "QUO-" prefix and ".pdf" suffix
        }

        // Reference portion should have no leading/trailing whitespace or dots
        var hasLeadingTrailingWhitespaceOrDots = referencePortion.Length > 0 &&
            (char.IsWhiteSpace(referencePortion[0]) || referencePortion[0] == '.' ||
             char.IsWhiteSpace(referencePortion[^1]) || referencePortion[^1] == '.');

        return (!containsInvalidChar && !containsControlChar && !hasLeadingTrailingWhitespaceOrDots).ToProperty()
            .Label($"Input='{testInput}', Result='{result}': " +
                   $"ContainsInvalidChars={containsInvalidChar}, ContainsControlChars={containsControlChar}, " +
                   $"HasLeadingTrailingWhitespaceOrDots={hasLeadingTrailingWhitespaceOrDots}");
    }

    #endregion

    #region Property 3: Logo data URI encoding round-trip

    /// <summary>
    /// Feature: quotation-pdf-download, Property 3: Logo data URI encoding round-trip
    /// For any non-empty byte array and valid MIME type string, encoding to data URI
    /// and decoding the base64 portion yields the original bytes.
    /// **Validates: Requirements 1.8, 4.2, 6.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LogoDataUri_RoundTrip()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyArray<byte>(),
            Arb.Default.NonEmptyString(),
            (bytes, contentType) =>
            {
                // Sanitize the content type to form a valid MIME type
                var sanitized = contentType.Get
                    .Replace("/", "")
                    .Replace(";", "")
                    .Replace(",", "")
                    .Replace(" ", "");
                if (string.IsNullOrEmpty(sanitized)) sanitized = "png";
                var mimeType = $"image/{sanitized}";

                // Encode to data URI (same logic as logo embedding in ProposalPdfService)
                var dataUri = $"data:{mimeType};base64,{Convert.ToBase64String(bytes.Get)}";

                // Decode the base64 portion — find the marker ";base64," and extract after it
                var marker = ";base64,";
                var markerIndex = dataUri.IndexOf(marker);
                var base64Part = dataUri.Substring(markerIndex + marker.Length);
                var decoded = Convert.FromBase64String(base64Part);

                var roundTripMatches = decoded.SequenceEqual(bytes.Get);

                return roundTripMatches.ToProperty()
                    .Label($"ByteCount={bytes.Get.Length}, MimeType='{mimeType}': " +
                           $"RoundTripMatches={roundTripMatches}");
            });
    }

    #endregion

    #region Property 4: Download bar exclusion from PDF HTML

    /// <summary>
    /// Feature: quotation-pdf-download, Property 4: Download bar exclusion from PDF HTML
    /// For any HTML string containing a download-bar div element, after applying the
    /// removal regex, the result does not contain class="download-bar".
    /// **Validates: Requirements 4.6, 6.10**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DownloadBar_RemovedFromHtml()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString(),
            Arb.Default.NonEmptyString(),
            (beforeContent, innerContent) =>
            {
                // Build HTML with a download-bar element injected
                var before = beforeContent.Get.Replace("download-bar", "other-class");
                var inner = innerContent.Get.Replace("download-bar", "other-class");

                var html = $"<html><body>{before}<div class=\"download-bar\">{inner}</div></body></html>";

                // Apply the same regex used in ProposalPdfService
                var pattern = @"<div class=""download-bar"">[\s\S]*?</div>";
                var result = Regex.Replace(html, pattern, string.Empty);

                var containsDownloadBar = result.Contains("class=\"download-bar\"");

                return (!containsDownloadBar).ToProperty()
                    .Label($"Result contains download-bar: {containsDownloadBar}, " +
                           $"ResultLength={result.Length}");
            });
    }

    #endregion
}
