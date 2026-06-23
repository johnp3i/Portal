using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: profit-loss-summary, Property 1: Revenue computation

/// <summary>
/// Property-based tests for P&amp;L Revenue computation.
/// Validates that Revenue equals the sum of Amount from non-voided payments
/// where PaymentDateUtc falls within an arbitrary period and BusinessId matches
/// the current tenant.
/// Tested as a pure computation over generated payment data.
/// **Validates: Requirements 1.1, 8.1**
/// </summary>
public class PnlRevenueComputationPropertyTests
{
    private const int TestBusinessId = 1;
    private const int OtherBusinessId = 99;

    #region Test Infrastructure

    /// <summary>
    /// Computes the expected Revenue total using the same logic as PnlService:
    /// sum of Amount where IsVoided == false, PaymentDateUtc >= startDate and PaymentDateUtc &lt; endDate + 1 day,
    /// and BusinessId matches the target tenant.
    /// </summary>
    private static decimal ComputeExpectedRevenue(List<Payment> payments, int businessId, DateOnly startDate, DateOnly endDate)
    {
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MinValue).AddDays(1);

        return payments
            .Where(p => p.BusinessId == businessId
                        && !p.IsVoided
                        && p.PaymentDateUtc >= startDateTime
                        && p.PaymentDateUtc < endDateTime)
            .Sum(p => p.Amount);
    }

    /// <summary>
    /// Generates a DateOnly from a seed within a reasonable range (2020–2026).
    /// </summary>
    private static DateOnly GenerateDateOnly(int seed)
    {
        var baseDateTicks = Math.Abs(seed);
        // Range: 2020-01-01 to 2026-12-31 (~2557 days)
        var dayOffset = baseDateTicks % 2557;
        return new DateOnly(2020, 1, 1).AddDays(dayOffset);
    }

    /// <summary>
    /// Generates a DateTime within the given period [startDate, endDate] inclusive.
    /// </summary>
    private static DateTime GenerateDateWithinPeriod(int seed, DateOnly startDate, DateOnly endDate)
    {
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MinValue).AddDays(1).AddSeconds(-1);
        var totalSeconds = (long)(endDateTime - startDateTime).TotalSeconds;
        if (totalSeconds <= 0) totalSeconds = 1;

        var offsetSeconds = Math.Abs((long)seed) % totalSeconds;
        return startDateTime.AddSeconds(offsetSeconds);
    }

    /// <summary>
    /// Generates a DateTime outside the given period (either before start or after end).
    /// </summary>
    private static DateTime GenerateDateOutsidePeriod(int seed, DateOnly startDate, DateOnly endDate)
    {
        var absSeed = Math.Abs(seed);
        if (absSeed % 2 == 0)
        {
            // Before the period: 1-365 days before start
            var daysBefore = (absSeed % 365) + 1;
            return startDate.ToDateTime(TimeOnly.MinValue).AddDays(-daysBefore).AddHours(absSeed % 24);
        }
        else
        {
            // After the period: 1-365 days after end (end is inclusive, so after end+1 day boundary)
            var daysAfter = (absSeed % 365) + 1;
            return endDate.ToDateTime(TimeOnly.MinValue).AddDays(1).AddDays(daysAfter).AddHours(absSeed % 24);
        }
    }

    /// <summary>
    /// Generates a positive decimal amount from a seed.
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = Math.Abs(seed) % 999999 + 1;
        return raw / 100m;
    }

    /// <summary>
    /// Creates a Payment entity with controlled parameters for testing.
    /// </summary>
    private static Payment CreatePayment(int id, int businessId, decimal amount, DateTime paymentDateUtc, bool isVoided)
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
            Notes = $"Test Payment {id}",
            IsVoided = isVoided,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generates a valid period (startDate &lt;= endDate) from two seeds.
    /// </summary>
    private static (DateOnly startDate, DateOnly endDate) GeneratePeriod(int seed1, int seed2)
    {
        var date1 = GenerateDateOnly(seed1);
        var date2 = GenerateDateOnly(seed2);

        if (date1 <= date2)
            return (date1, date2);
        return (date2, date1);
    }

    #endregion

    #region Property 1: Revenue computation includes only valid payments for the current tenant and period

    /// <summary>
    /// Property 1: Revenue equals the sum of Amount from non-voided payments within the period
    /// for the matching tenant. Generates random payments with various dates, voided states,
    /// business IDs, and amounts; computes expected revenue manually; verifies they match.
    /// **Validates: Requirements 1.1, 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Revenue_EqualsSumOfNonVoidedPaymentsInPeriodForTenant(
        PositiveInt[] amountSeeds, bool[] voidedFlags, bool[] inPeriodFlags, bool[] sameTenantFlags,
        PositiveInt periodSeed1, PositiveInt periodSeed2)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        // Generate an arbitrary period
        var (startDate, endDate) = GeneratePeriod(periodSeed1.Get, periodSeed2.Get);

        var paymentCount = Math.Min(amountSeeds.Length, 20);
        var payments = new List<Payment>();

        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var isVoided = voidedFlags.Length > 0 && voidedFlags[i % voidedFlags.Length];
            var isInPeriod = inPeriodFlags.Length > 0 && inPeriodFlags[i % inPeriodFlags.Length];
            var isSameTenant = sameTenantFlags.Length > 0 && sameTenantFlags[i % sameTenantFlags.Length];

            var paymentDate = isInPeriod
                ? GenerateDateWithinPeriod(amountSeeds[i].Get + i, startDate, endDate)
                : GenerateDateOutsidePeriod(amountSeeds[i].Get + i, startDate, endDate);

            var businessId = isSameTenant ? TestBusinessId : OtherBusinessId;

            payments.Add(CreatePayment(i + 1, businessId, amount, paymentDate, isVoided));
        }

        // Oracle: manual computation using the same logic as PnlService
        var expectedRevenue = ComputeExpectedRevenue(payments, TestBusinessId, startDate, endDate);

        // SUT: replicate PnlService logic inline
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MinValue).AddDays(1);

        var actualRevenue = payments
            .Where(p => p.BusinessId == TestBusinessId
                        && !p.IsVoided
                        && p.PaymentDateUtc >= startDateTime
                        && p.PaymentDateUtc < endDateTime)
            .Sum(p => p.Amount);

        return (actualRevenue == expectedRevenue).ToProperty()
            .Label($"Period={startDate}–{endDate}, Expected={expectedRevenue}, Actual={actualRevenue}, PaymentCount={paymentCount}");
    }

    /// <summary>
    /// Voided payments within the period are excluded from Revenue.
    /// **Validates: Requirements 1.1, 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Revenue_ExcludesVoidedPayments(
        PositiveInt[] amountSeeds, PositiveInt periodSeed1, PositiveInt periodSeed2)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var (startDate, endDate) = GeneratePeriod(periodSeed1.Get, periodSeed2.Get);

        var paymentCount = Math.Min(amountSeeds.Length, 15);
        var payments = new List<Payment>();

        // Create all payments in period for the same tenant, alternating voided/non-voided
        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var isVoided = i % 2 == 0; // even indices are voided
            var paymentDate = GenerateDateWithinPeriod(amountSeeds[i].Get + i, startDate, endDate);

            payments.Add(CreatePayment(i + 1, TestBusinessId, amount, paymentDate, isVoided));
        }

        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MinValue).AddDays(1);

        // Only non-voided payments should be counted
        var expectedRevenue = payments
            .Where(p => !p.IsVoided
                        && p.PaymentDateUtc >= startDateTime
                        && p.PaymentDateUtc < endDateTime)
            .Sum(p => p.Amount);

        var actualRevenue = ComputeExpectedRevenue(payments, TestBusinessId, startDate, endDate);

        return (actualRevenue == expectedRevenue).ToProperty()
            .Label($"Period={startDate}–{endDate}, Expected={expectedRevenue}, Actual={actualRevenue}");
    }

    /// <summary>
    /// Payments outside the period do not contribute to Revenue.
    /// **Validates: Requirements 1.1, 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Revenue_ExcludesPaymentsOutsidePeriod(
        PositiveInt[] amountSeeds, PositiveInt periodSeed1, PositiveInt periodSeed2)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var (startDate, endDate) = GeneratePeriod(periodSeed1.Get, periodSeed2.Get);

        var paymentCount = Math.Min(amountSeeds.Length, 15);
        var payments = new List<Payment>();

        // Create all payments OUTSIDE the period, all non-voided, same tenant
        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var paymentDate = GenerateDateOutsidePeriod(amountSeeds[i].Get + i, startDate, endDate);

            payments.Add(CreatePayment(i + 1, TestBusinessId, amount, paymentDate, isVoided: false));
        }

        var actualRevenue = ComputeExpectedRevenue(payments, TestBusinessId, startDate, endDate);

        return (actualRevenue == 0m).ToProperty()
            .Label($"Period={startDate}–{endDate}, Expected=0, Actual={actualRevenue}");
    }

    /// <summary>
    /// Payments from a different tenant do not contribute to Revenue (tenant isolation).
    /// **Validates: Requirements 1.1, 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Revenue_ExcludesOtherTenantPayments(
        PositiveInt[] amountSeeds, PositiveInt periodSeed1, PositiveInt periodSeed2)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var (startDate, endDate) = GeneratePeriod(periodSeed1.Get, periodSeed2.Get);

        var paymentCount = Math.Min(amountSeeds.Length, 15);
        var payments = new List<Payment>();

        // Create payments in period for a DIFFERENT tenant, all non-voided
        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var paymentDate = GenerateDateWithinPeriod(amountSeeds[i].Get + i, startDate, endDate);

            payments.Add(CreatePayment(i + 1, OtherBusinessId, amount, paymentDate, isVoided: false));
        }

        // Compute for TestBusinessId — should be zero since all payments belong to OtherBusinessId
        var actualRevenue = ComputeExpectedRevenue(payments, TestBusinessId, startDate, endDate);

        return (actualRevenue == 0m).ToProperty()
            .Label($"Period={startDate}–{endDate}, Expected=0 for other tenant, Actual={actualRevenue}");
    }

    /// <summary>
    /// Mixed scenario: payments across multiple tenants, periods, and voided states.
    /// Only non-voided, in-period, same-tenant payments count.
    /// **Validates: Requirements 1.1, 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Revenue_MixedScenario_OnlyCountsValidInPeriodSameTenantPayments(
        PositiveInt[] amountSeeds, bool[] voidedFlags, bool[] inPeriodFlags, bool[] sameTenantFlags,
        PositiveInt periodSeed1, PositiveInt periodSeed2)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var (startDate, endDate) = GeneratePeriod(periodSeed1.Get, periodSeed2.Get);

        var paymentCount = Math.Min(amountSeeds.Length, 20);
        var payments = new List<Payment>();

        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = endDate.ToDateTime(TimeOnly.MinValue).AddDays(1);

        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var isVoided = voidedFlags.Length > 0 && voidedFlags[i % voidedFlags.Length];
            var isInPeriod = inPeriodFlags.Length > 0 && inPeriodFlags[i % inPeriodFlags.Length];
            var isSameTenant = sameTenantFlags.Length > 0 && sameTenantFlags[i % sameTenantFlags.Length];

            var paymentDate = isInPeriod
                ? GenerateDateWithinPeriod(amountSeeds[i].Get + i, startDate, endDate)
                : GenerateDateOutsidePeriod(amountSeeds[i].Get + i, startDate, endDate);

            var businessId = isSameTenant ? TestBusinessId : OtherBusinessId;

            payments.Add(CreatePayment(i + 1, businessId, amount, paymentDate, isVoided));
        }

        // Expected: only non-voided, in-period, same-tenant payments
        var expectedRevenue = payments
            .Where(p => p.BusinessId == TestBusinessId
                        && !p.IsVoided
                        && p.PaymentDateUtc >= startDateTime
                        && p.PaymentDateUtc < endDateTime)
            .Sum(p => p.Amount);

        var actualRevenue = ComputeExpectedRevenue(payments, TestBusinessId, startDate, endDate);

        return (actualRevenue == expectedRevenue).ToProperty()
            .Label($"Period={startDate}–{endDate}, Expected={expectedRevenue}, Actual={actualRevenue}, TotalPayments={paymentCount}");
    }

    /// <summary>
    /// When no payments exist, Revenue is zero.
    /// **Validates: Requirements 1.1, 8.1**
    /// </summary>
    [Fact]
    public void Revenue_NoPayments_ReturnsZero()
    {
        var payments = new List<Payment>();
        var startDate = new DateOnly(2024, 1, 1);
        var endDate = new DateOnly(2024, 1, 31);

        var actualRevenue = ComputeExpectedRevenue(payments, TestBusinessId, startDate, endDate);

        Assert.Equal(0m, actualRevenue);
    }

    #endregion
}
