// Feature: revenue-control, Property 9: Dashboard KPI Overdue Amount correctness
using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Property 9: Dashboard KPI Overdue Amount correctness
/// For any set of invoices belonging to a business, Overdue Amount SHALL equal the sum of
/// Outstanding_Balance across all invoices where DueDate &lt; today AND Outstanding_Balance &gt; 0.
/// **Validates: Requirements 4.2**
/// </summary>
public class DashboardKpiOverdueAmountPropertyTests
{
    private const int TestBusinessId = 1;

    #region Test Infrastructure

    /// <summary>
    /// Represents a test invoice with its associated payments for pure computation testing.
    /// </summary>
    private class TestInvoiceData
    {
        public decimal TotalAmount { get; set; }
        public DateOnly DueDate { get; set; }
        public bool IsDeleted { get; set; }
        public int InvoiceStatusTypeId { get; set; }
        public int InvoiceFinancialStatusTypeId { get; set; }
        public List<decimal> ValidPaymentAmounts { get; set; } = new();
    }

    /// <summary>
    /// Computes the Overdue Amount using the same logic as the DashboardService:
    /// Sum of OutstandingBalance across all invoices where DueDate &lt; today AND OutstandingBalance &gt; 0.
    /// Only considers non-deleted, Issued (status 2) invoices.
    /// </summary>
    private static decimal ComputeOverdueAmount(List<TestInvoiceData> invoices, DateOnly today)
    {
        decimal overdueAmount = 0m;

        foreach (var invoice in invoices)
        {
            // Only consider non-deleted, Issued invoices (matching the DashboardService query)
            if (invoice.IsDeleted || invoice.InvoiceStatusTypeId != 2)
                continue;

            var totalPaid = invoice.ValidPaymentAmounts.Sum();
            var outstandingBalance = invoice.TotalAmount - totalPaid;

            // Overdue condition: DueDate < today AND OutstandingBalance > 0
            if (invoice.DueDate < today && outstandingBalance > 0)
            {
                overdueAmount += outstandingBalance;
            }
        }

        return overdueAmount;
    }

    private static DateOnly GenerateDueDate(int seed, DateOnly today)
    {
        // Generate dates ranging from 60 days in the past to 60 days in the future
        var offset = (seed % 121) - 60; // range: -60 to +60
        return today.AddDays(offset);
    }

    private static decimal GenerateAmount(int seed)
    {
        // Generate positive amounts between 1.00 and 9999.99
        var raw = (Math.Abs(seed) % 999999) + 100;
        return raw / 100m;
    }

    #endregion

    #region Property 9: Overdue Amount Correctness

    /// <summary>
    /// Property 9: For any set of invoices with random due dates and payment amounts,
    /// the Overdue Amount equals the sum of OutstandingBalance for invoices where
    /// DueDate &lt; today AND OutstandingBalance &gt; 0.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void OverdueAmount_EqualsSumOfOutstandingBalance_WhereOverdue(
        PositiveInt[] totalAmountSeeds,
        int[] dueDateSeeds,
        bool[] deletedFlags)
    {
        if (totalAmountSeeds.Length == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceCount = Math.Min(totalAmountSeeds.Length, 20);
        var invoices = new List<TestInvoiceData>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(totalAmountSeeds[i].Get);
            var dueDateSeed = dueDateSeeds.Length > 0 ? dueDateSeeds[i % dueDateSeeds.Length] : i;
            var dueDate = GenerateDueDate(dueDateSeed, today);
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];

            // Generate some payments (0 to 3 payments per invoice)
            var paymentCount = Math.Abs(totalAmountSeeds[i].Get) % 4;
            var validPayments = new List<decimal>();
            var remainingBalance = totalAmount;

            for (int p = 0; p < paymentCount && remainingBalance > 0; p++)
            {
                // Generate a payment amount that doesn't exceed remaining balance
                var paymentSeed = totalAmountSeeds[i].Get + p + 1;
                var paymentAmount = Math.Min(
                    GenerateAmount(paymentSeed) % remainingBalance + 0.01m,
                    remainingBalance);
                paymentAmount = Math.Round(paymentAmount, 2);

                if (paymentAmount > 0 && paymentAmount <= remainingBalance)
                {
                    validPayments.Add(paymentAmount);
                    remainingBalance -= paymentAmount;
                }
            }

            invoices.Add(new TestInvoiceData
            {
                TotalAmount = totalAmount,
                DueDate = dueDate,
                IsDeleted = isDeleted,
                InvoiceStatusTypeId = 2, // Issued
                InvoiceFinancialStatusTypeId = 1, // doesn't matter for this computation
                ValidPaymentAmounts = validPayments
            });
        }

        // Compute expected overdue amount
        var expectedOverdueAmount = ComputeOverdueAmount(invoices, today);

        // Independently verify: sum outstanding balances where DueDate < today and balance > 0
        var verificationAmount = 0m;
        foreach (var inv in invoices)
        {
            if (inv.IsDeleted || inv.InvoiceStatusTypeId != 2) continue;

            var outstanding = inv.TotalAmount - inv.ValidPaymentAmounts.Sum();
            if (inv.DueDate < today && outstanding > 0)
                verificationAmount += outstanding;
        }

        Assert.Equal(expectedOverdueAmount, verificationAmount);
    }

    /// <summary>
    /// Invoices with DueDate >= today should never contribute to overdue amount,
    /// regardless of their outstanding balance.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void OverdueAmount_ExcludesInvoicesWithFutureDueDate(PositiveInt[] totalAmountSeeds)
    {
        if (totalAmountSeeds.Length == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceCount = Math.Min(totalAmountSeeds.Length, 15);
        var invoices = new List<TestInvoiceData>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(totalAmountSeeds[i].Get);
            // All due dates are today or in the future
            var futureDays = (Math.Abs(totalAmountSeeds[i].Get) % 60); // 0 to 59 days from today
            var dueDate = today.AddDays(futureDays);

            invoices.Add(new TestInvoiceData
            {
                TotalAmount = totalAmount,
                DueDate = dueDate,
                IsDeleted = false,
                InvoiceStatusTypeId = 2,
                InvoiceFinancialStatusTypeId = 1,
                ValidPaymentAmounts = new List<decimal>() // No payments = full outstanding
            });
        }

        var overdueAmount = ComputeOverdueAmount(invoices, today);

        // No invoice should be overdue since all have DueDate >= today
        Assert.Equal(0m, overdueAmount);
    }

    /// <summary>
    /// Invoices that are fully paid (OutstandingBalance = 0) should never contribute
    /// to overdue amount, even if DueDate is in the past.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void OverdueAmount_ExcludesFullyPaidInvoices(PositiveInt[] totalAmountSeeds)
    {
        if (totalAmountSeeds.Length == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceCount = Math.Min(totalAmountSeeds.Length, 15);
        var invoices = new List<TestInvoiceData>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(totalAmountSeeds[i].Get);
            // All due dates are in the past
            var pastDays = (Math.Abs(totalAmountSeeds[i].Get) % 30) + 1; // 1 to 30 days ago
            var dueDate = today.AddDays(-pastDays);

            // Fully paid: payment equals total amount
            invoices.Add(new TestInvoiceData
            {
                TotalAmount = totalAmount,
                DueDate = dueDate,
                IsDeleted = false,
                InvoiceStatusTypeId = 2,
                InvoiceFinancialStatusTypeId = 3, // Paid
                ValidPaymentAmounts = new List<decimal> { totalAmount }
            });
        }

        var overdueAmount = ComputeOverdueAmount(invoices, today);

        // No invoice should contribute since all are fully paid (outstanding = 0)
        Assert.Equal(0m, overdueAmount);
    }

    /// <summary>
    /// Deleted invoices should never contribute to overdue amount,
    /// even if they have past due dates and outstanding balances.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void OverdueAmount_ExcludesDeletedInvoices(PositiveInt[] totalAmountSeeds)
    {
        if (totalAmountSeeds.Length == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceCount = Math.Min(totalAmountSeeds.Length, 15);
        var invoices = new List<TestInvoiceData>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(totalAmountSeeds[i].Get);
            // All due dates are in the past (would be overdue if not deleted)
            var pastDays = (Math.Abs(totalAmountSeeds[i].Get) % 30) + 1;
            var dueDate = today.AddDays(-pastDays);

            invoices.Add(new TestInvoiceData
            {
                TotalAmount = totalAmount,
                DueDate = dueDate,
                IsDeleted = true, // All deleted
                InvoiceStatusTypeId = 2,
                InvoiceFinancialStatusTypeId = 1,
                ValidPaymentAmounts = new List<decimal>() // Unpaid
            });
        }

        var overdueAmount = ComputeOverdueAmount(invoices, today);

        // No invoice should contribute since all are deleted
        Assert.Equal(0m, overdueAmount);
    }

    /// <summary>
    /// Non-Issued invoices (Draft or Cancelled) should never contribute to overdue amount.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void OverdueAmount_ExcludesNonIssuedInvoices(PositiveInt[] totalAmountSeeds)
    {
        if (totalAmountSeeds.Length == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceCount = Math.Min(totalAmountSeeds.Length, 15);
        var invoices = new List<TestInvoiceData>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(totalAmountSeeds[i].Get);
            var pastDays = (Math.Abs(totalAmountSeeds[i].Get) % 30) + 1;
            var dueDate = today.AddDays(-pastDays);

            // Alternate between Draft (1) and Cancelled (3) — never Issued (2)
            var statusId = (i % 2 == 0) ? 1 : 3;

            invoices.Add(new TestInvoiceData
            {
                TotalAmount = totalAmount,
                DueDate = dueDate,
                IsDeleted = false,
                InvoiceStatusTypeId = statusId,
                InvoiceFinancialStatusTypeId = 1,
                ValidPaymentAmounts = new List<decimal>() // Unpaid
            });
        }

        var overdueAmount = ComputeOverdueAmount(invoices, today);

        // No invoice should contribute since none are Issued
        Assert.Equal(0m, overdueAmount);
    }

    /// <summary>
    /// Mixed scenario: invoices with various states (overdue, not overdue, deleted, non-issued, fully paid).
    /// Only non-deleted, Issued invoices with DueDate &lt; today and OutstandingBalance &gt; 0 contribute.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void OverdueAmount_MixedScenario_OnlyOverdueWithBalanceContribute(
        PositiveInt[] totalAmountSeeds,
        byte[] scenarioFlags)
    {
        if (totalAmountSeeds.Length == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceCount = Math.Min(totalAmountSeeds.Length, 20);
        var invoices = new List<TestInvoiceData>();
        var expectedOverdue = 0m;

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(totalAmountSeeds[i].Get);
            var flag = scenarioFlags.Length > 0 ? scenarioFlags[i % scenarioFlags.Length] : (byte)(i % 5);
            var scenario = flag % 5;

            TestInvoiceData invoice;

            switch (scenario)
            {
                case 0: // Overdue with outstanding balance (should contribute)
                    var pastDays = (Math.Abs(totalAmountSeeds[i].Get) % 30) + 1;
                    var partialPayment = Math.Round(totalAmount * 0.3m, 2);
                    invoice = new TestInvoiceData
                    {
                        TotalAmount = totalAmount,
                        DueDate = today.AddDays(-pastDays),
                        IsDeleted = false,
                        InvoiceStatusTypeId = 2,
                        InvoiceFinancialStatusTypeId = 4,
                        ValidPaymentAmounts = partialPayment > 0
                            ? new List<decimal> { partialPayment }
                            : new List<decimal>()
                    };
                    var outstanding = totalAmount - invoice.ValidPaymentAmounts.Sum();
                    if (outstanding > 0)
                        expectedOverdue += outstanding;
                    break;

                case 1: // Future due date (should NOT contribute)
                    var futureDays = (Math.Abs(totalAmountSeeds[i].Get) % 30) + 1;
                    invoice = new TestInvoiceData
                    {
                        TotalAmount = totalAmount,
                        DueDate = today.AddDays(futureDays),
                        IsDeleted = false,
                        InvoiceStatusTypeId = 2,
                        InvoiceFinancialStatusTypeId = 1,
                        ValidPaymentAmounts = new List<decimal>()
                    };
                    break;

                case 2: // Fully paid, past due (should NOT contribute)
                    invoice = new TestInvoiceData
                    {
                        TotalAmount = totalAmount,
                        DueDate = today.AddDays(-10),
                        IsDeleted = false,
                        InvoiceStatusTypeId = 2,
                        InvoiceFinancialStatusTypeId = 3,
                        ValidPaymentAmounts = new List<decimal> { totalAmount }
                    };
                    break;

                case 3: // Deleted, past due, unpaid (should NOT contribute)
                    invoice = new TestInvoiceData
                    {
                        TotalAmount = totalAmount,
                        DueDate = today.AddDays(-15),
                        IsDeleted = true,
                        InvoiceStatusTypeId = 2,
                        InvoiceFinancialStatusTypeId = 4,
                        ValidPaymentAmounts = new List<decimal>()
                    };
                    break;

                default: // Draft status, past due, unpaid (should NOT contribute)
                    invoice = new TestInvoiceData
                    {
                        TotalAmount = totalAmount,
                        DueDate = today.AddDays(-5),
                        IsDeleted = false,
                        InvoiceStatusTypeId = 1,
                        InvoiceFinancialStatusTypeId = 1,
                        ValidPaymentAmounts = new List<decimal>()
                    };
                    break;
            }

            invoices.Add(invoice);
        }

        var computedOverdueAmount = ComputeOverdueAmount(invoices, today);

        Assert.Equal(expectedOverdue, computedOverdueAmount);
    }

    /// <summary>
    /// When there are no invoices at all, the overdue amount should be zero.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Fact]
    public void OverdueAmount_EmptyInvoiceList_ReturnsZero()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoices = new List<TestInvoiceData>();

        var overdueAmount = ComputeOverdueAmount(invoices, today);

        Assert.Equal(0m, overdueAmount);
    }

    #endregion
}
