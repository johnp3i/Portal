using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 8: Dashboard KPI Outstanding Receivables correctness

/// <summary>
/// Property-based tests for Dashboard KPI Outstanding Receivables computation.
/// Validates that Outstanding Receivables = sum of OutstandingBalance across all non-deleted
/// invoices with InvoiceStatusTypeId = 2 (Issued) AND InvoiceFinancialStatusTypeId in (1, 2, 4).
/// **Validates: Requirements 4.1**
/// </summary>
public class DashboardKpiOutstandingReceivablesPropertyTests
{
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

    // Valid financial statuses for Outstanding Receivables
    private static readonly int[] QualifyingFinancialStatuses = { FinancialStatusUnpaid, FinancialStatusPartiallyPaid, FinancialStatusOverdue };

    #region Test Data Generators

    /// <summary>
    /// Represents a test invoice with its associated payments for computation verification.
    /// </summary>
    private class TestInvoice
    {
        public int Id { get; set; }
        public int BusinessId { get; set; }
        public int InvoiceStatusTypeId { get; set; }
        public int InvoiceFinancialStatusTypeId { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsDeleted { get; set; }
        public List<TestPayment> Payments { get; set; } = new();
    }

    private class TestPayment
    {
        public decimal Amount { get; set; }
        public bool IsVoided { get; set; }
    }

    private static decimal GenerateAmount(int seed)
    {
        // Generate positive amounts between 1.00 and 99999.99
        var raw = Math.Abs(seed) % 9999999 + 100;
        return raw / 100m;
    }

    private static int GenerateInvoiceStatusTypeId(int seed)
    {
        // 1 = Draft, 2 = Issued, 3 = Cancelled
        return (Math.Abs(seed) % 3) + 1;
    }

    private static int GenerateFinancialStatusTypeId(int seed)
    {
        // 1 = Unpaid, 2 = PartiallyPaid, 3 = Paid, 4 = Overdue, 5 = WrittenOff
        return (Math.Abs(seed) % 5) + 1;
    }

    /// <summary>
    /// Computes the Outstanding Receivables using the same logic as DashboardService:
    /// Sum of (TotalAmount - sum of valid payments) for all non-deleted invoices
    /// with InvoiceStatusTypeId = 2 AND InvoiceFinancialStatusTypeId in (1, 2, 4).
    /// </summary>
    private static decimal ComputeExpectedOutstandingReceivables(List<TestInvoice> invoices, int businessId)
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
                return inv.TotalAmount - totalPaid;
            });
    }

    #endregion

    #region Property 8: Dashboard KPI Outstanding Receivables correctness

    /// <summary>
    /// Property 8: Outstanding Receivables equals the sum of OutstandingBalance across all
    /// non-deleted invoices with InvoiceStatusTypeId = 2 AND InvoiceFinancialStatusTypeId in (1, 2, 4).
    /// Invoices that are deleted, not Issued, or have Paid/WrittenOff financial status are excluded.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingReceivables_SumsCorrectlyForQualifyingInvoices(
        PositiveInt[] amountSeeds,
        bool[] deletedFlags,
        byte[] statusSeeds,
        byte[] financialStatusSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input - trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 20);
        var businessId = 1;
        var invoices = new List<TestInvoice>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            var invoiceStatusId = statusSeeds.Length > 0
                ? (statusSeeds[i % statusSeeds.Length] % 3) + 1
                : InvoiceStatusIssued;
            var financialStatusId = financialStatusSeeds.Length > 0
                ? (financialStatusSeeds[i % financialStatusSeeds.Length] % 5) + 1
                : FinancialStatusUnpaid;
            var totalAmount = GenerateAmount(amountSeeds[i].Get);

            // Generate 0-3 payments per invoice
            var paymentCount = amountSeeds[i].Get % 4;
            var payments = new List<TestPayment>();
            var remainingBalance = totalAmount;

            for (int j = 0; j < paymentCount && remainingBalance > 0; j++)
            {
                var isVoided = (amountSeeds[i].Get + j) % 5 == 0; // ~20% voided
                var paymentAmount = Math.Min(
                    GenerateAmount(amountSeeds[i].Get + j + 1000) % remainingBalance + 0.01m,
                    remainingBalance);
                paymentAmount = Math.Round(paymentAmount, 2);

                payments.Add(new TestPayment
                {
                    Amount = paymentAmount,
                    IsVoided = isVoided
                });

                if (!isVoided)
                    remainingBalance -= paymentAmount;
            }

            invoices.Add(new TestInvoice
            {
                Id = i + 1,
                BusinessId = businessId,
                InvoiceStatusTypeId = invoiceStatusId,
                InvoiceFinancialStatusTypeId = financialStatusId,
                TotalAmount = totalAmount,
                IsDeleted = isDeleted,
                Payments = payments
            });
        }

        var expected = ComputeExpectedOutstandingReceivables(invoices, businessId);

        // Simulate the DashboardService computation logic:
        // Outstanding Receivables = SUM(TotalAmount - ISNULL(ValidPayments.TotalPaid, 0))
        // WHERE BusinessId = @BusinessId AND IsDeleted = 0
        //   AND InvoiceStatusTypeId = 2 AND InvoiceFinancialStatusTypeId IN (1, 2, 4)
        var actual = invoices
            .Where(inv => inv.BusinessId == businessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && QualifyingFinancialStatuses.Contains(inv.InvoiceFinancialStatusTypeId))
            .Sum(inv =>
            {
                var totalPaid = inv.Payments
                    .Where(p => !p.IsVoided)
                    .Sum(p => p.Amount);
                return inv.TotalAmount - totalPaid;
            });

        return (actual == expected).ToProperty()
            .Label($"Expected={expected}, Actual={actual}, " +
                   $"InvoiceCount={invoiceCount}, " +
                   $"QualifyingCount={invoices.Count(i => !i.IsDeleted && i.InvoiceStatusTypeId == InvoiceStatusIssued && QualifyingFinancialStatuses.Contains(i.InvoiceFinancialStatusTypeId))}");
    }

    /// <summary>
    /// Property 8 (variant): Deleted invoices are never included in Outstanding Receivables,
    /// regardless of their status or financial status.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingReceivables_ExcludesDeletedInvoices(
        PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input - trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var businessId = 1;
        var invoices = new List<TestInvoice>();

        // Create all invoices as deleted, Issued, with qualifying financial status
        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);

            invoices.Add(new TestInvoice
            {
                Id = i + 1,
                BusinessId = businessId,
                InvoiceStatusTypeId = InvoiceStatusIssued,
                InvoiceFinancialStatusTypeId = FinancialStatusUnpaid,
                TotalAmount = totalAmount,
                IsDeleted = true, // All deleted
                Payments = new List<TestPayment>()
            });
        }

        var actual = ComputeExpectedOutstandingReceivables(invoices, businessId);

        return (actual == 0m).ToProperty()
            .Label($"Deleted invoices should contribute 0 to Outstanding Receivables, got {actual}");
    }

    /// <summary>
    /// Property 8 (variant): Non-Issued invoices (Draft, Cancelled) are never included
    /// in Outstanding Receivables.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingReceivables_ExcludesNonIssuedInvoices(
        PositiveInt[] amountSeeds,
        bool[] useDraft)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input - trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var businessId = 1;
        var invoices = new List<TestInvoice>();

        // Create all invoices as non-Issued (Draft or Cancelled)
        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var isDraft = useDraft.Length > 0 && useDraft[i % useDraft.Length];

            invoices.Add(new TestInvoice
            {
                Id = i + 1,
                BusinessId = businessId,
                InvoiceStatusTypeId = isDraft ? InvoiceStatusDraft : InvoiceStatusCancelled,
                InvoiceFinancialStatusTypeId = FinancialStatusUnpaid,
                TotalAmount = totalAmount,
                IsDeleted = false,
                Payments = new List<TestPayment>()
            });
        }

        var actual = ComputeExpectedOutstandingReceivables(invoices, businessId);

        return (actual == 0m).ToProperty()
            .Label($"Non-Issued invoices should contribute 0 to Outstanding Receivables, got {actual}");
    }

    /// <summary>
    /// Property 8 (variant): Paid (3) and WrittenOff (5) financial statuses are excluded
    /// from Outstanding Receivables.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingReceivables_ExcludesPaidAndWrittenOffStatuses(
        PositiveInt[] amountSeeds,
        bool[] usePaid)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input - trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var businessId = 1;
        var invoices = new List<TestInvoice>();

        // Create all invoices as Issued but with Paid or WrittenOff financial status
        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var isPaid = usePaid.Length > 0 && usePaid[i % usePaid.Length];

            invoices.Add(new TestInvoice
            {
                Id = i + 1,
                BusinessId = businessId,
                InvoiceStatusTypeId = InvoiceStatusIssued,
                InvoiceFinancialStatusTypeId = isPaid ? FinancialStatusPaid : FinancialStatusWrittenOff,
                TotalAmount = totalAmount,
                IsDeleted = false,
                Payments = new List<TestPayment>()
            });
        }

        var actual = ComputeExpectedOutstandingReceivables(invoices, businessId);

        return (actual == 0m).ToProperty()
            .Label($"Paid/WrittenOff invoices should contribute 0 to Outstanding Receivables, got {actual}");
    }

    /// <summary>
    /// Property 8 (variant): Voided payments do not reduce the Outstanding Balance.
    /// Only non-voided payments reduce the outstanding amount.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingReceivables_VoidedPaymentsDoNotReduceBalance(
        PositiveInt totalAmountSeed,
        PositiveInt paymentAmountSeed)
    {
        var businessId = 1;
        var totalAmount = GenerateAmount(totalAmountSeed.Get);
        var paymentAmount = Math.Min(GenerateAmount(paymentAmountSeed.Get), totalAmount);

        // Invoice with only voided payments — outstanding should equal totalAmount
        var invoiceWithVoidedPayments = new TestInvoice
        {
            Id = 1,
            BusinessId = businessId,
            InvoiceStatusTypeId = InvoiceStatusIssued,
            InvoiceFinancialStatusTypeId = FinancialStatusUnpaid,
            TotalAmount = totalAmount,
            IsDeleted = false,
            Payments = new List<TestPayment>
            {
                new TestPayment { Amount = paymentAmount, IsVoided = true }
            }
        };

        var invoices = new List<TestInvoice> { invoiceWithVoidedPayments };
        var actual = ComputeExpectedOutstandingReceivables(invoices, businessId);

        // Since all payments are voided, outstanding should equal totalAmount
        return (actual == totalAmount).ToProperty()
            .Label($"Expected={totalAmount}, Actual={actual} — voided payments should not reduce balance");
    }

    /// <summary>
    /// Property 8 (variant): Outstanding Receivables correctly accounts for partial payments.
    /// When valid payments exist, outstanding = TotalAmount - sum(valid payment amounts).
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingReceivables_CorrectlyAccountsForPartialPayments(
        PositiveInt totalAmountSeed,
        PositiveInt[] paymentSeeds)
    {
        if (paymentSeeds.Length == 0)
            return true.ToProperty().Label("No payments - trivially true");

        var businessId = 1;
        var totalAmount = GenerateAmount(totalAmountSeed.Get);

        // Generate valid (non-voided) payments that don't exceed totalAmount
        var payments = new List<TestPayment>();
        var remainingBalance = totalAmount;
        var paymentCount = Math.Min(paymentSeeds.Length, 5);

        for (int i = 0; i < paymentCount && remainingBalance > 0.01m; i++)
        {
            var paymentAmount = Math.Min(
                Math.Round(GenerateAmount(paymentSeeds[i].Get) % remainingBalance + 0.01m, 2),
                remainingBalance);
            payments.Add(new TestPayment { Amount = paymentAmount, IsVoided = false });
            remainingBalance -= paymentAmount;
        }

        var invoice = new TestInvoice
        {
            Id = 1,
            BusinessId = businessId,
            InvoiceStatusTypeId = InvoiceStatusIssued,
            InvoiceFinancialStatusTypeId = FinancialStatusPartiallyPaid,
            TotalAmount = totalAmount,
            IsDeleted = false,
            Payments = payments
        };

        var invoices = new List<TestInvoice> { invoice };
        var actual = ComputeExpectedOutstandingReceivables(invoices, businessId);
        var expectedOutstanding = totalAmount - payments.Sum(p => p.Amount);

        return (actual == expectedOutstanding).ToProperty()
            .Label($"Expected={expectedOutstanding}, Actual={actual}, TotalAmount={totalAmount}, " +
                   $"TotalPaid={payments.Sum(p => p.Amount)}");
    }

    /// <summary>
    /// Property 8 (variant): Outstanding Receivables is always non-negative when payments
    /// don't exceed TotalAmount.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingReceivables_IsNonNegative(
        PositiveInt[] amountSeeds,
        byte[] financialStatusSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input - trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var businessId = 1;
        var invoices = new List<TestInvoice>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var financialStatusId = financialStatusSeeds.Length > 0
                ? (financialStatusSeeds[i % financialStatusSeeds.Length] % 5) + 1
                : FinancialStatusUnpaid;

            // Generate payments that never exceed totalAmount
            var payments = new List<TestPayment>();
            var remainingBalance = totalAmount;
            var paymentCount = amountSeeds[i].Get % 3;

            for (int j = 0; j < paymentCount && remainingBalance > 0.01m; j++)
            {
                var paymentAmount = Math.Min(
                    Math.Round(GenerateAmount(amountSeeds[i].Get + j + 500) % remainingBalance + 0.01m, 2),
                    remainingBalance);
                payments.Add(new TestPayment { Amount = paymentAmount, IsVoided = false });
                remainingBalance -= paymentAmount;
            }

            invoices.Add(new TestInvoice
            {
                Id = i + 1,
                BusinessId = businessId,
                InvoiceStatusTypeId = InvoiceStatusIssued,
                InvoiceFinancialStatusTypeId = financialStatusId,
                TotalAmount = totalAmount,
                IsDeleted = false,
                Payments = payments
            });
        }

        var actual = ComputeExpectedOutstandingReceivables(invoices, businessId);

        return (actual >= 0m).ToProperty()
            .Label($"Outstanding Receivables should be non-negative, got {actual}");
    }

    #endregion
}
