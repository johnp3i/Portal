using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Controllers;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for ProposalController acceptance endpoints.
/// Validates: Requirements 1.1, 1.4, 2.2, 2.3, 3.2, 6.1
/// </summary>
public class ProposalControllerAcceptanceTests
{
    private readonly Mock<IProposalService> _proposalServiceMock;
    private readonly Mock<IProposalAcceptanceService> _acceptanceServiceMock;
    private readonly ProposalController _controller;

    public ProposalControllerAcceptanceTests()
    {
        _proposalServiceMock = new Mock<IProposalService>();
        _acceptanceServiceMock = new Mock<IProposalAcceptanceService>();

        _controller = new ProposalController(
            _proposalServiceMock.Object,
            _acceptanceServiceMock.Object,
            Mock.Of<IWebHostEnvironment>(),
            Mock.Of<ILogoService>(),
            Mock.Of<IViewRenderService>(),
            Mock.Of<ILogger<ProposalController>>());

        // Set up default HttpContext for all tests
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");
        httpContext.Request.Headers["User-Agent"] = "TestBrowser/1.0";
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    #region Requirement 1.1 — GET injects acceptance form for active non-accepted share

    [Fact]
    public async Task ViewProposal_InjectsAcceptanceForm_WhenShareIsActiveAndNotAccepted()
    {
        // Arrange
        const string token = "valid-share-token";
        var share = new ProposalShare
        {
            Id = 1,
            QuotationId = 100,
            BusinessId = 1,
            ShareToken = token,
            SnapshotHtml = "<div class=\"page\">Proposal content here</div>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedByUserId = "user-001"
        };

        _proposalServiceMock
            .Setup(s => s.GetByTokenAsync(token))
            .ReturnsAsync(share);

        _acceptanceServiceMock
            .Setup(s => s.GetByProposalShareIdAsync(share.Id))
            .ReturnsAsync((ProposalAcceptance?)null);

        // Act
        var result = await _controller.ViewProposal(token);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("text/html", contentResult.ContentType);
        Assert.Contains("acceptTermsCheckbox", contentResult.Content);
        Assert.Contains("acceptProposalBtn", contentResult.Content);
        Assert.Contains("I accept this proposal and agree to proceed with the quoted work.", contentResult.Content);
    }

    #endregion

    #region Requirement 1.4 — GET injects read-only message for already-accepted share

    [Fact]
    public async Task ViewProposal_InjectsReadOnlyMessage_WhenShareIsAlreadyAccepted()
    {
        // Arrange
        const string token = "accepted-share-token";
        var share = new ProposalShare
        {
            Id = 2,
            QuotationId = 101,
            BusinessId = 1,
            ShareToken = token,
            SnapshotHtml = "<div class=\"page\">Proposal content here</div>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedByUserId = "user-001"
        };

        var acceptance = new ProposalAcceptance
        {
            Id = 1,
            ProposalShareId = share.Id,
            AcceptedTerms = "I accept this proposal and agree to proceed with the quoted work.",
            AcceptedAtUtc = new DateTimeOffset(2025, 3, 15, 10, 30, 0, TimeSpan.Zero),
            IpAddress = "10.0.0.1",
            UserAgent = "Mozilla/5.0",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };

        _proposalServiceMock
            .Setup(s => s.GetByTokenAsync(token))
            .ReturnsAsync(share);

        _acceptanceServiceMock
            .Setup(s => s.GetByProposalShareIdAsync(share.Id))
            .ReturnsAsync(acceptance);

        // Act
        var result = await _controller.ViewProposal(token);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("text/html", contentResult.ContentType);
        Assert.Contains("Accepted on", contentResult.Content);
        Assert.DoesNotContain("acceptTermsCheckbox", contentResult.Content);
        Assert.DoesNotContain("acceptProposalBtn", contentResult.Content);
    }

    #endregion

    #region Requirement 6.1 — GET does not inject acceptance UI for inactive/expired share

    [Fact]
    public async Task ViewProposal_DoesNotInjectAcceptanceUI_ForInactiveShare()
    {
        // Arrange
        const string token = "inactive-share-token";
        var share = new ProposalShare
        {
            Id = 3,
            QuotationId = 102,
            BusinessId = 1,
            ShareToken = token,
            SnapshotHtml = "<div class=\"page\">Proposal content here</div>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = false,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-3),
            CreatedByUserId = "user-001"
        };

        _proposalServiceMock
            .Setup(s => s.GetByTokenAsync(token))
            .ReturnsAsync(share);

        // Act
        var result = await _controller.ViewProposal(token);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Shared/Unavailable.cshtml", viewResult.ViewName);
    }

    [Fact]
    public async Task ViewProposal_DoesNotInjectAcceptanceUI_ForExpiredShare()
    {
        // Arrange
        const string token = "expired-share-token";
        var share = new ProposalShare
        {
            Id = 4,
            QuotationId = 103,
            BusinessId = 1,
            ShareToken = token,
            SnapshotHtml = "<div class=\"page\">Proposal content here</div>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5),
            CreatedByUserId = "user-001"
        };

        _proposalServiceMock
            .Setup(s => s.GetByTokenAsync(token))
            .ReturnsAsync(share);

        // Act
        var result = await _controller.ViewProposal(token);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Shared/Unavailable.cshtml", viewResult.ViewName);
    }

    #endregion

    #region Requirement 2.2 — POST returns success JSON on first acceptance

    [Fact]
    public async Task AcceptProposal_ReturnsSuccessJson_OnFirstAcceptance()
    {
        // Arrange
        const string token = "fresh-share-token";
        var acceptedAt = DateTimeOffset.UtcNow;

        _acceptanceServiceMock
            .Setup(s => s.AcceptAsync(token, "192.168.1.1", "TestBrowser/1.0"))
            .ReturnsAsync(new ProposalAcceptanceResult
            {
                Success = true,
                Message = "Proposal accepted successfully.",
                AcceptedAtUtc = acceptedAt,
                AlreadyAccepted = false
            });

        // Act
        var result = await _controller.AcceptProposal(token);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var value = jsonResult.Value!;
        var successProp = value.GetType().GetProperty("success")!.GetValue(value);
        var alreadyAcceptedProp = value.GetType().GetProperty("alreadyAccepted")!.GetValue(value);
        var acceptedAtProp = value.GetType().GetProperty("acceptedAt")!.GetValue(value);

        Assert.Equal(true, successProp);
        Assert.Equal(false, alreadyAcceptedProp);
        Assert.Equal(acceptedAt, acceptedAtProp);
    }

    #endregion

    #region Requirement 3.2 — POST returns alreadyAccepted JSON on duplicate

    [Fact]
    public async Task AcceptProposal_ReturnsAlreadyAcceptedJson_OnDuplicate()
    {
        // Arrange
        const string token = "already-accepted-token";
        var originalAcceptedAt = DateTimeOffset.UtcNow.AddHours(-2);

        _acceptanceServiceMock
            .Setup(s => s.AcceptAsync(token, "192.168.1.1", "TestBrowser/1.0"))
            .ReturnsAsync(new ProposalAcceptanceResult
            {
                Success = false,
                Message = "This proposal has already been accepted.",
                AcceptedAtUtc = originalAcceptedAt,
                AlreadyAccepted = true
            });

        // Act
        var result = await _controller.AcceptProposal(token);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var value = jsonResult.Value!;
        var successProp = value.GetType().GetProperty("success")!.GetValue(value);
        var alreadyAcceptedProp = value.GetType().GetProperty("alreadyAccepted")!.GetValue(value);
        var acceptedAtProp = value.GetType().GetProperty("acceptedAt")!.GetValue(value);

        Assert.Equal(false, successProp);
        Assert.Equal(true, alreadyAcceptedProp);
        Assert.Equal(originalAcceptedAt, acceptedAtProp);
    }

    #endregion

    #region Requirement 2.3 — POST returns error JSON for expired share

    [Fact]
    public async Task AcceptProposal_ReturnsErrorJson_ForExpiredShare()
    {
        // Arrange
        const string token = "expired-share-token";

        _acceptanceServiceMock
            .Setup(s => s.AcceptAsync(token, "192.168.1.1", "TestBrowser/1.0"))
            .ReturnsAsync(new ProposalAcceptanceResult
            {
                Success = false,
                Message = "This share link is no longer valid.",
                AcceptedAtUtc = null,
                AlreadyAccepted = false
            });

        // Act
        var result = await _controller.AcceptProposal(token);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(result);
        var value = jsonResult.Value!;
        var successProp = value.GetType().GetProperty("success")!.GetValue(value);
        var alreadyAcceptedProp = value.GetType().GetProperty("alreadyAccepted")!.GetValue(value);
        var messageProp = value.GetType().GetProperty("message")!.GetValue(value);

        Assert.Equal(false, successProp);
        Assert.Equal(false, alreadyAcceptedProp);
        Assert.NotNull(messageProp);
    }

    #endregion
}
