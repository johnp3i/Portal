using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Services.Sales;
using Portal.Web.Controllers;
using Portal.Web.Models;
using Xunit;

namespace Portal.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for QuotationController acceptance status display on Detail and Index actions.
/// Validates: Requirements 4.1, 4.2, 4.3, 5.1, 5.2, 5.3
/// </summary>
public class QuotationControllerAcceptanceStatusTests
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
    private readonly QuotationController _controller;

    public QuotationControllerAcceptanceStatusTests()
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

        // Concrete repositories need a DbContext — use an in-memory DbContext
        var dbContextOptions = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var dbContext = new DbContext(dbContextOptions);

        var contactRepository = new QuotationContactRepository(dbContext);
        var productRepository = new ProductRepository(dbContext);

        // Set up common defaults
        _tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(1);

        _businessServiceMock
            .Setup(s => s.GetBusinessProfileAsync(1))
            .ReturnsAsync(new BusinessProfile { Id = 1, BusinessId = 1, CurrencySymbol = "€" });

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
            Mock.Of<IProposalPdfService>(),
            Mock.Of<ILeadRequestService>(),
            Mock.Of<IProductPriceTierService>(),
            Mock.Of<ILogger<QuotationController>>());

        // Set up TempData to avoid null reference exceptions
        _controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    private void SetupDetailDefaults(int quotationId)
    {
        var quotation = new Quotation
        {
            Id = quotationId,
            BusinessId = 1,
            CustomerId = 1,
            Reference = "QUO-001",
            QuotationStatusTypeId = 2,
            ValidUntil = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            Subtotal = 500m,
            TaxAmount = 115m,
            TotalAmount = 615m
        };

        _quotationServiceMock
            .Setup(s => s.GetQuotationByIdAsync(quotationId))
            .ReturnsAsync(quotation);

        _quotationServiceMock
            .Setup(s => s.GetQuotationLinesAsync(quotationId))
            .ReturnsAsync(new List<QuotationLine>());

        _quotationServiceMock
            .Setup(s => s.GetValidTransitions())
            .Returns(new Dictionary<int, List<int>>
            {
                { 2, new List<int> { 3, 5 } }
            });

        _quotationServiceMock
            .Setup(s => s.IsExpired(It.IsAny<Quotation>()))
            .Returns(false);

        _customerServiceMock
            .Setup(s => s.GetCustomerByIdAsync(1))
            .ReturnsAsync(new Customer { Id = 1, Name = "Test Customer", Email = "test@example.com" });

        _logoServiceMock
            .Setup(s => s.GetByBusinessIdAsync(1))
            .ReturnsAsync(new List<BusinessLogo>());
    }

    #region Requirement 4.1 — AcceptanceStatus = "accepted" when acceptance exists

    [Fact]
    public async Task Detail_SetsAcceptanceStatusToAccepted_WhenAcceptanceExists()
    {
        // Arrange
        const int quotationId = 10;
        SetupDetailDefaults(quotationId);

        var activeShare = new ProposalShare
        {
            Id = 5,
            QuotationId = quotationId,
            BusinessId = 1,
            ShareToken = "token-abc",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "test@example.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedByUserId = "user-1",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var acceptance = new ProposalAcceptance
        {
            Id = 1,
            ProposalShareId = 5,
            AcceptedTerms = "I accept this proposal and agree to proceed with the quoted work.",
            AcceptedAtUtc = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero),
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _proposalServiceMock
            .Setup(s => s.GetActiveShareByQuotationIdAsync(quotationId))
            .ReturnsAsync(activeShare);

        _acceptanceServiceMock
            .Setup(s => s.GetByProposalShareIdAsync(5))
            .ReturnsAsync(acceptance);

        // Act
        var result = await _controller.Detail(quotationId) as ViewResult;

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
        const int quotationId = 20;
        SetupDetailDefaults(quotationId);

        var activeShare = new ProposalShare
        {
            Id = 8,
            QuotationId = quotationId,
            BusinessId = 1,
            ShareToken = "token-xyz",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@example.com",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true,
            CreatedByUserId = "user-1",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2)
        };

        _proposalServiceMock
            .Setup(s => s.GetActiveShareByQuotationIdAsync(quotationId))
            .ReturnsAsync(activeShare);

        _acceptanceServiceMock
            .Setup(s => s.GetByProposalShareIdAsync(8))
            .ReturnsAsync((ProposalAcceptance?)null);

        // Act
        var result = await _controller.Detail(quotationId) as ViewResult;

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
        const int quotationId = 30;
        SetupDetailDefaults(quotationId);

        _proposalServiceMock
            .Setup(s => s.GetActiveShareByQuotationIdAsync(quotationId))
            .ReturnsAsync((ProposalShare?)null);

        // Act
        var result = await _controller.Detail(quotationId) as ViewResult;

        // Assert
        Assert.NotNull(result);
        Assert.Null(_controller.ViewBag.AcceptanceStatus);
    }

    #endregion

    #region Requirements 5.1, 5.2, 5.3 — Index populates AcceptanceStatus on QuotationListDto

    [Fact]
    public async Task Index_PopulatesAcceptanceStatusCorrectly_ForAllThreeStates()
    {
        // Arrange — three quotations: one accepted, one awaiting, one with no share
        var items = new List<QuotationListDto>
        {
            new() { Id = 1, Reference = "QUO-001", CustomerName = "Customer A", StatusName = "Sent", QuotationStatusTypeId = 2, TotalAmount = 100m },
            new() { Id = 2, Reference = "QUO-002", CustomerName = "Customer B", StatusName = "Sent", QuotationStatusTypeId = 2, TotalAmount = 200m },
            new() { Id = 3, Reference = "QUO-003", CustomerName = "Customer C", StatusName = "Draft", QuotationStatusTypeId = 1, TotalAmount = 300m }
        };

        var pagedResult = new PagedResult<QuotationListDto>
        {
            Items = items,
            CurrentPage = 1,
            PageSize = 10,
            TotalCount = 3
        };

        _quotationServiceMock
            .Setup(s => s.GetQuotationsPagedAsync(
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync(pagedResult);

        _customerServiceMock
            .Setup(s => s.GetCustomersAsync(null, true))
            .ReturnsAsync(new List<Customer>());

        // Quotation 1 has an active share (Id=10) that is accepted
        _proposalServiceMock
            .Setup(s => s.GetActiveShareByQuotationIdAsync(1))
            .ReturnsAsync(new ProposalShare
            {
                Id = 10,
                QuotationId = 1,
                BusinessId = 1,
                ShareToken = "token-1",
                SnapshotHtml = "<html></html>",
                CustomerEmail = "a@example.com",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
                IsActive = true,
                CreatedByUserId = "user-1",
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-3)
            });

        // Quotation 2 has an active share (Id=20) that is NOT accepted (awaiting)
        _proposalServiceMock
            .Setup(s => s.GetActiveShareByQuotationIdAsync(2))
            .ReturnsAsync(new ProposalShare
            {
                Id = 20,
                QuotationId = 2,
                BusinessId = 1,
                ShareToken = "token-2",
                SnapshotHtml = "<html></html>",
                CustomerEmail = "b@example.com",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
                IsActive = true,
                CreatedByUserId = "user-1",
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2)
            });

        // Quotation 3 has no active share
        _proposalServiceMock
            .Setup(s => s.GetActiveShareByQuotationIdAsync(3))
            .ReturnsAsync((ProposalShare?)null);

        // Batch acceptance check: share 10 is accepted, share 20 is not
        _acceptanceServiceMock
            .Setup(s => s.GetAcceptedShareIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(new HashSet<int> { 10 });

        // Act
        var result = await _controller.Index(null, null, null, null, null, 1) as ViewResult;

        // Assert
        Assert.NotNull(result);
        var viewModel = result.Model as QuotationListViewModel;
        Assert.NotNull(viewModel);

        var quotation1 = viewModel.Quotations.First(q => q.Id == 1);
        var quotation2 = viewModel.Quotations.First(q => q.Id == 2);
        var quotation3 = viewModel.Quotations.First(q => q.Id == 3);

        Assert.Equal("accepted", quotation1.AcceptanceStatus);
        Assert.Equal("awaiting", quotation2.AcceptanceStatus);
        Assert.Null(quotation3.AcceptanceStatus);
    }

    #endregion
}
