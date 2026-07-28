using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Services.Sales;
using Portal.Web.Controllers;
using Xunit;

namespace Portal.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for QuotationController.AxGetDownloadPdf action.
/// Validates: PDF download endpoint behavior for various scenarios.
/// </summary>
public class QuotationControllerDownloadPdfTests
{
    private readonly Mock<IQuotationService> _quotationServiceMock;
    private readonly Mock<ICustomerService> _customerServiceMock;
    private readonly Mock<IProposalService> _proposalServiceMock;
    private readonly Mock<ILogoService> _logoServiceMock;
    private readonly Mock<ICurrentTenantService> _tenantServiceMock;
    private readonly Mock<IProposalSectionService> _sectionServiceMock;
    private readonly Mock<IInvoiceService> _invoiceServiceMock;
    private readonly Mock<IBusinessService> _businessServiceMock;
    private readonly Mock<IDocumentDuplicationService> _duplicationServiceMock;
    private readonly Mock<IDocumentSoftDeleteService> _softDeleteServiceMock;
    private readonly Mock<IProposalAcceptanceService> _acceptanceServiceMock;
    private readonly Mock<IProposalPdfService> _proposalPdfServiceMock;
    private readonly Mock<ILogger<QuotationController>> _loggerMock;
    private readonly QuotationController _controller;

    public QuotationControllerDownloadPdfTests()
    {
        _quotationServiceMock = new Mock<IQuotationService>();
        _customerServiceMock = new Mock<ICustomerService>();
        _proposalServiceMock = new Mock<IProposalService>();
        _logoServiceMock = new Mock<ILogoService>();
        _tenantServiceMock = new Mock<ICurrentTenantService>();
        _sectionServiceMock = new Mock<IProposalSectionService>();
        _invoiceServiceMock = new Mock<IInvoiceService>();
        _businessServiceMock = new Mock<IBusinessService>();
        _duplicationServiceMock = new Mock<IDocumentDuplicationService>();
        _softDeleteServiceMock = new Mock<IDocumentSoftDeleteService>();
        _acceptanceServiceMock = new Mock<IProposalAcceptanceService>();
        _proposalPdfServiceMock = new Mock<IProposalPdfService>();
        _loggerMock = new Mock<ILogger<QuotationController>>();

        var dbContextOptions = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new DbContext(dbContextOptions);

        var contactRepository = new QuotationContactRepository(dbContext);
        var productRepository = new ProductRepository(dbContext);

        _tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(1);

        _controller = new QuotationController(
            _quotationServiceMock.Object,
            _customerServiceMock.Object,
            _proposalServiceMock.Object,
            _logoServiceMock.Object,
            _tenantServiceMock.Object,
            contactRepository,
            _sectionServiceMock.Object,
            _invoiceServiceMock.Object,
            _businessServiceMock.Object,
            _duplicationServiceMock.Object,
            _softDeleteServiceMock.Object,
            productRepository,
            _acceptanceServiceMock.Object,
            _proposalPdfServiceMock.Object,
            Mock.Of<ILeadRequestService>(),
            _loggerMock.Object);

        _controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task AxGetDownloadPdf_QuotationNotFound_ReturnsNotFound()
    {
        // Arrange
        _quotationServiceMock.Setup(s => s.GetQuotationByIdAsync(999)).ReturnsAsync((Quotation?)null);

        // Act
        var result = await _controller.AxGetDownloadPdf(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AxGetDownloadPdf_WrongBusiness_ReturnsNotFound()
    {
        // Arrange: quotation belongs to business 2, but tenant is business 1
        var quotation = new Quotation
        {
            Id = 10,
            BusinessId = 2,
            CustomerId = 1,
            Reference = "2025-00010",
            QuotationStatusTypeId = 2
        };
        _quotationServiceMock.Setup(s => s.GetQuotationByIdAsync(10)).ReturnsAsync(quotation);

        // Act
        var result = await _controller.AxGetDownloadPdf(10);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AxGetDownloadPdf_TimeoutException_Returns500Json()
    {
        // Arrange: valid quotation, service throws OperationCanceledException
        var quotation = new Quotation
        {
            Id = 5,
            BusinessId = 1,
            CustomerId = 1,
            Reference = "2025-00005",
            QuotationStatusTypeId = 2
        };
        _quotationServiceMock.Setup(s => s.GetQuotationByIdAsync(5)).ReturnsAsync(quotation);
        _logoServiceMock.Setup(s => s.GetByBusinessIdAsync(1)).ReturnsAsync(new List<BusinessLogo>());
        _proposalPdfServiceMock
            .Setup(s => s.GenerateAsync(5, It.IsAny<List<int>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
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
        // Arrange: valid quotation, service throws a generic exception with internal details
        var quotation = new Quotation
        {
            Id = 7,
            BusinessId = 1,
            CustomerId = 1,
            Reference = "2025-00007",
            QuotationStatusTypeId = 2
        };
        _quotationServiceMock.Setup(s => s.GetQuotationByIdAsync(7)).ReturnsAsync(quotation);
        _logoServiceMock.Setup(s => s.GetByBusinessIdAsync(1)).ReturnsAsync(new List<BusinessLogo>());
        _proposalPdfServiceMock
            .Setup(s => s.GenerateAsync(7, It.IsAny<List<int>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
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
        // Arrange: valid quotation, service returns PDF bytes
        var quotation = new Quotation
        {
            Id = 3,
            BusinessId = 1,
            CustomerId = 1,
            Reference = "2025-00042",
            QuotationStatusTypeId = 2
        };
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-

        _quotationServiceMock.Setup(s => s.GetQuotationByIdAsync(3)).ReturnsAsync(quotation);
        _logoServiceMock.Setup(s => s.GetByBusinessIdAsync(1)).ReturnsAsync(new List<BusinessLogo>());
        _proposalPdfServiceMock
            .Setup(s => s.GenerateAsync(3, It.IsAny<List<int>>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfBytes);

        // Act
        var result = await _controller.AxGetDownloadPdf(3);

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal("QUO-2025-00042.pdf", fileResult.FileDownloadName);
        Assert.Equal(pdfBytes, fileResult.FileContents);
    }
}
