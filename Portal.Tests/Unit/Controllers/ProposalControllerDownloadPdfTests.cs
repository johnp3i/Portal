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
/// Unit tests for ProposalController.DownloadPdf anonymous endpoint.
/// Validates: Requirements 6.1, 6.2, 6.3, 6.12 — token-based PDF download for shared proposals.
/// </summary>
public class ProposalControllerDownloadPdfTests
{
    private readonly Mock<IProposalService> _proposalServiceMock;
    private readonly Mock<IProposalAcceptanceService> _acceptanceServiceMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly Mock<ILogoService> _logoServiceMock;
    private readonly Mock<ILogger<ProposalController>> _loggerMock;
    private readonly ProposalController _controller;

    public ProposalControllerDownloadPdfTests()
    {
        _proposalServiceMock = new Mock<IProposalService>();
        _acceptanceServiceMock = new Mock<IProposalAcceptanceService>();
        _environmentMock = new Mock<IWebHostEnvironment>();
        _logoServiceMock = new Mock<ILogoService>();
        _loggerMock = new Mock<ILogger<ProposalController>>();

        _controller = new ProposalController(
            _proposalServiceMock.Object,
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
        _proposalServiceMock
            .Setup(s => s.GetByTokenAsync("nonexistent-token"))
            .ReturnsAsync((ProposalShare?)null);

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
        var share = new ProposalShare
        {
            Id = 1,
            QuotationId = 100,
            BusinessId = 1,
            ShareToken = "expired-token",
            SnapshotHtml = "<div>Proposal</div>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1), // Expired yesterday
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            CreatedByUserId = "user-001"
        };

        _proposalServiceMock
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
        var share = new ProposalShare
        {
            Id = 2,
            QuotationId = 101,
            BusinessId = 1,
            ShareToken = "inactive-token",
            SnapshotHtml = "<div>Proposal</div>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7), // Not expired
            IsActive = false, // Inactive
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5),
            CreatedByUserId = "user-001"
        };

        _proposalServiceMock
            .Setup(s => s.GetByTokenAsync("inactive-token"))
            .ReturnsAsync(share);

        // Act
        var result = await _controller.DownloadPdf("inactive-token");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Null/empty SnapshotHtml → NotFound

    [Fact]
    public async Task DownloadPdf_NullSnapshotHtml_ReturnsNotFound()
    {
        // Arrange
        var share = new ProposalShare
        {
            Id = 3,
            QuotationId = 102,
            BusinessId = 1,
            ShareToken = "null-html-token",
            SnapshotHtml = null!,
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedByUserId = "user-001"
        };

        _proposalServiceMock
            .Setup(s => s.GetByTokenAsync("null-html-token"))
            .ReturnsAsync(share);

        // Act
        var result = await _controller.DownloadPdf("null-html-token");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DownloadPdf_EmptySnapshotHtml_ReturnsNotFound()
    {
        // Arrange
        var share = new ProposalShare
        {
            Id = 4,
            QuotationId = 103,
            BusinessId = 1,
            ShareToken = "empty-html-token",
            SnapshotHtml = "",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedByUserId = "user-001"
        };

        _proposalServiceMock
            .Setup(s => s.GetByTokenAsync("empty-html-token"))
            .ReturnsAsync(share);

        // Act
        var result = await _controller.DownloadPdf("empty-html-token");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    #endregion

    #region Valid active share → returns FileResult with application/pdf

    [Fact]
    public async Task DownloadPdf_ValidActiveShare_ReturnsFileResultWithPdfContentType()
    {
        // Arrange: Valid, active, non-expired share with quotation reference for filename.
        var share = new ProposalShare
        {
            Id = 5,
            QuotationId = 104,
            BusinessId = 1,
            ShareToken = "valid-active-token",
            SnapshotHtml = "<html><head></head><body><div>Proposal</div></body></html>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedByUserId = "user-001",
            Quotation = new Quotation
            {
                Id = 104,
                BusinessId = 1,
                Reference = "2025-00042"
            }
        };

        _proposalServiceMock
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
        Assert.StartsWith("QUO-", fileResult.FileDownloadName);
        Assert.EndsWith(".pdf", fileResult.FileDownloadName);
        Assert.True(fileResult.FileContents.Length > 0);
    }

    #endregion
}
