using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 1: Payment validation rejects non-Issued invoices

/// <summary>
/// Property-based tests for PaymentService.RecordPaymentAsync validation.
/// Verifies that payment recording is rejected for any invoice that is not in Issued status (Id = 2).
/// **Validates: Requirements 1.1, 1.2**
/// </summary>
public class PaymentValidationNonIssuedPropertyTests
{
    // Invoice Status Type IDs
    private const int StatusDraft = 1;
    private const int StatusIssued = 2;
    private const int StatusCancelled = 3;

    /// <summary>
    /// Property 1: Payment validation rejects non-Issued invoices
    /// For any InvoiceStatusTypeId ≠ 2 (Issued), RecordPaymentAsync SHALL return failure
    /// and no Payment record SHALL be created.
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecordPayment_RejectsNonIssuedInvoice(
        PositiveInt invoiceIdSeed,
        PositiveInt businessIdSeed,
        PositiveInt statusSeed,
        PositiveInt amountSeed,
        PositiveInt paymentMethodSeed)
    {
        // Generate a non-Issued status (any value except 2)
        // Map to valid non-Issued statuses: 1 (Draft), 3 (Cancelled), or higher values
        var rawStatus = (statusSeed.Get % 10) + 1; // 1-10
        var invoiceStatusTypeId = rawStatus >= StatusIssued ? rawStatus + 1 : rawStatus; // Skip 2

        var invoiceId = (invoiceIdSeed.Get % 1000) + 1;
        var businessId = (businessIdSeed.Get % 100) + 1;
        var amount = Math.Round((amountSeed.Get % 10000 + 1) / 100m, 2);
        var paymentMethodTypeId = (paymentMethodSeed.Get % 5) + 1;

        // Create an invoice with the non-Issued status
        var invoice = new Invoice
        {
            Id = invoiceId,
            BusinessId = businessId,
            InvoiceStatusTypeId = invoiceStatusTypeId,
            InvoiceFinancialStatusTypeId = 1,
            TotalAmount = 1000m,
            InvoiceNumber = $"INV-{invoiceId:D5}",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            CurrencyCode = "EUR",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        // Mock InvoiceRepository
        var mockInvoiceRepo = new Mock<InvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        mockInvoiceRepo
            .Setup(r => r.GetByIdAndBusinessIdAsync(invoiceId, businessId))
            .ReturnsAsync(invoice);

        // Mock PaymentRepository
        var mockPaymentRepo = new Mock<PaymentRepository>(MockBehavior.Loose, new object[] { null! });
        mockPaymentRepo
            .Setup(r => r.InsertAsync(It.IsAny<Payment>()))
            .ReturnsAsync(1);
        mockPaymentRepo
            .Setup(r => r.GetTotalPaidAsync(invoiceId, businessId))
            .ReturnsAsync(0m);

        // Mock FinancialStatusEngine
        var mockStatusEngine = new Mock<IFinancialStatusEngine>();

        // Mock PortalDbContext (not used in this code path)
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        var mockTenantService = new Mock<ICurrentTenantService>();
        var dbContext = new PortalDbContext(options, mockTenantService.Object);

        // Create PaymentService with mocked dependencies
        var paymentService = new PaymentService(
            mockPaymentRepo.Object,
            mockInvoiceRepo.Object,
            new Mock<CreditNoteRepository>(null!).Object,
            mockStatusEngine.Object,
            new Mock<IPaymentScheduleService>().Object,
            new Mock<IPaymentAllocationEngine>().Object,
            dbContext);

        // Act
        var dto = new RecordPaymentDto
        {
            InvoiceId = invoiceId,
            PaymentMethodTypeId = paymentMethodTypeId,
            PaymentDateUtc = DateTime.UtcNow,
            Amount = amount,
            Reference = "REF-001",
            Notes = "Test payment"
        };

        var result = paymentService.RecordPaymentAsync(dto, businessId, "test-user-id").GetAwaiter().GetResult();

        // Assert: result should be failure
        var isFailure = !result.Success;

        // Assert: error message should indicate payments can only be recorded against issued invoices
        var hasCorrectMessage = result.Message != null &&
            result.Message.Contains("issued invoices", StringComparison.OrdinalIgnoreCase);

        // Assert: InsertAsync was never called (no payment created)
        var insertNeverCalled = true;
        try
        {
            mockPaymentRepo.Verify(r => r.InsertAsync(It.IsAny<Payment>()), Times.Never());
        }
        catch
        {
            insertNeverCalled = false;
        }

        // Clean up
        dbContext.Dispose();

        return (isFailure && hasCorrectMessage && insertNeverCalled).ToProperty()
            .Label($"statusId={invoiceStatusTypeId}, success={result.Success}, " +
                   $"message='{result.Message}', insertCalled={!insertNeverCalled}");
    }

    /// <summary>
    /// Specifically tests Draft status (Id = 1) is rejected.
    /// **Validates: Requirement 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecordPayment_RejectsDraftInvoice(
        PositiveInt invoiceIdSeed,
        PositiveInt businessIdSeed,
        PositiveInt amountSeed)
    {
        var invoiceId = (invoiceIdSeed.Get % 1000) + 1;
        var businessId = (businessIdSeed.Get % 100) + 1;
        var amount = Math.Round((amountSeed.Get % 10000 + 1) / 100m, 2);

        var invoice = new Invoice
        {
            Id = invoiceId,
            BusinessId = businessId,
            InvoiceStatusTypeId = StatusDraft,
            InvoiceFinancialStatusTypeId = 1,
            TotalAmount = 1000m,
            InvoiceNumber = $"INV-{invoiceId:D5}",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            CurrencyCode = "EUR",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var mockInvoiceRepo = new Mock<InvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        mockInvoiceRepo
            .Setup(r => r.GetByIdAndBusinessIdAsync(invoiceId, businessId))
            .ReturnsAsync(invoice);

        var mockPaymentRepo = new Mock<PaymentRepository>(MockBehavior.Loose, new object[] { null! });

        var mockStatusEngine = new Mock<IFinancialStatusEngine>();

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        var mockTenantService = new Mock<ICurrentTenantService>();
        var dbContext = new PortalDbContext(options, mockTenantService.Object);

        var paymentService = new PaymentService(
            mockPaymentRepo.Object,
            mockInvoiceRepo.Object,
            new Mock<CreditNoteRepository>(null!).Object,
            mockStatusEngine.Object,
            new Mock<IPaymentScheduleService>().Object,
            new Mock<IPaymentAllocationEngine>().Object,
            dbContext);

        var dto = new RecordPaymentDto
        {
            InvoiceId = invoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow,
            Amount = amount
        };

        var result = paymentService.RecordPaymentAsync(dto, businessId, "test-user-id").GetAwaiter().GetResult();

        // Clean up
        dbContext.Dispose();

        var isRejected = !result.Success &&
            result.Message != null &&
            result.Message.Contains("issued invoices", StringComparison.OrdinalIgnoreCase);

        mockPaymentRepo.Verify(r => r.InsertAsync(It.IsAny<Payment>()), Times.Never());

        return isRejected.ToProperty()
            .Label($"Draft invoice should be rejected: success={result.Success}, message='{result.Message}'");
    }

    /// <summary>
    /// Specifically tests Cancelled status (Id = 3) is rejected.
    /// **Validates: Requirement 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecordPayment_RejectsCancelledInvoice(
        PositiveInt invoiceIdSeed,
        PositiveInt businessIdSeed,
        PositiveInt amountSeed)
    {
        var invoiceId = (invoiceIdSeed.Get % 1000) + 1;
        var businessId = (businessIdSeed.Get % 100) + 1;
        var amount = Math.Round((amountSeed.Get % 10000 + 1) / 100m, 2);

        var invoice = new Invoice
        {
            Id = invoiceId,
            BusinessId = businessId,
            InvoiceStatusTypeId = StatusCancelled,
            InvoiceFinancialStatusTypeId = 1,
            TotalAmount = 1000m,
            InvoiceNumber = $"INV-{invoiceId:D5}",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            CurrencyCode = "EUR",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var mockInvoiceRepo = new Mock<InvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        mockInvoiceRepo
            .Setup(r => r.GetByIdAndBusinessIdAsync(invoiceId, businessId))
            .ReturnsAsync(invoice);

        var mockPaymentRepo = new Mock<PaymentRepository>(MockBehavior.Loose, new object[] { null! });

        var mockStatusEngine = new Mock<IFinancialStatusEngine>();

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        var mockTenantService = new Mock<ICurrentTenantService>();
        var dbContext = new PortalDbContext(options, mockTenantService.Object);

        var paymentService = new PaymentService(
            mockPaymentRepo.Object,
            mockInvoiceRepo.Object,
            new Mock<CreditNoteRepository>(null!).Object,
            mockStatusEngine.Object,
            new Mock<IPaymentScheduleService>().Object,
            new Mock<IPaymentAllocationEngine>().Object,
            dbContext);

        var dto = new RecordPaymentDto
        {
            InvoiceId = invoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow,
            Amount = amount
        };

        var result = paymentService.RecordPaymentAsync(dto, businessId, "test-user-id").GetAwaiter().GetResult();

        // Clean up
        dbContext.Dispose();

        var isRejected = !result.Success &&
            result.Message != null &&
            result.Message.Contains("issued invoices", StringComparison.OrdinalIgnoreCase);

        mockPaymentRepo.Verify(r => r.InsertAsync(It.IsAny<Payment>()), Times.Never());

        return isRejected.ToProperty()
            .Label($"Cancelled invoice should be rejected: success={result.Success}, message='{result.Message}'");
    }
}
