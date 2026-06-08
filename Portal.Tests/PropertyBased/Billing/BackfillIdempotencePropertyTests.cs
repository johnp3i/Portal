using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Configuration;
using Portal.Web.Services.Billing;

namespace Portal.Tests.PropertyBased.Billing;

// Feature: subscription-billing-invoices, Property 8: Backfill idempotence

/// <summary>
/// Property-based tests for InvoiceBackfillService idempotence.
/// For any database state, running the backfill operation twice SHALL produce the same final state —
/// invoices that already have an InvoiceNumber SHALL not be modified, and no additional InvoiceNumbers
/// SHALL be assigned on the second run.
/// **Validates: Requirements 7.3**
/// </summary>
public class BackfillIdempotencePropertyTests
{
    private const int TestBusinessId = 1;
    private const string TestPlatformCode = "BILI";

    #region Generators

    /// <summary>
    /// Generates a random set of BillingInvoice records (1-20) with null InvoiceNumber,
    /// varying CreatedAtUtc dates across 1-3 years.
    /// </summary>
    private static Gen<List<BillingInvoice>> InvoiceSetGen =>
        from count in Gen.Choose(1, 20)
        from invoices in Gen.ListOf(count, InvoiceGen)
        select invoices.ToList();

    private static Gen<BillingInvoice> InvoiceGen =>
        from year in Gen.Choose(2024, 2026)
        from month in Gen.Choose(1, 12)
        from day in Gen.Choose(1, 28)
        from hour in Gen.Choose(0, 23)
        from minute in Gen.Choose(0, 59)
        from amountCents in Gen.Choose(100, 50000)
        select new BillingInvoice
        {
            BusinessId = TestBusinessId,
            StripeInvoiceId = $"in_{Guid.NewGuid():N}",
            AmountEur = Math.Round((decimal)amountCents / 100m, 2),
            PeriodStart = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc),
            PeriodEnd = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc).AddMonths(1),
            Status = "paid",
            PaidAtUtc = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc),
            InvoiceNumber = null,
            IsEmailSent = false,
            CreatedAtUtc = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc)
        };

    #endregion

    /// <summary>
    /// Creates the test infrastructure: in-memory DbContext, mocked sequence repository,
    /// and the InvoiceBackfillService instance.
    /// The sequence repository tracks state per year to simulate real sequential behavior.
    /// </summary>
    private static (InvoiceBackfillService service, PortalDbContext dbContext) CreateService()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"BackfillIdempotence_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        // Track sequence state per year to simulate real atomic counter behavior
        var sequenceState = new Dictionary<int, int>();

        var sequenceRepoMock = new Mock<IInvoiceSequenceRepository>();
        sequenceRepoMock
            .Setup(r => r.IncrementAndGetAsync(It.IsAny<int>()))
            .ReturnsAsync((int year) =>
            {
                if (!sequenceState.ContainsKey(year))
                    sequenceState[year] = 0;
                sequenceState[year]++;
                return sequenceState[year];
            });

        var settings = Options.Create(new InvoiceSettings { PlatformCode = TestPlatformCode });
        var generator = new InvoiceNumberGenerator(sequenceRepoMock.Object, settings);

        var loggerMock = new Mock<ILogger<InvoiceBackfillService>>();

        var service = new InvoiceBackfillService(dbContext, generator, loggerMock.Object);

        return (service, dbContext);
    }

    /// <summary>
    /// Property 8: Backfill idempotence — running backfill twice SHALL produce the same final state.
    /// The second run SHALL return 0 (no changes) and all InvoiceNumbers SHALL remain identical.
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SecondBackfillRun_ProducesNoChanges_AndInvoiceNumbersRemainIdentical()
    {
        return Prop.ForAll(
            InvoiceSetGen.ToArbitrary(),
            invoices =>
            {
                var (service, dbContext) = CreateService();

                try
                {
                    // Seed invoices into the in-memory database
                    dbContext.BillingInvoices.AddRange(invoices);
                    dbContext.SaveChanges();

                    // First backfill run — assigns numbers to all records
                    var firstRunCount = service.BackfillAsync().GetAwaiter().GetResult();

                    // Capture the invoice numbers after first run
                    var invoiceNumbersAfterFirstRun = dbContext.BillingInvoices
                        .AsNoTracking()
                        .OrderBy(bi => bi.Id)
                        .Select(bi => new { bi.Id, bi.InvoiceNumber })
                        .ToList();

                    // Verify first run assigned numbers to all records
                    var allHaveNumbers = invoiceNumbersAfterFirstRun.All(x => !string.IsNullOrEmpty(x.InvoiceNumber));

                    // Second backfill run — should produce no changes
                    var secondRunCount = service.BackfillAsync().GetAwaiter().GetResult();

                    // Capture the invoice numbers after second run
                    var invoiceNumbersAfterSecondRun = dbContext.BillingInvoices
                        .AsNoTracking()
                        .OrderBy(bi => bi.Id)
                        .Select(bi => new { bi.Id, bi.InvoiceNumber })
                        .ToList();

                    // Assert: second run returns 0 (no changes)
                    var secondRunNoChanges = (secondRunCount == 0)
                        .Label($"Second backfill run should return 0 but returned {secondRunCount}");

                    // Assert: all invoice numbers remain identical between runs
                    var numbersUnchanged = invoiceNumbersAfterFirstRun
                        .Zip(invoiceNumbersAfterSecondRun, (first, second) =>
                            first.Id == second.Id && first.InvoiceNumber == second.InvoiceNumber)
                        .All(match => match);

                    var numbersIdentical = numbersUnchanged
                        .Label("InvoiceNumbers should be identical between first and second run");

                    // Assert: first run assigned numbers to all records
                    var firstRunAssignedAll = allHaveNumbers
                        .Label("First run should assign InvoiceNumbers to all records");

                    // Assert: first run count matches input count
                    var firstRunCountCorrect = (firstRunCount == invoices.Count)
                        .Label($"First run should update {invoices.Count} records but updated {firstRunCount}");

                    return firstRunAssignedAll
                        .And(firstRunCountCorrect)
                        .And(secondRunNoChanges)
                        .And(numbersIdentical);
                }
                finally
                {
                    dbContext.Database.EnsureDeleted();
                    dbContext.Dispose();
                }
            });
    }
}
