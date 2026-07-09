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
/// Unit tests for InvoiceController.AxGetDownloadPdf action.
/// Validates: PDF download endpoint behavior for various scenarios.
/// </summary>
public class InvoiceControllerDownloadPdfTests
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

    public InvoiceControllerDownloadPdfTests()
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

        var dbContextOptions = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new DbContext(dbContextOptions);

        var paymentDetailRepository = new BusinessPaymentDetailRepository(dbContext);
        var vatPeriodRepository = new VatSubmissionPeriodRepository(dbContext);

        _tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(1);

        var portalOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"Portal_InvCtrlPdf_{Guid.NewGuid()}")
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
            portalDbContext,
            _loggerMock.Object);

        _controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task AxGetDownloadPdf_InvoiceNotFound_ReturnsNotFound()
    {
        // Arrange
        _invoiceServiceMock.Setup(s => s.GetInvoiceByIdAsync(999)).ReturnsAsync((Invoice?)null);

        // Act
        var result = await _controller.AxGetDownloadPdf(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AxGetDownloadPdf_WrongBusiness_ReturnsNotFound()
    {
        // Arrange: invoice belongs to business 2, but tenant is business 1
        var invoice = new Invoice
        {
            Id = 10,
            BusinessId = 2,
            InvoiceNumber = "1-00010",
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30))
        };
        _invoiceServiceMock.Setup(s => s.GetInvoiceByIdAsync(10)).ReturnsAsync(invoice);

        // Act
        var result = await _controller.AxGetDownloadPdf(10);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AxGetDownloadPdf_TimeoutException_Returns500Json()
    {
        // Arrange: valid invoice, service throws OperationCanceledException
        var invoice = new Invoice
        {
            Id = 5,
            BusinessId = 1,
            InvoiceNumber = "1-00005",
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30))
        };
        _invoiceServiceMock.Setup(s => s.GetInvoiceByIdAsync(5)).ReturnsAsync(invoice);
        _invoicePdfServiceMock
            .Setup(s => s.GenerateAsync(5, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _controller.AxGetDownloadPdf(5);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);

        var value = objectResult.Value;
        var successProp = value!.GetType().GetProperty("success");
        var messageProp = value.GetType().GetProperty("message");

        Assert.NotNull(successProp);
        Assert.NotNull(messageProp);
        Assert.False((bool)successProp.GetValue(value)!);
        Assert.Contains("timed out", (string)messageProp.GetValue(value)!);
    }

    [Fact]
    public async Task AxGetDownloadPdf_GenericException_Returns500Json()
    {
        // Arrange: valid invoice, service throws a generic exception with internal details
        var invoice = new Invoice
        {
            Id = 7,
            BusinessId = 1,
            InvoiceNumber = "1-00007",
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30))
        };
        _invoiceServiceMock.Setup(s => s.GetInvoiceByIdAsync(7)).ReturnsAsync(invoice);
        _invoicePdfServiceMock
            .Setup(s => s.GenerateAsync(7, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("internal error details that should not be exposed"));

        // Act
        var result = await _controller.AxGetDownloadPdf(7);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);

        var value = objectResult.Value;
        var successProp = value!.GetType().GetProperty("success");
        var messageProp = value.GetType().GetProperty("message");

        Assert.NotNull(successProp);
        Assert.NotNull(messageProp);
        Assert.False((bool)successProp.GetValue(value)!);

        var message = (string)messageProp.GetValue(value)!;
        Assert.DoesNotContain("internal error details", message);
        Assert.Contains("Failed to generate PDF", message);
    }

    [Fact]
    public async Task AxGetDownloadPdf_Success_ReturnsFileResult()
    {
        // Arrange: valid invoice, service returns PDF bytes
        var invoice = new Invoice
        {
            Id = 3,
            BusinessId = 1,
            InvoiceNumber = "1-00090",
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30))
        };
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-

        _invoiceServiceMock.Setup(s => s.GetInvoiceByIdAsync(3)).ReturnsAsync(invoice);
        _invoicePdfServiceMock
            .Setup(s => s.GenerateAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfBytes);

        // Act
        var result = await _controller.AxGetDownloadPdf(3);

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal("INV-1-00090.pdf", fileResult.FileDownloadName);
        Assert.Equal(pdfBytes, fileResult.FileContents);
    }
}
