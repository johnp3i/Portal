using System.Reflection;
using Portal.Web.Controllers;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for InvoiceController.GenerateInvoicePdfFilename verifying
/// filename sanitization logic for PDF downloads.
/// </summary>
public class InvoicePdfFilenameTests
{
    private static string InvokeGenerateInvoicePdfFilename(string invoiceNumber)
    {
        var method = typeof(InvoiceController).GetMethod(
            "GenerateInvoicePdfFilename",
            BindingFlags.NonPublic | BindingFlags.Static);

        return (string)method!.Invoke(null, new object[] { invoiceNumber })!;
    }

    [Fact]
    public void GenerateInvoicePdfFilename_NormalNumber_ReturnsCorrectFormat()
    {
        var result = InvokeGenerateInvoicePdfFilename("1-00090");
        Assert.Equal("INV-1-00090.pdf", result);
    }

    [Fact]
    public void GenerateInvoicePdfFilename_InvalidChars_RemovesThem()
    {
        var result = InvokeGenerateInvoicePdfFilename("1/00:090");
        Assert.Equal("INV-100090.pdf", result);
    }

    [Fact]
    public void GenerateInvoicePdfFilename_AllInvalidChars_ReturnsFallback()
    {
        var result = InvokeGenerateInvoicePdfFilename("<>:\"|?*");
        Assert.Equal("INV-download.pdf", result);
    }

    [Fact]
    public void GenerateInvoicePdfFilename_EmptyString_ReturnsFallback()
    {
        var result = InvokeGenerateInvoicePdfFilename("");
        Assert.Equal("INV-download.pdf", result);
    }

    [Fact]
    public void GenerateInvoicePdfFilename_WhitespaceOnly_ReturnsFallback()
    {
        var result = InvokeGenerateInvoicePdfFilename("   ");
        Assert.Equal("INV-download.pdf", result);
    }
}
