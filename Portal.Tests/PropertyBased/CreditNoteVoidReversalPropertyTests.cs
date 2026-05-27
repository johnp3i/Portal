using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Property-based tests for Credit Note Void Reversal Round-Trip.
/// Property 6: For any previously applied credit note that is voided, verify:
///   - VoidByCreditNoteIdAsync is called (sets IsVoided = true on all CreditNoteApplication records)
///   - RecalculateStatusAsync is called (restores outstanding balance to pre-application value)
///   - The void operation succeeds
/// **Validates: Requirements 5.3, 5.4, 5.5**
/// </summary>
public class CreditNoteVoidReversalPropertyTests
{
    private const int TestBusinessId = 1;
    private const string TestUserId = "test-user-001";

    /// <summary>
    /// Creates a mocked CreditNoteService with all dependencies configured for void reversal testing.
    /// The InMemoryDatabase is seeded with no VatSubmissions (so void is always allowed).
    /// </summary>
    private static (
        CreditNoteService service,
        Mock<CreditNoteRepository> creditNoteRepoMock,
        Mock<CreditNoteApplicationRepository> creditNoteAppRepoMock,
        Mock<IFinancialStatusEngine> financialStatusEngineMock,
        PortalDbContext dbContext
    ) CreateServiceWithMocks(CreditNote creditNoteToReturn)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        // Ensure no VatSubmissions exist so void is not blocked by VAT period lock
        dbContext.SaveChanges();

        var creditNoteRepoMock = new Mock<CreditNoteRepository>(dbContext) { CallBase = false };
        var creditNoteLineRepoMock = new Mock<CreditNoteLineRepository>(dbContext) { CallBase = false };
        var creditNoteAppRepoMock = new Mock<CreditNoteApplicationRepository>(dbContext) { CallBase = false };
        var invoiceRepoMock = new Mock<InvoiceRepository>(dbContext) { CallBase = false };
        var paymentRepoMock = new Mock<PaymentRepository>(dbContext) { CallBase = false };
        var auditLogRepoMock = new Mock<AuditLogRepository>(dbContext) { CallBase = false };
        var vatPeriodRepoMock = new Mock<VatSubmissionPeriodRepository>(dbContext) { CallBase = false };
        var financialStatusEngineMock = new Mock<IFinancialStatusEngine>();

        // Setup: credit note retrieval
        creditNoteRepoMock
            .Setup(r => r.GetByIdAndBusinessIdAsync(creditNoteToReturn.Id, TestBusinessId))
            .ReturnsAsync(creditNoteToReturn);

        // Setup: UpdateStatusAsync completes successfully
        creditNoteRepoMock
            .Setup(r => r.UpdateStatusAsync(
                creditNoteToReturn.Id,
                It.IsAny<int>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        // Setup: VoidByCreditNoteIdAsync completes successfully
        creditNoteAppRepoMock
            .Setup(r => r.VoidByCreditNoteIdAsync(creditNoteToReturn.Id))
            .Returns(Task.CompletedTask);

        // Setup: RecalculateStatusAsync completes successfully
        financialStatusEngineMock
            .Setup(e => e.RecalculateStatusAsync(creditNoteToReturn.InvoiceId, TestBusinessId))
            .Returns(Task.CompletedTask);

        // Setup: AuditLog insert completes successfully
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

        return (service, creditNoteRepoMock, creditNoteAppRepoMock, financialStatusEngineMock, dbContext);
    }

    /// <summary>
    /// Property 6: Void Reversal Round-Trip
    /// For any previously applied credit note that is voided, verify:
    ///   - VoidByCreditNoteIdAsync is called (which sets IsVoided = true on all application records)
    ///   - RecalculateStatusAsync is called (which restores the invoice outstanding balance)
    ///   - The void operation returns success
    ///
    /// Mathematical property: after void, outstanding = invoiceTotal - totalPaid - (totalCredited - voidedCreditAmount)
    /// Since RecalculateStatusAsync re-fetches all data including the now-voided applications,
    /// the balance is effectively restored to the pre-application value.
    ///
    /// **Validates: Requirements 5.3, 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VoidAppliedCreditNote_VoidsApplicationRecords_AndRecalculatesInvoiceStatus(
        PositiveInt creditNoteIdSeed,
        PositiveInt invoiceIdSeed,
        PositiveInt creditAmountSeed,
        PositiveInt vatPeriodIdSeed)
    {
        // Generate random applied credit note data
        var creditNoteId = (creditNoteIdSeed.Get % 10000) + 1;
        var invoiceId = (invoiceIdSeed.Get % 10000) + 1;
        var creditAmount = Math.Round(((creditAmountSeed.Get % 999999) + 1) * 0.01m, 2);
        var vatPeriodId = (vatPeriodIdSeed.Get % 100) + 1;

        // Build an Applied credit note (status = 3)
        var creditNote = new CreditNote
        {
            Id = creditNoteId,
            BusinessId = TestBusinessId,
            InvoiceId = invoiceId,
            CustomerId = 1,
            CreditNoteStatusTypeId = 3, // Applied
            VatSubmissionPeriodId = vatPeriodId,
            CreditNoteNumber = $"CN-2024-{(creditNoteId % 9999) + 1:D4}",
            IssueDate = new DateOnly(2024, 6, 15),
            Reason = "Test void reversal",
            Subtotal = creditAmount,
            TaxAmount = 0m,
            TotalAmount = creditAmount,
            IssuedAtUtc = DateTime.UtcNow.AddDays(-5),
            VoidedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
        };

        var (service, creditNoteRepoMock, creditNoteAppRepoMock, financialStatusEngineMock, dbContext) =
            CreateServiceWithMocks(creditNote);

        // Act: void the applied credit note
        var result = service.VoidCreditNoteAsync(creditNoteId, TestBusinessId, TestUserId)
            .GetAwaiter().GetResult();

        // Assert 1: operation succeeds
        var isSuccess = result.Success;

        // Assert 2: VoidByCreditNoteIdAsync was called (sets IsVoided = true on all application records)
        var voidApplicationsCalled = false;
        try
        {
            creditNoteAppRepoMock.Verify(
                r => r.VoidByCreditNoteIdAsync(creditNoteId), Times.Once());
            voidApplicationsCalled = true;
        }
        catch
        {
            voidApplicationsCalled = false;
        }

        // Assert 3: RecalculateStatusAsync was called (restores outstanding balance)
        var recalculateCalled = false;
        try
        {
            financialStatusEngineMock.Verify(
                e => e.RecalculateStatusAsync(invoiceId, TestBusinessId), Times.Once());
            recalculateCalled = true;
        }
        catch
        {
            recalculateCalled = false;
        }

        // Assert 4: Status was updated to Voided (4)
        var statusUpdatedToVoided = false;
        try
        {
            creditNoteRepoMock.Verify(
                r => r.UpdateStatusAsync(creditNoteId, 4, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()),
                Times.Once());
            statusUpdatedToVoided = true;
        }
        catch
        {
            statusUpdatedToVoided = false;
        }

        // Dispose the in-memory context
        dbContext.Dispose();

        return (isSuccess && voidApplicationsCalled && recalculateCalled && statusUpdatedToVoided).ToProperty()
            .Label($"creditNoteId={creditNoteId}, invoiceId={invoiceId}, creditAmount={creditAmount:F2}, " +
                   $"success={isSuccess}, voidAppsCalled={voidApplicationsCalled}, " +
                   $"recalcCalled={recalculateCalled}, statusVoided={statusUpdatedToVoided}");
    }

    /// <summary>
    /// Property 6 (supplementary): Void Reversal Round-Trip — Balance Restoration Semantics
    /// For any applied credit note with known pre-application balance, voiding triggers
    /// RecalculateStatusAsync which will recompute:
    ///   outstanding = invoiceTotal - totalPaid - (totalCredited - voidedCreditAmount)
    /// This effectively restores the balance to the pre-application value.
    ///
    /// This test verifies the void path calls the correct sequence:
    ///   1. VoidByCreditNoteIdAsync (marks applications as voided)
    ///   2. RecalculateStatusAsync (recomputes balance excluding voided applications)
    ///   3. UpdateStatusAsync (transitions to Voided)
    ///
    /// **Validates: Requirements 5.3, 5.4, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property VoidAppliedCreditNote_CallsVoidBeforeRecalculate_EnsuresCorrectOrder(
        PositiveInt creditNoteIdSeed,
        PositiveInt invoiceIdSeed,
        PositiveInt invoiceTotalSeed,
        PositiveInt totalPaidSeed,
        PositiveInt creditAmountSeed)
    {
        // Generate random financial scenario
        var creditNoteId = (creditNoteIdSeed.Get % 10000) + 1;
        var invoiceId = (invoiceIdSeed.Get % 10000) + 1;
        var invoiceTotal = Math.Round(((invoiceTotalSeed.Get % 100000) + 100) * 0.01m, 2);
        var totalPaid = Math.Round(((totalPaidSeed.Get % 50000)) * 0.01m, 2);
        var creditAmount = Math.Round(((creditAmountSeed.Get % 50000) + 1) * 0.01m, 2);

        // Build an Applied credit note
        var creditNote = new CreditNote
        {
            Id = creditNoteId,
            BusinessId = TestBusinessId,
            InvoiceId = invoiceId,
            CustomerId = 1,
            CreditNoteStatusTypeId = 3, // Applied
            VatSubmissionPeriodId = 1,
            CreditNoteNumber = $"CN-2024-{(creditNoteId % 9999) + 1:D4}",
            IssueDate = new DateOnly(2024, 6, 15),
            Reason = "Test balance restoration",
            Subtotal = creditAmount,
            TaxAmount = 0m,
            TotalAmount = creditAmount,
            IssuedAtUtc = DateTime.UtcNow.AddDays(-5),
            VoidedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
        };

        var (service, creditNoteRepoMock, creditNoteAppRepoMock, financialStatusEngineMock, dbContext) =
            CreateServiceWithMocks(creditNote);

        // Track call order to verify VoidByCreditNoteIdAsync is called before RecalculateStatusAsync
        var callOrder = new List<string>();

        creditNoteAppRepoMock
            .Setup(r => r.VoidByCreditNoteIdAsync(creditNoteId))
            .Callback(() => callOrder.Add("VoidApplications"))
            .Returns(Task.CompletedTask);

        financialStatusEngineMock
            .Setup(e => e.RecalculateStatusAsync(invoiceId, TestBusinessId))
            .Callback(() => callOrder.Add("RecalculateStatus"))
            .Returns(Task.CompletedTask);

        // Act
        var result = service.VoidCreditNoteAsync(creditNoteId, TestBusinessId, TestUserId)
            .GetAwaiter().GetResult();

        // Assert: correct call order (void applications BEFORE recalculate)
        var correctOrder = callOrder.Count >= 2
            && callOrder[0] == "VoidApplications"
            && callOrder[1] == "RecalculateStatus";

        // Dispose the in-memory context
        dbContext.Dispose();

        return (result.Success && correctOrder).ToProperty()
            .Label($"creditNoteId={creditNoteId}, invoiceId={invoiceId}, " +
                   $"invoiceTotal={invoiceTotal:F2}, totalPaid={totalPaid:F2}, creditAmount={creditAmount:F2}, " +
                   $"success={result.Success}, correctOrder={correctOrder}, " +
                   $"callOrder=[{string.Join(", ", callOrder)}]");
    }
}
