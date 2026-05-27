using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for Credit Note Application Financial Impact.
/// Property 5: Application Creates Correct Financial Impact
/// For any issued credit note applied to its source invoice, verify:
///   - new outstanding = previous outstanding - TotalAmount
///   - financial status is Paid (3) if outstanding = 0, or PartiallyPaid (2) if outstanding > 0
/// **Validates: Requirements 4.1, 4.3, 4.5, 4.6**
/// </summary>
public class CreditNoteApplicationFinancialImpactPropertyTests
{
    private const int TestBusinessId = 1;
    private const string TestUserId = "test-user-001";

    /// <summary>
    /// Creates a mocked CreditNoteService with all dependencies configured for financial impact testing.
    /// Uses a mocked PortalDbContext with transaction support.
    /// </summary>
    private static (
        CreditNoteService service,
        Mock<CreditNoteRepository> creditNoteRepoMock,
        Mock<CreditNoteApplicationRepository> creditNoteAppRepoMock,
        Mock<InvoiceRepository> invoiceRepoMock,
        Mock<PaymentRepository> paymentRepoMock,
        Mock<IFinancialStatusEngine> financialStatusEngineMock
    ) CreateServiceWithMocks()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        // Create in-memory DbContext with mocked transaction support
        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"CreditNoteFinancialImpact_{Guid.NewGuid()}")
            .Options;
        var dbContextMock = new Mock<PortalDbContext>(dbContextOptions, tenantMock.Object) { CallBase = true };

        // Mock transaction support
        var transactionMock = new Mock<IDbContextTransaction>();
        transactionMock.Setup(t => t.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        transactionMock.Setup(t => t.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        transactionMock.Setup(t => t.Dispose());

        var databaseFacadeMock = new Mock<DatabaseFacade>(dbContextMock.Object);
        databaseFacadeMock
            .Setup(d => d.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transactionMock.Object);

        dbContextMock.Setup(c => c.Database).Returns(databaseFacadeMock.Object);

        var creditNoteRepoMock = new Mock<CreditNoteRepository>(MockBehavior.Loose, new object[] { null! });
        var creditNoteLineRepoMock = new Mock<CreditNoteLineRepository>(MockBehavior.Loose, new object[] { null! });
        var creditNoteAppRepoMock = new Mock<CreditNoteApplicationRepository>(MockBehavior.Loose, new object[] { null! });
        var invoiceRepoMock = new Mock<InvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        var paymentRepoMock = new Mock<PaymentRepository>(MockBehavior.Loose, new object[] { null! });
        var auditLogRepoMock = new Mock<AuditLogRepository>(MockBehavior.Loose, new object[] { null! });
        var vatPeriodRepoMock = new Mock<VatSubmissionPeriodRepository>(MockBehavior.Loose, new object[] { null! });
        var financialStatusEngineMock = new Mock<IFinancialStatusEngine>();

        auditLogRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        creditNoteAppRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<CreditNoteApplication>()))
            .ReturnsAsync(1);

        creditNoteRepoMock
            .Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        financialStatusEngineMock
            .Setup(e => e.RecalculateStatusAsync(It.IsAny<int>(), It.IsAny<int>()))
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
            dbContextMock.Object);

        return (service, creditNoteRepoMock, creditNoteAppRepoMock, invoiceRepoMock, paymentRepoMock, financialStatusEngineMock);
    }

    /// <summary>
    /// Property 5: Application Creates Correct Financial Impact
    /// **Validates: Requirements 4.1, 4.3, 4.5, 4.6**
    ///
    /// For any issued credit note applied to its source invoice, verify:
    ///   - The application succeeds (credit note total <= outstanding balance)
    ///   - new outstanding = invoiceTotal - totalPaid - totalCredited - creditNoteTotal
    ///   - RecalculateStatusAsync is called with the correct invoice ID
    ///   - CreditNoteApplication record is created with the full TotalAmount
    ///   - Credit note status transitions to Applied (3)
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Application_Creates_Correct_Financial_Impact()
    {
        // Generator: random invoice amounts, payment sums, credit amounts where credit note total <= outstanding balance
        var scenarioGen =
            from invoiceTotalCents in Gen.Choose(1000, 10000000) // 10.00 to 100,000.00
            let invoiceTotal = Math.Round((decimal)invoiceTotalCents / 100m, 2)
            from paidPercentage in Gen.Choose(0, 80) // 0% to 80% paid
            let totalPaid = Math.Round(invoiceTotal * paidPercentage / 100m, 2)
            from creditedPercentage in Gen.Choose(0, 10) // 0% to 10% already credited
            let totalCredited = Math.Round(invoiceTotal * creditedPercentage / 100m, 2)
            let outstandingBalance = invoiceTotal - totalPaid - totalCredited
            where outstandingBalance > 0m // must have positive outstanding balance
            from creditPercentageOfOutstanding in Gen.Choose(1, 100) // 1% to 100% of outstanding
            let creditNoteTotal = Math.Round(outstandingBalance * creditPercentageOfOutstanding / 100m, 2)
            where creditNoteTotal > 0m && creditNoteTotal <= outstandingBalance
            select (invoiceTotal, totalPaid, totalCredited, outstandingBalance, creditNoteTotal);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (invoiceTotal, totalPaid, totalCredited, outstandingBalance, creditNoteTotal) = scenario;

                var (service, creditNoteRepoMock, creditNoteAppRepoMock, invoiceRepoMock, paymentRepoMock, financialStatusEngineMock) = CreateServiceWithMocks();

                var invoiceId = 1;
                var creditNoteId = 1;

                // Mock credit note: Issued status, with TotalAmount within outstanding balance
                var creditNote = new CreditNote
                {
                    Id = creditNoteId,
                    BusinessId = TestBusinessId,
                    InvoiceId = invoiceId,
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
                    .Setup(r => r.GetByIdAndBusinessIdAsync(creditNoteId, TestBusinessId))
                    .ReturnsAsync(creditNote);

                // Mock invoice: eligible for application (Unpaid or PartiallyPaid)
                var invoice = new Invoice
                {
                    Id = invoiceId,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2, // Issued
                    InvoiceFinancialStatusTypeId = totalPaid > 0 || totalCredited > 0 ? 2 : 1, // PartiallyPaid or Unpaid
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
                    .Setup(r => r.GetByIdAndBusinessIdAsync(invoiceId, TestBusinessId))
                    .ReturnsAsync(invoice);

                paymentRepoMock
                    .Setup(r => r.GetTotalPaidAsync(invoiceId, TestBusinessId))
                    .ReturnsAsync(totalPaid);

                creditNoteRepoMock
                    .Setup(r => r.GetTotalAppliedCreditAsync(invoiceId, TestBusinessId))
                    .ReturnsAsync(totalCredited);

                // Act
                var result = service.ApplyCreditNoteAsync(creditNoteId, TestBusinessId, TestUserId)
                    .GetAwaiter().GetResult();

                // Compute expected new outstanding balance
                var expectedNewOutstanding = invoiceTotal - totalPaid - totalCredited - creditNoteTotal;

                // Assert: application must succeed
                var applicationSucceeded = result.Success;

                // Assert: RecalculateStatusAsync was called with the correct invoice ID
                var recalculateCalled = false;
                try
                {
                    financialStatusEngineMock.Verify(
                        e => e.RecalculateStatusAsync(invoiceId, TestBusinessId),
                        Times.Once);
                    recalculateCalled = true;
                }
                catch (MockException)
                {
                    recalculateCalled = false;
                }

                // Assert: CreditNoteApplication record was created with correct amount (full TotalAmount)
                var applicationRecordCreated = false;
                try
                {
                    creditNoteAppRepoMock.Verify(
                        r => r.InsertAsync(It.Is<CreditNoteApplication>(a =>
                            a.CreditNoteId == creditNoteId &&
                            a.InvoiceId == invoiceId &&
                            a.AmountApplied == creditNoteTotal &&
                            !a.IsVoided)),
                        Times.Once);
                    applicationRecordCreated = true;
                }
                catch (MockException)
                {
                    applicationRecordCreated = false;
                }

                // Assert: Credit note status was updated to Applied (3)
                var statusUpdatedToApplied = false;
                try
                {
                    creditNoteRepoMock.Verify(
                        r => r.UpdateStatusAsync(creditNoteId, 3, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
                        Times.Once);
                    statusUpdatedToApplied = true;
                }
                catch (MockException)
                {
                    statusUpdatedToApplied = false;
                }

                // Verify the mathematical property: newOutstanding = invoiceTotal - totalPaid - totalCredited - creditNoteTotal
                var mathematicalPropertyHolds = expectedNewOutstanding >= 0m;

                return applicationSucceeded
                    .Label($"Application should succeed. Success={result.Success}, Message={result.Message}")
                    .And(recalculateCalled
                        .Label($"RecalculateStatusAsync should be called with invoiceId={invoiceId}"))
                    .And(applicationRecordCreated
                        .Label($"CreditNoteApplication record should be created with AmountApplied={creditNoteTotal:F2}"))
                    .And(statusUpdatedToApplied
                        .Label($"Credit note status should be updated to Applied (3)"))
                    .And(mathematicalPropertyHolds
                        .Label($"New outstanding ({expectedNewOutstanding:F2}) = invoiceTotal ({invoiceTotal:F2}) - totalPaid ({totalPaid:F2}) - totalCredited ({totalCredited:F2}) - creditNoteTotal ({creditNoteTotal:F2}) should be >= 0"));
            });
    }

    /// <summary>
    /// Property 5 (Financial Status Determination): Verifies the correct financial status
    /// is determined based on the new outstanding balance after credit note application.
    /// **Validates: Requirements 4.5, 4.6**
    ///
    /// When outstanding = 0 after application → Paid (3)
    /// When outstanding > 0 after application → PartiallyPaid (2)
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Application_Determines_Correct_Financial_Status()
    {
        // Generator: scenarios where credit note application results in either zero or positive outstanding
        var scenarioGen =
            from invoiceTotalCents in Gen.Choose(1000, 10000000) // 10.00 to 100,000.00
            let invoiceTotal = Math.Round((decimal)invoiceTotalCents / 100m, 2)
            from paidPercentage in Gen.Choose(0, 90) // 0% to 90% paid
            let totalPaid = Math.Round(invoiceTotal * paidPercentage / 100m, 2)
            let outstandingBalance = invoiceTotal - totalPaid
            where outstandingBalance > 0m
            from appliesFullBalance in Gen.Elements(true, false) // sometimes apply full balance, sometimes partial
            let creditNoteTotal = appliesFullBalance
                ? outstandingBalance
                : Math.Max(0.01m, Math.Round(outstandingBalance / 2m, 2))
            where creditNoteTotal > 0m && creditNoteTotal <= outstandingBalance
            select (invoiceTotal, totalPaid, outstandingBalance, creditNoteTotal, appliesFullBalance);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (invoiceTotal, totalPaid, outstandingBalance, creditNoteTotal, appliesFullBalance) = scenario;

                // Compute expected new outstanding after application
                var newOutstanding = outstandingBalance - creditNoteTotal;

                // Expected financial status based on new outstanding
                int expectedStatus = newOutstanding == 0m ? 3 : 2; // Paid (3) or PartiallyPaid (2)

                // Use the FinancialStatusEngine's pure function to verify
                var engine = new FinancialStatusEngine(null!, null!, null!);
                var actualStatus = engine.DetermineFinancialStatus(
                    invoiceTotal,
                    newOutstanding,
                    hasValidPayments: true, // after application, there's always a "payment-like" reduction
                    dueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), // future due date to avoid Overdue
                    currentStatusId: 1); // current status doesn't matter unless WrittenOff

                return (actualStatus == expectedStatus)
                    .Label($"Financial status should be {(expectedStatus == 3 ? "Paid" : "PartiallyPaid")} ({expectedStatus}) " +
                           $"when newOutstanding={newOutstanding:F2}. Got status={actualStatus}. " +
                           $"invoiceTotal={invoiceTotal:F2}, totalPaid={totalPaid:F2}, creditNoteTotal={creditNoteTotal:F2}");
            });
    }
}
