using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Controllers;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for InvoiceViewController acceptance endpoints.
/// Validates: Requirements 1.1, 1.4, 2.2, 2.3, 3.2, 5.1
/// </summary>
public class InvoiceViewControllerAcceptanceTests
{
    private readonly Mock<IInvoiceSharingService> _sharingServiceMock;
    private readonly Mock<IInvoiceAcceptanceService> _acceptanceServiceMock;
    private readonly Mock<IInvoiceRenderer> _invoiceRendererMock;
    private readonly Mock<IWebHostEnvironment> _environmentMock;
    private readonly Mock<ILogoService> _logoServiceMock;
    private readonly Mock<ILogger<InvoiceViewController>> _loggerMock;
    private readonly InvoiceViewController _controller;

    public InvoiceViewControllerAcceptanceTests()
    {
        _sharingServiceMock = new Mock<IInvoiceSharingService>();
        _acceptanceServiceMock = new Mock<IInvoiceAcceptanceService>();
        _invoiceRendererMock = new Mock<IInvoiceRenderer>();
        _environmentMock = new Mock<IWebHostEnvironment>();
        _logoServiceMock = new Mock<ILogoService>();
        _loggerMock = new Mock<ILogger<InvoiceViewController>>();

        var tenantServiceMock = new Mock<ICurrentTenantService>();
        tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(1);
        var portalOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"Portal_ViewCtrlAccept_{Guid.NewGuid()}")
            .Options;
        var portalDbContext = new PortalDbContext(portalOptions, tenantServiceMock.Object);

        _controller = new InvoiceViewController(
            _sharingServiceMock.Object,
            _acceptanceServiceMock.Object,
            _invoiceRendererMock.Object,
            _environmentMock.Object,
            _logoServiceMock.Object,
            Mock.Of<IPaymentInstructionsService>(),
            portalDbContext,
            _loggerMock.Object,
            Mock.Of<IStripeConnectService>());

        // Set up default HttpContext for all tests
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");
        httpContext.Request.Headers["User-Agent"] = "TestBrowser/1.0";
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    #region Requirement 1.1 — GET injects acceptance form for active non-accepted share

    [Fact]
    public async Task ViewInvoice_InjectsAcceptanceForm_WhenShareIsActiveAndNotAccepted()
    {
        // Arrange
        const string token = "valid-share-token";
        var share = new InvoiceShare
        {
            Id = 1,
            InvoiceId = 100,
            BusinessId = 1,
            ShareToken = token,
            SnapshotHtml = "<div class=\"page\">Invoice content here</div>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedByUserId = "user-001"
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync(token))
            .ReturnsAsync(share);

        _acceptanceServiceMock
            .Setup(s => s.GetByInvoiceShareIdAsync(share.Id))
            .ReturnsAsync((InvoiceAcceptance?)null);

        // Act
        var result = await _controller.ViewInvoice(token);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("text/html", contentResult.ContentType);
        Assert.Contains("acceptance-checkbox", contentResult.Content);
        Assert.Contains("accept-btn", contentResult.Content);
        Assert.Contains("I accept this invoice as correct and agree to pay by the due date.", contentResult.Content);
    }

    #endregion

    #region Requirement 1.4 — GET injects read-only message for already-accepted share

    [Fact]
    public async Task ViewInvoice_InjectsReadOnlyMessage_WhenShareIsAlreadyAccepted()
    {
        // Arrange
        const string token = "accepted-share-token";
        var share = new InvoiceShare
        {
            Id = 2,
            InvoiceId = 101,
            BusinessId = 1,
            ShareToken = token,
            SnapshotHtml = "<div class=\"page\">Invoice content here</div>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
            CreatedByUserId = "user-001"
        };

        var acceptance = new InvoiceAcceptance
        {
            Id = 1,
            InvoiceShareId = share.Id,
            AcceptedTerms = "I accept this invoice as correct and agree to pay by the due date.",
            AcceptedAtUtc = new DateTimeOffset(2025, 3, 15, 10, 30, 0, TimeSpan.Zero),
            IpAddress = "10.0.0.1",
            UserAgent = "Mozilla/5.0",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync(token))
            .ReturnsAsync(share);

        _acceptanceServiceMock
            .Setup(s => s.GetByInvoiceShareIdAsync(share.Id))
            .ReturnsAsync(acceptance);

        // Act
        var result = await _controller.ViewInvoice(token);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("text/html", contentResult.ContentType);
        Assert.Contains("Accepted on", contentResult.Content);
        Assert.DoesNotContain("acceptance-checkbox", contentResult.Content);
        Assert.DoesNotContain("accept-btn", contentResult.Content);
    }

    #endregion

    #region Requirement 5.1 — GET does not inject acceptance UI for inactive/expired share

    [Fact]
    public async Task ViewInvoice_DoesNotInjectAcceptanceUI_ForInactiveShare()
    {
        // Arrange
        const string token = "inactive-share-token";
        var share = new InvoiceShare
        {
            Id = 3,
            InvoiceId = 102,
            BusinessId = 1,
            ShareToken = token,
            SnapshotHtml = "<div class=\"page\">Invoice content here</div>",
            CustomerEmail = "customer@test.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = false,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-3),
            CreatedByUserId = "user-001"
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync(token))
            .ReturnsAsync(share);

        // Act
        var result = await _controller.ViewInvoice(token);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Shared/Unavailable.cshtml", viewResult.ViewName);
    }

    #endregion

    #region Requirement 2.2 — POST returns success JSON on first acceptance

    [Fact]
    public async Task AcceptInvoice_ReturnsSuccessJson_OnFirstAcceptance()
    {
        // Arrange
        const string token = "fresh-share-token";
        var acceptedAt = DateTimeOffset.UtcNow;

        _acceptanceServiceMock
            .Setup(s => s.AcceptAsync(token, "192.168.1.1", "TestBrowser/1.0"))
            .ReturnsAsync(new InvoiceAcceptanceResult
            {
                Success = true,
                Message = "Invoice accepted successfully.",
                AcceptedAtUtc = acceptedAt,
                AlreadyAccepted = false
            });

        // Act
        var result = await _controller.AcceptInvoice(token);

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
    public async Task AcceptInvoice_ReturnsAlreadyAcceptedJson_OnDuplicate()
    {
        // Arrange
        const string token = "already-accepted-token";
        var originalAcceptedAt = DateTimeOffset.UtcNow.AddHours(-2);

        _acceptanceServiceMock
            .Setup(s => s.AcceptAsync(token, "192.168.1.1", "TestBrowser/1.0"))
            .ReturnsAsync(new InvoiceAcceptanceResult
            {
                Success = false,
                Message = "This invoice has already been accepted.",
                AcceptedAtUtc = originalAcceptedAt,
                AlreadyAccepted = true
            });

        // Act
        var result = await _controller.AcceptInvoice(token);

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

    #region Requirement 2.3, 5.2 — POST returns error JSON for expired share

    [Fact]
    public async Task AcceptInvoice_ReturnsErrorJson_ForExpiredShare()
    {
        // Arrange
        const string token = "expired-share-token";

        _acceptanceServiceMock
            .Setup(s => s.AcceptAsync(token, "192.168.1.1", "TestBrowser/1.0"))
            .ReturnsAsync(new InvoiceAcceptanceResult
            {
                Success = false,
                Message = "This share link is no longer valid.",
                AcceptedAtUtc = null,
                AlreadyAccepted = false
            });

        // Act
        var result = await _controller.AcceptInvoice(token);

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
