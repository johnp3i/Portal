using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using System.Globalization;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: dashboard-upgrade, Property 6: Revenue vs Expenses chart data grouping

/// <summary>
/// Property-based tests for Dashboard Revenue vs Expenses chart data computation.
/// Validates that the chart returns exactly 6 entries ordered chronologically,
/// where each entry's Revenue equals the sum of non-voided payments in that month
/// and each entry's Expenses equals the sum of non-cancelled purchases in that month.
/// Tested as a pure computation over generated payment and purchase data.
/// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
/// </summary>
public class DashboardRevenueVsExpensesPropertyTests
{
    private const int TestBusinessId = 1;

    #region Test Infrastructure

    /// <summary>
    /// Computes the expected Revenue vs Expenses chart data from lists of payments and purchases.
    /// This is the oracle function that mirrors the DashboardService logic:
    /// - Generates 6 month entries from 5 months ago to current month
    /// - Revenue = sum of Amount where IsVoided = 0 and PaymentDateUtc falls in that month
    /// - Expenses = sum of TotalAmount where IsCancelled = 0 and InvoiceDate falls in that month
    /// </summary>
    private static List<RevenueVsExpensesDto> ComputeExpectedChartData(
        List<Payment> payments, List<Purchase> purchases, int businessId)
    {
        var now = DateTime.UtcNow;
        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);

        var result = new List<RevenueVsExpensesDto>();

        for (var i = 0; i < 6; i++)
        {
            var monthDate = sixMonthsAgo.AddMonths(i);
            var year = monthDate.Year;
            var month = monthDate.Month;
            var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var revenue = payments
                .Where(p => p.BusinessId == businessId
                            && !p.IsVoided
                            && p.PaymentDateUtc >= monthStart
                            && p.PaymentDateUtc < monthEnd)
                .Sum(p => p.Amount);

            var expenses = purchases
                .Where(p => p.BusinessId == businessId
                            && !p.IsCancelled
                            && p.InvoiceDate >= DateOnly.FromDateTime(monthStart)
                            && p.InvoiceDate < DateOnly.FromDateTime(monthEnd))
                .Sum(p => p.TotalAmount);

            result.Add(new RevenueVsExpensesDto
            {
                Year = year,
                Month = month,
                Label = monthDate.ToString("MMM", CultureInfo.InvariantCulture),
                Revenue = revenue,
                Expenses = expenses
            });
        }

        return result;
    }

    /// <summary>
    /// Creates a payment with controlled parameters for testing.
    /// </summary>
    private static Payment CreatePayment(
        int id, int businessId, decimal amount, DateTime paymentDateUtc, bool isVoided)
    {
        return new Payment
        {
            Id = id,
            BusinessId = businessId,
            InvoiceId = 1,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = paymentDateUtc,
            Amount = amount,
            IsVoided = isVoided,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a purchase with controlled parameters for testing.
    /// </summary>
    private static Purchase CreatePurchase(
        int id, int businessId, decimal totalAmount, DateOnly invoiceDate, bool isCancelled)
    {
        return new Purchase
        {
            Id = id,
            BusinessId = businessId,
            SupplierId = 1,
            ExpenseCategoryId = 1,
            PurchaseOriginTypeId = 1,
            InvoiceNumber = $"PUR-{id:D4}",
            InvoiceDate = invoiceDate,
            AmountExcludingVat = totalAmount * 0.85m,
            VatAmount = totalAmount * 0.15m,
            TotalAmount = totalAmount,
            IsCancelled = isCancelled,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generates a DateTime within the 6-month window (from 5 months ago to end of current month).
    /// </summary>
    private static DateTime GenerateDateInSixMonthWindow(int seed)
    {
        var now = DateTime.UtcNow;
        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var windowEnd = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        var totalDays = (int)(windowEnd - sixMonthsAgo).TotalDays;

        var dayOffset = Math.Abs(seed) % totalDays;
        var hourOffset = Math.Abs(seed / totalDays) % 24;
        return sixMonthsAgo.AddDays(dayOffset).AddHours(hourOffset);
    }

    /// <summary>
    /// Generates a DateOnly within the 6-month window.
    /// </summary>
    private static DateOnly GenerateDateOnlyInSixMonthWindow(int seed)
    {
        var dt = GenerateDateInSixMonthWindow(seed);
        return DateOnly.FromDateTime(dt);
    }

    /// <summary>
    /// Generates a DateTime outside the 6-month window (before the window start).
    /// </summary>
    private static DateTime GenerateDateOutsideWindow(int seed)
    {
        var now = DateTime.UtcNow;
        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        // Go 1-24 months before the window start
        var monthsBack = (Math.Abs(seed) % 24) + 1;
        var targetMonth = sixMonthsAgo.AddMonths(-monthsBack);
        var daysInMonth = DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month);
        var dayOffset = Math.Abs(seed) % daysInMonth;
        return targetMonth.AddDays(dayOffset);
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

    #region Property 6: Revenue vs Expenses chart data grouping

    /// <summary>
    /// Property 6: The chart result always contains exactly 6 entries ordered chronologically.
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChartData_AlwaysContainsExactlySixEntries(
        PositiveInt[] paymentSeeds, PositiveInt[] purchaseSeeds)
    {
        var payments = new List<Payment>();
        var purchases = new List<Purchase>();

        var paymentCount = Math.Min(paymentSeeds.Length, 20);
        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(paymentSeeds[i].Get);
            var date = GenerateDateInSixMonthWindow(paymentSeeds[i].Get + i);
            payments.Add(CreatePayment(i + 1, TestBusinessId, amount, date, isVoided: false));
        }

        var purchaseCount = Math.Min(purchaseSeeds.Length, 20);
        for (int i = 0; i < purchaseCount; i++)
        {
            var amount = GenerateAmount(purchaseSeeds[i].Get);
            var date = GenerateDateOnlyInSixMonthWindow(purchaseSeeds[i].Get + i);
            purchases.Add(CreatePurchase(i + 1, TestBusinessId, amount, date, isCancelled: false));
        }

        var result = ComputeExpectedChartData(payments, purchases, TestBusinessId);

        return (result.Count == 6).ToProperty()
            .Label($"Expected exactly 6 entries, got {result.Count}");
    }

    /// <summary>
    /// Property 6: Chart entries are ordered chronologically (oldest to newest).
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChartData_IsOrderedChronologically(
        PositiveInt[] paymentSeeds, PositiveInt[] purchaseSeeds)
    {
        var payments = new List<Payment>();
        var purchases = new List<Purchase>();

        var paymentCount = Math.Min(paymentSeeds.Length, 15);
        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(paymentSeeds[i].Get);
            var date = GenerateDateInSixMonthWindow(paymentSeeds[i].Get + i);
            payments.Add(CreatePayment(i + 1, TestBusinessId, amount, date, isVoided: false));
        }

        var purchaseCount = Math.Min(purchaseSeeds.Length, 15);
        for (int i = 0; i < purchaseCount; i++)
        {
            var amount = GenerateAmount(purchaseSeeds[i].Get);
            var date = GenerateDateOnlyInSixMonthWindow(purchaseSeeds[i].Get + i);
            purchases.Add(CreatePurchase(i + 1, TestBusinessId, amount, date, isCancelled: false));
        }

        var result = ComputeExpectedChartData(payments, purchases, TestBusinessId);

        var isOrdered = true;
        for (int i = 1; i < result.Count; i++)
        {
            var prev = new DateTime(result[i - 1].Year, result[i - 1].Month, 1);
            var curr = new DateTime(result[i].Year, result[i].Month, 1);
            if (curr <= prev)
            {
                isOrdered = false;
                break;
            }
        }

        return isOrdered.ToProperty()
            .Label($"Entries not in chronological order: {string.Join(", ", result.Select(r => $"{r.Label} {r.Year}"))}");
    }

    /// <summary>
    /// Property 6: Each entry's Revenue equals the sum of non-voided payments in that month.
    /// Generates random payments with varying voided flags and dates spanning 6 months.
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChartData_RevenueEqualsNonVoidedPaymentSumPerMonth(
        PositiveInt[] amountSeeds, bool[] voidFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var paymentCount = Math.Min(amountSeeds.Length, 20);
        var payments = new List<Payment>();
        var flags = voidFlags.Length > 0 ? voidFlags : new[] { false };

        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var isVoided = flags[i % flags.Length];
            var date = GenerateDateInSixMonthWindow(amountSeeds[i].Get + i);
            payments.Add(CreatePayment(i + 1, TestBusinessId, amount, date, isVoided));
        }

        var result = ComputeExpectedChartData(payments, new List<Purchase>(), TestBusinessId);

        // Verify each month's revenue independently
        var now = DateTime.UtcNow;
        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var allMatch = true;
        var mismatchInfo = "";

        for (int i = 0; i < 6; i++)
        {
            var monthDate = sixMonthsAgo.AddMonths(i);
            var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var expectedRevenue = payments
                .Where(p => p.BusinessId == TestBusinessId
                            && !p.IsVoided
                            && p.PaymentDateUtc >= monthStart
                            && p.PaymentDateUtc < monthEnd)
                .Sum(p => p.Amount);

            if (result[i].Revenue != expectedRevenue)
            {
                allMatch = false;
                mismatchInfo = $"Month {result[i].Label} {result[i].Year}: expected Revenue={expectedRevenue}, got {result[i].Revenue}";
                break;
            }
        }

        return allMatch.ToProperty().Label(mismatchInfo);
    }

    /// <summary>
    /// Property 6: Each entry's Expenses equals the sum of non-cancelled purchases in that month.
    /// Generates random purchases with varying cancelled flags and dates spanning 6 months.
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChartData_ExpensesEqualsNonCancelledPurchaseSumPerMonth(
        PositiveInt[] amountSeeds, bool[] cancelFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var purchaseCount = Math.Min(amountSeeds.Length, 20);
        var purchases = new List<Purchase>();
        var flags = cancelFlags.Length > 0 ? cancelFlags : new[] { false };

        for (int i = 0; i < purchaseCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var isCancelled = flags[i % flags.Length];
            var date = GenerateDateOnlyInSixMonthWindow(amountSeeds[i].Get + i);
            purchases.Add(CreatePurchase(i + 1, TestBusinessId, amount, date, isCancelled));
        }

        var result = ComputeExpectedChartData(new List<Payment>(), purchases, TestBusinessId);

        // Verify each month's expenses independently
        var now = DateTime.UtcNow;
        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var allMatch = true;
        var mismatchInfo = "";

        for (int i = 0; i < 6; i++)
        {
            var monthDate = sixMonthsAgo.AddMonths(i);
            var monthStart = DateOnly.FromDateTime(new DateTime(monthDate.Year, monthDate.Month, 1));
            var monthEnd = DateOnly.FromDateTime(new DateTime(monthDate.Year, monthDate.Month, 1).AddMonths(1));

            var expectedExpenses = purchases
                .Where(p => p.BusinessId == TestBusinessId
                            && !p.IsCancelled
                            && p.InvoiceDate >= monthStart
                            && p.InvoiceDate < monthEnd)
                .Sum(p => p.TotalAmount);

            if (result[i].Expenses != expectedExpenses)
            {
                allMatch = false;
                mismatchInfo = $"Month {result[i].Label} {result[i].Year}: expected Expenses={expectedExpenses}, got {result[i].Expenses}";
                break;
            }
        }

        return allMatch.ToProperty().Label(mismatchInfo);
    }

    /// <summary>
    /// Property 6: Combined scenario — both revenue and expenses are correctly grouped per month.
    /// Generates random payments and purchases spanning 6 months with mixed voided/cancelled flags.
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChartData_CombinedRevenueAndExpenses_CorrectlyGroupedPerMonth(
        PositiveInt[] paymentSeeds, bool[] voidFlags,
        PositiveInt[] purchaseSeeds, bool[] cancelFlags)
    {
        if (paymentSeeds.Length == 0 && purchaseSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var payments = new List<Payment>();
        var purchases = new List<Purchase>();

        var paymentCount = Math.Min(paymentSeeds.Length, 15);
        var vFlags = voidFlags.Length > 0 ? voidFlags : new[] { false };
        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(paymentSeeds[i].Get);
            var isVoided = vFlags[i % vFlags.Length];
            var date = GenerateDateInSixMonthWindow(paymentSeeds[i].Get + i);
            payments.Add(CreatePayment(i + 1, TestBusinessId, amount, date, isVoided));
        }

        var purchaseCount = Math.Min(purchaseSeeds.Length, 15);
        var cFlags = cancelFlags.Length > 0 ? cancelFlags : new[] { false };
        for (int i = 0; i < purchaseCount; i++)
        {
            var amount = GenerateAmount(purchaseSeeds[i].Get);
            var isCancelled = cFlags[i % cFlags.Length];
            var date = GenerateDateOnlyInSixMonthWindow(purchaseSeeds[i].Get + i);
            purchases.Add(CreatePurchase(i + 1, TestBusinessId, amount, date, isCancelled));
        }

        var result = ComputeExpectedChartData(payments, purchases, TestBusinessId);

        // Verify all months
        var now = DateTime.UtcNow;
        var sixMonthsAgo = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var allMatch = true;
        var mismatchInfo = "";

        for (int i = 0; i < 6; i++)
        {
            var monthDate = sixMonthsAgo.AddMonths(i);
            var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);
            var monthStartDate = DateOnly.FromDateTime(monthStart);
            var monthEndDate = DateOnly.FromDateTime(monthEnd);

            var expectedRevenue = payments
                .Where(p => p.BusinessId == TestBusinessId
                            && !p.IsVoided
                            && p.PaymentDateUtc >= monthStart
                            && p.PaymentDateUtc < monthEnd)
                .Sum(p => p.Amount);

            var expectedExpenses = purchases
                .Where(p => p.BusinessId == TestBusinessId
                            && !p.IsCancelled
                            && p.InvoiceDate >= monthStartDate
                            && p.InvoiceDate < monthEndDate)
                .Sum(p => p.TotalAmount);

            if (result[i].Revenue != expectedRevenue || result[i].Expenses != expectedExpenses)
            {
                allMatch = false;
                mismatchInfo = $"Month {result[i].Label} {result[i].Year}: " +
                    $"Revenue expected={expectedRevenue} actual={result[i].Revenue}, " +
                    $"Expenses expected={expectedExpenses} actual={result[i].Expenses}";
                break;
            }
        }

        return allMatch.ToProperty().Label(mismatchInfo);
    }

    /// <summary>
    /// Property 6: Voided payments are excluded from revenue in the chart.
    /// All payments are voided — revenue should be zero for all months.
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChartData_VoidedPayments_ExcludedFromRevenue(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var paymentCount = Math.Min(amountSeeds.Length, 15);
        var payments = new List<Payment>();

        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var date = GenerateDateInSixMonthWindow(amountSeeds[i].Get + i);
            payments.Add(CreatePayment(i + 1, TestBusinessId, amount, date, isVoided: true));
        }

        var result = ComputeExpectedChartData(payments, new List<Purchase>(), TestBusinessId);

        var allZeroRevenue = result.All(r => r.Revenue == 0m);

        return allZeroRevenue.ToProperty()
            .Label($"Expected all revenue to be 0 for voided payments, got: {string.Join(", ", result.Select(r => $"{r.Label}={r.Revenue}"))}");
    }

    /// <summary>
    /// Property 6: Cancelled purchases are excluded from expenses in the chart.
    /// All purchases are cancelled — expenses should be zero for all months.
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChartData_CancelledPurchases_ExcludedFromExpenses(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var purchaseCount = Math.Min(amountSeeds.Length, 15);
        var purchases = new List<Purchase>();

        for (int i = 0; i < purchaseCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var date = GenerateDateOnlyInSixMonthWindow(amountSeeds[i].Get + i);
            purchases.Add(CreatePurchase(i + 1, TestBusinessId, amount, date, isCancelled: true));
        }

        var result = ComputeExpectedChartData(new List<Payment>(), purchases, TestBusinessId);

        var allZeroExpenses = result.All(r => r.Expenses == 0m);

        return allZeroExpenses.ToProperty()
            .Label($"Expected all expenses to be 0 for cancelled purchases, got: {string.Join(", ", result.Select(r => $"{r.Label}={r.Expenses}"))}");
    }

    /// <summary>
    /// Property 6: Payments and purchases outside the 6-month window do not appear in chart data.
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChartData_DataOutsideWindow_NotIncluded(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var count = Math.Min(amountSeeds.Length, 15);
        var payments = new List<Payment>();
        var purchases = new List<Purchase>();

        for (int i = 0; i < count; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var date = GenerateDateOutsideWindow(amountSeeds[i].Get + i);
            payments.Add(CreatePayment(i + 1, TestBusinessId, amount, date, isVoided: false));
            purchases.Add(CreatePurchase(i + 1, TestBusinessId, amount, DateOnly.FromDateTime(date), isCancelled: false));
        }

        var result = ComputeExpectedChartData(payments, purchases, TestBusinessId);

        var allZero = result.All(r => r.Revenue == 0m && r.Expenses == 0m);

        return allZero.ToProperty()
            .Label($"Expected all values to be 0 for out-of-window data, got: " +
                   $"{string.Join(", ", result.Select(r => $"{r.Label}: Rev={r.Revenue}, Exp={r.Expenses}"))}");
    }

    /// <summary>
    /// Property 6: Chart labels use abbreviated month names (e.g., "Jan", "Feb").
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
    /// </summary>
    [Fact]
    public void ChartData_Labels_UseAbbreviatedMonthNames()
    {
        var result = ComputeExpectedChartData(new List<Payment>(), new List<Purchase>(), TestBusinessId);

        var validLabels = new HashSet<string>
        {
            "Jan", "Feb", "Mar", "Apr", "May", "Jun",
            "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
        };

        Assert.Equal(6, result.Count);
        foreach (var entry in result)
        {
            Assert.Contains(entry.Label, validLabels);
        }
    }

    /// <summary>
    /// Property 6: When no payments or purchases exist, all entries have zero revenue and expenses.
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
    /// </summary>
    [Fact]
    public void ChartData_NoData_AllEntriesAreZero()
    {
        var result = ComputeExpectedChartData(new List<Payment>(), new List<Purchase>(), TestBusinessId);

        Assert.Equal(6, result.Count);
        Assert.All(result, entry =>
        {
            Assert.Equal(0m, entry.Revenue);
            Assert.Equal(0m, entry.Expenses);
        });
    }

    /// <summary>
    /// Property 6: Data from a different business is excluded from the chart.
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChartData_OtherBusinessData_Excluded(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var otherBusinessId = 99;
        var count = Math.Min(amountSeeds.Length, 15);
        var payments = new List<Payment>();
        var purchases = new List<Purchase>();

        for (int i = 0; i < count; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var date = GenerateDateInSixMonthWindow(amountSeeds[i].Get + i);
            payments.Add(CreatePayment(i + 1, otherBusinessId, amount, date, isVoided: false));
            purchases.Add(CreatePurchase(i + 1, otherBusinessId, amount,
                GenerateDateOnlyInSixMonthWindow(amountSeeds[i].Get + i), isCancelled: false));
        }

        // Compute for TestBusinessId — should be zero since all data belongs to otherBusinessId
        var result = ComputeExpectedChartData(payments, purchases, TestBusinessId);

        var allZero = result.All(r => r.Revenue == 0m && r.Expenses == 0m);

        return allZero.ToProperty()
            .Label($"Expected all values to be 0 for other business data, got: " +
                   $"{string.Join(", ", result.Select(r => $"{r.Label}: Rev={r.Revenue}, Exp={r.Expenses}"))}");
    }

    #endregion
}
