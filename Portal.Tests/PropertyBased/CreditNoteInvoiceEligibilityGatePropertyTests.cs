using FsCheck;
using FsCheck.Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Property-based tests for Invoice Eligibility Gate (Property 11).
/// For any invoice with InvoiceStatusTypeId ≠ 2 or InvoiceFinancialStatusTypeId in {3, 5},
/// verify credit note creation/application is rejected.
///
/// Two paths tested:
/// 1. Creation: CreateCreditNoteAsync rejects if invoice.InvoiceStatusTypeId ≠ 2
/// 2. Application: ApplyCreditNoteAsync rejects if invoice.InvoiceFinancialStatusTypeId is 3 (Paid) or 5 (WrittenOff)
///
/// **Validates: Requirements 1.3, 4.9, 12.1**
/// </summary>
public class CreditNoteInvoiceEligibilityGatePropertyTests
{
    private const int TestBusinessId = 1;
    private const int TestInvoiceId = 1;
    private const int TestCreditNoteId = 100;
    private const string TestUserId = "user-eligibility-test";

    /// <summary>
    /// Creates a mocked CreditNoteService with all dependencies configured for eligibility testing.
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
            .UseInMemoryDatabase(databaseName: $"InvoiceEligibility_{Guid.NewGuid()}")
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

        // Mock transaction support for Apply operations
        var transactionMock = new Mock<IDbContextTransaction>();
        transactionMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        transactionMock.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        transactionMock.Setup(t => t.Dispose());

        var databaseFacadeMock = new Mock<DatabaseFacade>(dbContext);
        databaseFacadeMock
            .Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionMock.Object);

        var dbContextMock = new Mock<PortalDbContext>(dbContextOptions, tenantMock.Object) { CallBase = true };
        dbContextMock.Setup(c => c.Database).Returns(databaseFacadeMock.Object);

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
            dbContextMock.Object);

        return (service, creditNoteRepoMock, invoiceRepoMock, paymentRepoMock, dbContext);
    }

    /// <summary>
    /// Property 11 (Creation Path): Invoice Eligibility Gate
    /// **Validates: Requirements 1.3, 12.1**
    ///
    /// For any invoice with InvoiceStatusTypeId ≠ 2 (not Issued), verify that
    /// CreateCreditNoteAsync rejects the request with an appropriate error message.
    /// Generator: random InvoiceStatusTypeId values that are NOT 2.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Creation_Rejected_When_Invoice_Status_Is_Not_Issued()
    {
        // Generator: produce invoice status IDs that are NOT 2 (Issued)
        // Valid statuses in the system: 1=Draft, 3=Cancelled, 4=Overdue, etc.
        // We generate any positive int that is not 2
        var ineligibleStatusGen =
            from statusId in Gen.Choose(1, 10)
            where statusId != 2
            select statusId;

        return Prop.ForAll(
            ineligibleStatusGen.ToArbitrary(),
            ineligibleStatusId =>
            {
                var (service, creditNoteRepoMock, invoiceRepoMock, paymentRepoMock, _) = CreateServiceWithMocks();

                // Mock invoice with non-Issued status
                var invoice = new Invoice
                {
                    Id = TestInvoiceId,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = ineligibleStatusId, // NOT 2 (not Issued)
                    InvoiceFinancialStatusTypeId = 1, // Unpaid
                    InvoiceNumber = "INV-2025-0001",
                    InvoiceDate = new DateOnly(2025, 1, 1),
                    DueDate = new DateOnly(2025, 2, 1),
                    Subtotal = 1000m,
                    TaxAmount = 150m,
                    TotalAmount = 1150m,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                invoiceRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(TestInvoiceId, TestBusinessId))
                    .ReturnsAsync(invoice);

                paymentRepoMock
                    .Setup(r => r.GetTotalPaidAsync(TestInvoiceId, TestBusinessId))
                    .ReturnsAsync(0m);

                creditNoteRepoMock
                    .Setup(r => r.GetTotalAppliedCreditAsync(TestInvoiceId, TestBusinessId))
                    .ReturnsAsync(0m);

                // Build a valid DTO (only the invoice status should cause rejection)
                var dto = new CreateCreditNoteDto
                {
                    InvoiceId = TestInvoiceId,
                    IssueDate = new DateOnly(2025, 1, 15),
                    Reason = "Valid reason for credit note",
                    VatSubmissionPeriodId = 1,
                    Lines = new List<CreateCreditNoteLineDto>
                    {
                        new CreateCreditNoteLineDto
                        {
                            Description = "Test line item",
                            Quantity = 1m,
                            UnitPrice = 100m,
                            VatRate = 15m
                        }
                    }
                };

                // Act
                var result = service.CreateCreditNoteAsync(dto, TestBusinessId, TestUserId)
                    .GetAwaiter().GetResult();

                // Assert: creation must be rejected
                var isRejected = !result.Success;
                var hasStatusMessage = result.Message != null
                    && result.Message.Contains("Issued status", StringComparison.OrdinalIgnoreCase);

                return isRejected
                    .Label($"Creation should be rejected when InvoiceStatusTypeId={ineligibleStatusId} (not 2). " +
                           $"Success={result.Success}, Message={result.Message}")
                    .And(hasStatusMessage
                        .Label($"Error message should mention 'Issued status'. Got: {result.Message}"));
            });
    }

    /// <summary>
    /// Property 11 (Application Path): Invoice Eligibility Gate
    /// **Validates: Requirements 4.9**
    ///
    /// For any invoice with InvoiceFinancialStatusTypeId in {3 (Paid), 5 (WrittenOff)},
    /// verify that ApplyCreditNoteAsync rejects the application with an appropriate error message.
    /// Generator: random invoices with financial status 3 or 5.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Application_Rejected_When_Invoice_Financial_Status_Is_Paid_Or_WrittenOff()
    {
        // Generator: produce financial status IDs that are ineligible (3=Paid, 5=WrittenOff)
        var ineligibleFinancialStatusGen = Gen.Elements(3, 5);

        return Prop.ForAll(
            ineligibleFinancialStatusGen.ToArbitrary(),
            ineligibleFinancialStatusId =>
            {
                var (service, creditNoteRepoMock, invoiceRepoMock, paymentRepoMock, _) = CreateServiceWithMocks();

                // Mock credit note: Issued status (eligible for application)
                var creditNote = new CreditNote
                {
                    Id = TestCreditNoteId,
                    BusinessId = TestBusinessId,
                    InvoiceId = TestInvoiceId,
                    CustomerId = 1,
                    CreditNoteStatusTypeId = 2, // Issued (valid for application)
                    VatSubmissionPeriodId = 1,
                    CreditNoteNumber = "CN-2025-0001",
                    IssueDate = new DateOnly(2025, 1, 15),
                    Reason = "Test credit note",
                    Subtotal = 100m,
                    TaxAmount = 15m,
                    TotalAmount = 115m,
                    IssuedAtUtc = DateTime.UtcNow.AddDays(-1),
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
                };

                creditNoteRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(TestCreditNoteId, TestBusinessId))
                    .ReturnsAsync(creditNote);

                // Mock invoice with ineligible financial status (Paid=3 or WrittenOff=5)
                var invoice = new Invoice
                {
                    Id = TestInvoiceId,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2, // Issued
                    InvoiceFinancialStatusTypeId = ineligibleFinancialStatusId, // Paid or WrittenOff
                    InvoiceNumber = "INV-2025-0001",
                    InvoiceDate = new DateOnly(2025, 1, 1),
                    DueDate = new DateOnly(2025, 2, 1),
                    Subtotal = 1000m,
                    TaxAmount = 150m,
                    TotalAmount = 1150m,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                invoiceRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(TestInvoiceId, TestBusinessId))
                    .ReturnsAsync(invoice);

                paymentRepoMock
                    .Setup(r => r.GetTotalPaidAsync(TestInvoiceId, TestBusinessId))
                    .ReturnsAsync(0m);

                creditNoteRepoMock
                    .Setup(r => r.GetTotalAppliedCreditAsync(TestInvoiceId, TestBusinessId))
                    .ReturnsAsync(0m);

                // Act
                var result = service.ApplyCreditNoteAsync(TestCreditNoteId, TestBusinessId, TestUserId)
                    .GetAwaiter().GetResult();

                // Assert: application must be rejected
                var isRejected = !result.Success;
                var hasEligibilityMessage = result.Message != null
                    && result.Message.Contains("not eligible", StringComparison.OrdinalIgnoreCase);

                var statusLabel = ineligibleFinancialStatusId == 3 ? "Paid" : "WrittenOff";

                return isRejected
                    .Label($"Application should be rejected when InvoiceFinancialStatusTypeId={ineligibleFinancialStatusId} ({statusLabel}). " +
                           $"Success={result.Success}, Message={result.Message}")
                    .And(hasEligibilityMessage
                        .Label($"Error message should mention 'not eligible'. Got: {result.Message}"));
            });
    }
}
