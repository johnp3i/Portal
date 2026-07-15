using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Integration;

/// <summary>
/// Integration test: End-to-end payment recording flow.
/// Records a payment → verifies status update → verifies balance change → verifies payment in history.
/// Uses in-memory database with seeded invoice data and actual service logic.
/// **Validates: Requirements 1.5, 1.6, 2.1**
/// </summary>
public class PaymentRecordingIntegrationTests
{
    private const int TestBusinessId = 1;
    private const string TestUserId = "test-user-001";

    // Invoice Status Type IDs
    private const int StatusIssued = 2;

    // Financial Status Type IDs
    private const int FinancialUnpaid = 1;
    private const int FinancialPartiallyPaid = 2;
    private const int FinancialPaid = 3;

    #region Test Infrastructure

    private static PortalDbContext CreateDbContext()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"PaymentRecording_{Guid.NewGuid()}")
            .Options;

        var dbContext = new PortalDbContext(options, tenantMock.Object);
        SeedReferenceData(dbContext);
        return dbContext;
    }

    private static void SeedReferenceData(PortalDbContext dbContext)
    {
        dbContext.Businesses.Add(new Business
        {
            Id = TestBusinessId,
            Name = "Test Business",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        dbContext.InvoiceStatusTypes.AddRange(
            new InvoiceStatusType { Id = 1, Name = "Draft" },
            new InvoiceStatusType { Id = 2, Name = "Issued" },
            new InvoiceStatusType { Id = 3, Name = "Cancelled" }
        );

        dbContext.InvoiceFinancialStatusTypes.AddRange(
            new InvoiceFinancialStatusType { Id = 1, Name = "Unpaid" },
            new InvoiceFinancialStatusType { Id = 2, Name = "PartiallyPaid" },
            new InvoiceFinancialStatusType { Id = 3, Name = "Paid" },
            new InvoiceFinancialStatusType { Id = 4, Name = "Overdue" },
            new InvoiceFinancialStatusType { Id = 5, Name = "WrittenOff" }
        );

        dbContext.PaymentMethodTypes.AddRange(
            new PaymentMethodType { Id = 1, Name = "Cash", IsActive = true },
            new PaymentMethodType { Id = 2, Name = "BankTransfer", IsActive = true }
        );

        dbContext.Customers.Add(new Customer
        {
            Id = 1,
            BusinessId = TestBusinessId,
            Name = "Test Customer",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        dbContext.SaveChanges();
    }

    private static Invoice SeedIssuedInvoice(PortalDbContext dbContext, decimal totalAmount)
    {
        var invoice = new Invoice
        {
            Id = 1,
            BusinessId = TestBusinessId,
            CustomerId = 1,
            InvoiceStatusTypeId = StatusIssued,
            InvoiceFinancialStatusTypeId = FinancialUnpaid,
            InvoiceNumber = "INV-00001",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            Subtotal = Math.Round(totalAmount / 1.15m, 2),
            TaxAmount = Math.Round(totalAmount - (totalAmount / 1.15m), 2),
            TotalAmount = totalAmount,
            CurrencyCode = "EUR",
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.Invoices.Add(invoice);
        dbContext.SaveChanges();
        return invoice;
    }

    /// <summary>
    /// Creates a PaymentService and FinancialStatusEngine backed by mocked repositories
    /// that delegate to LINQ queries against the in-memory DbContext.
    /// This gives us real service logic with in-memory data access.
    /// </summary>
    private static (PaymentService PaymentService, FinancialStatusEngine StatusEngine) CreateServices(
        PortalDbContext dbContext)
    {
        var mockPaymentRepo = new Mock<PaymentRepository>(MockBehavior.Loose, new object[] { null! });
        var mockInvoiceRepo = new Mock<InvoiceRepository>(MockBehavior.Loose, new object[] { null! });

        // Wire up InvoiceRepository.GetByIdAndBusinessIdAsync to use LINQ
        mockInvoiceRepo
            .Setup(r => r.GetByIdAndBusinessIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns((int id, int businessId) =>
                Task.FromResult(dbContext.Invoices
                    .IgnoreQueryFilters()
                    .FirstOrDefault(inv => inv.Id == id && inv.BusinessId == businessId)));

        // Wire up InvoiceRepository.UpdateFinancialStatusAsync to update in-memory entity
        mockInvoiceRepo
            .Setup(r => r.UpdateFinancialStatusAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns((int invoiceId, int financialStatusTypeId) =>
            {
                var invoice = dbContext.Invoices
                    .IgnoreQueryFilters()
                    .First(inv => inv.Id == invoiceId);
                invoice.InvoiceFinancialStatusTypeId = financialStatusTypeId;
                invoice.UpdatedAtUtc = DateTime.UtcNow;
                dbContext.SaveChanges();
                return Task.CompletedTask;
            });

        // Wire up PaymentRepository.InsertAsync to add to in-memory DbContext
        var nextPaymentId = 1;
        mockPaymentRepo
            .Setup(r => r.InsertAsync(It.IsAny<Payment>()))
            .Returns((Payment entity) =>
            {
                entity.Id = nextPaymentId++;
                dbContext.Payments.Add(entity);
                dbContext.SaveChanges();
                return Task.FromResult(entity.Id);
            });

        // Wire up PaymentRepository.GetValidPaymentsByInvoiceIdAsync to use LINQ
        mockPaymentRepo
            .Setup(r => r.GetValidPaymentsByInvoiceIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns((int invoiceId, int businessId) =>
                Task.FromResult(dbContext.Payments
                    .IgnoreQueryFilters()
                    .Where(p => p.InvoiceId == invoiceId && p.BusinessId == businessId && !p.IsVoided)
                    .ToList()));

        // Wire up PaymentRepository.GetAllPaymentsByInvoiceIdAsync to use LINQ
        mockPaymentRepo
            .Setup(r => r.GetAllPaymentsByInvoiceIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns((int invoiceId, int businessId) =>
                Task.FromResult(dbContext.Payments
                    .IgnoreQueryFilters()
                    .Where(p => p.InvoiceId == invoiceId && p.BusinessId == businessId)
                    .OrderByDescending(p => p.PaymentDateUtc)
                    .ToList()));

        // Wire up PaymentRepository.GetTotalPaidAsync to use LINQ
        mockPaymentRepo
            .Setup(r => r.GetTotalPaidAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns((int invoiceId, int businessId) =>
                Task.FromResult(dbContext.Payments
                    .IgnoreQueryFilters()
                    .Where(p => p.InvoiceId == invoiceId && p.BusinessId == businessId && !p.IsVoided)
                    .Sum(p => p.Amount)));

        // Wire up PaymentRepository.GetByIdAndBusinessIdAsync to use LINQ
        mockPaymentRepo
            .Setup(r => r.GetByIdAndBusinessIdAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns((int id, int businessId) =>
                Task.FromResult(dbContext.Payments
                    .IgnoreQueryFilters()
                    .FirstOrDefault(p => p.Id == id && p.BusinessId == businessId)));

        // Create the actual FinancialStatusEngine with mocked repos
        var mockCreditNoteRepo = new Mock<CreditNoteRepository>(MockBehavior.Loose, new object[] { null! });
        mockCreditNoteRepo
            .Setup(r => r.GetTotalAppliedCreditAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(0m);

        var statusEngine = new FinancialStatusEngine(
            mockPaymentRepo.Object,
            mockInvoiceRepo.Object,
            mockCreditNoteRepo.Object);

        // Create the actual PaymentService with mocked repos and real status engine
        var paymentService = new PaymentService(
            mockPaymentRepo.Object,
            mockInvoiceRepo.Object,
            mockCreditNoteRepo.Object,
            statusEngine,
            new Mock<IPaymentScheduleService>().Object,
            new Mock<IPaymentAllocationEngine>().Object,
            dbContext);

        return (paymentService, statusEngine);
    }

    #endregion

    #region End-to-End Payment Recording Flow

    [Fact]
    public async Task RecordPartialPayment_UpdatesStatusToPartiallyPaid_AndAppearsInHistory()
    {
        // Arrange: Seed an Issued invoice with known TotalAmount
        var dbContext = CreateDbContext();
        try
        {
            var totalAmount = 1000.00m;
            var paymentAmount = 400.00m;
            var invoice = SeedIssuedInvoice(dbContext, totalAmount);
            var (paymentService, _) = CreateServices(dbContext);

            var dto = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow,
                Amount = paymentAmount,
                Reference = "PAY-001",
                Notes = "Partial payment"
            };

            // Act: Record the payment
            var result = await paymentService.RecordPaymentAsync(dto, TestBusinessId, TestUserId);

            // Assert 1: Result is success
            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.NotNull(result.Id);

            // Assert 2: Invoice financial status updated to PartiallyPaid
            var updatedInvoice = dbContext.Invoices
                .IgnoreQueryFilters()
                .First(inv => inv.Id == invoice.Id);
            Assert.Equal(FinancialPartiallyPaid, updatedInvoice.InvoiceFinancialStatusTypeId);

            // Assert 3: Outstanding balance changed correctly (TotalAmount - payment amount)
            var totalPaid = dbContext.Payments
                .IgnoreQueryFilters()
                .Where(p => p.InvoiceId == invoice.Id && !p.IsVoided)
                .Sum(p => p.Amount);
            var outstandingBalance = totalAmount - totalPaid;
            Assert.Equal(totalAmount - paymentAmount, outstandingBalance);

            // Assert 4: Payment appears in history
            var history = await paymentService.GetPaymentHistoryAsync(invoice.Id, TestBusinessId);
            Assert.Single(history);
            Assert.Equal(paymentAmount, history[0].Amount);
            Assert.Equal("PAY-001", history[0].Reference);
            Assert.Equal("Partial payment", history[0].Notes);
            Assert.Equal("Cash", history[0].PaymentMethodName);
            Assert.False(history[0].IsVoided);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    [Fact]
    public async Task RecordFullPayment_UpdatesStatusToPaid_AndBalanceBecomesZero()
    {
        // Arrange: Seed an Issued invoice with known TotalAmount
        var dbContext = CreateDbContext();
        try
        {
            var totalAmount = 750.50m;
            var invoice = SeedIssuedInvoice(dbContext, totalAmount);
            var (paymentService, _) = CreateServices(dbContext);

            var dto = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 2,
                PaymentDateUtc = DateTime.UtcNow,
                Amount = totalAmount,
                Reference = "FULL-PAY-001",
                Notes = "Full payment via bank transfer"
            };

            // Act: Record the full payment
            var result = await paymentService.RecordPaymentAsync(dto, TestBusinessId, TestUserId);

            // Assert 1: Result is success
            Assert.True(result.Success, $"Expected success but got: {result.Message}");

            // Assert 2: Invoice financial status updated to Paid
            var updatedInvoice = dbContext.Invoices
                .IgnoreQueryFilters()
                .First(inv => inv.Id == invoice.Id);
            Assert.Equal(FinancialPaid, updatedInvoice.InvoiceFinancialStatusTypeId);

            // Assert 3: Outstanding balance is zero
            var totalPaid = dbContext.Payments
                .IgnoreQueryFilters()
                .Where(p => p.InvoiceId == invoice.Id && !p.IsVoided)
                .Sum(p => p.Amount);
            var outstandingBalance = totalAmount - totalPaid;
            Assert.Equal(0m, outstandingBalance);

            // Assert 4: Payment appears in history with correct data
            var history = await paymentService.GetPaymentHistoryAsync(invoice.Id, TestBusinessId);
            Assert.Single(history);
            Assert.Equal(totalAmount, history[0].Amount);
            Assert.Equal("FULL-PAY-001", history[0].Reference);
            Assert.Equal("BankTransfer", history[0].PaymentMethodName);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    [Fact]
    public async Task RecordMultiplePartialPayments_UpdatesStatusProgressively()
    {
        // Arrange: Seed an Issued invoice
        var dbContext = CreateDbContext();
        try
        {
            var totalAmount = 1200.00m;
            var invoice = SeedIssuedInvoice(dbContext, totalAmount);
            var (paymentService, _) = CreateServices(dbContext);

            // Act 1: Record first partial payment (500)
            var dto1 = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow.AddDays(-2),
                Amount = 500.00m,
                Reference = "PAY-001"
            };
            var result1 = await paymentService.RecordPaymentAsync(dto1, TestBusinessId, TestUserId);
            Assert.True(result1.Success);

            // Verify: Status is PartiallyPaid after first payment
            var invoiceAfterFirst = dbContext.Invoices
                .IgnoreQueryFilters()
                .First(inv => inv.Id == invoice.Id);
            Assert.Equal(FinancialPartiallyPaid, invoiceAfterFirst.InvoiceFinancialStatusTypeId);

            // Verify: Outstanding balance is 700
            var paidAfterFirst = dbContext.Payments
                .IgnoreQueryFilters()
                .Where(p => p.InvoiceId == invoice.Id && !p.IsVoided)
                .Sum(p => p.Amount);
            Assert.Equal(700.00m, totalAmount - paidAfterFirst);

            // Act 2: Record second partial payment (700) — completes the invoice
            var dto2 = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 2,
                PaymentDateUtc = DateTime.UtcNow,
                Amount = 700.00m,
                Reference = "PAY-002"
            };
            var result2 = await paymentService.RecordPaymentAsync(dto2, TestBusinessId, TestUserId);
            Assert.True(result2.Success);

            // Verify: Status is now Paid
            var invoiceAfterSecond = dbContext.Invoices
                .IgnoreQueryFilters()
                .First(inv => inv.Id == invoice.Id);
            Assert.Equal(FinancialPaid, invoiceAfterSecond.InvoiceFinancialStatusTypeId);

            // Verify: Outstanding balance is zero
            var paidAfterSecond = dbContext.Payments
                .IgnoreQueryFilters()
                .Where(p => p.InvoiceId == invoice.Id && !p.IsVoided)
                .Sum(p => p.Amount);
            Assert.Equal(0m, totalAmount - paidAfterSecond);

            // Verify: Both payments appear in history
            var history = await paymentService.GetPaymentHistoryAsync(invoice.Id, TestBusinessId);
            Assert.Equal(2, history.Count);
            Assert.Contains(history, h => h.Reference == "PAY-001" && h.Amount == 500.00m);
            Assert.Contains(history, h => h.Reference == "PAY-002" && h.Amount == 700.00m);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    [Fact]
    public async Task RecordPayment_ExceedingOutstandingBalance_IsRejected()
    {
        // Arrange: Seed an Issued invoice
        var dbContext = CreateDbContext();
        try
        {
            var totalAmount = 500.00m;
            var invoice = SeedIssuedInvoice(dbContext, totalAmount);
            var (paymentService, _) = CreateServices(dbContext);

            var dto = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow,
                Amount = 600.00m,
                Reference = "OVER-PAY"
            };

            // Act: Attempt to record payment exceeding balance
            var result = await paymentService.RecordPaymentAsync(dto, TestBusinessId, TestUserId);

            // Assert: Payment is rejected
            Assert.False(result.Success);
            Assert.Contains("exceeds outstanding balance", result.Message!);

            // Assert: No payment was created
            var payments = dbContext.Payments
                .IgnoreQueryFilters()
                .Where(p => p.InvoiceId == invoice.Id)
                .ToList();
            Assert.Empty(payments);

            // Assert: Invoice status unchanged
            var unchangedInvoice = dbContext.Invoices
                .IgnoreQueryFilters()
                .First(inv => inv.Id == invoice.Id);
            Assert.Equal(FinancialUnpaid, unchangedInvoice.InvoiceFinancialStatusTypeId);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    [Fact]
    public async Task RecordPayment_PersistsAllFieldsCorrectly()
    {
        // Arrange: Seed an Issued invoice
        var dbContext = CreateDbContext();
        try
        {
            var totalAmount = 2000.00m;
            var invoice = SeedIssuedInvoice(dbContext, totalAmount);
            var (paymentService, _) = CreateServices(dbContext);

            var paymentDate = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
            var dto = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 2,
                PaymentDateUtc = paymentDate,
                Amount = 850.00m,
                Reference = "TXN-12345",
                Notes = "Bank transfer received"
            };

            // Act: Record the payment
            var result = await paymentService.RecordPaymentAsync(dto, TestBusinessId, TestUserId);

            // Assert: All fields persisted correctly (Requirement 1.5)
            Assert.True(result.Success);
            var payment = dbContext.Payments
                .IgnoreQueryFilters()
                .First(p => p.Id == result.Id);

            Assert.Equal(TestBusinessId, payment.BusinessId);
            Assert.Equal(invoice.Id, payment.InvoiceId);
            Assert.Equal(2, payment.PaymentMethodTypeId);
            Assert.Equal(paymentDate, payment.PaymentDateUtc);
            Assert.Equal(850.00m, payment.Amount);
            Assert.Equal("TXN-12345", payment.Reference);
            Assert.Equal("Bank transfer received", payment.Notes);
            Assert.Equal(TestUserId, payment.CreatedByUserId);
            Assert.False(payment.IsVoided);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion
}
