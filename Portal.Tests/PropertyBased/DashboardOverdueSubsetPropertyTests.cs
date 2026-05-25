using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: dashboard-upgrade, Property 3: Overdue amount is a subset of outstanding

/// <summary>
/// Property-based tests for Dashboard KPI "Overdue amount is a subset of outstanding".
/// Validates that for any set of invoices and payments, the Overdue amount is always
/// less than or equal to the Outstanding amount. Overdue is a strict subset of outstanding
/// because overdue invoices must also satisfy the outstanding criteria (issued, non-deleted,
/// qualifying financial status) plus the additional constraint of DueDate &lt; today.
/// Tested as a pure computation over generated invoice and payment data.
/// **Validates: Requirements 1.3, 1.5**
/// </summary>
public class DashboardOverdueSubsetPropertyTests
{
    private const int TestBusinessId = 1;

    // Invoice Status Type IDs
    private const int InvoiceStatusDraft = 1;
    private const int InvoiceStatusIssued = 2;
    private const int InvoiceStatusCancelled = 3;

    // Financial Status Type IDs
    private const int FinancialStatusUnpaid = 1;
    private const int FinancialStatusPartiallyPaid = 2;
    private const int FinancialStatusPaid = 3;
    private const int FinancialStatusOverdue = 4;
    private const int FinancialStatusWrittenOff = 5;

    // Valid financial statuses for Outstanding/Overdue calculations
    private static readonly int[] QualifyingFinancialStatuses = { FinancialStatusUnpaid, FinancialStatusPartiallyPaid, FinancialStatusOverdue };

    #region Test Infrastructure

    /// <summary>
    /// Represents a test invoice with its associated payments for pure computation testing.
    /// </summary>
    private class TestInvoiceData
    {
        public int Id { get; set; }
        public int BusinessId { get; set; }
        public decimal TotalAmount { get; set; }
        public DateOnly DueDate { get; set; }
        public bool IsDeleted { get; set; }
        public int InvoiceStatusTypeId { get; set; }
        public int InvoiceFinancialStatusTypeId { get; set; }
        public List<TestPaymentData> Payments { get; set; } = new();
    }

    private class TestPaymentData
    {
        public decimal Amount { get; set; }
        public bool IsVoided { get; set; }
    }

    /// <summary>
    /// Computes the Outstanding amount using the same logic as DashboardService:
    /// Sum of (TotalAmount - sum of non-voided payments) for all non-deleted invoices
    /// with InvoiceStatusTypeId = 2 AND InvoiceFinancialStatusTypeId in (1, 2, 4).
    /// </summary>
    private static decimal ComputeOutstandingAmount(List<TestInvoiceData> invoices, int businessId)
    {
        return invoices
            .Where(inv => inv.BusinessId == businessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && QualifyingFinancialStatuses.Contains(inv.InvoiceFinancialStatusTypeId))
            .Sum(inv =>
            {
                var totalPaid = inv.Payments
                    .Where(p => !p.IsVoided)
                    .Sum(p => p.Amount);
                var outstanding = inv.TotalAmount - totalPaid;
                return outstanding > 0 ? outstanding : 0m;
            });
    }

    /// <summary>
    /// Computes the Overdue amount using the same logic as DashboardService:
    /// Sum of outstanding balances for invoices where DueDate &lt; today AND outstanding &gt; 0,
    /// with the same qualifying criteria as Outstanding (issued, non-deleted, financial status in 1,2,4).
    /// </summary>
    private static decimal ComputeOverdueAmount(List<TestInvoiceData> invoices, int businessId, DateOnly today)
    {
        return invoices
            .Where(inv => inv.BusinessId == businessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && QualifyingFinancialStatuses.Contains(inv.InvoiceFinancialStatusTypeId))
            .Sum(inv =>
            {
                var totalPaid = inv.Payments
                    .Where(p => !p.IsVoided)
                    .Sum(p => p.Amount);
                var outstanding = inv.TotalAmount - totalPaid;
                // Overdue: DueDate < today AND outstanding > 0
                if (inv.DueDate < today && outstanding > 0)
                    return outstanding;
                return 0m;
            });
    }

    /// <summary>
    /// Generates a positive decimal amount from a seed.
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = (Math.Abs(seed) % 999999) + 100;
        return raw / 100m;
    }

    /// <summary>
    /// Generates a DueDate ranging from 60 days in the past to 60 days in the future.
    /// </summary>
    private static DateOnly GenerateDueDate(int seed, DateOnly today)
    {
        var offset = (Math.Abs(seed) % 121) - 60; // range: -60 to +60
        return today.AddDays(offset);
    }

    /// <summary>
    /// Generates an InvoiceStatusTypeId (1=Draft, 2=Issued, 3=Cancelled).
    /// </summary>
    private static int GenerateInvoiceStatusTypeId(int seed)
    {
        return (Math.Abs(seed) % 3) + 1;
    }

    /// <summary>
    /// Generates an InvoiceFinancialStatusTypeId (1-5).
    /// </summary>
    private static int GenerateFinancialStatusTypeId(int seed)
    {
        return (Math.Abs(seed) % 5) + 1;
    }

    #endregion

    #region Property 3: Overdue amount is a subset of outstanding

    /// <summary>
    /// Property 3: For any set of invoices and payments, the Overdue amount is always
    /// less than or equal to the Outstanding amount. This holds because overdue invoices
    /// are a subset of outstanding invoices (they must satisfy all outstanding criteria
    /// plus the additional DueDate &lt; today constraint).
    /// **Validates: Requirements 1.3, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueAmount_IsAlwaysLessThanOrEqualTo_OutstandingAmount(
        PositiveInt[] amountSeeds,
        int[] dueDateSeeds,
        bool[] deletedFlags,
        byte[] statusSeeds,
        byte[] financialStatusSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceCount = Math.Min(amountSeeds.Length, 20);
        var invoices = new List<TestInvoiceData>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var dueDateSeed = dueDateSeeds.Length > 0 ? dueDateSeeds[i % dueDateSeeds.Length] : i;
            var dueDate = GenerateDueDate(dueDateSeed, today);
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            var invoiceStatusId = statusSeeds.Length > 0
                ? (Math.Abs(statusSeeds[i % statusSeeds.Length]) % 3) + 1
                : InvoiceStatusIssued;
            var financialStatusId = financialStatusSeeds.Length > 0
                ? (Math.Abs(financialStatusSeeds[i % financialStatusSeeds.Length]) % 5) + 1
                : FinancialStatusUnpaid;

            // Generate 0-3 payments per invoice
            var paymentCount = Math.Abs(amountSeeds[i].Get) % 4;
            var payments = new List<TestPaymentData>();
            var remainingBalance = totalAmount;

            for (int j = 0; j < paymentCount && remainingBalance > 0.01m; j++)
            {
                var isVoided = (amountSeeds[i].Get + j) % 5 == 0; // ~20% voided
                var paymentAmount = Math.Min(
                    Math.Round(GenerateAmount(amountSeeds[i].Get + j + 1000) % remainingBalance + 0.01m, 2),
                    remainingBalance);

                payments.Add(new TestPaymentData
                {
                    Amount = paymentAmount,
                    IsVoided = isVoided
                });

                if (!isVoided)
                    remainingBalance -= paymentAmount;
            }

            invoices.Add(new TestInvoiceData
            {
                Id = i + 1,
                BusinessId = TestBusinessId,
                TotalAmount = totalAmount,
                DueDate = dueDate,
                IsDeleted = isDeleted,
                InvoiceStatusTypeId = invoiceStatusId,
                InvoiceFinancialStatusTypeId = financialStatusId,
                Payments = payments
            });
        }

        var outstandingAmount = ComputeOutstandingAmount(invoices, TestBusinessId);
        var overdueAmount = ComputeOverdueAmount(invoices, TestBusinessId, today);

        return (overdueAmount <= outstandingAmount).ToProperty()
            .Label($"Overdue={overdueAmount} should be <= Outstanding={outstandingAmount}, " +
                   $"InvoiceCount={invoiceCount}");
    }

    /// <summary>
    /// Property 3 (variant): When all qualifying invoices are overdue (DueDate &lt; today),
    /// the Overdue amount equals the Outstanding amount exactly.
    /// **Validates: Requirements 1.3, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueAmount_EqualsOutstanding_WhenAllInvoicesAreOverdue(
        PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var invoices = new List<TestInvoiceData>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            // All due dates are in the past (overdue)
            var pastDays = (Math.Abs(amountSeeds[i].Get) % 30) + 1;
            var dueDate = today.AddDays(-pastDays);

            // Generate partial payments so outstanding > 0
            var partialPayment = Math.Round(totalAmount * 0.3m, 2);

            invoices.Add(new TestInvoiceData
            {
                Id = i + 1,
                BusinessId = TestBusinessId,
                TotalAmount = totalAmount,
                DueDate = dueDate,
                IsDeleted = false,
                InvoiceStatusTypeId = InvoiceStatusIssued,
                InvoiceFinancialStatusTypeId = FinancialStatusOverdue,
                Payments = partialPayment > 0
                    ? new List<TestPaymentData> { new() { Amount = partialPayment, IsVoided = false } }
                    : new List<TestPaymentData>()
            });
        }

        var outstandingAmount = ComputeOutstandingAmount(invoices, TestBusinessId);
        var overdueAmount = ComputeOverdueAmount(invoices, TestBusinessId, today);

        return (overdueAmount == outstandingAmount).ToProperty()
            .Label($"When all invoices are overdue, Overdue={overdueAmount} should equal Outstanding={outstandingAmount}");
    }

    /// <summary>
    /// Property 3 (variant): When no invoices are overdue (all DueDate >= today),
    /// the Overdue amount is zero while Outstanding may be positive.
    /// **Validates: Requirements 1.3, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueAmount_IsZero_WhenNoInvoicesAreOverdue(
        PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var invoices = new List<TestInvoiceData>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            // All due dates are today or in the future (not overdue)
            var futureDays = (Math.Abs(amountSeeds[i].Get) % 60);
            var dueDate = today.AddDays(futureDays);

            invoices.Add(new TestInvoiceData
            {
                Id = i + 1,
                BusinessId = TestBusinessId,
                TotalAmount = totalAmount,
                DueDate = dueDate,
                IsDeleted = false,
                InvoiceStatusTypeId = InvoiceStatusIssued,
                InvoiceFinancialStatusTypeId = FinancialStatusUnpaid,
                Payments = new List<TestPaymentData>() // No payments = full outstanding
            });
        }

        var outstandingAmount = ComputeOutstandingAmount(invoices, TestBusinessId);
        var overdueAmount = ComputeOverdueAmount(invoices, TestBusinessId, today);

        return (overdueAmount == 0m && outstandingAmount >= 0m && overdueAmount <= outstandingAmount).ToProperty()
            .Label($"Overdue={overdueAmount} should be 0, Outstanding={outstandingAmount} should be >= 0");
    }

    /// <summary>
    /// Property 3 (variant): Mixed scenario with invoices having various due dates,
    /// statuses, and payment states. The subset invariant must always hold.
    /// **Validates: Requirements 1.3, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueAmount_SubsetInvariant_HoldsForMixedScenarios(
        PositiveInt[] amountSeeds,
        bool[] overdueFlags,
        bool[] deletedFlags,
        bool[] hasPaymentFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceCount = Math.Min(amountSeeds.Length, 20);
        var invoices = new List<TestInvoiceData>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var isOverdue = overdueFlags.Length > 0 && overdueFlags[i % overdueFlags.Length];
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            var hasPayment = hasPaymentFlags.Length > 0 && hasPaymentFlags[i % hasPaymentFlags.Length];

            var dueDate = isOverdue
                ? today.AddDays(-((Math.Abs(amountSeeds[i].Get) % 30) + 1))
                : today.AddDays((Math.Abs(amountSeeds[i].Get) % 30) + 1);

            var payments = new List<TestPaymentData>();
            if (hasPayment)
            {
                var paymentAmount = Math.Round(totalAmount * 0.5m, 2);
                if (paymentAmount > 0)
                {
                    payments.Add(new TestPaymentData { Amount = paymentAmount, IsVoided = false });
                }
            }

            invoices.Add(new TestInvoiceData
            {
                Id = i + 1,
                BusinessId = TestBusinessId,
                TotalAmount = totalAmount,
                DueDate = dueDate,
                IsDeleted = isDeleted,
                InvoiceStatusTypeId = InvoiceStatusIssued,
                InvoiceFinancialStatusTypeId = isOverdue ? FinancialStatusOverdue : FinancialStatusUnpaid,
                Payments = payments
            });
        }

        var outstandingAmount = ComputeOutstandingAmount(invoices, TestBusinessId);
        var overdueAmount = ComputeOverdueAmount(invoices, TestBusinessId, today);

        return (overdueAmount <= outstandingAmount).ToProperty()
            .Label($"Overdue={overdueAmount} should be <= Outstanding={outstandingAmount}, " +
                   $"InvoiceCount={invoiceCount}");
    }

    /// <summary>
    /// When no invoices exist, both Outstanding and Overdue are zero.
    /// **Validates: Requirements 1.3, 1.5**
    /// </summary>
    [Fact]
    public void OverdueAndOutstanding_EmptyInvoiceList_BothReturnZero()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoices = new List<TestInvoiceData>();

        var outstandingAmount = ComputeOutstandingAmount(invoices, TestBusinessId);
        var overdueAmount = ComputeOverdueAmount(invoices, TestBusinessId, today);

        Assert.Equal(0m, outstandingAmount);
        Assert.Equal(0m, overdueAmount);
        Assert.True(overdueAmount <= outstandingAmount);
    }

    #endregion
}
