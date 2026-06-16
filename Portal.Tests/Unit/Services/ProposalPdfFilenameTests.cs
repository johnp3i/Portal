using System.Reflection;
using Portal.Web.Controllers;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for QuotationController.GenerateProposalPdfFilename verifying
/// filename sanitization logic for PDF downloads.
/// </summary>
public class ProposalPdfFilenameTests
{
    private static string InvokeGenerateProposalPdfFilename(string reference)
    {
        var method = typeof(QuotationController).GetMethod(
            "GenerateProposalPdfFilename",
            BindingFlags.NonPublic | BindingFlags.Static);

        return (string)method!.Invoke(null, new object[] { reference })!;
    }

    [Fact]
    public void GenerateProposalPdfFilename_NormalReference_ReturnsCorrectFormat()
    {
        var result = InvokeGenerateProposalPdfFilename("2025-00042");
        Assert.Equal("QUO-2025-00042.pdf", result);
    }

    [Fact]
    public void GenerateProposalPdfFilename_InvalidChars_RemovesThem()
    {
        var result = InvokeGenerateProposalPdfFilename("2025/00:042");
        Assert.Equal("QUO-202500042.pdf", result);
    }

    [Fact]
    public void GenerateProposalPdfFilename_AllInvalidChars_ReturnsFallback()
    {
        var result = InvokeGenerateProposalPdfFilename("<>:\"|?*");
        Assert.Equal("QUO-download.pdf", result);
    }

    [Fact]
    public void GenerateProposalPdfFilename_EmptyString_ReturnsFallback()
    {
        var result = InvokeGenerateProposalPdfFilename("");
        Assert.Equal("QUO-download.pdf", result);
    }

    [Fact]
    public void GenerateProposalPdfFilename_WhitespaceOnly_ReturnsFallback()
    {
        var result = InvokeGenerateProposalPdfFilename("   ");
        Assert.Equal("QUO-download.pdf", result);
    }

    [Fact]
    public void GenerateProposalPdfFilename_LeadingTrailingDots_TrimsDots()
    {
        var result = InvokeGenerateProposalPdfFilename("..2025..");
        Assert.Equal("QUO-2025.pdf", result);
    }

    [Fact]
    public void GenerateProposalPdfFilename_LeadingTrailingSpaces_TrimsSpaces()
    {
        var result = InvokeGenerateProposalPdfFilename(" 2025 ");
        Assert.Equal("QUO-2025.pdf", result);
    }

    [Fact]
    public void GenerateProposalPdfFilename_AsciiControlCharacters_RemovesThem()
    {
        // Use char literals to avoid C# hex escape sequence ambiguity
        var input = new string(new[] { '\x01', '\x02', 'A', 'B', 'C', '\x1F' });
        var result = InvokeGenerateProposalPdfFilename(input);
        Assert.Equal("QUO-ABC.pdf", result);
    }
}
