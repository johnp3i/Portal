using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property-based tests for VatSubmissionService Output VAT computation logic.
/// Feature: invoice-vat-period-assignment
/// </summary>
public class VatSubmissionOutputVatPropertyTests
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

        // Use a real PortalDbContext backed by InMemoryDatabase so LINQ queries work
        var dbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        // Repositories take a DbContext — pass the real dbContext so the mock constructor is satisfied
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
    /// Feature: invoice-vat-period-assignment, Property 1: Explicit assignment determines period inclusion
    /// **Validates: Requirements 1.3, 5.1**
    ///
    /// For any invoice with a non-NULL VatSubmissionPeriodId, the Output VAT computation SHALL
    /// include that invoice's TaxAmount only in the period referenced by VatSubmissionPeriodId,
    /// regardless of the invoice's InvoiceDate.
    ///
    /// Test scenario:
    /// - Seed an invoice with VatSubmissionPeriodId = periodA.Id
    /// - The invoice's InvoiceDate falls within periodB's date range
    /// - Call CreateOrRecalculateAsync for periodA → invoice's TaxAmount IS included
    /// - Call CreateOrRecalculateAsync for periodB → invoice's TaxAmount is NOT included
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExplicitAssignment_DeterminesPeriodInclusion()
    {
        // Combine all generators into a single tuple to stay within ForAll's 3-argument limit
        var scenarioGen =
            from taxAmount in Gen.Choose(1, 9999999).Select(i => Math.Round((decimal)i / 100m, 2))
            from periodAId in Gen.Choose(1, 5000)
            from periodBOffset in Gen.Choose(1, 5000)  // periodB.Id = periodA.Id + offset (guaranteed distinct)
            from dayOffset in Gen.Choose(0, 89)        // 0..89 days into periodB's 90-day range
            select (taxAmount, periodAId, periodBOffset, dayOffset);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (taxAmount, periodAId, periodBOffset, dayOffset) = scenario;
                var periodBId = periodAId + periodBOffset; // guaranteed distinct

                var (dbContext, tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock) = CreateContext();

                // Define two non-overlapping periods
                // periodA: Jan 1 2024 – Mar 31 2024
                var periodAStart = new DateOnly(2024, 1, 1);
                var periodAEnd = new DateOnly(2024, 3, 31);

                // periodB: Apr 1 2024 – Jun 30 2024
                var periodBStart = new DateOnly(2024, 4, 1);
                var periodBEnd = new DateOnly(2024, 6, 30);

                // Invoice date falls within periodB's range
                var invoiceDate = periodBStart.AddDays(dayOffset % 90);

                // Seed the invoice: explicitly assigned to periodA, but InvoiceDate is in periodB
                var invoice = new Invoice
                {
                    Id = 1,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2,       // Issued — required for Output VAT inclusion
                    InvoiceFinancialStatusTypeId = 1,
                    InvoiceNumber = "INV-001",
                    InvoiceDate = invoiceDate,      // Falls in periodB's date range
                    DueDate = invoiceDate.AddDays(30),
                    Subtotal = taxAmount * 5m,
                    TaxAmount = taxAmount,
                    TotalAmount = taxAmount * 6m,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    VatSubmissionPeriodId = periodAId, // Explicitly assigned to periodA
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                dbContext.Invoices.Add(invoice);
                dbContext.SaveChanges();

                // Build period entities for mock returns
                var periodA = new VatSubmissionPeriod
                {
                    Id = periodAId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = periodAStart,
                    PeriodEndDate = periodAEnd,
                    PeriodLabel = "Jan-Mar 2024",
                    CreatedAtUtc = DateTime.UtcNow
                };

                var periodB = new VatSubmissionPeriod
                {
                    Id = periodBId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = periodBStart,
                    PeriodEndDate = periodBEnd,
                    PeriodLabel = "Apr-Jun 2024",
                    CreatedAtUtc = DateTime.UtcNow
                };

                // Mock: period lookup returns the correct period for each call
                vatPeriodRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(periodAId, TestBusinessId))
                    .ReturnsAsync(periodA);

                vatPeriodRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(periodBId, TestBusinessId))
                    .ReturnsAsync(periodB);

                // Mock: no existing submissions for either period (so service creates new ones)
                vatSubmissionRepoMock
                    .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(periodAId, TestBusinessId))
                    .ReturnsAsync((VatSubmission?)null);

                vatSubmissionRepoMock
                    .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(periodBId, TestBusinessId))
                    .ReturnsAsync((VatSubmission?)null);

                // Mock: InsertAsync is now virtual — mock it to avoid raw SQL execution against InMemoryDatabase
                vatSubmissionRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<VatSubmission>()))
                    .Returns(Task.CompletedTask);

                var service = CreateService(tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock, dbContext);

                // Act: compute Output VAT for periodA (the explicitly assigned period)
                var resultA = service.CreateOrRecalculateAsync(periodAId).GetAwaiter().GetResult();

                // Act: compute Output VAT for periodB (the date-range period — should NOT include the invoice)
                var resultB = service.CreateOrRecalculateAsync(periodBId).GetAwaiter().GetResult();

                // Assert: both calls succeeded
                var bothSucceeded = resultA.Success && resultB.Success;

                // The service returns the VatSubmission object with computed values in ServiceResult.Data
                var submissionForPeriodA = resultA.Data;
                var submissionForPeriodB = resultB.Data;

                // Assert: periodA's TotalOutputVat includes the invoice's TaxAmount
                // (explicit assignment: VatSubmissionPeriodId == periodAId)
                var periodAIncludesInvoice = submissionForPeriodA != null
                    && submissionForPeriodA.TotalOutputVat == taxAmount;

                // Assert: periodB's TotalOutputVat does NOT include the invoice's TaxAmount
                // (invoice has non-NULL VatSubmissionPeriodId, so date-range fallback is skipped)
                var periodBExcludesInvoice = submissionForPeriodB != null
                    && submissionForPeriodB.TotalOutputVat == 0m;

                return bothSucceeded
                    .Label($"Both CreateOrRecalculateAsync calls should succeed. A={resultA.Success}, B={resultB.Success}")
                    .And(periodAIncludesInvoice
                        .Label($"PeriodA TotalOutputVat should be {taxAmount} (explicit assignment) but was {submissionForPeriodA?.TotalOutputVat}"))
                    .And(periodBExcludesInvoice
                        .Label($"PeriodB TotalOutputVat should be 0 (invoice has explicit assignment to periodA) but was {submissionForPeriodB?.TotalOutputVat}"));
            });
    }

    /// <summary>
    /// Feature: invoice-vat-period-assignment, Property 2: NULL assignment falls back to date-range matching
    /// **Validates: Requirements 1.2, 5.2**
    ///
    /// For any invoice with NULL VatSubmissionPeriodId, InvoiceStatusTypeId=2, IsDeleted=false,
    /// the Output VAT computation SHALL include that invoice's TaxAmount in the period whose
    /// PeriodStartDate &lt;= InvoiceDate &lt;= PeriodEndDate for the same BusinessId.
    ///
    /// Test scenario:
    /// - Seed an invoice with VatSubmissionPeriodId = null, InvoiceDate inside the period's range
    /// - Call CreateOrRecalculateAsync for that period → TotalOutputVat == taxAmount
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullAssignment_FallsBackToDateRangeMatching()
    {
        var scenarioGen =
            from taxAmount in Gen.Choose(1, 9999999).Select(i => Math.Round((decimal)i / 100m, 2))
            from periodId in Gen.Choose(1, 5000)
            from dayOffset in Gen.Choose(0, 89)   // 0..89 days into the 90-day period range
            select (taxAmount, periodId, dayOffset);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (taxAmount, periodId, dayOffset) = scenario;

                var (dbContext, tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock) = CreateContext();

                // Period: Jan 1 2024 – Mar 31 2024
                var periodStart = new DateOnly(2024, 1, 1);
                var periodEnd = new DateOnly(2024, 3, 31);

                // Invoice date falls within the period's range
                var invoiceDate = periodStart.AddDays(dayOffset % 90);

                // Seed the invoice: VatSubmissionPeriodId = null, InvoiceDate inside the period
                var invoice = new Invoice
                {
                    Id = 1,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2,
                    InvoiceFinancialStatusTypeId = 1,
                    InvoiceNumber = "INV-NULL-001",
                    InvoiceDate = invoiceDate,
                    DueDate = invoiceDate.AddDays(30),
                    Subtotal = taxAmount * 5m,
                    TaxAmount = taxAmount,
                    TotalAmount = taxAmount * 6m,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    VatSubmissionPeriodId = null,   // NULL — must fall back to date-range matching
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                dbContext.Invoices.Add(invoice);
                dbContext.SaveChanges();

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

                // The NULL-assignment invoice's TaxAmount must be included via date-range matching
                var outputVatMatchesTaxAmount = submission != null
                    && submission.TotalOutputVat == taxAmount;

                return succeeded
                    .Label($"CreateOrRecalculateAsync should succeed but returned: {result.Message}")
                    .And(outputVatMatchesTaxAmount
                        .Label($"TotalOutputVat should be {taxAmount} (date-range fallback for NULL assignment) but was {submission?.TotalOutputVat}"));
            });
    }

    /// <summary>
    /// Feature: invoice-vat-period-assignment, Property 3: Mutual exclusivity — no invoice counted in multiple periods
    /// **Validates: Requirements 5.3, 5.4**
    ///
    /// For any set of invoices and two distinct periods, an invoice contributes TaxAmount to at most
    /// one period. An invoice with non-NULL VatSubmissionPeriodId is only in that period. An invoice
    /// with NULL VatSubmissionPeriodId is only in the date-range period.
    ///
    /// Test scenario:
    /// - Invoice A: explicitly assigned to periodA (VatSubmissionPeriodId=periodAId), InvoiceDate in periodB
    /// - Invoice B: NULL assignment (VatSubmissionPeriodId=null), InvoiceDate in periodB
    /// - Compute both periods:
    ///   periodA total = invoiceA.TaxAmount  (explicit assignment wins, date-range ignored)
    ///   periodB total = invoiceB.TaxAmount  (only the NULL-assignment invoice via date-range)
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MutualExclusivity_NoInvoiceCountedInMultiplePeriods()
    {
        var scenarioGen =
            from taxAmountA in Gen.Choose(1, 4999999).Select(i => Math.Round((decimal)i / 100m, 2))
            from taxAmountB in Gen.Choose(1, 4999999).Select(i => Math.Round((decimal)i / 100m, 2))
            from periodAId in Gen.Choose(1, 5000)
            from periodBOffset in Gen.Choose(1, 5000)
            from dayOffset in Gen.Choose(0, 89)
            select (taxAmountA, taxAmountB, periodAId, periodBOffset, dayOffset);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (taxAmountA, taxAmountB, periodAId, periodBOffset, dayOffset) = scenario;
                var periodBId = periodAId + periodBOffset; // guaranteed distinct

                var (dbContext, tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock) = CreateContext();

                // periodA: Jan 1 2024 – Mar 31 2024
                var periodAStart = new DateOnly(2024, 1, 1);
                var periodAEnd = new DateOnly(2024, 3, 31);

                // periodB: Apr 1 2024 – Jun 30 2024
                var periodBStart = new DateOnly(2024, 4, 1);
                var periodBEnd = new DateOnly(2024, 6, 30);

                // Invoice date for both invoices falls within periodB's range
                var invoiceDateInPeriodB = periodBStart.AddDays(dayOffset % 90);

                // Invoice A: explicitly assigned to periodA, but InvoiceDate is in periodB
                // → must appear ONLY in periodA (explicit assignment wins)
                var invoiceA = new Invoice
                {
                    Id = 1,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2,
                    InvoiceFinancialStatusTypeId = 1,
                    InvoiceNumber = "INV-EXPLICIT-001",
                    InvoiceDate = invoiceDateInPeriodB,  // date is in periodB
                    DueDate = invoiceDateInPeriodB.AddDays(30),
                    Subtotal = taxAmountA * 5m,
                    TaxAmount = taxAmountA,
                    TotalAmount = taxAmountA * 6m,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    VatSubmissionPeriodId = periodAId,  // explicitly assigned to periodA
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                // Invoice B: NULL assignment, InvoiceDate in periodB
                // → must appear ONLY in periodB (date-range fallback)
                var invoiceB = new Invoice
                {
                    Id = 2,
                    BusinessId = TestBusinessId,
                    CustomerId = 1,
                    InvoiceStatusTypeId = 2,
                    InvoiceFinancialStatusTypeId = 1,
                    InvoiceNumber = "INV-NULL-002",
                    InvoiceDate = invoiceDateInPeriodB,  // date is in periodB
                    DueDate = invoiceDateInPeriodB.AddDays(30),
                    Subtotal = taxAmountB * 5m,
                    TaxAmount = taxAmountB,
                    TotalAmount = taxAmountB * 6m,
                    CurrencyCode = "EUR",
                    IsDeleted = false,
                    VatSubmissionPeriodId = null,  // NULL — falls back to date-range
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                dbContext.Invoices.AddRange(invoiceA, invoiceB);
                dbContext.SaveChanges();

                var periodA = new VatSubmissionPeriod
                {
                    Id = periodAId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = periodAStart,
                    PeriodEndDate = periodAEnd,
                    PeriodLabel = "Jan-Mar 2024",
                    CreatedAtUtc = DateTime.UtcNow
                };

                var periodB = new VatSubmissionPeriod
                {
                    Id = periodBId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = periodBStart,
                    PeriodEndDate = periodBEnd,
                    PeriodLabel = "Apr-Jun 2024",
                    CreatedAtUtc = DateTime.UtcNow
                };

                vatPeriodRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(periodAId, TestBusinessId))
                    .ReturnsAsync(periodA);

                vatPeriodRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(periodBId, TestBusinessId))
                    .ReturnsAsync(periodB);

                vatSubmissionRepoMock
                    .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(periodAId, TestBusinessId))
                    .ReturnsAsync((VatSubmission?)null);

                vatSubmissionRepoMock
                    .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(periodBId, TestBusinessId))
                    .ReturnsAsync((VatSubmission?)null);

                vatSubmissionRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<VatSubmission>()))
                    .Returns(Task.CompletedTask);

                var service = CreateService(tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock, dbContext);

                // Act: compute both periods
                var resultA = service.CreateOrRecalculateAsync(periodAId).GetAwaiter().GetResult();
                var resultB = service.CreateOrRecalculateAsync(periodBId).GetAwaiter().GetResult();

                var bothSucceeded = resultA.Success && resultB.Success;

                var submissionA = resultA.Data;
                var submissionB = resultB.Data;

                // periodA must contain ONLY invoiceA.TaxAmount (explicit assignment)
                // invoiceB has NULL assignment and its date is in periodB, so it must NOT appear in periodA
                var periodACorrect = submissionA != null
                    && submissionA.TotalOutputVat == taxAmountA;

                // periodB must contain ONLY invoiceB.TaxAmount (date-range fallback for NULL assignment)
                // invoiceA has explicit assignment to periodA, so it must NOT appear in periodB even though its date is in periodB
                var periodBCorrect = submissionB != null
                    && submissionB.TotalOutputVat == taxAmountB;

                return bothSucceeded
                    .Label($"Both CreateOrRecalculateAsync calls should succeed. A={resultA.Success}, B={resultB.Success}")
                    .And(periodACorrect
                        .Label($"PeriodA TotalOutputVat should be {taxAmountA} (only explicit invoice) but was {submissionA?.TotalOutputVat}"))
                    .And(periodBCorrect
                        .Label($"PeriodB TotalOutputVat should be {taxAmountB} (only NULL-assignment invoice) but was {submissionB?.TotalOutputVat}"));
            });
    }

    /// <summary>
    /// Feature: invoice-vat-period-assignment, Property 9: Submitted period computation is immutable
    /// **Validates: Requirements 5.5**
    ///
    /// For any period that has a VatSubmission with IsSubmitted=true, calling CreateOrRecalculateAsync
    /// SHALL return the existing TotalOutputVat, TotalInputVat, and NetVatPayable values without
    /// modification. InsertAsync and UpdateValuesAsync must never be called.
    ///
    /// Test scenario:
    /// - Mock GetByPeriodIdAndBusinessIdAsync to return a submitted VatSubmission with known values
    /// - Call CreateOrRecalculateAsync → verify returned values match the existing submission exactly
    /// - Verify InsertAsync and UpdateValuesAsync were never called
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SubmittedPeriod_ComputationIsImmutable()
    {
        var scenarioGen =
            from outputVat in Gen.Choose(0, 9999999).Select(i => Math.Round((decimal)i / 100m, 2))
            from inputVat in Gen.Choose(0, 9999999).Select(i => Math.Round((decimal)i / 100m, 2))
            from periodId in Gen.Choose(1, 5000)
            from submissionId in Gen.Choose(1, 5000)
            select (outputVat, inputVat, periodId, submissionId);

        return Prop.ForAll(
            scenarioGen.ToArbitrary(),
            scenario =>
            {
                var (outputVat, inputVat, periodId, submissionId) = scenario;
                var netVat = outputVat - inputVat;

                var (dbContext, tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock) = CreateContext();

                var period = new VatSubmissionPeriod
                {
                    Id = periodId,
                    BusinessId = TestBusinessId,
                    PeriodStartDate = new DateOnly(2024, 1, 1),
                    PeriodEndDate = new DateOnly(2024, 3, 31),
                    PeriodLabel = "Jan-Mar 2024",
                    CreatedAtUtc = DateTime.UtcNow
                };

                // Existing submitted submission with known values
                var existingSubmission = new VatSubmission
                {
                    Id = submissionId,
                    BusinessId = TestBusinessId,
                    VatSubmissionPeriodId = periodId,
                    TotalOutputVat = outputVat,
                    TotalInputVat = inputVat,
                    NetVatPayable = netVat,
                    IsSubmitted = true,
                    SubmittedAtUtc = DateTime.UtcNow.AddDays(-1),
                    CreatedAtUtc = DateTime.UtcNow
                };

                vatPeriodRepoMock
                    .Setup(r => r.GetByIdAndBusinessIdAsync(periodId, TestBusinessId))
                    .ReturnsAsync(period);

                vatSubmissionRepoMock
                    .Setup(r => r.GetByPeriodIdAndBusinessIdAsync(periodId, TestBusinessId))
                    .ReturnsAsync(existingSubmission);

                // InsertAsync must not be called — set it up so Moq can track calls
                vatSubmissionRepoMock
                    .Setup(r => r.InsertAsync(It.IsAny<VatSubmission>()))
                    .Returns(Task.CompletedTask);

                var service = CreateService(tenantMock, vatPeriodRepoMock, vatSubmissionRepoMock, auditLogRepoMock, dbContext);

                // Act
                var result = service.CreateOrRecalculateAsync(periodId).GetAwaiter().GetResult();

                var succeeded = result.Success;
                var returned = result.Data;

                // The returned submission must have the exact same values as the existing one
                var outputVatUnchanged = returned != null && returned.TotalOutputVat == outputVat;
                var inputVatUnchanged = returned != null && returned.TotalInputVat == inputVat;
                var netVatUnchanged = returned != null && returned.NetVatPayable == netVat;

                // InsertAsync must never be called for a submitted period
                var insertNeverCalled = true;
                try
                {
                    vatSubmissionRepoMock.Verify(
                        r => r.InsertAsync(It.IsAny<VatSubmission>()),
                        Times.Never);
                }
                catch
                {
                    insertNeverCalled = false;
                }

                // Note: UpdateValuesAsync is not virtual so cannot be verified via Moq.
                // The immutability guarantee is validated by checking the returned values match
                // the existing submission exactly — if UpdateValuesAsync were called it would
                // attempt raw SQL against InMemoryDatabase and throw, causing the test to fail.

                return succeeded
                    .Label($"CreateOrRecalculateAsync should succeed but returned: {result.Message}")
                    .And(outputVatUnchanged
                        .Label($"TotalOutputVat should remain {outputVat} (immutable) but was {returned?.TotalOutputVat}"))
                    .And(inputVatUnchanged
                        .Label($"TotalInputVat should remain {inputVat} (immutable) but was {returned?.TotalInputVat}"))
                    .And(netVatUnchanged
                        .Label($"NetVatPayable should remain {netVat} (immutable) but was {returned?.NetVatPayable}"))
                    .And(insertNeverCalled
                        .Label("InsertAsync must never be called for a submitted period"));
            });
    }
}
