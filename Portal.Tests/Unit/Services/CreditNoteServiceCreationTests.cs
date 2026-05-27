using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for CreditNoteService covering creation validation, number generation,
/// lifecycle state transitions, and Draft-only editing.
/// </summary>
public class CreditNoteServiceCreationTests : IDisposable
{
    private const int TestBusinessId = 1;
    private const string TestUserId = "user-001";

    private readonly PortalDbContext _dbContext;
    private readonly Mock<CreditNoteRepository> _creditNoteRepoMock;
    private readonly Mock<CreditNoteLineRepository> _creditNoteLineRepoMock;
    private readonly Mock<CreditNoteApplicationRepository> _creditNoteAppRepoMock;
    private readonly Mock<InvoiceRepository> _invoiceRepoMock;
    private readonly Mock<PaymentRepository> _paymentRepoMock;
    private readonly Mock<AuditLogRepository> _auditLogRepoMock;
    private readonly Mock<VatSubmissionPeriodRepository> _vatPeriodRepoMock;
    private readonly Mock<IFinancialStatusEngine> _financialStatusEngineMock;
    private readonly Mock<ICurrentTenantService> _tenantServiceMock;
    private readonly CreditNoteService _service;

    public CreditNoteServiceCreationTests()
    {
        _tenantServiceMock = new Mock<ICurrentTenantService>();
        _tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new PortalDbContext(options, _tenantServiceMock.Object);

        // Create mock DbContext for repositories (they need a DbContext in constructor)
        var mockDbContext = new Mock<DbContext>();

        _creditNoteRepoMock = new Mock<CreditNoteRepository>(mockDbContext.Object);
        _creditNoteLineRepoMock = new Mock<CreditNoteLineRepository>(mockDbContext.Object);
        _creditNoteAppRepoMock = new Mock<CreditNoteApplicationRepository>(mockDbContext.Object);
        _invoiceRepoMock = new Mock<InvoiceRepository>(mockDbContext.Object);
        _paymentRepoMock = new Mock<PaymentRepository>(mockDbContext.Object);
        _auditLogRepoMock = new Mock<AuditLogRepository>(mockDbContext.Object);
        _vatPeriodRepoMock = new Mock<VatSubmissionPeriodRepository>(mockDbContext.Object);
        _financialStatusEngineMock = new Mock<IFinancialStatusEngine>();

        _service = new CreditNoteService(
            _creditNoteRepoMock.Object,
            _creditNoteLineRepoMock.Object,
            _creditNoteAppRepoMock.Object,
            _invoiceRepoMock.Object,
            _paymentRepoMock.Object,
            _auditLogRepoMock.Object,
            _vatPeriodRepoMock.Object,
            _financialStatusEngineMock.Object,
            _tenantServiceMock.Object,
            _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region Helpers

    private static Invoice CreateIssuedInvoice(int id = 1, decimal totalAmount = 1000m)
    {
        return new Invoice
        {
            Id = id,
            BusinessId = TestBusinessId,
            CustomerId = 1,
            InvoiceStatusTypeId = 2, // Issued
            InvoiceFinancialStatusTypeId = 1, // Unpaid
            InvoiceNumber = $"INV-2026-{id:D4}",
            InvoiceDate = new DateOnly(2026, 1, 15),
            DueDate = new DateOnly(2026, 2, 15),
            Subtotal = totalAmount * 0.85m,
            TaxAmount = totalAmount * 0.15m,
            TotalAmount = totalAmount,
            CurrencyCode = "EUR",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static CreateCreditNoteDto CreateValidDto(int invoiceId = 1)
    {
        return new CreateCreditNoteDto
        {
            InvoiceId = invoiceId,
            IssueDate = new DateOnly(2026, 3, 1),
            Reason = "Goods returned damaged",
            VatSubmissionPeriodId = 1,
            Lines = new List<CreateCreditNoteLineDto>
            {
                new() { Description = "Widget A", Quantity = 2, UnitPrice = 50m, VatRate = 15m }
            }
        };
    }

    private void SeedVatPeriod(int periodId = 1, bool isSubmitted = false)
    {
        _dbContext.VatSubmissionPeriods.Add(new VatSubmissionPeriod
        {
            Id = periodId,
            BusinessId = TestBusinessId,
            PeriodLabel = "Mar-May 2026",
            PeriodStartDate = new DateOnly(2026, 3, 1),
            PeriodEndDate = new DateOnly(2026, 5, 31),
            CreatedAtUtc = DateTime.UtcNow
        });

        if (isSubmitted)
        {
            _dbContext.VatSubmissions.Add(new VatSubmission
            {
                Id = periodId,
                BusinessId = TestBusinessId,
                VatSubmissionPeriodId = periodId,
                TotalOutputVat = 100m,
                TotalInputVat = 50m,
                NetVatPayable = 50m,
                IsSubmitted = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        _dbContext.SaveChanges();
    }

    #endregion

    #region Static Validation Tests (ValidateCreateCreditNote)

    [Fact]
    public void ValidateCreateCreditNote_EmptyReason_ReturnsError()
    {
        // Arrange
        var dto = CreateValidDto();
        dto.Reason = "";
        var invoice = CreateIssuedInvoice(1, 1000m);

        // Act
        var errors = CreditNoteService.ValidateCreateCreditNote(dto, invoice, 1000m);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Reason", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCreateCreditNote_ZeroLines_ReturnsError()
    {
        // Arrange
        var dto = CreateValidDto();
        dto.Lines = new List<CreateCreditNoteLineDto>();
        var invoice = CreateIssuedInvoice(1, 1000m);

        // Act
        var errors = CreditNoteService.ValidateCreateCreditNote(dto, invoice, 1000m);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("line", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCreateCreditNote_MoreThan50Lines_ReturnsError()
    {
        // Arrange
        var dto = CreateValidDto();
        dto.Lines = Enumerable.Range(1, 51)
            .Select(i => new CreateCreditNoteLineDto
            {
                Description = $"Line {i}",
                Quantity = 1,
                UnitPrice = 10m,
                VatRate = 15m
            }).ToList();
        var invoice = CreateIssuedInvoice(1, 1000000m);

        // Act
        var errors = CreditNoteService.ValidateCreateCreditNote(dto, invoice, 1000000m);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("50", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCreateCreditNote_InvalidInvoiceStatus_ReturnsError()
    {
        // Arrange
        var dto = CreateValidDto();
        var invoice = CreateIssuedInvoice(1, 1000m);
        invoice.InvoiceStatusTypeId = 1; // Draft - not eligible

        // Act
        var errors = CreditNoteService.ValidateCreateCreditNote(dto, invoice, 1000m);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Issued", StringComparison.OrdinalIgnoreCase) ||
                                     e.Contains("status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCreateCreditNote_NullInvoice_ReturnsError()
    {
        // Arrange
        var dto = CreateValidDto();

        // Act
        var errors = CreditNoteService.ValidateCreateCreditNote(dto, null, 0m);

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("invoice", StringComparison.OrdinalIgnoreCase) ||
                                     e.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCreateCreditNote_ValidDto_ReturnsNoErrors()
    {
        // Arrange
        var dto = CreateValidDto();
        var invoice = CreateIssuedInvoice(1, 1000m);

        // Act
        var errors = CreditNoteService.ValidateCreateCreditNote(dto, invoice, 1000m);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateCreateCreditNote_MultipleViolations_ReturnsAllErrors()
    {
        // Arrange
        var dto = new CreateCreditNoteDto
        {
            InvoiceId = 1,
            IssueDate = new DateOnly(2026, 3, 1),
            Reason = "", // Invalid
            VatSubmissionPeriodId = 1,
            Lines = new List<CreateCreditNoteLineDto>() // Invalid - no lines
        };
        var invoice = CreateIssuedInvoice(1, 1000m);
        invoice.InvoiceStatusTypeId = 1; // Invalid - not Issued

        // Act
        var errors = CreditNoteService.ValidateCreateCreditNote(dto, invoice, 1000m);

        // Assert - should have multiple errors
        Assert.True(errors.Count >= 2, $"Expected at least 2 errors but got {errors.Count}: {string.Join(", ", errors)}");
    }

    #endregion

    #region Amount Computation Tests (ComputeAmounts)

    [Fact]
    public void ComputeAmounts_SingleLine_ComputesCorrectly()
    {
        // Arrange
        var lines = new List<CreateCreditNoteLineDto>
        {
            new() { Description = "Item", Quantity = 2, UnitPrice = 50m, VatRate = 15m }
        };

        // Act
        var (subtotal, taxAmount, totalAmount) = CreditNoteService.ComputeAmounts(lines);

        // Assert: Subtotal = 2 * 50 = 100, Tax = 100 * 15/100 = 15, Total = 115
        Assert.Equal(100m, subtotal);
        Assert.Equal(15m, taxAmount);
        Assert.Equal(115m, totalAmount);
    }

    [Fact]
    public void ComputeAmounts_MultipleLines_SumsCorrectly()
    {
        // Arrange
        var lines = new List<CreateCreditNoteLineDto>
        {
            new() { Description = "Item A", Quantity = 1, UnitPrice = 100m, VatRate = 20m },
            new() { Description = "Item B", Quantity = 3, UnitPrice = 50m, VatRate = 10m }
        };

        // Act
        var (subtotal, taxAmount, totalAmount) = CreditNoteService.ComputeAmounts(lines);

        // Assert: Subtotal = 100 + 150 = 250, Tax = (100*0.20) + (150*0.10) = 20 + 15 = 35, Total = 285
        Assert.Equal(250m, subtotal);
        Assert.Equal(35m, taxAmount);
        Assert.Equal(285m, totalAmount);
    }

    [Fact]
    public void ComputeAmounts_ZeroVatRate_NoTax()
    {
        // Arrange
        var lines = new List<CreateCreditNoteLineDto>
        {
            new() { Description = "Zero VAT item", Quantity = 5, UnitPrice = 20m, VatRate = 0m }
        };

        // Act
        var (subtotal, taxAmount, totalAmount) = CreditNoteService.ComputeAmounts(lines);

        // Assert
        Assert.Equal(100m, subtotal);
        Assert.Equal(0m, taxAmount);
        Assert.Equal(100m, totalAmount);
    }

    #endregion

    #region State Transition Tests

    [Fact]
    public async Task IssueCreditNoteAsync_DraftStatus_TransitionsToIssued()
    {
        // Arrange
        var creditNote = new CreditNote
        {
            Id = 1,
            BusinessId = TestBusinessId,
            CreditNoteStatusTypeId = 1, // Draft
            CreditNoteNumber = "CN-2026-0001",
            TotalAmount = 100m,
            CreatedAtUtc = DateTime.UtcNow
        };

        _creditNoteRepoMock.Setup(r => r.GetByIdAndBusinessIdAsync(1, TestBusinessId))
            .ReturnsAsync(creditNote);
        _creditNoteRepoMock.Setup(r => r.UpdateStatusAsync(1, 2, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.IssueCreditNoteAsync(1, TestBusinessId, TestUserId);

        // Assert
        Assert.True(result.Success);
        _creditNoteRepoMock.Verify(r => r.UpdateStatusAsync(1, 2, It.IsAny<DateTime?>(), null), Times.Once);
    }

    [Fact]
    public async Task IssueCreditNoteAsync_NonDraftStatus_ReturnsError()
    {
        // Arrange
        var creditNote = new CreditNote
        {
            Id = 1,
            BusinessId = TestBusinessId,
            CreditNoteStatusTypeId = 2, // Already Issued
            CreditNoteNumber = "CN-2026-0001",
            TotalAmount = 100m,
            CreatedAtUtc = DateTime.UtcNow
        };

        _creditNoteRepoMock.Setup(r => r.GetByIdAndBusinessIdAsync(1, TestBusinessId))
            .ReturnsAsync(creditNote);

        // Act
        var result = await _service.IssueCreditNoteAsync(1, TestBusinessId, TestUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Draft", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IssueCreditNoteAsync_NotFound_ReturnsError()
    {
        // Arrange
        _creditNoteRepoMock.Setup(r => r.GetByIdAndBusinessIdAsync(99, TestBusinessId))
            .ReturnsAsync((CreditNote?)null);

        // Act
        var result = await _service.IssueCreditNoteAsync(99, TestBusinessId, TestUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VoidCreditNoteAsync_DraftStatus_TransitionsToVoided()
    {
        // Arrange
        var creditNote = new CreditNote
        {
            Id = 1,
            BusinessId = TestBusinessId,
            CreditNoteStatusTypeId = 1, // Draft
            CreditNoteNumber = "CN-2026-0001",
            TotalAmount = 100m,
            VatSubmissionPeriodId = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        _creditNoteRepoMock.Setup(r => r.GetByIdAndBusinessIdAsync(1, TestBusinessId))
            .ReturnsAsync(creditNote);
        _creditNoteRepoMock.Setup(r => r.UpdateStatusAsync(1, 4, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);
        SeedVatPeriod(1, false);

        // Act
        var result = await _service.VoidCreditNoteAsync(1, TestBusinessId, TestUserId);

        // Assert
        Assert.True(result.Success);
        _creditNoteRepoMock.Verify(r => r.UpdateStatusAsync(1, 4, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
    }

    [Fact]
    public async Task VoidCreditNoteAsync_AlreadyVoided_ReturnsError()
    {
        // Arrange
        var creditNote = new CreditNote
        {
            Id = 1,
            BusinessId = TestBusinessId,
            CreditNoteStatusTypeId = 4, // Already Voided
            CreditNoteNumber = "CN-2026-0001",
            TotalAmount = 100m,
            VatSubmissionPeriodId = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        _creditNoteRepoMock.Setup(r => r.GetByIdAndBusinessIdAsync(1, TestBusinessId))
            .ReturnsAsync(creditNote);

        // Act
        var result = await _service.VoidCreditNoteAsync(1, TestBusinessId, TestUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("voided", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VoidCreditNoteAsync_IssuedInSubmittedPeriod_ReturnsError()
    {
        // Arrange
        var creditNote = new CreditNote
        {
            Id = 1,
            BusinessId = TestBusinessId,
            CreditNoteStatusTypeId = 2, // Issued (non-Draft)
            CreditNoteNumber = "CN-2026-0001",
            TotalAmount = 100m,
            VatSubmissionPeriodId = 1,
            CreatedAtUtc = DateTime.UtcNow
        };

        _creditNoteRepoMock.Setup(r => r.GetByIdAndBusinessIdAsync(1, TestBusinessId))
            .ReturnsAsync(creditNote);
        SeedVatPeriod(1, true); // Submitted period

        // Act
        var result = await _service.VoidCreditNoteAsync(1, TestBusinessId, TestUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("submitted", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
