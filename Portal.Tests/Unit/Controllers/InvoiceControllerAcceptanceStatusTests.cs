using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Controllers;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for InvoiceController.Detail acceptance status display.
/// Validates: Requirements 4.1, 4.2, 4.3
/// </summary>
public class InvoiceControllerAcceptanceStatusTests
{
    private readonly Mock<IInvoiceService> _invoiceServiceMock;
    private readonly Mock<IInvoiceSectionService> _sectionServiceMock;
    private readonly Mock<ICurrentTenantService> _tenantServiceMock;
    private readonly Mock<ICustomerService> _customerServiceMock;
    private readonly Mock<ILogoService> _logoServiceMock;
    private readonly Mock<IBusinessService> _businessServiceMock;
    private readonly Mock<IInvoiceSharingService> _sharingServiceMock;
    private readonly Mock<IInvoiceAcceptanceService> _acceptanceServiceMock;
    private readonly Mock<IDocumentDuplicationService> _duplicationServiceMock;
    private readonly Mock<IDocumentSoftDeleteService> _softDeleteServiceMock;
    private readonly Mock<IViewRenderService> _viewRenderServiceMock;
    private readonly Mock<IInvoicePdfService> _invoicePdfServiceMock;
    private readonly Mock<ILogger<InvoiceController>> _loggerMock;
    private readonly InvoiceController _controller;

    public InvoiceControllerAcceptanceStatusTests()
    {
        _invoiceServiceMock = new Mock<IInvoiceService>();
        _sectionServiceMock = new Mock<IInvoiceSectionService>();
        _tenantServiceMock = new Mock<ICurrentTenantService>();
        _customerServiceMock = new Mock<ICustomerService>();
        _logoServiceMock = new Mock<ILogoService>();
        _businessServiceMock = new Mock<IBusinessService>();
        _sharingServiceMock = new Mock<IInvoiceSharingService>();
        _acceptanceServiceMock = new Mock<IInvoiceAcceptanceService>();
        _duplicationServiceMock = new Mock<IDocumentDuplicationService>();
        _softDeleteServiceMock = new Mock<IDocumentSoftDeleteService>();
        _viewRenderServiceMock = new Mock<IViewRenderService>();
        _invoicePdfServiceMock = new Mock<IInvoicePdfService>();
        _loggerMock = new Mock<ILogger<InvoiceController>>();

        // Concrete repositories need a DbContext — use a mock DbContext
        var dbContextOptions = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new DbContext(dbContextOptions);

        var paymentDetailRepository = new BusinessPaymentDetailRepository(dbContext);
        var vatPeriodRepository = new VatSubmissionPeriodRepository(dbContext);

        // Set up common defaults for the Detail action path
        _tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(1);

        _businessServiceMock
            .Setup(s => s.GetBusinessProfileAsync(1))
            .ReturnsAsync(new BusinessProfile { Id = 1, BusinessId = 1, CurrencySymbol = "€" });

        var portalOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"Portal_InvCtrlAccept_{Guid.NewGuid()}")
            .Options;
        var portalDbContext = new PortalDbContext(portalOptions, _tenantServiceMock.Object);

        _controller = new InvoiceController(
            _invoiceServiceMock.Object,
            _sectionServiceMock.Object,
            _tenantServiceMock.Object,
            _customerServiceMock.Object,
            _logoServiceMock.Object,
            _businessServiceMock.Object,
            paymentDetailRepository,
            _sharingServiceMock.Object,
            _acceptanceServiceMock.Object,
            _duplicationServiceMock.Object,
            _softDeleteServiceMock.Object,
            vatPeriodRepository,
            _viewRenderServiceMock.Object,
            _invoicePdfServiceMock.Object,
            Mock.Of<IPaymentInstructionsService>(),
            Mock.Of<IPlanCheckService>(),
            Mock.Of<IPermissionService>(),
            Mock.Of<IProductPriceTierService>(),
            new ProductRepository(portalDbContext),
            portalDbContext,
            _loggerMock.Object);

        // Set up TempData to avoid null reference exceptions
        _controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), Mock.Of<ITempDataProvider>());

        // Set up ControllerContext with an authenticated user
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, "test-user-id") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var user = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = user };
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private void SetupInvoiceDefaults(int invoiceId)
    {
        var invoice = new Invoice
        {
            Id = invoiceId,
            BusinessId = 1,
            CustomerId = 1,
            InvoiceNumber = "INV-001",
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            InvoiceStatusTypeId = 2,
            InvoiceFinancialStatusTypeId = 1,
            CurrencyCode = "EUR",
            Subtotal = 100m,
            TaxAmount = 21m,
            TotalAmount = 121m,
            VatSubmissionPeriodId = null
        };

        _invoiceServiceMock
            .Setup(s => s.GetInvoiceByIdAsync(invoiceId))
            .ReturnsAsync(invoice);

        _invoiceServiceMock
            .Setup(s => s.GetInvoiceLinesAsync(invoiceId))
            .ReturnsAsync(new List<InvoiceLine>());

        _sectionServiceMock
            .Setup(s => s.GetByInvoiceIdAsync(invoiceId))
            .ReturnsAsync(new List<InvoiceSection>());

        _customerServiceMock
            .Setup(s => s.GetCustomerByIdAsync(1))
            .ReturnsAsync(new Customer { Id = 1, Name = "Test Customer", Email = "test@example.com" });
    }

    #region Requirement 4.1 — AcceptanceStatus = "accepted" when acceptance exists

    [Fact]
    public async Task Detail_SetsAcceptanceStatusToAccepted_WhenAcceptanceExists()
    {
        // Arrange
        const int invoiceId = 10;
        SetupInvoiceDefaults(invoiceId);

        var activeShare = new InvoiceShare
        {
            Id = 5,
            InvoiceId = invoiceId,
            BusinessId = 1,
            ShareToken = "token-abc",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "test@example.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedByUserId = "user-1",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var acceptance = new InvoiceAcceptance
        {
            Id = 1,
            InvoiceShareId = 5,
            AcceptedTerms = "I accept this invoice as correct and agree to pay by the due date.",
            AcceptedAtUtc = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero),
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _sharingServiceMock
            .Setup(s => s.GetActiveShareByInvoiceIdAsync(invoiceId))
            .ReturnsAsync(activeShare);

        _acceptanceServiceMock
            .Setup(s => s.GetByInvoiceShareIdAsync(5))
            .ReturnsAsync(acceptance);

        // Act
        var result = await _controller.Detail(invoiceId) as ViewResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("accepted", _controller.ViewBag.AcceptanceStatus as string);
        Assert.Equal(acceptance.AcceptedAtUtc, (DateTimeOffset)_controller.ViewBag.AcceptedAtUtc);
    }

    #endregion

    #region Requirement 4.2 — AcceptanceStatus = "awaiting" when share exists but no acceptance

    [Fact]
    public async Task Detail_SetsAcceptanceStatusToAwaiting_WhenShareExistsButNoAcceptance()
    {
        // Arrange
        const int invoiceId = 20;
        SetupInvoiceDefaults(invoiceId);

        var activeShare = new InvoiceShare
        {
            Id = 8,
            InvoiceId = invoiceId,
            BusinessId = 1,
            ShareToken = "token-xyz",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@example.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedByUserId = "user-1",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2)
        };

        _sharingServiceMock
            .Setup(s => s.GetActiveShareByInvoiceIdAsync(invoiceId))
            .ReturnsAsync(activeShare);

        _acceptanceServiceMock
            .Setup(s => s.GetByInvoiceShareIdAsync(8))
            .ReturnsAsync((InvoiceAcceptance?)null);

        // Act
        var result = await _controller.Detail(invoiceId) as ViewResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal("awaiting", _controller.ViewBag.AcceptanceStatus as string);
    }

    #endregion

    #region Requirement 4.3 — AcceptanceStatus = null when no active share

    [Fact]
    public async Task Detail_SetsAcceptanceStatusToNull_WhenNoActiveShare()
    {
        // Arrange
        const int invoiceId = 30;
        SetupInvoiceDefaults(invoiceId);

        _sharingServiceMock
            .Setup(s => s.GetActiveShareByInvoiceIdAsync(invoiceId))
            .ReturnsAsync((InvoiceShare?)null);

        // Act
        var result = await _controller.Detail(invoiceId) as ViewResult;

        // Assert
        Assert.NotNull(result);
        Assert.Null(_controller.ViewBag.AcceptanceStatus);
    }

    #endregion
}
