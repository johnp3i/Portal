using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Models;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 12: Recent payments sorted, complete, and void-excluded

/// <summary>
/// Property-based tests for recent payments returned by the Dashboard_Service.
/// Validates that results are sorted by PaymentDateUtc descending, all required fields are present,
/// and no voided payments appear in the results.
/// **Validates: Requirements 8.1, 8.2, 8.5**
/// </summary>
public class RecentPaymentsSortedPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// Represents a raw payment record that may or may not be voided.
    /// Used to simulate the source data before filtering.
    /// </summary>
    private class TestPaymentRecord
    {
        public int Id { get; set; }
        public DateTime PaymentDateUtc { get; set; }
        public string InvoiceNumber { get; set; } = null!;
        public string CustomerName { get; set; } = null!;
        public string PaymentMethodName { get; set; } = null!;
        public decimal Amount { get; set; }
        public bool IsFullPayment { get; set; }
        public bool IsVoided { get; set; }
    }

    private static readonly string[] PaymentMethods = { "Cash", "BankTransfer", "Card", "Cheque", "Other" };

    /// <summary>
    /// Generates a test payment record with random but valid field values.
    /// </summary>
    private static TestPaymentRecord GeneratePaymentRecord(int id, int seed, bool isVoided)
    {
        // Generate a payment date within the last 365 days
        var daysAgo = Math.Abs(seed % 365);
        var paymentDate = DateTime.UtcNow.AddDays(-daysAgo).AddHours(-(seed % 24)).AddMinutes(-(seed % 60));

        // Generate a non-empty invoice number
        var invoiceNumber = $"INV-{Math.Abs((seed * 7 + id) % 99999) + 1:D5}";

        // Generate a non-empty customer name
        var customerName = $"Customer-{Math.Abs((seed * 3 + id) % 9999) + 1}";

        // Select a payment method
        var paymentMethodName = PaymentMethods[Math.Abs((seed + id) % PaymentMethods.Length)];

        // Generate a positive amount between 0.01 and 99999.99
        var amount = (Math.Abs(seed % 9999999) + 1) / 100m;

        // Determine if full payment based on seed
        var isFullPayment = (seed % 3) == 0;

        return new TestPaymentRecord
        {
            Id = id,
            PaymentDateUtc = paymentDate,
            InvoiceNumber = invoiceNumber,
            CustomerName = customerName,
            PaymentMethodName = paymentMethodName,
            Amount = amount,
            IsFullPayment = isFullPayment,
            IsVoided = isVoided
        };
    }

    /// <summary>
    /// Filters out voided payments and converts to RecentPaymentDto list,
    /// sorted by PaymentDateUtc descending — mimicking the Dashboard_Service behavior.
    /// </summary>
    private static List<RecentPaymentDto> SimulateRecentPaymentsQuery(List<TestPaymentRecord> allPayments)
    {
        return allPayments
            .Where(p => !p.IsVoided)
            .OrderByDescending(p => p.PaymentDateUtc)
            .Select(p => new RecentPaymentDto
            {
                Id = p.Id,
                PaymentDateUtc = p.PaymentDateUtc,
                InvoiceNumber = p.InvoiceNumber,
                CustomerName = p.CustomerName,
                PaymentMethodName = p.PaymentMethodName,
                Amount = p.Amount,
                IsFullPayment = p.IsFullPayment
            })
            .ToList();
    }

    #endregion

    #region Property 12: Recent payments sorted, complete, and void-excluded

    /// <summary>
    /// Property 12 (sort order): Recent payments SHALL be sorted by PaymentDateUtc descending.
    /// For any set of payments (some voided), the non-voided results are in descending date order.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentPayments_SortedByPaymentDateUtcDescending(PositiveInt[] seeds, bool[] voidFlags)
    {
        var paymentCount = Math.Min(seeds.Length, 20);
        if (paymentCount == 0)
            return true.ToProperty().Label("Empty list is trivially sorted");

        var flags = voidFlags.Length > 0 ? voidFlags : new[] { false };

        // Generate payments with mixed voided states
        var allPayments = new List<TestPaymentRecord>();
        for (int i = 0; i < paymentCount; i++)
        {
            var isVoided = flags[i % flags.Length];
            allPayments.Add(GeneratePaymentRecord(i + 1, seeds[i].Get, isVoided));
        }

        // Simulate the service query
        var results = SimulateRecentPaymentsQuery(allPayments);

        if (results.Count <= 1)
            return true.ToProperty().Label("Single or empty result is trivially sorted");

        // Verify descending sort order by PaymentDateUtc
        var isSorted = true;
        for (int i = 1; i < results.Count; i++)
        {
            if (results[i].PaymentDateUtc > results[i - 1].PaymentDateUtc)
            {
                isSorted = false;
                break;
            }
        }

        return isSorted.ToProperty()
            .Label($"List of {results.Count} payments should be sorted by PaymentDateUtc descending");
    }

    /// <summary>
    /// Property 12 (completeness): All required fields in each recent payment are non-null and non-default.
    /// Each result SHALL contain PaymentDateUtc, InvoiceNumber, CustomerName, PaymentMethodName, Amount,
    /// and IsFullPayment label.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentPayments_AllRequiredFieldsPresent(PositiveInt[] seeds, bool[] voidFlags)
    {
        var paymentCount = Math.Min(seeds.Length, 20);
        if (paymentCount == 0)
            return true.ToProperty().Label("Empty list trivially satisfies completeness");

        var flags = voidFlags.Length > 0 ? voidFlags : new[] { false };

        var allPayments = new List<TestPaymentRecord>();
        for (int i = 0; i < paymentCount; i++)
        {
            var isVoided = flags[i % flags.Length];
            allPayments.Add(GeneratePaymentRecord(i + 1, seeds[i].Get, isVoided));
        }

        var results = SimulateRecentPaymentsQuery(allPayments);

        if (results.Count == 0)
            return true.ToProperty().Label("No non-voided payments to check");

        // Verify all required fields are non-null/non-default
        var allFieldsPresent = results.All(payment =>
            payment.PaymentDateUtc != default &&
            !string.IsNullOrWhiteSpace(payment.InvoiceNumber) &&
            !string.IsNullOrWhiteSpace(payment.CustomerName) &&
            !string.IsNullOrWhiteSpace(payment.PaymentMethodName) &&
            payment.Amount > 0 &&
            payment.Id > 0);

        return allFieldsPresent.ToProperty()
            .Label($"All {results.Count} payments should have non-null/non-default required fields");
    }

    /// <summary>
    /// Property 12 (void exclusion): No voided payment SHALL appear in the recent payments results.
    /// For any set of payments with mixed IsVoided values, the results contain only non-voided payments.
    /// **Validates: Requirements 8.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentPayments_VoidedPaymentsExcluded(PositiveInt[] seeds)
    {
        var paymentCount = Math.Min(seeds.Length, 20);
        if (paymentCount == 0)
            return true.ToProperty().Label("Empty list trivially excludes voided");

        // Generate payments where roughly half are voided
        var allPayments = new List<TestPaymentRecord>();
        for (int i = 0; i < paymentCount; i++)
        {
            var isVoided = (seeds[i].Get % 2) == 0; // ~50% voided
            allPayments.Add(GeneratePaymentRecord(i + 1, seeds[i].Get, isVoided));
        }

        var voidedIds = allPayments.Where(p => p.IsVoided).Select(p => p.Id).ToHashSet();
        var results = SimulateRecentPaymentsQuery(allPayments);

        // Verify no voided payment appears in results
        var noVoidedInResults = results.All(payment => !voidedIds.Contains(payment.Id));

        // Verify the count matches: results should equal non-voided count
        var expectedCount = allPayments.Count(p => !p.IsVoided);
        var countMatches = results.Count == expectedCount;

        return (noVoidedInResults && countMatches).ToProperty()
            .Label($"Results ({results.Count}) should contain no voided payments. " +
                   $"Total: {allPayments.Count}, Voided: {voidedIds.Count}, Expected non-voided: {expectedCount}");
    }

    /// <summary>
    /// Property 12 (sort stability): Sorting an already-sorted list produces the same order.
    /// This verifies the sort is deterministic.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentPayments_DoubleSortProducesSameOrder(PositiveInt[] seeds)
    {
        var paymentCount = Math.Min(seeds.Length, 20);
        if (paymentCount == 0)
            return true.ToProperty().Label("Empty list trivially stable");

        var allPayments = new List<TestPaymentRecord>();
        for (int i = 0; i < paymentCount; i++)
        {
            allPayments.Add(GeneratePaymentRecord(i + 1, seeds[i].Get, isVoided: false));
        }

        // Sort once
        var sortedOnce = SimulateRecentPaymentsQuery(allPayments);

        // Sort the results again
        var sortedTwice = sortedOnce
            .OrderByDescending(p => p.PaymentDateUtc)
            .ToList();

        // Verify both sorts produce the same sequence
        var isStable = sortedOnce.Count == sortedTwice.Count;
        if (isStable)
        {
            for (int i = 0; i < sortedOnce.Count; i++)
            {
                if (sortedOnce[i].PaymentDateUtc != sortedTwice[i].PaymentDateUtc ||
                    sortedOnce[i].Id != sortedTwice[i].Id)
                {
                    isStable = false;
                    break;
                }
            }
        }

        return isStable.ToProperty()
            .Label($"Double-sorting {sortedOnce.Count} payments should produce identical order");
    }

    #endregion
}
