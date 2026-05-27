using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Property-based tests for VAT Output Reduction by Credit Notes.
/// Property 8: For any VAT period computation, verify Output VAT = invoice tax sum - credit note tax sum
/// (Issued/Applied only); Draft/Voided excluded.
/// **Validates: Requirements 6.2, 6.3**
/// </summary>
public class CreditNoteVatOutputReductionPropertyTests
{
    private const int TestBusinessId = 1;

    /// <summary>
    /// Creates a real InMemoryDatabase-backed PortalDbContext and returns it along with
    /// mocked repositories for the VatSubmissionService.
    /// </summary>
    private static (
        PortalDbContext dbContext,
        Mock<ICurrentTenantService> tenantMock,
        Mock<VatSubmissionPeriodRepository> vatPeriodRepoMock,
        Mock<VatSubmissionRepository> vatSubmissionRepoMock,
        Mock<AuditLogRepository> auditLogRepoMock
    ) CreateContext()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        var vatPeriodRepoMock = new Mock<VatSubmissionPeriodRepository>(dbContext) { CallBase = false };
        var vatSubmissionRepoMock = new Mock<VatSubmissionRepository>(dbContext) { CallBase = false };
        var auditLogRepoMock = new Mock<AuditLogRepository>(dbContext) { CallBase = false };

        auditLogRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<AuditLog>()))
            .Returns(Task.CompletedTask);

        return (dbContext, tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock);
    }

    private static VatSubmissionService CreateService(
        Mock<ICurrentTenantService> tenantMock,
        Mock<VatSubmissionPeriodRepository> vatPeriodRepoMock,
        Mock<VatSubmissionRepository> vatSubmissionRepoMock,
        Mock<AuditLogRepository> auditLogRepoMock,
        PortalDbContext dbContext)
    {
        return new VatSubmissionService(
            tenantMock.Object,
            vatSubmissionRepoMock.Object,
            vatPeriodRepoMock.Object,
            dbContext,
            auditLogRepoMock.Object);
    }

    /// <summary>
    /// Property 8: VAT Output Reduction
    /// For any VAT period computation with a mix of credit notes in different statuses,
    /// verify that Output VAT = invoice tax sum - credit note tax sum (Issued/Applied only).
    /// Draft (status 1) and Voided (status 4) credit notes must NOT reduce the Output VAT.
    ///
    /// Generator: random sets of credit notes with mixed statuses per period.
    /// Each test iteration seeds:
    ///   - One invoice explicitly assigned to the period (provides the base Output VAT)
    ///   - Multiple credit notes with random statuses (1=Draft, 2=Issued, 3=Applied, 4=Voided)
    ///     assigned to the same period
    ///
    /// Expected: TotalOutputVat = invoiceTaxAmount - sum(TaxAmount where status IN (2, 3))
    ///
    /// **Validates: Requirements 6.2, 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutputVat_EqualsInvoiceTaxSum_MinusCreditNoteTaxSum_ForIssuedAndAppliedOnly()
    {
        // Generator: produce a scenario with an invoice tax amount and a list of credit notes with mixed statuses
        var scenarioGen =
            from invoiceTaxAmount in Gen.Choose(100, 9999999).Select(i => Math.Round((decimal)i / 100m, 2))
            from periodId in Gen.Choose(1, 5000)
            from creditNoteCount in Gen.Choose(1, 8)
            from creditNoteTaxAmounts in Gen.ListOf(creditNoteCount,
                Gen.Choose(1, 500000).Select(i => Math.Round((decimal)i / 100m, 2)))
            from creditNoteStatuses in Gen.ListOf(creditNoteCount,
                Gen.Choose(1, 4)) // 1=Draft, 2=Issued, 3=Applied, 4=Voided
            select (invoiceTaxAmount, periodId, creditNoteTaxAmounts.ToList(), creditNoteStatuses.ToList());

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (invoiceTaxAmount, periodId, creditNoteTaxAmounts, creditNoteStatuses) = scenario;

                var (dbContext, tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock) = CreateContext();

                // Period: Jan 1 2024 – Mar 31 2024
                var periodStart = new DateOnly(2024, 1, 1);
                var periodEnd = new DateOnly(2024, 3, 31);

                // Seed an invoice explicitly assigned to this period
                var invoice = new Invoice
                {
                    Id = 1,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2, // Issued
                    InvoiceFinancialStatusTypeId = 1,
                    InvoiceNumber = "INV-001",
                    InvoiceDate = new DateOnly(2024, 2, 15),
                    DueDate = new DateOnly(2024, 3, 15),
                    Subtotal = invoiceTaxAmount * 5m,
                    TaxAmount = invoiceTaxAmount,
                    TotalAmount = invoiceTaxAmount * 6m,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    VatSubmissionPeriodId = periodId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                dbContext.Invoices.Add(invoice);

                // Seed credit notes with mixed statuses assigned to the same period
                for (int i = 0; i < creditNoteTaxAmounts.Count; i++)
                {
                    var creditNote = new CreditNote
                    {
                        Id = i + 1,
                        BusinessId = TestBusinessId,
                        InvoiceId = 1,
                        CustomerId = 1,
                        CreditNoteStatusTypeId = creditNoteStatuses[i],
                        VatSubmissionPeriodId = periodId,
                        CreditNoteNumber = $"CN-2024-{(i + 1):D4}",
                        IssueDate = new DateOnly(2024, 2, 20),
                        Reason = $"Test credit note {i + 1}",
                        Subtotal = creditNoteTaxAmounts[i] * 5m,
                        TaxAmount = creditNoteTaxAmounts[i],
                        TotalAmount = creditNoteTaxAmounts[i] * 6m,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    dbContext.CreditNotes.Add(creditNote);
                }

                dbContext.SaveChanges();

                // Compute expected credit note tax reduction: only Issued (2) and Applied (3)
                decimal expectedCreditNoteTaxReduction = 0m;
                for (int i = 0; i < creditNoteTaxAmounts.Count; i++)
                {
                    if (creditNoteStatuses[i] == 2 || creditNoteStatuses[i] == 3)
                    {
                        expectedCreditNoteTaxReduction += creditNoteTaxAmounts[i];
                    }
                }

                var expectedOutputVat = invoiceTaxAmount - expectedCreditNoteTaxReduction;

                // Setup mocks
                var period = new VatSubmissionPeriod
                {
                    Id = periodId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = periodStart,
                    PeriodEndDate = periodEnd,
                    PeriodLabel = "Jan-Mar 2024",
                    CreatedAtUtc = DateTime.UtcNow
                };

                vatPeriodRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(periodId, TestBusinessId))
                    .ReturnsAsync(period);

                vatSubmissionRepoMock
                    .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(periodId, TestBusinessId))
                    .ReturnsAsync((VatSubmission?)null);

                vatSubmissionRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<VatSubmission>()))
                    .Returns(Task.CompletedTask);

                var service = CreateService(tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock, dbContext);

                // Act
                var result = service.CreateOrRecalculateAsync(periodId).GetAwaiter().GetResult();

                var succeeded = result.Success;
                var submission = result.Data;

                var outputVatCorrect = submission != null
                    && submission.TotalOutputVat == expectedOutputVat;

                // Dispose context
                dbContext.Dispose();

                return succeeded
                    .Label($"CreateOrRecalculateAsync should succeed but returned: {result.Message}")
                    .And(outputVatCorrect
                        .Label($"TotalOutputVat should be {expectedOutputVat} " +
                               $"(invoiceTax={invoiceTaxAmount} - creditReduction={expectedCreditNoteTaxReduction}) " +
                               $"but was {submission?.TotalOutputVat}. " +
                               $"Statuses: [{string.Join(",", creditNoteStatuses)}], " +
                               $"TaxAmounts: [{string.Join(",", creditNoteTaxAmounts.Select(a => a.ToString("F2")))}]"));
            });
    }

    /// <summary>
    /// Property 8 (supplementary): Draft and Voided credit notes have zero impact on Output VAT.
    /// For any set of credit notes where ALL are in Draft (1) or Voided (4) status,
    /// the Output VAT must equal the invoice tax sum with no reduction.
    ///
    /// **Validates: Requirements 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DraftAndVoidedCreditNotes_HaveZeroImpactOnOutputVat()
    {
        var scenarioGen =
            from invoiceTaxAmount in Gen.Choose(100, 9999999).Select(i => Math.Round((decimal)i / 100m, 2))
            from periodId in Gen.Choose(1, 5000)
            from creditNoteCount in Gen.Choose(1, 6)
            from creditNoteTaxAmounts in Gen.ListOf(creditNoteCount,
                Gen.Choose(1, 500000).Select(i => Math.Round((decimal)i / 100m, 2)))
            from creditNoteStatuses in Gen.ListOf(creditNoteCount,
                Gen.Elements(1, 4)) // Only Draft (1) or Voided (4)
            select (invoiceTaxAmount, periodId, creditNoteTaxAmounts.ToList(), creditNoteStatuses.ToList());

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (invoiceTaxAmount, periodId, creditNoteTaxAmounts, creditNoteStatuses) = scenario;

                var (dbContext, tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock) = CreateContext();

                var periodStart = new DateOnly(2024, 1, 1);
                var periodEnd = new DateOnly(2024, 3, 31);

                // Seed an invoice explicitly assigned to this period
                var invoice = new Invoice
                {
                    Id = 1,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2,
                    InvoiceFinancialStatusTypeId = 1,
                    InvoiceNumber = "INV-001",
                    InvoiceDate = new DateOnly(2024, 2, 15),
                    DueDate = new DateOnly(2024, 3, 15),
                    Subtotal = invoiceTaxAmount * 5m,
                    TaxAmount = invoiceTaxAmount,
                    TotalAmount = invoiceTaxAmount * 6m,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    VatSubmissionPeriodId = periodId,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                dbContext.Invoices.Add(invoice);

                // Seed credit notes — all Draft or Voided
                for (int i = 0; i < creditNoteTaxAmounts.Count; i++)
                {
                    var creditNote = new CreditNote
                    {
                        Id = i + 1,
                        BusinessId = TestBusinessId,
                        InvoiceId = 1,
                        CustomerId = 1,
                        CreditNoteStatusTypeId = creditNoteStatuses[i],
                        VatSubmissionPeriodId = periodId,
                        CreditNoteNumber = $"CN-2024-{(i + 1):D4}",
                        IssueDate = new DateOnly(2024, 2, 20),
                        Reason = $"Test credit note {i + 1}",
                        Subtotal = creditNoteTaxAmounts[i] * 5m,
                        TaxAmount = creditNoteTaxAmounts[i],
                        TotalAmount = creditNoteTaxAmounts[i] * 6m,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    dbContext.CreditNotes.Add(creditNote);
                }

                dbContext.SaveChanges();

                // Expected: no reduction since all credit notes are Draft or Voided
                var expectedOutputVat = invoiceTaxAmount;

                var period = new VatSubmissionPeriod
                {
                    Id = periodId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = periodStart,
                    PeriodEndDate = periodEnd,
                    PeriodLabel = "Jan-Mar 2024",
                    CreatedAtUtc = DateTime.UtcNow
                };

                vatPeriodRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(periodId, TestBusinessId))
                    .ReturnsAsync(period);

                vatSubmissionRepoMock
                    .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(periodId, TestBusinessId))
                    .ReturnsAsync((VatSubmission?)null);

                vatSubmissionRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<VatSubmission>()))
                    .Returns(Task.CompletedTask);

                var service = CreateService(tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock, dbContext);

                // Act
                var result = service.CreateOrRecalculateAsync(periodId).GetAwaiter().GetResult();

                var succeeded = result.Success;
                var submission = result.Data;

                var outputVatUnchanged = submission != null
                    && submission.TotalOutputVat == expectedOutputVat;

                // Dispose context
                dbContext.Dispose();

                return succeeded
                    .Label($"CreateOrRecalculateAsync should succeed but returned: {result.Message}")
                    .And(outputVatUnchanged
                        .Label($"TotalOutputVat should be {expectedOutputVat} (no reduction from Draft/Voided) " +
                               $"but was {submission?.TotalOutputVat}. " +
                               $"Statuses: [{string.Join(",", creditNoteStatuses)}]"));
            });
    }
}
