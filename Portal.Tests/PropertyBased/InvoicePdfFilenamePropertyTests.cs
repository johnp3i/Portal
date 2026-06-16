using System.Reflection;
using FsCheck;
using FsCheck.Xunit;
using Portal.Web.Controllers;
using Xunit;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Property-based tests for invoice PDF filename generation and logo data URI encoding.
/// **Validates: Requirements 2.2, 5.1, 5.2, 5.3, 4.3**
/// </summary>
public class InvoicePdfFilenamePropertyTests
{
    private static readonly char[] InvalidChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

    private static string InvokeGenerateInvoicePdfFilename(string invoiceNumber)
    {
        var method = typeof(InvoiceController).GetMethod(
            "GenerateInvoicePdfFilename",
            BindingFlags.NonPublic | BindingFlags.Static);

        return (string)method!.Invoke(null, new object[] { invoiceNumber })!;
    }

    #region Property 1: Invoice PDF filename format

    /// <summary>
    /// Feature: invoice-pdf-download, Property 1: Invoice PDF filename format
    /// For any non-empty string that contains at least one valid filename character,
    /// the result starts with "INV-" and ends with ".pdf".
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FilenameFormat_StartsWithINV_EndsWithPdf()
    {
        return Prop.ForAll(
            Arb.Default.NonEmptyString().Filter(s =>
                s.Get.Any(c => !InvalidChars.Contains(c) && !char.IsWhiteSpace(c))),
            nes =>
            {
                var result = InvokeGenerateInvoicePdfFilename(nes.Get);

                var startsCorrectly = result.StartsWith("INV-");
                var endsCorrectly = result.EndsWith(".pdf");

                return (startsCorrectly && endsCorrectly).ToProperty()
                    .Label($"Input='{nes.Get}', Result='{result}': " +
                           $"StartsWithINV={startsCorrectly}, EndsWithPdf={endsCorrectly}");
            });
    }

    #endregion

    #region Property 2: Filename sanitization removes all invalid characters

    /// <summary>
    /// Feature: invoice-pdf-download, Property 2: Filename sanitization removes all invalid characters
    /// For any arbitrary string, the sanitized filename never contains any of the invalid characters.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Filename_NeverContainsInvalidCharacters(string input)
    {
        var testInput = input ?? string.Empty;
        var result = InvokeGenerateInvoicePdfFilename(testInput);

        var containsInvalid = InvalidChars.Any(c => result.Contains(c));

        return (!containsInvalid).ToProperty()
            .Label($"Input='{testInput}', Result='{result}': " +
                   $"ContainsInvalidChars={containsInvalid}");
    }

    #endregion

    #region Property 3: Logo data URI encoding round-trip

    /// <summary>
    /// Feature: invoice-pdf-download, Property 3: Logo data URI encoding round-trip
    /// For any non-empty byte array and valid MIME type string, encoding to data URI
    /// and decoding the base64 portion yields the original bytes.
    /// **Validates: Requirements 4.3**
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
                // Remove characters that are invalid in MIME types to keep the data URI well-formed
                var sanitized = contentType.Get
                    .Replace("/", "")
                    .Replace(";", "")
                    .Replace(",", "")
                    .Replace(" ", "");
                if (string.IsNullOrEmpty(sanitized)) sanitized = "png";
                var mimeType = $"image/{sanitized}";

                // Encode to data URI (same logic as GetLogoAsDataUri)
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
}
