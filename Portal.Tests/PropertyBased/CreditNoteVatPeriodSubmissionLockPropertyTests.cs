using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Property-based tests for VAT Period Submission Lock.
/// Property 9: For any credit note assigned to a submitted VAT period:
///   - Verify creation is rejected (Requirement 6.5)
///   - For non-Draft credit notes in submitted periods, verify voiding is rejected (Requirement 6.6)
///   - For Draft credit notes in submitted periods, void should still succeed (Draft has no financial impact)
///
/// **Validates: Requirements 6.5, 6.6**
/// </summary>
public class CreditNoteVatPeriodSubmissionLockPropertyTests
{
    private const int TestBusinessId = 1;
    private const string TestUserId = "test-user-001";

    /// <summary>
    /// Creates a CreditNoteService with an InMemoryDatabase seeded with a VatSubmission record.
    /// The VatSubmission's IsSubmitted flag is controlled by the parameter.
    /// </summary>
    private static (
        CreditNoteService service,
        Mock<CreditNoteRepository> creditNoteRepoMock,
        Mock<InvoiceRepository> invoiceRepoMock,
        Mock<PaymentRepository> paymentRepoMock,
        PortalDbContext dbContext
    ) CreateServiceForVoidTest(CreditNote creditNoteToReturn, int vatPeriodId, bool isSubmitted)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"VatPeriodLock_Void_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        // Seed a VatSubmissionPeriod
        dbContext.VatSubmissionPeriods.Add(new VatSubmissionPeriod
        {
            Id = vatPeriodId,
            BusinessId = TestBusinessId,
            PeriodStartDate = new DateOnly(2024, 1, 1),
            PeriodEndDate = new DateOnly(2024, 3, 31),
            PeriodLabel = "Jan-Mar 2024",
            CreatedAtUtc = DateTime.UtcNow
        });

        // Seed a VatSubmission record with the specified IsSubmitted flag
        dbContext.VatSubmissions.Add(new VatSubmission
        {
            Id = vatPeriodId, // Use same Id for simplicity
            BusinessId = TestBusinessId,
            VatSubmissionPeriodId = vatPeriodId,
            TotalOutputVat = 1000m,
            TotalInputVat = 500m,
            NetVatPayable = 500m,
            IsSubmitted = isSubmitted,
            SubmittedAtUtc = isSubmitted ? DateTime.UtcNow.AddDays(-1) : null,
            CreatedAtUtc = DateTime.UtcNow
        });

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
            .Setup(e => e.RecalculateStatusAsync(It.IsAny<int>(), It.IsAny<int>()))
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

        return (service, creditNoteRepoMock, invoiceRepoMock, paymentRepoMock, dbContext);
    }

    /// <summary>
    /// Creates a CreditNoteService with an InMemoryDatabase seeded with a VatSubmission record
    /// for testing the creation path. All repositories are mocked to avoid relational provider issues.
    /// </summary>
    private static (
        CreditNoteService service,
        Mock<CreditNoteRepository> creditNoteRepoMock,
        Mock<InvoiceRepository> invoiceRepoMock,
        Mock<PaymentRepository> paymentRepoMock,
        PortalDbContext dbContext
    ) CreateServiceForCreationTest(int vatPeriodId, bool isSubmitted)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"VatPeriodLock_Create_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        // Seed a VatSubmissionPeriod
        dbContext.VatSubmissionPeriods.Add(new VatSubmissionPeriod
        {
            Id = vatPeriodId,
            BusinessId = TestBusinessId,
            PeriodStartDate = new DateOnly(2024, 1, 1),
            PeriodEndDate = new DateOnly(2024, 3, 31),
            PeriodLabel = "Jan-Mar 2024",
            CreatedAtUtc = DateTime.UtcNow
        });

        // Seed a VatSubmission record with the specified IsSubmitted flag
        dbContext.VatSubmissions.Add(new VatSubmission
        {
            Id = vatPeriodId,
            BusinessId = TestBusinessId,
            VatSubmissionPeriodId = vatPeriodId,
            TotalOutputVat = 1000m,
            TotalInputVat = 500m,
            NetVatPayable = 500m,
            IsSubmitted = isSubmitted,
            SubmittedAtUtc = isSubmitted ? DateTime.UtcNow.AddDays(-1) : null,
            CreatedAtUtc = DateTime.UtcNow
        });

        dbContext.SaveChanges();

        var creditNoteRepoMock = new Mock<CreditNoteRepository>(dbContext) { CallBase = false };
        var creditNoteLineRepoMock = new Mock<CreditNoteLineRepository>(dbContext) { CallBase = false };
        var creditNoteAppRepoMock = new Mock<CreditNoteApplicationRepository>(dbContext) { CallBase = false };
        var invoiceRepoMock = new Mock<InvoiceRepository>(dbContext) { CallBase = false };
        var paymentRepoMock = new Mock<PaymentRepository>(dbContext) { CallBase = false };
        var auditLogRepoMock = new Mock<AuditLogRepository>(dbContext) { CallBase = false };
        var vatPeriodRepoMock = new Mock<VatSubmissionPeriodRepository>(dbContext) { CallBase = false };
        var financialStatusEngineMock = new Mock<IFinancialStatusEngine>();

        // Setup: AuditLog insert completes successfully
        auditLogRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        // Setup: CreditNote insert returns an Id
        creditNoteRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<CreditNote>()))
            .ReturnsAsync(1);

        // Setup: GetHighestNumberForYearAsync returns null (first credit note)
        creditNoteRepoMock
            .Setup(r => r.GetHighestNumberForYearAsync(TestBusinessId, It.IsAny<int>()))
            .ReturnsAsync((int?)null);

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
    /// Property 9 (Part A): VAT Period Submission Lock — Void Rejection
    /// For any non-Draft credit note (Issued or Applied) assigned to a submitted VAT period,
    /// verify that voiding is rejected with the appropriate error message.
    ///
    /// **Validates: Requirement 6.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonDraft_CreditNote_In_SubmittedPeriod_Void_Is_Rejected()
    {
        var scenarioGen =
            from statusId in Gen.Elements(2, 3) // Issued or Applied (non-Draft)
            from creditNoteId in Gen.Choose(1, 10000)
            from invoiceId in Gen.Choose(1, 10000)
            from totalAmount in Gen.Choose(100, 1000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from vatPeriodId in Gen.Choose(1, 100)
            select (statusId, creditNoteId, invoiceId, totalAmount, vatPeriodId);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (statusId, creditNoteId, invoiceId, totalAmount, vatPeriodId) = scenario;

                var creditNote = new CreditNote
                {
                    Id = creditNoteId,
                    BusinessId = TestBusinessId,
                    InvoiceId = invoiceId,
                    CustomerId = 1,
                    CreditNoteStatusTypeId = statusId,
                    VatSubmissionPeriodId = vatPeriodId,
                    CreditNoteNumber = $"CN-2024-{(creditNoteId % 9999) + 1:D4}",
                    IssueDate = new DateOnly(2024, 2, 15),
                    Reason = "Test VAT period lock",
                    Subtotal = totalAmount,
                    TaxAmount = Math.Round(totalAmount * 0.15m, 2),
                    TotalAmount = totalAmount + Math.Round(totalAmount * 0.15m, 2),
                    IssuedAtUtc = DateTime.UtcNow.AddDays(-5),
                    VoidedAtUtc = null,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
                };

                var (service, _, _, _, dbContext) =
                    CreateServiceForVoidTest(creditNote, vatPeriodId, isSubmitted: true);

                try
                {
                    // Act: attempt to void the non-Draft credit note in a submitted period
                    var result = service.VoidCreditNoteAsync(creditNoteId, TestBusinessId, TestUserId)
                        .GetAwaiter().GetResult();

                    // Assert: void should be rejected
                    var isRejected = !result.Success;
                    var hasCorrectMessage = result.Message != null &&
                        result.Message.Contains("Cannot void a credit note in a submitted VAT period");

                    var statusName = statusId == 2 ? "Issued" : "Applied";

                    return isRejected
                        .Label($"Void should be rejected for {statusName} credit note in submitted period " +
                               $"(Id={creditNoteId}, PeriodId={vatPeriodId}). Success={result.Success}, Message={result.Message}")
                        .And(hasCorrectMessage
                            .Label($"Error message should mention submitted VAT period. Got: '{result.Message}'"));
                }
                finally
                {
                    dbContext.Dispose();
                }
            });
    }

    /// <summary>
    /// Property 9 (Part B): VAT Period Submission Lock — Draft Void Allowed
    /// For any Draft credit note assigned to a submitted VAT period,
    /// verify that voiding is still allowed (Draft has no financial impact).
    ///
    /// **Validates: Requirement 6.6** (Draft exemption)
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Draft_CreditNote_In_SubmittedPeriod_Void_Is_Allowed()
    {
        var scenarioGen =
            from creditNoteId in Gen.Choose(1, 10000)
            from invoiceId in Gen.Choose(1, 10000)
            from totalAmount in Gen.Choose(100, 1000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from vatPeriodId in Gen.Choose(1, 100)
            select (creditNoteId, invoiceId, totalAmount, vatPeriodId);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (creditNoteId, invoiceId, totalAmount, vatPeriodId) = scenario;

                var creditNote = new CreditNote
                {
                    Id = creditNoteId,
                    BusinessId = TestBusinessId,
                    InvoiceId = invoiceId,
                    CustomerId = 1,
                    CreditNoteStatusTypeId = 1, // Draft
                    VatSubmissionPeriodId = vatPeriodId,
                    CreditNoteNumber = $"CN-2024-{(creditNoteId % 9999) + 1:D4}",
                    IssueDate = new DateOnly(2024, 2, 15),
                    Reason = "Test Draft void in submitted period",
                    Subtotal = totalAmount,
                    TaxAmount = Math.Round(totalAmount * 0.15m, 2),
                    TotalAmount = totalAmount + Math.Round(totalAmount * 0.15m, 2),
                    IssuedAtUtc = null,
                    VoidedAtUtc = null,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
                };

                var (service, _, _, _, dbContext) =
                    CreateServiceForVoidTest(creditNote, vatPeriodId, isSubmitted: true);

                try
                {
                    // Act: attempt to void the Draft credit note in a submitted period
                    var result = service.VoidCreditNoteAsync(creditNoteId, TestBusinessId, TestUserId)
                        .GetAwaiter().GetResult();

                    // Assert: void should succeed (Draft exemption)
                    return result.Success
                        .Label($"Void should succeed for Draft credit note in submitted period " +
                               $"(Id={creditNoteId}, PeriodId={vatPeriodId}). Success={result.Success}, Message={result.Message}");
                }
                finally
                {
                    dbContext.Dispose();
                }
            });
    }

    /// <summary>
    /// Property 9 (Part C): VAT Period Submission Lock — Unsubmitted Period Allows Void
    /// For any non-Draft credit note assigned to an unsubmitted VAT period,
    /// verify that voiding is allowed (no submission lock applies).
    ///
    /// **Validates: Requirements 6.5, 6.6** (negative case — no lock when unsubmitted)
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonDraft_CreditNote_In_UnsubmittedPeriod_Void_Is_Allowed()
    {
        var scenarioGen =
            from statusId in Gen.Elements(2, 3) // Issued or Applied
            from creditNoteId in Gen.Choose(1, 10000)
            from invoiceId in Gen.Choose(1, 10000)
            from totalAmount in Gen.Choose(100, 1000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from vatPeriodId in Gen.Choose(1, 100)
            select (statusId, creditNoteId, invoiceId, totalAmount, vatPeriodId);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (statusId, creditNoteId, invoiceId, totalAmount, vatPeriodId) = scenario;

                var creditNote = new CreditNote
                {
                    Id = creditNoteId,
                    BusinessId = TestBusinessId,
                    InvoiceId = invoiceId,
                    CustomerId = 1,
                    CreditNoteStatusTypeId = statusId,
                    VatSubmissionPeriodId = vatPeriodId,
                    CreditNoteNumber = $"CN-2024-{(creditNoteId % 9999) + 1:D4}",
                    IssueDate = new DateOnly(2024, 2, 15),
                    Reason = "Test void in unsubmitted period",
                    Subtotal = totalAmount,
                    TaxAmount = Math.Round(totalAmount * 0.15m, 2),
                    TotalAmount = totalAmount + Math.Round(totalAmount * 0.15m, 2),
                    IssuedAtUtc = DateTime.UtcNow.AddDays(-5),
                    VoidedAtUtc = null,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
                };

                var (service, _, _, _, dbContext) =
                    CreateServiceForVoidTest(creditNote, vatPeriodId, isSubmitted: false);

                try
                {
                    // Act: attempt to void the non-Draft credit note in an unsubmitted period
                    var result = service.VoidCreditNoteAsync(creditNoteId, TestBusinessId, TestUserId)
                        .GetAwaiter().GetResult();

                    // Assert: void should succeed (period is not submitted)
                    var statusName = statusId == 2 ? "Issued" : "Applied";

                    return result.Success
                        .Label($"Void should succeed for {statusName} credit note in unsubmitted period " +
                               $"(Id={creditNoteId}, PeriodId={vatPeriodId}). Success={result.Success}, Message={result.Message}");
                }
                finally
                {
                    dbContext.Dispose();
                }
            });
    }

    /// <summary>
    /// Property 9 (Part D): VAT Period Submission Lock — Creation Rejection
    /// For any credit note creation attempt targeting a submitted VAT period,
    /// verify that creation is rejected with an appropriate error message.
    ///
    /// **Validates: Requirement 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CreditNote_Creation_In_SubmittedPeriod_Is_Rejected()
    {
        var scenarioGen =
            from invoiceId in Gen.Choose(1, 10000)
            from totalAmount in Gen.Choose(100, 1000000).Select(i => Math.Round((decimal)i / 100m, 2))
            from vatPeriodId in Gen.Choose(1, 100)
            from quantity in Gen.Choose(1, 100)
            from unitPrice in Gen.Choose(100, 100000).Select(i => Math.Round((decimal)i / 100m, 2))
            from vatRate in Gen.Choose(0, 25).Select(i => (decimal)i)
            select (invoiceId, totalAmount, vatPeriodId, quantity, unitPrice, vatRate);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (invoiceId, totalAmount, vatPeriodId, quantity, unitPrice, vatRate) = scenario;

                // Ensure outstanding balance is large enough to cover the credit note
                var lineTotal = quantity * unitPrice;
                var lineTax = lineTotal * vatRate / 100m;
                var creditTotal = lineTotal + lineTax;
                var invoiceTotalAmount = creditTotal + 1000m; // Ensure invoice total > credit total

                var (service, creditNoteRepoMock, invoiceRepoMock, paymentRepoMock, dbContext) =
                    CreateServiceForCreationTest(vatPeriodId, isSubmitted: true);

                try
                {
                    // Setup: invoice exists and is in Issued status
                    var invoice = new Invoice
                    {
                        Id = invoiceId,
                        BusinessId = TestBusinessId,
                        CustomerId = 1,
                        InvoiceStatusTypeId = 2, // Issued
                        InvoiceFinancialStatusTypeId = 1, // Unpaid
                        TotalAmount = invoiceTotalAmount,
                        InvoiceNumber = "INV-2024-0001",
                        InvoiceDate = new DateOnly(2024, 1, 15),
                        DueDate = new DateOnly(2024, 2, 15),
                        CurrencyCode = "EUR",
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    };

                    invoiceRepoMock
                        .Setup(r => r.GetByIdAndBusinessIdAsync(invoiceId, TestBusinessId))
                        .ReturnsAsync(invoice);

                    // Setup: no payments or existing credits
                    paymentRepoMock
                        .Setup(r => r.GetTotalPaidAsync(invoiceId, TestBusinessId))
                        .ReturnsAsync(0m);

                    creditNoteRepoMock
                        .Setup(r => r.GetTotalAppliedCreditAsync(invoiceId, TestBusinessId))
                        .ReturnsAsync(0m);

                    // Build a valid DTO targeting the submitted period
                    var dto = new CreateCreditNoteDto
                    {
                        InvoiceId = invoiceId,
                        IssueDate = new DateOnly(2024, 2, 15),
                        Reason = "Test creation in submitted period",
                        VatSubmissionPeriodId = vatPeriodId,
                        Lines = new List<CreateCreditNoteLineDto>
                        {
                            new CreateCreditNoteLineDto
                            {
                                Description = "Test line item",
                                Quantity = quantity,
                                UnitPrice = unitPrice,
                                VatRate = vatRate
                            }
                        }
                    };

                    // Act: attempt to create a credit note in a submitted period
                    // If the code doesn't reject at validation layer, it may throw an exception
                    // when reaching the non-mockable repository layer — this means validation is missing.
                    ServiceResult<int> result;
                    try
                    {
                        result = service.CreateCreditNoteAsync(dto, TestBusinessId, TestUserId)
                            .GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        // If an exception is thrown, it means the code passed validation
                        // and tried to proceed to the repository layer — validation is missing
                        return false.Label(
                            $"Creation should be rejected at validation layer for submitted period " +
                            $"(PeriodId={vatPeriodId}), but code proceeded past validation and threw: {ex.GetType().Name}: {ex.Message}");
                    }

                    // Assert: creation should be rejected
                    var isRejected = !result.Success;
                    var hasRelevantMessage = result.Message != null &&
                        (result.Message.Contains("submitted") || result.Message.Contains("filed"));

                    return isRejected
                        .Label($"Creation should be rejected for submitted period " +
                               $"(PeriodId={vatPeriodId}). Success={result.Success}, Message={result.Message}")
                        .And(hasRelevantMessage
                            .Label($"Error message should mention submitted/filed period. Got: '{result.Message}'"));
                }
                finally
                {
                    dbContext.Dispose();
                }
            });
    }
}
