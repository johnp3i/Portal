using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

// Feature: subscription-billing-invoices, Property 7: Backfill chronological ordering with correct year

/// <summary>
/// Property-based tests for InvoiceBackfillService chronological ordering.
/// Verifies that for any set of BillingInvoice records with null InvoiceNumber and varying
/// CreatedAtUtc values, after backfill completes: (a) each assigned InvoiceNumber's year
/// component matches the record's CreatedAtUtc.Year, and (b) within the same year, records
/// with earlier CreatedAtUtc receive lower sequence numbers.
/// **Validates: Requirements 7.1, 7.2**
/// </summary>
public class BackfillOrderingPropertyTests
{
    private const string TestPlatformCode = "BILI";

    #region Generators

    /// <summary>
    /// Generates a random DateTime between 2020-01-01 and 2026-12-31 UTC.
    /// </summary>
    private static Gen<DateTime> DateTimeGen =>
        Gen.Choose(2020, 2026).SelectMany(year =>
            Gen.Choose(1, 12).SelectMany(month =>
                Gen.Choose(1, 28).SelectMany(day =>
                    Gen.Choose(0, 23).SelectMany(hour =>
                        Gen.Choose(0, 59).Select(minute =>
                            new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc))))));

    /// <summary>
    /// Generates a non-empty list of BillingInvoice records (1-20) with null InvoiceNumber
    /// and varying CreatedAtUtc values.
    /// </summary>
    private static Gen<List<BillingInvoice>> InvoiceListGen =>
        Gen.Choose(1, 20).SelectMany(count =>
            Gen.ListOf(count, DateTimeGen).Select(dates =>
                dates.Select((date, index) => new BillingInvoice
                {
                    Id = index + 1,
                    BusinessId = 1,
                    AmountEur = 29.99m,
                    PeriodStart = date.AddDays(-30),
                    PeriodEnd = date,
                    Status = "Paid",
                    PaidAtUtc = date,
                    InvoiceNumber = null,
                    IsEmailSent = false,
                    CreatedAtUtc = date
                }).ToList()));

    #endregion

    /// <summary>
    /// For any set of BillingInvoice records with null InvoiceNumber and varying CreatedAtUtc values,
    /// after backfill completes, each assigned InvoiceNumber's year component SHALL match
    /// the record's CreatedAtUtc.Year.
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Backfill_AssignedYearComponentMatchesCreatedAtUtcYear()
    {
        return Prop.ForAll(
            InvoiceListGen.ToArbitrary(),
            invoices =>
            {
                if (invoices.Count == 0)
                    return true.Label("Empty list — trivially true");

                var result = RunBackfillAndGetResults(invoices);

                // Verify year component matches CreatedAtUtc.Year for each invoice
                foreach (var invoice in result)
                {
                    if (invoice.InvoiceNumber == null)
                        return false.Label($"Invoice {invoice.Id} has null InvoiceNumber after backfill");

                    var components = ParseInvoiceNumber(invoice.InvoiceNumber);
                    if (components == null)
                        return false.Label($"Invoice {invoice.Id} has unparseable InvoiceNumber: {invoice.InvoiceNumber}");

                    if (components.Value.Year != invoice.CreatedAtUtc.Year)
                        return false.Label(
                            $"Invoice {invoice.Id}: year component {components.Value.Year} does not match CreatedAtUtc.Year {invoice.CreatedAtUtc.Year}");
                }

                return true.Label("All year components match CreatedAtUtc.Year");
            });
    }

    /// <summary>
    /// For any set of BillingInvoice records with null InvoiceNumber, after backfill completes,
    /// within the same year, records with earlier CreatedAtUtc SHALL receive lower sequence numbers.
    /// **Validates: Requirements 7.1, 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Backfill_EarlierRecordsGetLowerSequenceNumbersWithinSameYear()
    {
        return Prop.ForAll(
            InvoiceListGen.ToArbitrary(),
            invoices =>
            {
                if (invoices.Count == 0)
                    return true.Label("Empty list — trivially true");

                var result = RunBackfillAndGetResults(invoices);

                // Group by year and verify ordering within each year
                var yearGroups = result
                    .Where(i => i.InvoiceNumber != null)
                    .GroupBy(i => i.CreatedAtUtc.Year);

                foreach (var yearGroup in yearGroups)
                {
                    var orderedByDate = yearGroup.OrderBy(i => i.CreatedAtUtc).ToList();

                    for (int i = 0; i < orderedByDate.Count - 1; i++)
                    {
                        var current = orderedByDate[i];
                        var next = orderedByDate[i + 1];

                        var currentComponents = ParseInvoiceNumber(current.InvoiceNumber!);
                        var nextComponents = ParseInvoiceNumber(next.InvoiceNumber!);

                        if (currentComponents == null || nextComponents == null)
                            return false.Label("Unparseable InvoiceNumber found");

                        if (current.CreatedAtUtc < next.CreatedAtUtc &&
                            currentComponents.Value.Sequence >= nextComponents.Value.Sequence)
                        {
                            return false.Label(
                                $"Year {yearGroup.Key}: Invoice created at {current.CreatedAtUtc:O} has sequence " +
                                $"{currentComponents.Value.Sequence} which is not less than sequence " +
                                $"{nextComponents.Value.Sequence} for invoice created at {next.CreatedAtUtc:O}");
                        }
                    }
                }

                return true.Label("Chronological ordering maintained within each year");
            });
    }

    #region Helpers

    /// <summary>
    /// Sets up an in-memory database, seeds with the given invoices, and runs the backfill.
    /// Returns the invoices after backfill with their assigned InvoiceNumbers.
    /// </summary>
    private static List<BillingInvoice> RunBackfillAndGetResults(List<BillingInvoice> invoices)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(1);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"BackfillOrdering_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        // Seed invoices with re-assigned Ids to avoid conflicts
        for (int i = 0; i < invoices.Count; i++)
        {
            var invoice = new BillingInvoice
            {
                BusinessId = invoices[i].BusinessId,
                AmountEur = invoices[i].AmountEur,
                PeriodStart = invoices[i].PeriodStart,
                PeriodEnd = invoices[i].PeriodEnd,
                Status = invoices[i].Status,
                PaidAtUtc = invoices[i].PaidAtUtc,
                InvoiceNumber = null,
                IsEmailSent = false,
                CreatedAtUtc = invoices[i].CreatedAtUtc
            };
            dbContext.BillingInvoices.Add(invoice);
        }
        dbContext.SaveChanges();

        // Create a real InvoiceNumberGenerator with a mock repository that tracks per-year sequences
        var sequenceCounters = new Dictionary<int, int>();
        var mockRepo = new Mock<IInvoiceSequenceRepository>();
        mockRepo.Setup(r => r.IncrementAndGetAsync(It.IsAny<int>()))
            .ReturnsAsync((int year) =>
            {
                if (!sequenceCounters.ContainsKey(year))
                    sequenceCounters[year] = 0;
                sequenceCounters[year]++;
                return sequenceCounters[year];
            });

        var settings = Options.Create(new InvoiceSettings { PlatformCode = TestPlatformCode });
        var generator = new InvoiceNumberGenerator(mockRepo.Object, settings);
        var loggerMock = new Mock<ILogger<InvoiceBackfillService>>();

        var backfillService = new InvoiceBackfillService(dbContext, generator, loggerMock.Object);

        // Run backfill
        backfillService.BackfillAsync().GetAwaiter().GetResult();

        // Return results
        return dbContext.BillingInvoices.ToList();
    }

    /// <summary>
    /// Parses an invoice number into its components (PlatformCode, Year, Sequence).
    /// Returns null if format is invalid.
    /// </summary>
    private static (string PlatformCode, int Year, int Sequence)? ParseInvoiceNumber(string invoiceNumber)
    {
        var parts = invoiceNumber.Split("-INV-");
        if (parts.Length != 2)
            return null;

        var platformCode = parts[0];
        var yearAndSeq = parts[1].Split('-');
        if (yearAndSeq.Length != 2)
            return null;

        if (!int.TryParse(yearAndSeq[0], out var year))
            return null;
        if (!int.TryParse(yearAndSeq[1], out var sequence))
            return null;

        return (platformCode, year, sequence);
    }

    #endregion
}
