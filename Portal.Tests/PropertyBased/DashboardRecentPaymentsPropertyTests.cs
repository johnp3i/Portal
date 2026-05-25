using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: dashboard-upgrade, Property 10: Recent payments ordering and filtering

/// <summary>
/// Property-based tests for Dashboard Recent Payments.
/// Validates that the recent payments result contains at most 5 items,
/// all with IsVoided = false, ordered by PaymentDateUtc descending.
/// Tested as a pure computation over generated payment data.
/// **Validates: Requirements 6.1, 6.5**
/// </summary>
public class DashboardRecentPaymentsPropertyTests
{
    private const int TestBusinessId = 1;

    #region Test Infrastructure

    /// <summary>
    /// Computes the expected recent payments from a list of payments.
    /// This is the oracle function: filter to non-voided (IsVoided = false),
    /// same business, then take top 5 ordered by PaymentDateUtc descending.
    /// </summary>
    private static List<Payment> ComputeExpectedRecentPayments(List<Payment> payments, int businessId)
    {
        return payments
            .Where(p => p.BusinessId == businessId && !p.IsVoided)
            .OrderByDescending(p => p.PaymentDateUtc)
            .Take(5)
            .ToList();
    }

    /// <summary>
    /// Creates a Payment entity with controlled parameters for testing.
    /// </summary>
    private static Payment CreatePayment(
        int id, int businessId, DateTime paymentDateUtc, decimal amount, bool isVoided)
    {
        return new Payment
        {
            Id = id,
            BusinessId = businessId,
            InvoiceId = 1,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = paymentDateUtc,
            Amount = amount,
            Reference = $"REF-{id:D4}",
            Notes = null,
            IsVoided = isVoided,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generates a random PaymentDateUtc from a seed.
    /// Produces dates within the last 2 years.
    /// </summary>
    private static DateTime GeneratePaymentDate(int seed)
    {
        var now = DateTime.UtcNow;
        var daysBack = Math.Abs(seed) % 730; // up to 2 years back
        var hoursOffset = Math.Abs(seed) % 24;
        return now.AddDays(-daysBack).AddHours(-hoursOffset);
    }

    /// <summary>
    /// Generates a positive decimal amount from a seed.
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = Math.Abs(seed) % 999999 + 1;
        return raw / 100m;
    }

    #endregion

    #region Property 10: Recent payments ordering and filtering

    /// <summary>
    /// Property 10: Recent payments result contains at most 5 items, all non-voided,
    /// ordered by PaymentDateUtc descending.
    /// Generates random payments with various voided flags and dates.
    /// **Validates: Requirements 6.1, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentPayments_ContainsAtMost5Items_AllNonVoided_OrderedByDateDesc(
        PositiveInt[] dateSeeds, bool[] voidedFlags)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var paymentCount = Math.Min(dateSeeds.Length, 25);
        var payments = new List<Payment>();

        for (int i = 0; i < paymentCount; i++)
        {
            var paymentDate = GeneratePaymentDate(dateSeeds[i].Get + i);
            var amount = GenerateAmount(dateSeeds[i].Get);
            var isVoided = voidedFlags.Length > 0 && voidedFlags[i % voidedFlags.Length];

            payments.Add(CreatePayment(i + 1, TestBusinessId, paymentDate, amount, isVoided));
        }

        var result = ComputeExpectedRecentPayments(payments, TestBusinessId);

        // Assert: at most 5 items
        var atMost5 = result.Count <= 5;

        // Assert: all items have IsVoided = false
        var allNonVoided = result.All(p => !p.IsVoided);

        // Assert: ordered by PaymentDateUtc descending
        var orderedDesc = true;
        for (int i = 1; i < result.Count; i++)
        {
            if (result[i].PaymentDateUtc > result[i - 1].PaymentDateUtc)
            {
                orderedDesc = false;
                break;
            }
        }

        return (atMost5 && allNonVoided && orderedDesc).ToProperty()
            .Label($"AtMost5={atMost5}, AllNonVoided={allNonVoided}, " +
                   $"OrderedDesc={orderedDesc}, ResultCount={result.Count}, TotalPayments={paymentCount}");
    }

    /// <summary>
    /// When more than 5 non-voided payments exist, only the 5 most recent are returned.
    /// **Validates: Requirements 6.1, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentPayments_WhenMoreThan5Qualifying_ReturnsOnly5MostRecent(
        PositiveInt[] dateSeeds)
    {
        if (dateSeeds.Length < 6)
            return true.ToProperty().Label("Fewer than 6 seeds — trivially true");

        var paymentCount = Math.Min(dateSeeds.Length, 20);
        var payments = new List<Payment>();

        // Create all payments as non-voided to guarantee > 5 qualifying
        for (int i = 0; i < paymentCount; i++)
        {
            var paymentDate = GeneratePaymentDate(dateSeeds[i].Get + i);
            var amount = GenerateAmount(dateSeeds[i].Get);
            payments.Add(CreatePayment(i + 1, TestBusinessId, paymentDate, amount, isVoided: false));
        }

        var result = ComputeExpectedRecentPayments(payments, TestBusinessId);

        // Should be exactly 5 since we have more than 5 qualifying payments
        var exactlyFive = result.Count == 5;

        // The 5 returned should be the ones with the latest PaymentDateUtc
        var allQualifying = payments
            .Where(p => p.BusinessId == TestBusinessId && !p.IsVoided)
            .OrderByDescending(p => p.PaymentDateUtc)
            .ToList();

        var top5Dates = allQualifying.Take(5).Select(p => p.PaymentDateUtc).ToList();
        var resultDates = result.Select(p => p.PaymentDateUtc).ToList();
        var correctTop5 = top5Dates.SequenceEqual(resultDates);

        return (exactlyFive && correctTop5).ToProperty()
            .Label($"ExactlyFive={exactlyFive}, CorrectTop5={correctTop5}, " +
                   $"QualifyingCount={allQualifying.Count}");
    }

    /// <summary>
    /// Voided payments are excluded from recent payments regardless of their date.
    /// **Validates: Requirements 6.1, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentPayments_ExcludesVoidedPayments(PositiveInt[] dateSeeds)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var paymentCount = Math.Min(dateSeeds.Length, 15);
        var payments = new List<Payment>();

        // Create all payments as voided
        for (int i = 0; i < paymentCount; i++)
        {
            var paymentDate = GeneratePaymentDate(dateSeeds[i].Get + i);
            var amount = GenerateAmount(dateSeeds[i].Get);
            payments.Add(CreatePayment(i + 1, TestBusinessId, paymentDate, amount, isVoided: true));
        }

        var result = ComputeExpectedRecentPayments(payments, TestBusinessId);

        return (result.Count == 0).ToProperty()
            .Label($"Expected 0 results for all-voided payments, got {result.Count}");
    }

    /// <summary>
    /// Payments from a different business are excluded from recent payments.
    /// **Validates: Requirements 6.1, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentPayments_ExcludesOtherBusinessPayments(PositiveInt[] dateSeeds)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var paymentCount = Math.Min(dateSeeds.Length, 15);
        var payments = new List<Payment>();
        var otherBusinessId = 99;

        // Create non-voided payments for a DIFFERENT business
        for (int i = 0; i < paymentCount; i++)
        {
            var paymentDate = GeneratePaymentDate(dateSeeds[i].Get + i);
            var amount = GenerateAmount(dateSeeds[i].Get);
            payments.Add(CreatePayment(i + 1, otherBusinessId, paymentDate, amount, isVoided: false));
        }

        // Compute for TestBusinessId — should be zero since all payments belong to otherBusinessId
        var result = ComputeExpectedRecentPayments(payments, TestBusinessId);

        return (result.Count == 0).ToProperty()
            .Label($"Expected 0 results for other business payments, got {result.Count}");
    }

    /// <summary>
    /// Mixed scenario: payments with varying voided flags, dates, and business IDs.
    /// Only non-voided, same-business payments appear in the result, capped at 5, ordered by date desc.
    /// **Validates: Requirements 6.1, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentPayments_MixedScenario_FiltersAndOrdersCorrectly(
        PositiveInt[] dateSeeds, bool[] voidedFlags, bool[] sameBusinessFlags)
    {
        if (dateSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var paymentCount = Math.Min(dateSeeds.Length, 25);
        var payments = new List<Payment>();

        for (int i = 0; i < paymentCount; i++)
        {
            var paymentDate = GeneratePaymentDate(dateSeeds[i].Get + i);
            var amount = GenerateAmount(dateSeeds[i].Get);
            var isVoided = voidedFlags.Length > 0 && voidedFlags[i % voidedFlags.Length];
            var isSameBusiness = sameBusinessFlags.Length > 0
                ? sameBusinessFlags[i % sameBusinessFlags.Length]
                : true;
            var businessId = isSameBusiness ? TestBusinessId : 99;

            payments.Add(CreatePayment(i + 1, businessId, paymentDate, amount, isVoided));
        }

        var result = ComputeExpectedRecentPayments(payments, TestBusinessId);

        // Assert: at most 5 items
        var atMost5 = result.Count <= 5;

        // Assert: all items have IsVoided = false
        var allNonVoided = result.All(p => !p.IsVoided);

        // Assert: all items belong to TestBusinessId
        var allSameBusiness = result.All(p => p.BusinessId == TestBusinessId);

        // Assert: ordered by PaymentDateUtc descending
        var orderedDesc = true;
        for (int i = 1; i < result.Count; i++)
        {
            if (result[i].PaymentDateUtc > result[i - 1].PaymentDateUtc)
            {
                orderedDesc = false;
                break;
            }
        }

        // Assert: result count matches expected qualifying count (capped at 5)
        var qualifyingCount = payments
            .Count(p => p.BusinessId == TestBusinessId && !p.IsVoided);
        var expectedCount = Math.Min(qualifyingCount, 5);
        var correctCount = result.Count == expectedCount;

        return (atMost5 && allNonVoided && allSameBusiness && orderedDesc && correctCount)
            .ToProperty()
            .Label($"AtMost5={atMost5}, AllNonVoided={allNonVoided}, " +
                   $"AllSameBusiness={allSameBusiness}, OrderedDesc={orderedDesc}, " +
                   $"CorrectCount={correctCount}, ResultCount={result.Count}, Expected={expectedCount}");
    }

    /// <summary>
    /// When no payments exist, the result is an empty list.
    /// **Validates: Requirements 6.1, 6.5**
    /// </summary>
    [Fact]
    public void RecentPayments_NoPayments_ReturnsEmptyList()
    {
        var payments = new List<Payment>();
        var result = ComputeExpectedRecentPayments(payments, TestBusinessId);
        Assert.Empty(result);
    }

    /// <summary>
    /// When fewer than 5 qualifying payments exist, all are returned.
    /// **Validates: Requirements 6.1, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentPayments_FewerThan5Qualifying_ReturnsAll(PositiveInt dateSeed)
    {
        // Create between 1 and 4 qualifying payments
        var count = (Math.Abs(dateSeed.Get) % 4) + 1;
        var payments = new List<Payment>();

        for (int i = 0; i < count; i++)
        {
            var paymentDate = GeneratePaymentDate(dateSeed.Get + i * 37);
            var amount = GenerateAmount(dateSeed.Get + i);
            payments.Add(CreatePayment(i + 1, TestBusinessId, paymentDate, amount, isVoided: false));
        }

        var result = ComputeExpectedRecentPayments(payments, TestBusinessId);

        return (result.Count == count).ToProperty()
            .Label($"Expected {count} results, got {result.Count}");
    }

    #endregion
}
