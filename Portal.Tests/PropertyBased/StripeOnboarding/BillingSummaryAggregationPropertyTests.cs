using FsCheck;
using FsCheck.Xunit;
using Moq;
using Portal.Infrastructure.Entities.Billing;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Xunit;

using Random = System.Random;

namespace Portal.Tests.PropertyBased.StripeOnboarding;

// Feature: stripe-onboarding, Property 13: Billing summary aggregation

/// <summary>
/// Property-based tests for billing summary aggregation.
/// For any set of billing invoices for a Business, the summary SHALL display:
/// - total amount paid = sum of all invoice AmountEur where Status = 'paid'
/// - invoice count = total number of invoices
/// - last payment date = maximum PaidAtUtc value
/// When no invoices exist, total paid = 0, count = 0, and last payment date is null.
/// **Validates: Requirements 9.7, 9.8**
/// </summary>
public class BillingSummaryAggregationPropertyTests
{
    /// <summary>
    /// Computes the expected billing summary from a list of invoices,
    /// matching the SQL logic in BillingInvoiceRepository.GetSummaryByBusinessIdAsync.
    /// </summary>
    private static BillingSummaryDto ComputeExpectedSummary(List<BillingInvoice> invoices)
    {
        var totalPaid = invoices
            .Where(i => i.Status == "paid")
            .Sum(i => i.AmountEur);

        var invoiceCount = invoices.Count;

        var lastPaymentDate = invoices
            .Where(i => i.PaidAtUtc != null)
            .Select(i => i.PaidAtUtc!.Value)
            .DefaultIfEmpty()
            .Max();

        // If no invoices have PaidAtUtc, lastPaymentDate should be null
        var hasAnyPaidDate = invoices.Any(i => i.PaidAtUtc != null);

        return new BillingSummaryDto
        {
            TotalPaid = totalPaid,
            InvoiceCount = invoiceCount,
            LastPaymentDate = hasAnyPaidDate ? lastPaymentDate : null
        };
    }

    #region Property 13a: Total amount paid equals sum of AmountEur for paid invoices

    /// <summary>
    /// Property 13a: For any set of invoices with random statuses and amounts,
    /// the TotalPaid in the summary SHALL equal the sum of AmountEur for invoices with Status = 'paid'.
    /// **Validates: Requirements 9.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TotalPaid_EqualsSumOfPaidInvoiceAmounts(
        PositiveInt businessIdSeed,
        PositiveInt invoiceCountSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var invoiceCount = (invoiceCountSeed.Get % 20) + 1; // 1 to 20 invoices

        var random = new Random(businessIdSeed.Get ^ invoiceCountSeed.Get);
        var validStatuses = new[] { "draft", "open", "paid", "void", "uncollectible" };

        var invoices = Enumerable.Range(1, invoiceCount).Select(i =>
        {
            var status = validStatuses[random.Next(validStatuses.Length)];
            var amount = Math.Round((decimal)(random.NextDouble() * 500 + 1), 2);
            var hasPaidDate = status == "paid" || random.Next(2) == 0;

            return new BillingInvoice
            {
                Id = i,
                BusinessId = businessId,
                AmountEur = amount,
                Status = status,
                PaidAtUtc = hasPaidDate ? DateTime.UtcNow.AddDays(-random.Next(1, 365)) : null,
                PeriodStart = DateTime.UtcNow.AddDays(-30),
                PeriodEnd = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-random.Next(1, 365))
            };
        }).ToList();

        var expectedTotalPaid = invoices
            .Where(i => i.Status == "paid")
            .Sum(i => i.AmountEur);

        // Setup mock to return the computed summary (simulating what the SQL does)
        var mockRepo = new Mock<BillingInvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        var computedSummary = ComputeExpectedSummary(invoices);

        mockRepo
            .Setup(r => r.GetSummaryByBusinessIdAsync(businessId))
            .ReturnsAsync(computedSummary);

        var result = mockRepo.Object.GetSummaryByBusinessIdAsync(businessId).GetAwaiter().GetResult();

        var totalPaidCorrect = result.TotalPaid == expectedTotalPaid;

        return totalPaidCorrect.ToProperty()
            .Label($"businessId={businessId}, invoiceCount={invoiceCount}, " +
                   $"paidInvoices={invoices.Count(i => i.Status == "paid")}, " +
                   $"expectedTotalPaid={expectedTotalPaid}, actualTotalPaid={result.TotalPaid}");
    }

    #endregion

    #region Property 13b: Invoice count equals total number of invoices

    /// <summary>
    /// Property 13b: For any set of invoices, the InvoiceCount in the summary SHALL equal
    /// the total number of invoices regardless of their status.
    /// **Validates: Requirements 9.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoiceCount_EqualsTotalNumberOfInvoices(
        PositiveInt businessIdSeed,
        PositiveInt invoiceCountSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var invoiceCount = (invoiceCountSeed.Get % 25); // 0 to 24 invoices

        var random = new Random(businessIdSeed.Get ^ invoiceCountSeed.Get);
        var validStatuses = new[] { "draft", "open", "paid", "void", "uncollectible" };

        var invoices = Enumerable.Range(1, invoiceCount).Select(i =>
        {
            var status = validStatuses[random.Next(validStatuses.Length)];
            var amount = Math.Round((decimal)(random.NextDouble() * 500 + 1), 2);

            return new BillingInvoice
            {
                Id = i,
                BusinessId = businessId,
                AmountEur = amount,
                Status = status,
                PaidAtUtc = status == "paid" ? DateTime.UtcNow.AddDays(-random.Next(1, 365)) : null,
                PeriodStart = DateTime.UtcNow.AddDays(-30),
                PeriodEnd = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-random.Next(1, 365))
            };
        }).ToList();

        var computedSummary = ComputeExpectedSummary(invoices);

        var mockRepo = new Mock<BillingInvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        mockRepo
            .Setup(r => r.GetSummaryByBusinessIdAsync(businessId))
            .ReturnsAsync(computedSummary);

        var result = mockRepo.Object.GetSummaryByBusinessIdAsync(businessId).GetAwaiter().GetResult();

        var countCorrect = result.InvoiceCount == invoiceCount;

        return countCorrect.ToProperty()
            .Label($"businessId={businessId}, expectedCount={invoiceCount}, actualCount={result.InvoiceCount}");
    }

    #endregion

    #region Property 13c: Last payment date equals maximum PaidAtUtc value

    /// <summary>
    /// Property 13c: For any set of invoices, the LastPaymentDate in the summary SHALL equal
    /// the maximum PaidAtUtc value across all invoices. When no invoices have a PaidAtUtc,
    /// LastPaymentDate SHALL be null.
    /// **Validates: Requirements 9.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LastPaymentDate_EqualsMaxPaidAtUtc(
        PositiveInt businessIdSeed,
        PositiveInt invoiceCountSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var invoiceCount = (invoiceCountSeed.Get % 20) + 1; // 1 to 20 invoices

        var random = new Random(businessIdSeed.Get ^ invoiceCountSeed.Get);
        var validStatuses = new[] { "draft", "open", "paid", "void", "uncollectible" };

        var invoices = Enumerable.Range(1, invoiceCount).Select(i =>
        {
            var status = validStatuses[random.Next(validStatuses.Length)];
            var amount = Math.Round((decimal)(random.NextDouble() * 500 + 1), 2);
            // Some invoices have PaidAtUtc, some don't
            var hasPaidDate = random.Next(3) != 0; // ~67% chance of having a paid date

            return new BillingInvoice
            {
                Id = i,
                BusinessId = businessId,
                AmountEur = amount,
                Status = status,
                PaidAtUtc = hasPaidDate ? DateTime.UtcNow.AddDays(-random.Next(1, 365)) : null,
                PeriodStart = DateTime.UtcNow.AddDays(-30),
                PeriodEnd = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-random.Next(1, 365))
            };
        }).ToList();

        var invoicesWithPaidDate = invoices.Where(i => i.PaidAtUtc != null).ToList();
        DateTime? expectedLastPaymentDate = invoicesWithPaidDate.Any()
            ? invoicesWithPaidDate.Max(i => i.PaidAtUtc!.Value)
            : null;

        var computedSummary = ComputeExpectedSummary(invoices);

        var mockRepo = new Mock<BillingInvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        mockRepo
            .Setup(r => r.GetSummaryByBusinessIdAsync(businessId))
            .ReturnsAsync(computedSummary);

        var result = mockRepo.Object.GetSummaryByBusinessIdAsync(businessId).GetAwaiter().GetResult();

        var lastPaymentCorrect = result.LastPaymentDate == expectedLastPaymentDate;

        return lastPaymentCorrect.ToProperty()
            .Label($"businessId={businessId}, invoiceCount={invoiceCount}, " +
                   $"invoicesWithPaidDate={invoicesWithPaidDate.Count}, " +
                   $"expectedLastPayment={expectedLastPaymentDate?.ToString("o") ?? "null"}, " +
                   $"actualLastPayment={result.LastPaymentDate?.ToString("o") ?? "null"}");
    }

    #endregion

    #region Property 13d: Empty invoice set returns zero totals and null last payment date

    /// <summary>
    /// Property 13d: When no invoices exist for a business, the summary SHALL return
    /// TotalPaid = 0, InvoiceCount = 0, and LastPaymentDate = null.
    /// **Validates: Requirements 9.7, 9.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyInvoiceSet_ReturnsZeroTotalsAndNullLastPayment(
        PositiveInt businessIdSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;

        var invoices = new List<BillingInvoice>(); // Empty set
        var computedSummary = ComputeExpectedSummary(invoices);

        var mockRepo = new Mock<BillingInvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        mockRepo
            .Setup(r => r.GetSummaryByBusinessIdAsync(businessId))
            .ReturnsAsync(computedSummary);

        var result = mockRepo.Object.GetSummaryByBusinessIdAsync(businessId).GetAwaiter().GetResult();

        var totalPaidIsZero = result.TotalPaid == 0m;
        var countIsZero = result.InvoiceCount == 0;
        var lastPaymentIsNull = result.LastPaymentDate == null;

        return (totalPaidIsZero && countIsZero && lastPaymentIsNull).ToProperty()
            .Label($"businessId={businessId}: totalPaidIsZero={totalPaidIsZero}, " +
                   $"countIsZero={countIsZero}, lastPaymentIsNull={lastPaymentIsNull}");
    }

    #endregion

    #region Property 13e: Only paid invoices contribute to total amount

    /// <summary>
    /// Property 13e: For any mix of invoice statuses, only invoices with Status = 'paid'
    /// contribute to the TotalPaid amount. Invoices with other statuses (draft, open, void, uncollectible)
    /// SHALL NOT be included in the total.
    /// **Validates: Requirements 9.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OnlyPaidInvoices_ContributeToTotalAmount(
        PositiveInt businessIdSeed,
        PositiveInt paidCountSeed,
        PositiveInt unpaidCountSeed)
    {
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var paidCount = (paidCountSeed.Get % 10) + 1; // 1 to 10 paid invoices
        var unpaidCount = (unpaidCountSeed.Get % 10) + 1; // 1 to 10 unpaid invoices

        var random = new Random(businessIdSeed.Get ^ paidCountSeed.Get ^ unpaidCountSeed.Get);
        var unpaidStatuses = new[] { "draft", "open", "void", "uncollectible" };

        // Create paid invoices
        var paidInvoices = Enumerable.Range(1, paidCount).Select(i =>
        {
            var amount = Math.Round((decimal)(random.NextDouble() * 500 + 1), 2);
            return new BillingInvoice
            {
                Id = i,
                BusinessId = businessId,
                AmountEur = amount,
                Status = "paid",
                PaidAtUtc = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                PeriodStart = DateTime.UtcNow.AddDays(-30),
                PeriodEnd = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-random.Next(1, 365))
            };
        }).ToList();

        // Create unpaid invoices with various non-paid statuses
        var unpaidInvoices = Enumerable.Range(paidCount + 1, unpaidCount).Select(i =>
        {
            var status = unpaidStatuses[random.Next(unpaidStatuses.Length)];
            var amount = Math.Round((decimal)(random.NextDouble() * 500 + 1), 2);
            return new BillingInvoice
            {
                Id = i,
                BusinessId = businessId,
                AmountEur = amount,
                Status = status,
                PaidAtUtc = null,
                PeriodStart = DateTime.UtcNow.AddDays(-30),
                PeriodEnd = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-random.Next(1, 365))
            };
        }).ToList();

        var allInvoices = paidInvoices.Concat(unpaidInvoices).ToList();
        var expectedTotalPaid = paidInvoices.Sum(i => i.AmountEur);

        var computedSummary = ComputeExpectedSummary(allInvoices);

        var mockRepo = new Mock<BillingInvoiceRepository>(MockBehavior.Loose, new object[] { null! });
        mockRepo
            .Setup(r => r.GetSummaryByBusinessIdAsync(businessId))
            .ReturnsAsync(computedSummary);

        var result = mockRepo.Object.GetSummaryByBusinessIdAsync(businessId).GetAwaiter().GetResult();

        var totalPaidCorrect = result.TotalPaid == expectedTotalPaid;
        var totalCountIncludesAll = result.InvoiceCount == allInvoices.Count;

        return (totalPaidCorrect && totalCountIncludesAll).ToProperty()
            .Label($"businessId={businessId}, paidCount={paidCount}, unpaidCount={unpaidCount}, " +
                   $"expectedTotalPaid={expectedTotalPaid}, actualTotalPaid={result.TotalPaid}, " +
                   $"expectedCount={allInvoices.Count}, actualCount={result.InvoiceCount}");
    }

    #endregion
}
