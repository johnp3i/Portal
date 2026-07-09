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
/// Integration test: End-to-end void flow.
/// Record payment → void payment → verify status recalculation → verify balance restored.
/// Uses actual services (PaymentService, FinancialStatusEngine) with in-memory EF Core database
/// and test repository subclasses that use LINQ instead of raw SQL.
/// **Validates: Requirements 3.1, 3.3**
/// </summary>
public class EndToEndVoidFlowTests
{
    // Constants
    private const int BusinessId = 1;
    private const int CustomerId = 1;
    private const int InvoiceStatusIssued = 2;
    private const int FinancialUnpaid = 1;
    private const int FinancialPartiallyPaid = 2;
    private const int FinancialPaid = 3;
    private const string UserId = "test-user-001";

    #region Test Infrastructure

    /// <summary>
    /// Creates a fresh in-memory PortalDbContext with reference data seeded.
    /// </summary>
    private static PortalDbContext CreateDbContext()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(BusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"VoidFlowIntegration_{Guid.NewGuid()}")
            .Options;

        var dbContext = new PortalDbContext(options, tenantMock.Object);
        SeedReferenceData(dbContext);
        return dbContext;
    }

    private static void SeedReferenceData(PortalDbContext dbContext)
    {
        dbContext.Businesses.Add(new Business
        {
            Id = BusinessId,
            Name = "Test Business",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        dbContext.Customers.Add(new Customer
        {
            Id = CustomerId,
            BusinessId = BusinessId,
            Name = "Test Customer",
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

        dbContext.PaymentMethodTypes.Add(new PaymentMethodType
        {
            Id = 1,
            Name = "BankTransfer",
            IsActive = true
        });

        dbContext.SaveChanges();
    }

    /// <summary>
    /// Seeds an Issued invoice with the given total amount.
    /// </summary>
    private static Invoice SeedIssuedInvoice(PortalDbContext dbContext, decimal totalAmount)
    {
        var subtotal = Math.Round(totalAmount / 1.15m, 2);
        var taxAmount = totalAmount - subtotal;

        var invoice = new Invoice
        {
            BusinessId = BusinessId,
            CustomerId = CustomerId,
            InvoiceStatusTypeId = InvoiceStatusIssued,
            InvoiceFinancialStatusTypeId = FinancialUnpaid,
            InvoiceNumber = "INV-00001",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            Subtotal = subtotal,
            TaxAmount = taxAmount,
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
    /// Test-friendly PaymentRepository that uses LINQ against in-memory EF Core
    /// instead of raw SQL (which is not supported by the in-memory provider).
    /// </summary>
    private class InMemoryPaymentRepository : PaymentRepository
    {
        private readonly PortalDbContext _dbContext;

        public InMemoryPaymentRepository(PortalDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public override async Task<int> InsertAsync(Payment entity)
        {
            _dbContext.Payments.Add(entity);
            await _dbContext.SaveChangesAsync();
            return entity.Id;
        }

        public override async Task<Payment?> GetByIdAndBusinessIdAsync(int id, int businessId)
        {
            return await _dbContext.Payments
                .FirstOrDefaultAsync(p => p.Id == id && p.BusinessId == businessId);
        }

        public override async Task VoidAsync(int paymentId)
        {
            var payment = await _dbContext.Payments.FindAsync(paymentId);
            if (payment != null)
            {
                payment.IsVoided = true;
                await _dbContext.SaveChangesAsync();
            }
        }

        public override async Task<List<Payment>> GetValidPaymentsByInvoiceIdAsync(int invoiceId, int businessId)
        {
            return await _dbContext.Payments
                .Where(p => p.InvoiceId == invoiceId
                         && p.BusinessId == businessId
                         && !p.IsVoided)
                .ToListAsync();
        }

        public override async Task<List<Payment>> GetAllPaymentsByInvoiceIdAsync(int invoiceId, int businessId)
        {
            return await _dbContext.Payments
                .Where(p => p.InvoiceId == invoiceId && p.BusinessId == businessId)
                .OrderByDescending(p => p.PaymentDateUtc)
                .ToListAsync();
        }

        public override async Task<decimal> GetTotalPaidAsync(int invoiceId, int businessId)
        {
            return await _dbContext.Payments
                .Where(p => p.InvoiceId == invoiceId
                         && p.BusinessId == businessId
                         && !p.IsVoided)
                .SumAsync(p => p.Amount);
        }
    }

    /// <summary>
    /// Test-friendly InvoiceRepository that uses LINQ against in-memory EF Core.
    /// </summary>
    private class InMemoryInvoiceRepository : InvoiceRepository
    {
        private readonly PortalDbContext _dbContext;

        public InMemoryInvoiceRepository(PortalDbContext dbContext) : base(dbContext)
        {
            _dbContext = dbContext;
        }

        public override async Task<Invoice?> GetByIdAndBusinessIdAsync(int id, int businessId)
        {
            return await _dbContext.Invoices
                .FirstOrDefaultAsync(i => i.Id == id && i.BusinessId == businessId);
        }

        public override async Task UpdateFinancialStatusAsync(int invoiceId, int financialStatusTypeId)
        {
            var invoice = await _dbContext.Invoices.FindAsync(invoiceId);
            if (invoice != null)
            {
                invoice.InvoiceFinancialStatusTypeId = financialStatusTypeId;
                invoice.UpdatedAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Test-friendly CreditNoteRepository that returns 0 for applied credit totals.
    /// Used in payment-focused integration tests where no credit notes are involved.
    /// </summary>
    private class InMemoryCreditNoteRepository : CreditNoteRepository
    {
        public InMemoryCreditNoteRepository(PortalDbContext dbContext) : base(dbContext) { }

        public override Task<decimal> GetTotalAppliedCreditAsync(int invoiceId, int businessId)
        {
            return Task.FromResult(0m);
        }
    }

    #endregion

    #region End-to-End Void Flow Tests

    /// <summary>
    /// Full end-to-end void flow:
    /// 1. Seed an Issued invoice (TotalAmount = 1000)
    /// 2. Record a partial payment (400)
    /// 3. Verify status changed to PartiallyPaid
    /// 4. Void the payment
    /// 5. Verify IsVoided = true on the payment
    /// 6. Verify the invoice status reverted to Unpaid
    /// 7. Verify the outstanding balance was restored to TotalAmount
    /// </summary>
    [Fact]
    public async Task VoidPayment_EndToEnd_RestoresInvoiceToOriginalState()
    {
        // Arrange
        var dbContext = CreateDbContext();
        try
        {
            var paymentRepo = new InMemoryPaymentRepository(dbContext);
            var invoiceRepo = new InMemoryInvoiceRepository(dbContext);
            var creditNoteRepo = new InMemoryCreditNoteRepository(dbContext);
            var financialStatusEngine = new FinancialStatusEngine(paymentRepo, invoiceRepo, creditNoteRepo);
            var paymentService = new PaymentService(paymentRepo, invoiceRepo, financialStatusEngine, new Mock<IPaymentScheduleService>().Object, dbContext);

            var totalAmount = 1000.00m;
            var partialPaymentAmount = 400.00m;

            // Step 1: Seed an Issued invoice
            var invoice = SeedIssuedInvoice(dbContext, totalAmount);
            Assert.Equal(FinancialUnpaid, invoice.InvoiceFinancialStatusTypeId);

            // Step 2: Record a partial payment
            var recordDto = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow,
                Amount = partialPaymentAmount,
                Reference = "PAY-001",
                Notes = "Partial payment for testing"
            };

            var recordResult = await paymentService.RecordPaymentAsync(recordDto, BusinessId, UserId);
            Assert.True(recordResult.Success, $"RecordPayment failed: {recordResult.Message}");
            Assert.NotNull(recordResult.Id);
            var paymentId = recordResult.Id!.Value;

            // Step 3: Verify status changed to PartiallyPaid
            var invoiceAfterPayment = await invoiceRepo.GetByIdAndBusinessIdAsync(invoice.Id, BusinessId);
            Assert.NotNull(invoiceAfterPayment);
            Assert.Equal(FinancialPartiallyPaid, invoiceAfterPayment!.InvoiceFinancialStatusTypeId);

            // Verify outstanding balance is TotalAmount - partialPaymentAmount
            var outstandingAfterPayment = await paymentRepo.GetTotalPaidAsync(invoice.Id, BusinessId);
            Assert.Equal(partialPaymentAmount, outstandingAfterPayment);
            Assert.Equal(totalAmount - partialPaymentAmount, totalAmount - outstandingAfterPayment);

            // Step 4: Void the payment
            var voidResult = await paymentService.VoidPaymentAsync(paymentId, BusinessId);
            Assert.True(voidResult.Success, $"VoidPayment failed: {voidResult.Message}");

            // Step 5: Verify IsVoided = true on the payment
            var voidedPayment = await paymentRepo.GetByIdAndBusinessIdAsync(paymentId, BusinessId);
            Assert.NotNull(voidedPayment);
            Assert.True(voidedPayment!.IsVoided, "Payment should be marked as voided (IsVoided = 1)");

            // Step 6: Verify the invoice status reverted to Unpaid
            var invoiceAfterVoid = await invoiceRepo.GetByIdAndBusinessIdAsync(invoice.Id, BusinessId);
            Assert.NotNull(invoiceAfterVoid);
            Assert.Equal(FinancialUnpaid, invoiceAfterVoid!.InvoiceFinancialStatusTypeId);

            // Step 7: Verify the outstanding balance was restored to TotalAmount
            var totalPaidAfterVoid = await paymentRepo.GetTotalPaidAsync(invoice.Id, BusinessId);
            Assert.Equal(0m, totalPaidAfterVoid);
            var restoredOutstanding = totalAmount - totalPaidAfterVoid;
            Assert.Equal(totalAmount, restoredOutstanding);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    /// <summary>
    /// Void flow with multiple payments: void one payment and verify partial balance is restored.
    /// 1. Seed an Issued invoice (TotalAmount = 1000)
    /// 2. Record first partial payment (300)
    /// 3. Record second partial payment (200)
    /// 4. Verify status is PartiallyPaid and outstanding = 500
    /// 5. Void the second payment (200)
    /// 6. Verify IsVoided = true on second payment only
    /// 7. Verify status remains PartiallyPaid (first payment still valid)
    /// 8. Verify outstanding balance = 700 (only first payment of 300 counts)
    /// </summary>
    [Fact]
    public async Task VoidPayment_WithMultiplePayments_RestoresPartialBalance()
    {
        // Arrange
        var dbContext = CreateDbContext();
        try
        {
            var paymentRepo = new InMemoryPaymentRepository(dbContext);
            var invoiceRepo = new InMemoryInvoiceRepository(dbContext);
            var creditNoteRepo = new InMemoryCreditNoteRepository(dbContext);
            var financialStatusEngine = new FinancialStatusEngine(paymentRepo, invoiceRepo, creditNoteRepo);
            var paymentService = new PaymentService(paymentRepo, invoiceRepo, financialStatusEngine, new Mock<IPaymentScheduleService>().Object, dbContext);

            var totalAmount = 1000.00m;

            // Step 1: Seed an Issued invoice
            var invoice = SeedIssuedInvoice(dbContext, totalAmount);

            // Step 2: Record first partial payment (300)
            var firstPaymentDto = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow.AddDays(-2),
                Amount = 300.00m,
                Reference = "PAY-001"
            };
            var firstResult = await paymentService.RecordPaymentAsync(firstPaymentDto, BusinessId, UserId);
            Assert.True(firstResult.Success);

            // Step 3: Record second partial payment (200)
            var secondPaymentDto = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow.AddDays(-1),
                Amount = 200.00m,
                Reference = "PAY-002"
            };
            var secondResult = await paymentService.RecordPaymentAsync(secondPaymentDto, BusinessId, UserId);
            Assert.True(secondResult.Success);
            var secondPaymentId = secondResult.Id!.Value;

            // Step 4: Verify status is PartiallyPaid and outstanding = 500
            var invoiceAfterPayments = await invoiceRepo.GetByIdAndBusinessIdAsync(invoice.Id, BusinessId);
            Assert.Equal(FinancialPartiallyPaid, invoiceAfterPayments!.InvoiceFinancialStatusTypeId);
            var totalPaidBefore = await paymentRepo.GetTotalPaidAsync(invoice.Id, BusinessId);
            Assert.Equal(500.00m, totalPaidBefore);

            // Step 5: Void the second payment (200)
            var voidResult = await paymentService.VoidPaymentAsync(secondPaymentId, BusinessId);
            Assert.True(voidResult.Success);

            // Step 6: Verify IsVoided = true on second payment only
            var secondPayment = await paymentRepo.GetByIdAndBusinessIdAsync(secondPaymentId, BusinessId);
            Assert.True(secondPayment!.IsVoided);

            var firstPayment = await paymentRepo.GetByIdAndBusinessIdAsync(firstResult.Id!.Value, BusinessId);
            Assert.False(firstPayment!.IsVoided);

            // Step 7: Verify status remains PartiallyPaid (first payment still valid)
            var invoiceAfterVoid = await invoiceRepo.GetByIdAndBusinessIdAsync(invoice.Id, BusinessId);
            Assert.Equal(FinancialPartiallyPaid, invoiceAfterVoid!.InvoiceFinancialStatusTypeId);

            // Step 8: Verify outstanding balance = 700 (only first payment of 300 counts)
            var totalPaidAfterVoid = await paymentRepo.GetTotalPaidAsync(invoice.Id, BusinessId);
            Assert.Equal(300.00m, totalPaidAfterVoid);
            var outstandingAfterVoid = totalAmount - totalPaidAfterVoid;
            Assert.Equal(700.00m, outstandingAfterVoid);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    /// <summary>
    /// Void flow where invoice was fully paid: void the final payment and verify status reverts.
    /// 1. Seed an Issued invoice (TotalAmount = 500)
    /// 2. Record a full payment (500)
    /// 3. Verify status changed to Paid
    /// 4. Void the payment
    /// 5. Verify status reverted to Unpaid
    /// 6. Verify outstanding balance restored to TotalAmount (500)
    /// </summary>
    [Fact]
    public async Task VoidPayment_AfterFullPayment_RevertsFromPaidToUnpaid()
    {
        // Arrange
        var dbContext = CreateDbContext();
        try
        {
            var paymentRepo = new InMemoryPaymentRepository(dbContext);
            var invoiceRepo = new InMemoryInvoiceRepository(dbContext);
            var creditNoteRepo = new InMemoryCreditNoteRepository(dbContext);
            var financialStatusEngine = new FinancialStatusEngine(paymentRepo, invoiceRepo, creditNoteRepo);
            var paymentService = new PaymentService(paymentRepo, invoiceRepo, financialStatusEngine, new Mock<IPaymentScheduleService>().Object, dbContext);

            var totalAmount = 500.00m;

            // Step 1: Seed an Issued invoice
            var invoice = SeedIssuedInvoice(dbContext, totalAmount);

            // Step 2: Record a full payment
            var paymentDto = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow,
                Amount = totalAmount,
                Reference = "PAY-FULL"
            };
            var recordResult = await paymentService.RecordPaymentAsync(paymentDto, BusinessId, UserId);
            Assert.True(recordResult.Success);
            var paymentId = recordResult.Id!.Value;

            // Step 3: Verify status changed to Paid
            var invoiceAfterPayment = await invoiceRepo.GetByIdAndBusinessIdAsync(invoice.Id, BusinessId);
            Assert.Equal(FinancialPaid, invoiceAfterPayment!.InvoiceFinancialStatusTypeId);

            // Step 4: Void the payment
            var voidResult = await paymentService.VoidPaymentAsync(paymentId, BusinessId);
            Assert.True(voidResult.Success);

            // Step 5: Verify status reverted to Unpaid
            var invoiceAfterVoid = await invoiceRepo.GetByIdAndBusinessIdAsync(invoice.Id, BusinessId);
            Assert.Equal(FinancialUnpaid, invoiceAfterVoid!.InvoiceFinancialStatusTypeId);

            // Step 6: Verify outstanding balance restored to TotalAmount
            var totalPaidAfterVoid = await paymentRepo.GetTotalPaidAsync(invoice.Id, BusinessId);
            Assert.Equal(0m, totalPaidAfterVoid);
            Assert.Equal(totalAmount, totalAmount - totalPaidAfterVoid);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    /// <summary>
    /// Void an already-voided payment returns informational message (not an error).
    /// Verifies Requirement 3.4: double-void returns informational message.
    /// </summary>
    [Fact]
    public async Task VoidPayment_AlreadyVoided_ReturnsInformationalMessage()
    {
        // Arrange
        var dbContext = CreateDbContext();
        try
        {
            var paymentRepo = new InMemoryPaymentRepository(dbContext);
            var invoiceRepo = new InMemoryInvoiceRepository(dbContext);
            var creditNoteRepo = new InMemoryCreditNoteRepository(dbContext);
            var financialStatusEngine = new FinancialStatusEngine(paymentRepo, invoiceRepo, creditNoteRepo);
            var paymentService = new PaymentService(paymentRepo, invoiceRepo, financialStatusEngine, new Mock<IPaymentScheduleService>().Object, dbContext);

            var totalAmount = 1000.00m;
            var invoice = SeedIssuedInvoice(dbContext, totalAmount);

            // Record and void a payment
            var paymentDto = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow,
                Amount = 250.00m,
                Reference = "PAY-DOUBLE-VOID"
            };
            var recordResult = await paymentService.RecordPaymentAsync(paymentDto, BusinessId, UserId);
            Assert.True(recordResult.Success);
            var paymentId = recordResult.Id!.Value;

            // First void — should succeed
            var firstVoidResult = await paymentService.VoidPaymentAsync(paymentId, BusinessId);
            Assert.True(firstVoidResult.Success);

            // Act: Second void — should return failure with informational message
            var secondVoidResult = await paymentService.VoidPaymentAsync(paymentId, BusinessId);

            // Assert
            Assert.False(secondVoidResult.Success);
            Assert.Contains("already been voided", secondVoidResult.Message!);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    /// <summary>
    /// Verifies that the payment record is never physically deleted after voiding.
    /// The total count of Payment records for the invoice should not decrease.
    /// </summary>
    [Fact]
    public async Task VoidPayment_NeverDeletesRecord_PaymentCountPreserved()
    {
        // Arrange
        var dbContext = CreateDbContext();
        try
        {
            var paymentRepo = new InMemoryPaymentRepository(dbContext);
            var invoiceRepo = new InMemoryInvoiceRepository(dbContext);
            var creditNoteRepo = new InMemoryCreditNoteRepository(dbContext);
            var financialStatusEngine = new FinancialStatusEngine(paymentRepo, invoiceRepo, creditNoteRepo);
            var paymentService = new PaymentService(paymentRepo, invoiceRepo, financialStatusEngine, new Mock<IPaymentScheduleService>().Object, dbContext);

            var totalAmount = 1000.00m;
            var invoice = SeedIssuedInvoice(dbContext, totalAmount);

            // Record two payments
            var dto1 = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow.AddDays(-1),
                Amount = 200.00m,
                Reference = "PAY-A"
            };
            var dto2 = new RecordPaymentDto
            {
                InvoiceId = invoice.Id,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow,
                Amount = 300.00m,
                Reference = "PAY-B"
            };

            await paymentService.RecordPaymentAsync(dto1, BusinessId, UserId);
            var secondResult = await paymentService.RecordPaymentAsync(dto2, BusinessId, UserId);
            var secondPaymentId = secondResult.Id!.Value;

            // Count before void
            var allPaymentsBefore = await paymentRepo.GetAllPaymentsByInvoiceIdAsync(invoice.Id, BusinessId);
            var countBefore = allPaymentsBefore.Count;
            Assert.Equal(2, countBefore);

            // Act: Void the second payment
            await paymentService.VoidPaymentAsync(secondPaymentId, BusinessId);

            // Assert: Count is preserved (no deletion)
            var allPaymentsAfter = await paymentRepo.GetAllPaymentsByInvoiceIdAsync(invoice.Id, BusinessId);
            Assert.Equal(countBefore, allPaymentsAfter.Count);

            // The voided payment still exists in the list
            var voidedPayment = allPaymentsAfter.FirstOrDefault(p => p.Id == secondPaymentId);
            Assert.NotNull(voidedPayment);
            Assert.True(voidedPayment!.IsVoided);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion
}
