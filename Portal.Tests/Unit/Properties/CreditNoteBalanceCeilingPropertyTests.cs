using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for Credit Note Balance Ceiling Validation.
/// Property 4: For any credit note where TotalAmount > outstanding balance,
/// verify creation/application is rejected.
/// </summary>
public class CreditNoteBalanceCeilingPropertyTests
{
    private const int TestBusinessId = 1;
    private const string TestUserId = "test-user-001";

    /// <summary>
    /// Creates a mocked CreditNoteService with all dependencies configured for balance ceiling testing.
    /// </summary>
    private static (
        CreditNoteService service,
        Mock<CreditNoteRepository> creditNoteRepoMock,
        Mock<InvoiceRepository> invoiceRepoMock,
        Mock<PaymentRepository> paymentRepoMock,
        PortalDbContext dbContext
    ) CreateServiceWithMocks()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        var creditNoteRepoMock = new Mock<CreditNoteRepository>(dbContext) { CallBase = false };
        var creditNoteLineRepoMock = new Mock<CreditNoteLineRepository>(dbContext) { CallBase = false };
        var creditNoteAppRepoMock = new Mock<CreditNoteApplicationRepository>(dbContext) { CallBase = false };
        var invoiceRepoMock = new Mock<InvoiceRepository>(dbContext) { CallBase = false };
        var paymentRepoMock = new Mock<PaymentRepository>(dbContext) { CallBase = false };
        var auditLogRepoMock = new Mock<AuditLogRepository>(dbContext) { CallBase = false };
        var vatPeriodRepoMock = new Mock<VatSubmissionPeriodRepository>(dbContext) { CallBase = false };
        var financialStatusEngineMock = new Mock<IFinancialStatusEngine>();

        auditLogRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        var service = new CreditNoteService(
            creditNoteRepoMock.Object,
            creditNoteLineRepoMock.Object,
            creditNoteAppRepoMock.Object,
            invoiceRepoMock.Object,
            paymentRepoMock.Object,
            auditLogRepoMock.Object,
            vatPeriodRepoMock.Object,
            financialStatusEngineMock.Object,
            tenantMock.Object,
            dbContext);

        return (service, creditNoteRepoMock, invoiceRepoMock, paymentRepoMock, dbContext);
    }

    /// <summary>
    /// Property 4 (Creation Path): Balance Ceiling Validation
    /// **Validates: Requirements 1.10**
    ///
    /// For any (invoiceTotal, totalPaid, totalCredited, creditNoteLineTotal) where the computed
    /// credit note TotalAmount exceeds the outstanding balance (invoiceTotal - totalPaid - totalCredited),
    /// CreateCreditNoteAsync must reject the creation with a failure result.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Creation_Rejected_When_TotalAmount_Exceeds_OutstandingBalance()
    {
        // Generator: produce financial values where credit note total > outstanding balance
        var scenarioGen =
            from invoiceTotal in Gen.Choose(100, 10000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from totalPaid in Gen.Choose(0, 5000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from totalCredited in Gen.Choose(0, 5000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from excessCents in Gen.Choose(1, 1000000) // ensure credit note exceeds balance by at least 0.01
            let outstandingBalance = invoiceTotal - totalPaid - totalCredited
            where outstandingBalance >= 0m // only valid scenarios where balance is non-negative
            let creditNoteTotal = outstandingBalance + Math.Round((decimal)excessCents / 100m, 2)
            where creditNoteTotal > 0m
            select (invoiceTotal, totalPaid, totalCredited, outstandingBalance, creditNoteTotal);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (invoiceTotal, totalPaid, totalCredited, outstandingBalance, creditNoteTotal) = scenario;

                var (service, creditNoteRepoMock, invoiceRepoMock, paymentRepoMock, dbContext) = CreateServiceWithMocks();

                // Mock invoice: Issued status (eligible for credit note)
                var invoice = new Invoice
                {
                    Id = 1,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2, // Issued
                    InvoiceFinancialStatusTypeId = 1,
                    InvoiceNumber = "INV-2024-0001",
                    InvoiceDate = new DateOnly(2024, 6, 1),
                    DueDate = new DateOnly(2024, 7, 1),
                    Subtotal = invoiceTotal,
                    TaxAmount = 0m,
                    TotalAmount = invoiceTotal,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                invoiceRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(1, TestBusinessId))
                    .ReturnsAsync(invoice);

                paymentRepoMock
                    .Setup(r => r.GetTotalPaidAsync(1, TestBusinessId))
                    .ReturnsAsync(totalPaid);

                creditNoteRepoMock
                    .Setup(r => r.GetTotalAppliedCreditAsync(1, TestBusinessId))
                    .ReturnsAsync(totalCredited);

                // Build a DTO with a single line that produces the desired total
                // creditNoteTotal = quantity * unitPrice + tax
                // Use quantity=1, unitPrice=creditNoteTotal, vatRate=0 for simplicity
                var dto = new CreateCreditNoteDto
                {
                    InvoiceId = 1,
                    IssueDate = new DateOnly(2024, 6, 15),
                    Reason = "Test credit note exceeding balance",
                    VatSubmissionPeriodId = 1,
                    Lines = new List<CreateCreditNoteLineDto>
                    {
                        new CreateCreditNoteLineDto
                        {
                            Description = "Test line item",
                            Quantity = 1m,
                            UnitPrice = creditNoteTotal,
                            VatRate = 0m
                        }
                    }
                };

                // Act
                var result = service.CreateCreditNoteAsync(dto, TestBusinessId, TestUserId)
                    .GetAwaiter().GetResult();

                // Assert: creation must be rejected
                var isRejected = !result.Success;
                var hasBalanceMessage = result.Message != null
                    && result.Message.Contains("exceeds", StringComparison.OrdinalIgnoreCase);

                return isRejected
                    .Label($"Creation should be rejected when TotalAmount ({creditNoteTotal:F2}) > OutstandingBalance ({outstandingBalance:F2}). " +
                           $"Success={result.Success}, Message={result.Message}")
                    .And(hasBalanceMessage
                        .Label($"Error message should mention 'exceeds'. Got: {result.Message}"));
            });
    }

    /// <summary>
    /// Property 4 (Application Path): Balance Ceiling Validation
    /// **Validates: Requirements 4.7**
    ///
    /// For any credit note in Issued status where TotalAmount > current outstanding balance
    /// at the time of application, ApplyCreditNoteAsync must reject the application.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Application_Rejected_When_CreditNoteAmount_Exceeds_OutstandingBalance()
    {
        // Generator: produce financial values where credit note total > outstanding balance at application time
        var scenarioGen =
            from invoiceTotal in Gen.Choose(100, 10000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from totalPaid in Gen.Choose(0, 5000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from totalCredited in Gen.Choose(0, 5000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from excessCents in Gen.Choose(1, 1000000) // ensure credit note exceeds balance by at least 0.01
            let outstandingBalance = invoiceTotal - totalPaid - totalCredited
            where outstandingBalance >= 0m // only valid scenarios where balance is non-negative
            let creditNoteTotal = outstandingBalance + Math.Round((decimal)excessCents / 100m, 2)
            where creditNoteTotal > 0m
            select (invoiceTotal, totalPaid, totalCredited, outstandingBalance, creditNoteTotal);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (invoiceTotal, totalPaid, totalCredited, outstandingBalance, creditNoteTotal) = scenario;

                var (service, creditNoteRepoMock, invoiceRepoMock, paymentRepoMock, dbContext) = CreateServiceWithMocks();

                // Mock credit note: Issued status, with TotalAmount exceeding outstanding balance
                var creditNote = new CreditNote
                {
                    Id = 1,
                    BusinessId = TestBusinessId,
                    InvoiceId = 1,
                    CustomerId = 1,
                    CreditNoteStatusTypeId = 2, // Issued
                    VatSubmissionPeriodId = 1,
                    CreditNoteNumber = "CN-2024-0001",
                    IssueDate = new DateOnly(2024, 6, 15),
                    Reason = "Test credit note",
                    Subtotal = creditNoteTotal,
                    TaxAmount = 0m,
                    TotalAmount = creditNoteTotal,
                    IssuedAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow
                };

                creditNoteRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(1, TestBusinessId))
                    .ReturnsAsync(creditNote);

                // Mock invoice: eligible for application (not Paid or WrittenOff)
                var invoice = new Invoice
                {
                    Id = 1,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2, // Issued
                    InvoiceFinancialStatusTypeId = 1, // Unpaid (eligible)
                    InvoiceNumber = "INV-2024-0001",
                    InvoiceDate = new DateOnly(2024, 6, 1),
                    DueDate = new DateOnly(2024, 7, 1),
                    Subtotal = invoiceTotal,
                    TaxAmount = 0m,
                    TotalAmount = invoiceTotal,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                invoiceRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(1, TestBusinessId))
                    .ReturnsAsync(invoice);

                paymentRepoMock
                    .Setup(r => r.GetTotalPaidAsync(1, TestBusinessId))
                    .ReturnsAsync(totalPaid);

                creditNoteRepoMock
                    .Setup(r => r.GetTotalAppliedCreditAsync(1, TestBusinessId))
                    .ReturnsAsync(totalCredited);

                // Act
                var result = service.ApplyCreditNoteAsync(1, TestBusinessId, TestUserId)
                    .GetAwaiter().GetResult();

                // Assert: application must be rejected
                var isRejected = !result.Success;
                var hasBalanceMessage = result.Message != null
                    && result.Message.Contains("exceeds", StringComparison.OrdinalIgnoreCase);

                return isRejected
                    .Label($"Application should be rejected when CreditNote.TotalAmount ({creditNoteTotal:F2}) > OutstandingBalance ({outstandingBalance:F2}). " +
                           $"Success={result.Success}, Message={result.Message}")
                    .And(hasBalanceMessage
                        .Label($"Error message should mention 'exceeds'. Got: {result.Message}"));
            });
    }
}
