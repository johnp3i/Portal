using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for invoice edit-eligibility (feature: editable issued-but-unpaid invoices, Option A).
/// Verifies GetEditEligibilityAsync across all branches and the settlement/VAT-filed guards.
/// </summary>
public class InvoiceEditEligibilityTests
{
    private const int TestBusinessId = 1;
    private const int DraftStatus = 1;
    private const int IssuedStatus = 2;
    private const int CancelledStatus = 3;

    private readonly Mock<ICurrentTenantService> _tenantMock;
    private readonly PortalDbContext _dbContext;
    private readonly Mock<InvoiceRepository> _invoiceRepoMock;
    private readonly Mock<VatSubmissionPeriodRepository> _vatPeriodRepoMock;
    private readonly Mock<VatSubmissionRepository> _vatSubmissionRepoMock;
    private readonly Mock<CreditNoteRepository> _creditNoteRepoMock;
    private readonly Mock<IVatSubmissionService> _vatSubmissionServiceMock;

    public InvoiceEditEligibilityTests()
    {
        _tenantMock = new Mock<ICurrentTenantService>();
        _tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"InvoiceEditEligibility_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new PortalDbContext(options, _tenantMock.Object);

        var dbCtxForRepos = new Mock<PortalDbContext>(options, _tenantMock.Object) { CallBase = true };
        _invoiceRepoMock = new Mock<InvoiceRepository>(dbCtxForRepos.Object) { CallBase = false };
        _vatPeriodRepoMock = new Mock<VatSubmissionPeriodRepository>(dbCtxForRepos.Object) { CallBase = false };
        _vatSubmissionRepoMock = new Mock<VatSubmissionRepository>(dbCtxForRepos.Object) { CallBase = false };
        _creditNoteRepoMock = new Mock<CreditNoteRepository>(dbCtxForRepos.Object) { CallBase = false };
        _vatSubmissionServiceMock = new Mock<IVatSubmissionService>();

        // Default: no applied credit notes.
        _creditNoteRepoMock
            .Setup(r => r.GetTotalAppliedCreditAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(0m);
    }

    private InvoiceService CreateService()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"InvoiceEditEligibility_Repos_{Guid.NewGuid()}")
            .Options;
        var dbCtxMock = new Mock<PortalDbContext>(options, _tenantMock.Object) { CallBase = true };

        return new InvoiceService(
            _tenantMock.Object,
            _invoiceRepoMock.Object,
            new Mock<InvoiceLineRepository>(dbCtxMock.Object) { CallBase = false }.Object,
            new Mock<InvoiceSectionRepository>(dbCtxMock.Object) { CallBase = false }.Object,
            new Mock<QuotationRepository>(dbCtxMock.Object) { CallBase = false }.Object,
            new Mock<QuotationLineRepository>(dbCtxMock.Object) { CallBase = false }.Object,
            new Mock<ProposalSectionRepository>(dbCtxMock.Object) { CallBase = false }.Object,
            new Mock<CustomerRepository>(dbCtxMock.Object) { CallBase = false }.Object,
            new Mock<AuditLogRepository>(dbCtxMock.Object) { CallBase = false }.Object,
            _vatPeriodRepoMock.Object,
            _vatSubmissionRepoMock.Object,
            _creditNoteRepoMock.Object,
            _vatSubmissionServiceMock.Object,
            _dbContext,
            new Mock<IProductService>().Object,
            new Mock<ProductRepository>(dbCtxMock.Object).Object,
            new Mock<ProductPriceTierRepository>(dbCtxMock.Object) { CallBase = false }.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<ILogger<InvoiceService>>().Object);
    }

    private void SetupInvoice(Invoice invoice)
    {
        _invoiceRepoMock
            .Setup(r => r.GetByIdAndBusinessIdAsync(invoice.Id, TestBusinessId))
            .ReturnsAsync(invoice);
    }

    private static Invoice MakeInvoice(int id, int status, int? vatPeriodId = null) => new()
    {
        Id = id,
        BusinessId = TestBusinessId,
        InvoiceStatusTypeId = status,
        InvoiceFinancialStatusTypeId = 1,
        VatSubmissionPeriodId = vatPeriodId
    };

    private void SeedPayment(int invoiceId, decimal amount, bool voided)
    {
        _dbContext.Payments.Add(new Payment
        {
            BusinessId = TestBusinessId,
            InvoiceId = invoiceId,
            Amount = amount,
            IsVoided = voided,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow
        });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task Draft_Invoice_Is_Editable()
    {
        var invoice = MakeInvoice(10, DraftStatus);
        SetupInvoice(invoice);

        var result = await CreateService().GetEditEligibilityAsync(10);

        Assert.True(result.CanEdit);
        Assert.False(result.IsIssued);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task Issued_Unpaid_Unfiled_Invoice_Is_Editable()
    {
        var invoice = MakeInvoice(11, IssuedStatus, vatPeriodId: null);
        SetupInvoice(invoice);

        var result = await CreateService().GetEditEligibilityAsync(11);

        Assert.True(result.CanEdit);
        Assert.True(result.IsIssued);
    }

    [Fact]
    public async Task Issued_With_NonVoided_Payment_Is_Not_Editable()
    {
        var invoice = MakeInvoice(12, IssuedStatus);
        SetupInvoice(invoice);
        SeedPayment(12, 50m, voided: false);

        var result = await CreateService().GetEditEligibilityAsync(12);

        Assert.False(result.CanEdit);
        Assert.True(result.IsIssued);
        Assert.Contains("payment", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Issued_With_Only_Voided_Payment_Is_Editable()
    {
        var invoice = MakeInvoice(13, IssuedStatus);
        SetupInvoice(invoice);
        SeedPayment(13, 50m, voided: true); // voided → ignored

        var result = await CreateService().GetEditEligibilityAsync(13);

        Assert.True(result.CanEdit);
    }

    [Fact]
    public async Task Issued_With_Applied_CreditNote_Is_Not_Editable()
    {
        var invoice = MakeInvoice(14, IssuedStatus);
        SetupInvoice(invoice);
        _creditNoteRepoMock
            .Setup(r => r.GetTotalAppliedCreditAsync(14, TestBusinessId))
            .ReturnsAsync(25m);

        var result = await CreateService().GetEditEligibilityAsync(14);

        Assert.False(result.CanEdit);
        Assert.Contains("credit note", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Issued_With_Submitted_Vat_Period_Is_Not_Editable()
    {
        var invoice = MakeInvoice(15, IssuedStatus, vatPeriodId: 99);
        SetupInvoice(invoice);
        _vatSubmissionRepoMock
            .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(99, TestBusinessId))
            .ReturnsAsync(new VatSubmission { Id = 1, BusinessId = TestBusinessId, VatSubmissionPeriodId = 99, IsSubmitted = true });

        var result = await CreateService().GetEditEligibilityAsync(15);

        Assert.False(result.CanEdit);
        Assert.Contains("VAT period", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Issued_With_Unsubmitted_Vat_Period_Is_Editable()
    {
        var invoice = MakeInvoice(16, IssuedStatus, vatPeriodId: 88);
        SetupInvoice(invoice);
        _vatSubmissionRepoMock
            .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(88, TestBusinessId))
            .ReturnsAsync(new VatSubmission { Id = 2, BusinessId = TestBusinessId, VatSubmissionPeriodId = 88, IsSubmitted = false });

        var result = await CreateService().GetEditEligibilityAsync(16);

        Assert.True(result.CanEdit);
    }

    [Fact]
    public async Task Cancelled_Invoice_Is_Not_Editable()
    {
        var invoice = MakeInvoice(17, CancelledStatus);
        SetupInvoice(invoice);

        var result = await CreateService().GetEditEligibilityAsync(17);

        Assert.False(result.CanEdit);
        Assert.Contains("Cancelled", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_Invoice_Is_Not_Editable()
    {
        _invoiceRepoMock
            .Setup(r => r.GetByIdAndBusinessIdAsync(999, TestBusinessId))
            .ReturnsAsync((Invoice?)null);

        var result = await CreateService().GetEditEligibilityAsync(999);

        Assert.False(result.CanEdit);
        Assert.Contains("not found", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }
}
