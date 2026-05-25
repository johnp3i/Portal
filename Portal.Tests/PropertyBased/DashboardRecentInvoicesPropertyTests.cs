using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: dashboard-upgrade, Property 8: Recent invoices ordering and filtering

/// <summary>
/// Property-based tests for Dashboard Recent Invoices.
/// Validates that the recent invoices result contains at most 5 items,
/// all with InvoiceStatusTypeId = 2 and IsDeleted = 0,
/// ordered by InvoiceDate descending.
/// Tested as a pure computation over generated invoice data.
/// **Validates: Requirements 4.1, 4.5**
/// </summary>
public class DashboardRecentInvoicesPropertyTests
{
    private const int TestBusinessId = 1;
    private const int InvoiceStatusIssued = 2;

    #region Test Infrastructure

    /// <summary>
    /// Computes the expected recent invoices from a list of invoices.
    /// This is the oracle function: filter to issued (InvoiceStatusTypeId = 2),
    /// non-deleted (IsDeleted = false), same business, then take top 5
    /// ordered by InvoiceDate descending.
    /// </summary>
    private static List<Invoice> ComputeExpectedRecentInvoices(List<Invoice> invoices, int businessId)
    {
        return invoices
            .Where(i => i.BusinessId == businessId
                        && i.InvoiceStatusTypeId == InvoiceStatusIssued
                        && !i.IsDeleted)
            .OrderByDescending(i => i.InvoiceDate)
            .Take(5)
            .ToList();
    }

    /// <summary>
    /// Generates an invoice with controlled parameters for testing.
    /// </summary>
    private static Invoice CreateInvoice(
        int id, int businessId, int invoiceStatusTypeId, bool isDeleted, DateOnly invoiceDate)
    {
        return new Invoice
        {
            Id = id,
            BusinessId = businessId,
            CustomerId = 1,
            InvoiceStatusTypeId = invoiceStatusTypeId,
            InvoiceFinancialStatusTypeId = 1,
            InvoiceNumber = $"INV-{id:D5}",
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(30),
            Subtotal = 100m,
            TaxAmount = 15m,
            TotalAmount = 115m,
            CurrencyCode = "EUR",
            IsDeleted = isDeleted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generates a random InvoiceDate from a seed.
    /// Produces dates within the last 2 years.
    /// </summary>
    private static DateOnly GenerateInvoiceDate(int seed)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysBack = Math.Abs(seed) % 730; // up to 2 years back
        return today.AddDays(-daysBack);
    }

    /// <summary>
    /// Generates a random InvoiceStatusTypeId from a seed.
    /// Values: 1 = Draft, 2 = Issued, 3 = Cancelled
    /// </summary>
    private static int GenerateInvoiceStatusTypeId(int seed)
    {
        return (Math.Abs(seed) % 3) + 1;
    }

    #endregion

    #region Property 8: Recent invoices ordering and filtering

    /// <summary>
    /// Property 8: Recent invoices result contains at most 5 items, all issued and non-deleted,
    /// ordered by InvoiceDate descending.
    /// Generates random invoices with various statuses, deletion flags, and dates.
    /// **Validates: Requirements 4.1, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentInvoices_ContainsAtMost5Items_AllIssuedNonDeleted_OrderedByDateDesc(
        PositiveInt[] dateSeeds, int[] statusSeeds, bool[] deletedFlags)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(dateSeeds.Length, 25);
        var invoices = new List<Invoice>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var invoiceDate = GenerateInvoiceDate(dateSeeds[i].Get);
            var statusTypeId = statusSeeds.Length > 0
                ? GenerateInvoiceStatusTypeId(statusSeeds[i % statusSeeds.Length])
                : InvoiceStatusIssued;
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, statusTypeId, isDeleted, invoiceDate));
        }

        var result = ComputeExpectedRecentInvoices(invoices, TestBusinessId);

        // Assert: at most 5 items
        var atMost5 = result.Count <= 5;

        // Assert: all items have InvoiceStatusTypeId = 2
        var allIssued = result.All(i => i.InvoiceStatusTypeId == InvoiceStatusIssued);

        // Assert: all items have IsDeleted = false
        var allNonDeleted = result.All(i => !i.IsDeleted);

        // Assert: ordered by InvoiceDate descending
        var orderedDesc = true;
        for (int i = 1; i < result.Count; i++)
        {
            if (result[i].InvoiceDate > result[i - 1].InvoiceDate)
            {
                orderedDesc = false;
                break;
            }
        }

        return (atMost5 && allIssued && allNonDeleted && orderedDesc).ToProperty()
            .Label($"AtMost5={atMost5}, AllIssued={allIssued}, AllNonDeleted={allNonDeleted}, " +
                   $"OrderedDesc={orderedDesc}, ResultCount={result.Count}, TotalInvoices={invoiceCount}");
    }

    /// <summary>
    /// When more than 5 issued non-deleted invoices exist, only the 5 most recent are returned.
    /// **Validates: Requirements 4.1, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentInvoices_WhenMoreThan5Qualifying_ReturnsOnly5MostRecent(
        PositiveInt[] dateSeeds)
    {
        if (dateSeeds.Length < 6)
            return true.ToProperty().Label("Fewer than 6 seeds — trivially true");

        var invoiceCount = Math.Min(dateSeeds.Length, 20);
        var invoices = new List<Invoice>();

        // Create all invoices as issued and non-deleted to guarantee > 5 qualifying
        for (int i = 0; i < invoiceCount; i++)
        {
            var invoiceDate = GenerateInvoiceDate(dateSeeds[i].Get);
            invoices.Add(CreateInvoice(i + 1, TestBusinessId, InvoiceStatusIssued, isDeleted: false, invoiceDate));
        }

        var result = ComputeExpectedRecentInvoices(invoices, TestBusinessId);

        // Should be exactly 5 since we have more than 5 qualifying invoices
        var exactlyFive = result.Count == 5;

        // The 5 returned should be the ones with the latest InvoiceDate
        var allQualifying = invoices
            .Where(i => i.BusinessId == TestBusinessId
                        && i.InvoiceStatusTypeId == InvoiceStatusIssued
                        && !i.IsDeleted)
            .OrderByDescending(i => i.InvoiceDate)
            .ToList();

        var top5Dates = allQualifying.Take(5).Select(i => i.InvoiceDate).ToList();
        var resultDates = result.Select(i => i.InvoiceDate).ToList();
        var correctTop5 = top5Dates.SequenceEqual(resultDates);

        return (exactlyFive && correctTop5).ToProperty()
            .Label($"ExactlyFive={exactlyFive}, CorrectTop5={correctTop5}, " +
                   $"QualifyingCount={allQualifying.Count}");
    }

    /// <summary>
    /// Deleted invoices are excluded from recent invoices regardless of their status or date.
    /// **Validates: Requirements 4.1, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentInvoices_ExcludesDeletedInvoices(PositiveInt[] dateSeeds)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(dateSeeds.Length, 15);
        var invoices = new List<Invoice>();

        // Create all invoices as issued but deleted
        for (int i = 0; i < invoiceCount; i++)
        {
            var invoiceDate = GenerateInvoiceDate(dateSeeds[i].Get);
            invoices.Add(CreateInvoice(i + 1, TestBusinessId, InvoiceStatusIssued, isDeleted: true, invoiceDate));
        }

        var result = ComputeExpectedRecentInvoices(invoices, TestBusinessId);

        return (result.Count == 0).ToProperty()
            .Label($"Expected 0 results for all-deleted invoices, got {result.Count}");
    }

    /// <summary>
    /// Non-issued invoices (Draft, Cancelled) are excluded from recent invoices.
    /// **Validates: Requirements 4.1, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentInvoices_ExcludesNonIssuedInvoices(PositiveInt[] dateSeeds)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(dateSeeds.Length, 15);
        var invoices = new List<Invoice>();

        // Create invoices with Draft (1) and Cancelled (3) statuses only
        for (int i = 0; i < invoiceCount; i++)
        {
            var invoiceDate = GenerateInvoiceDate(dateSeeds[i].Get);
            var statusTypeId = (i % 2 == 0) ? 1 : 3; // Alternate between Draft and Cancelled
            invoices.Add(CreateInvoice(i + 1, TestBusinessId, statusTypeId, isDeleted: false, invoiceDate));
        }

        var result = ComputeExpectedRecentInvoices(invoices, TestBusinessId);

        return (result.Count == 0).ToProperty()
            .Label($"Expected 0 results for non-issued invoices, got {result.Count}");
    }

    /// <summary>
    /// Mixed scenario: invoices with varying statuses, deletion flags, and dates.
    /// Only issued, non-deleted invoices appear in the result, capped at 5, ordered by date desc.
    /// **Validates: Requirements 4.1, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentInvoices_MixedScenario_FiltersAndOrdersCorrectly(
        PositiveInt[] dateSeeds, int[] statusSeeds, bool[] deletedFlags, bool[] sameBusinessFlags)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(dateSeeds.Length, 25);
        var invoices = new List<Invoice>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var invoiceDate = GenerateInvoiceDate(dateSeeds[i].Get);
            var statusTypeId = statusSeeds.Length > 0
                ? GenerateInvoiceStatusTypeId(statusSeeds[i % statusSeeds.Length])
                : InvoiceStatusIssued;
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            var isSameBusiness = sameBusinessFlags.Length > 0
                ? sameBusinessFlags[i % sameBusinessFlags.Length]
                : true;
            var businessId = isSameBusiness ? TestBusinessId : 99;

            invoices.Add(CreateInvoice(i + 1, businessId, statusTypeId, isDeleted, invoiceDate));
        }

        var result = ComputeExpectedRecentInvoices(invoices, TestBusinessId);

        // Assert: at most 5 items
        var atMost5 = result.Count <= 5;

        // Assert: all items have InvoiceStatusTypeId = 2
        var allIssued = result.All(i => i.InvoiceStatusTypeId == InvoiceStatusIssued);

        // Assert: all items have IsDeleted = false
        var allNonDeleted = result.All(i => !i.IsDeleted);

        // Assert: all items belong to TestBusinessId
        var allSameBusiness = result.All(i => i.BusinessId == TestBusinessId);

        // Assert: ordered by InvoiceDate descending
        var orderedDesc = true;
        for (int i = 1; i < result.Count; i++)
        {
            if (result[i].InvoiceDate > result[i - 1].InvoiceDate)
            {
                orderedDesc = false;
                break;
            }
        }

        // Assert: result count matches expected qualifying count (capped at 5)
        var qualifyingCount = invoices
            .Count(i => i.BusinessId == TestBusinessId
                        && i.InvoiceStatusTypeId == InvoiceStatusIssued
                        && !i.IsDeleted);
        var expectedCount = Math.Min(qualifyingCount, 5);
        var correctCount = result.Count == expectedCount;

        return (atMost5 && allIssued && allNonDeleted && allSameBusiness && orderedDesc && correctCount)
            .ToProperty()
            .Label($"AtMost5={atMost5}, AllIssued={allIssued}, AllNonDeleted={allNonDeleted}, " +
                   $"AllSameBusiness={allSameBusiness}, OrderedDesc={orderedDesc}, " +
                   $"CorrectCount={correctCount}, ResultCount={result.Count}, Expected={expectedCount}");
    }

    /// <summary>
    /// When no invoices exist, the result is an empty list.
    /// **Validates: Requirements 4.1, 4.5**
    /// </summary>
    [Fact]
    public void RecentInvoices_NoInvoices_ReturnsEmptyList()
    {
        var invoices = new List<Invoice>();
        var result = ComputeExpectedRecentInvoices(invoices, TestBusinessId);
        Assert.Empty(result);
    }

    /// <summary>
    /// When fewer than 5 qualifying invoices exist, all are returned.
    /// **Validates: Requirements 4.1, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentInvoices_FewerThan5Qualifying_ReturnsAll(PositiveInt dateSeed)
    {
        // Create between 1 and 4 qualifying invoices
        var count = (Math.Abs(dateSeed.Get) % 4) + 1;
        var invoices = new List<Invoice>();

        for (int i = 0; i < count; i++)
        {
            var invoiceDate = GenerateInvoiceDate(dateSeed.Get + i * 37);
            invoices.Add(CreateInvoice(i + 1, TestBusinessId, InvoiceStatusIssued, isDeleted: false, invoiceDate));
        }

        var result = ComputeExpectedRecentInvoices(invoices, TestBusinessId);

        return (result.Count == count).ToProperty()
            .Label($"Expected {count} results, got {result.Count}");
    }

    #endregion
}
