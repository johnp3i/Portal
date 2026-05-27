using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for Draft/Issued Void Has No Financial Side-Effect.
/// Property 7: For any credit note in Draft or Issued status that is voided,
/// verify invoice outstanding balance and financial status remain unchanged,
/// no CreditNoteApplication records are voided, and RecalculateStatusAsync is NOT called.
///
/// **Validates: Requirements 5.9**
/// </summary>
public class CreditNoteDraftIssuedVoidNoSideEffectPropertyTests
{
    private const int TestBusinessId = 1;
    private const string TestUserId = "test-user-001";

    /// <summary>
    /// Creates a mocked CreditNoteService with all dependencies configured for void side-effect testing.
    /// Uses a mocked PortalDbContext with transaction support and empty VatSubmissions.
    /// Returns the service and relevant mocks for verification.
    /// </summary>
    private static (
        CreditNoteService service,
        Mock<CreditNoteRepository> creditNoteRepoMock,
        Mock<CreditNoteApplicationRepository> creditNoteAppRepoMock,
        Mock<IFinancialStatusEngine> financialStatusEngineMock
    ) CreateServiceWithMocks(CreditNote creditNoteToReturn)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        // Create in-memory DbContext with mocked transaction support
        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"DraftIssuedVoidNoSideEffect_{Guid.NewGuid()}")
            .Options;
        var dbContextMock = new Mock<PortalDbContext>(dbContextOptions, tenantMock.Object) { CallBase = true };

        // Mock transaction support (InMemory doesn't support transactions natively)
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

        // Setup: AuditLog insert completes successfully
        auditLogRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        // Setup: VoidByCreditNoteIdAsync (should NOT be called for Draft/Issued)
        creditNoteAppRepoMock
            .Setup(r => r.VoidByCreditNoteIdAsync(It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        // Setup: RecalculateStatusAsync (should NOT be called for Draft/Issued)
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

        return (service, creditNoteRepoMock, creditNoteAppRepoMock, financialStatusEngineMock);
    }

    /// <summary>
    /// Property 7: Draft/Issued Void Has No Financial Side-Effect
    /// **Validates: Requirements 5.9**
    ///
    /// For any credit note in Draft (1) or Issued (2) status that is voided,
    /// verify that:
    /// - VoidByCreditNoteIdAsync is NEVER called (no CreditNoteApplication records are voided)
    /// - RecalculateStatusAsync is NEVER called (invoice financial status remains unchanged)
    /// - The void operation succeeds (status transitions to Voided)
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Voiding_Draft_Or_Issued_CreditNote_Has_No_Financial_SideEffect()
    {
        // Generator: random Draft (1) or Issued (2) credit notes with random financial values
        var scenarioGen =
            from statusId in Gen.Elements(1, 2) // Draft or Issued
            from creditNoteId in Gen.Choose(1, 10000)
            from invoiceId in Gen.Choose(1, 10000)
            from totalAmount in Gen.Choose(100, 10000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from taxAmount in Gen.Choose(0, 1000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from vatPeriodId in Gen.Choose(1, 100)
            select (statusId, creditNoteId, invoiceId, totalAmount, taxAmount, vatPeriodId);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (statusId, creditNoteId, invoiceId, totalAmount, taxAmount, vatPeriodId) = scenario;

                // Build a credit note in Draft or Issued status
                var creditNote = new CreditNote
                {
                    Id = creditNoteId,
                    BusinessId = TestBusinessId,
                    InvoiceId = invoiceId,
                    CustomerId = 1,
                    CreditNoteStatusTypeId = statusId,
                    VatSubmissionPeriodId = vatPeriodId,
                    CreditNoteNumber = $"CN-2024-{(creditNoteId % 9999) + 1:D4}",
                    IssueDate = new DateOnly(2024, 6, 15),
                    Reason = "Test credit note",
                    Subtotal = totalAmount - taxAmount,
                    TaxAmount = taxAmount,
                    TotalAmount = totalAmount,
                    IssuedAtUtc = statusId == 2 ? DateTime.UtcNow : null,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var (service, creditNoteRepoMock, creditNoteAppRepoMock, financialStatusEngineMock) =
                    CreateServiceWithMocks(creditNote);

                // Act: void the Draft/Issued credit note
                var result = service.VoidCreditNoteAsync(creditNoteId, TestBusinessId, TestUserId)
                    .GetAwaiter().GetResult();

                // Assert: void operation should succeed
                var voidSucceeded = result.Success;

                // Assert: VoidByCreditNoteIdAsync should NEVER be called (no financial reversal)
                bool voidAppNeverCalled;
                try
                {
                    creditNoteAppRepoMock.Verify(
                        r => r.VoidByCreditNoteIdAsync(It.IsAny<int>()),
                        Times.Never());
                    voidAppNeverCalled = true;
                }
                catch
                {
                    voidAppNeverCalled = false;
                }

                // Assert: RecalculateStatusAsync should NEVER be called (no financial status change)
                bool recalculateNeverCalled;
                try
                {
                    financialStatusEngineMock.Verify(
                        e => e.RecalculateStatusAsync(It.IsAny<int>(), It.IsAny<int>()),
                        Times.Never());
                    recalculateNeverCalled = true;
                }
                catch
                {
                    recalculateNeverCalled = false;
                }

                var statusName = statusId == 1 ? "Draft" : "Issued";

                return voidSucceeded
                    .Label($"Void should succeed for {statusName} credit note (Id={creditNoteId}). Success={result.Success}, Message={result.Message}")
                    .And(voidAppNeverCalled
                        .Label($"VoidByCreditNoteIdAsync should NOT be called for {statusName} credit note (Id={creditNoteId})"))
                    .And(recalculateNeverCalled
                        .Label($"RecalculateStatusAsync should NOT be called for {statusName} credit note (Id={creditNoteId})"));
            });
    }
}
