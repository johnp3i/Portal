using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Web.Controllers;
using Xunit;

namespace Portal.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for InvoiceViewController.DownloadPdf anonymous endpoint.
/// Validates: Requirement 6.2, 6.3 — token-based PDF download for shared invoices.
/// </summary>
public class InvoiceViewControllerDownloadPdfTests
{
    private readonly Mock<IInvoiceSharingService> _sharingServiceMock;
    private readonly Mock<IInvoiceAcceptanceService> _acceptanceServiceMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly Mock<ILogoService> _logoServiceMock;
    private readonly Mock<ILogger<InvoiceViewController>> _loggerMock;
    private readonly InvoiceViewController _controller;

    public InvoiceViewControllerDownloadPdfTests()
    {
        _sharingServiceMock = new Mock<IInvoiceSharingService>();
        _acceptanceServiceMock = new Mock<IInvoiceAcceptanceService>();
        _environmentMock = new Mock<IWebHostEnvironment>();
        _logoServiceMock = new Mock<ILogoService>();
        _loggerMock = new Mock<ILogger<InvoiceViewController>>();

        _controller = new InvoiceViewController(
            _sharingServiceMock.Object,
            _acceptanceServiceMock.Object,
            _environmentMock.Object,
            _logoServiceMock.Object,
            _loggerMock.Object);

        // Set up default HttpContext
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    #region Invalid token → NotFound

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DownloadPdf_InvalidToken_ReturnsNotFound(string? token)
    {
        // Act
        var result = await _controller.DownloadPdf(token!);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DownloadPdf_TokenNotFound_ReturnsNotFound()
    {
        // Arrange
        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync("nonexistent-token"))
            .ReturnsAsync((InvoiceShare?)null);

        // Act
        var result = await _controller.DownloadPdf("nonexistent-token");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Expired share → NotFound

    [Fact]
    public async Task DownloadPdf_ExpiredShare_ReturnsNotFound()
    {
        // Arrange
        var share = new InvoiceShare
        {
            Id = 1,
            InvoiceId = 100,
            BusinessId = 1,
            ShareToken = "expired-token",
            SnapshotHtml = "<div>Invoice</div>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1), // Expired yesterday
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            CreatedByUserId = "user-001"
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync("expired-token"))
            .ReturnsAsync(share);

        // Act
        var result = await _controller.DownloadPdf("expired-token");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Inactive share → NotFound

    [Fact]
    public async Task DownloadPdf_InactiveShare_ReturnsNotFound()
    {
        // Arrange
        var share = new InvoiceShare
        {
            Id = 2,
            InvoiceId = 101,
            BusinessId = 1,
            ShareToken = "inactive-token",
            SnapshotHtml = "<div>Invoice</div>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7), // Not expired
            IsActive = false, // Inactive
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5),
            CreatedByUserId = "user-001"
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync("inactive-token"))
            .ReturnsAsync(share);

        // Act
        var result = await _controller.DownloadPdf("inactive-token");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Valid active share → returns FileResult with application/pdf

    [Fact]
    public async Task DownloadPdf_ValidActiveShare_ReturnsFileResultWithPdfContentType()
    {
        // Arrange: Valid, active, non-expired share with invoice number extractable from HTML.
        var share = new InvoiceShare
        {
            Id = 3,
            InvoiceId = 102,
            BusinessId = 1,
            ShareToken = "valid-active-token",
            SnapshotHtml = "<html><head></head><body><div class=\"page\">Invoice #1-00042</div></body></html>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedByUserId = "user-001"
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync("valid-active-token"))
            .ReturnsAsync(share);

        _logoServiceMock
            .Setup(s => s.GetByBusinessIdAsync(1))
            .ReturnsAsync(new List<BusinessLogo>());

        _environmentMock
            .Setup(e => e.WebRootPath)
            .Returns("C:\\fake\\wwwroot");

        // Act
        var result = await _controller.DownloadPdf("valid-active-token");

        // Assert: With PuppeteerSharp/Chromium available, returns a PDF file download.
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.NotNull(fileResult.FileDownloadName);
        Assert.StartsWith("INV-", fileResult.FileDownloadName);
        Assert.EndsWith(".pdf", fileResult.FileDownloadName);
        Assert.True(fileResult.FileContents.Length > 0);
    }

    #endregion
}
