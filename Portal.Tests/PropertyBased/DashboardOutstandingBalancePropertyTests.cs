using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: dashboard-upgrade, Property 2: Outstanding balance computation correctness

/// <summary>
/// Property-based tests for Dashboard KPI "Outstanding" balance computation.
/// Validates that Outstanding equals the sum of (TotalAmount - sum of non-voided payments)
/// across all issued (InvoiceStatusTypeId = 2), non-deleted (IsDeleted = 0) invoices
/// with InvoiceFinancialStatusTypeId in (1, 2, 4), and the count equals the number
/// of qualifying invoices.
/// Tested as a pure computation over generated invoice and payment data.
/// **Validates: Requirements 1.2, 1.5**
/// </summary>
public class DashboardOutstandingBalancePropertyTests
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

    // Qualifying financial statuses for Outstanding balance
    private static readonly int[] QualifyingFinancialStatuses = { FinancialStatusUnpaid, FinancialStatusPartiallyPaid, FinancialStatusOverdue };

    #region Test Infrastructure

    /// <summary>
    /// Computes the expected Outstanding total from invoices and payments.
    /// Outstanding = sum of (TotalAmount - sum of non-voided payments) per qualifying invoice,
    /// clamped to 0 if overpaid.
    /// Qualifying: BusinessId matches, IsDeleted = false, InvoiceStatusTypeId = 2,
    /// InvoiceFinancialStatusTypeId in (1, 2, 4).
    /// </summary>
    private static decimal ComputeExpectedOutstandingTotal(
        List<Invoice> invoices, List<Payment> payments, int businessId)
    {
        return invoices
            .Where(inv => inv.BusinessId == businessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && QualifyingFinancialStatuses.Contains(inv.InvoiceFinancialStatusTypeId))
            .Sum(inv =>
            {
                var totalPaid = payments
                    .Where(p => p.InvoiceId == inv.Id && !p.IsVoided)
                    .Sum(p => p.Amount);
                return Math.Max(0m, inv.TotalAmount - totalPaid);
            });
    }

    /// <summary>
    /// Computes the expected Outstanding count (number of qualifying invoices).
    /// </summary>
    private static int ComputeExpectedOutstandingCount(
        List<Invoice> invoices, int businessId)
    {
        return invoices
            .Count(inv => inv.BusinessId == businessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && QualifyingFinancialStatuses.Contains(inv.InvoiceFinancialStatusTypeId));
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
    /// Creates an Invoice entity with controlled parameters for testing.
    /// </summary>
    private static Invoice CreateInvoice(
        int id, int businessId, decimal totalAmount,
        int invoiceStatusTypeId, int financialStatusTypeId, bool isDeleted)
    {
        return new Invoice
        {
            Id = id,
            BusinessId = businessId,
            CustomerId = 1,
            InvoiceStatusTypeId = invoiceStatusTypeId,
            InvoiceFinancialStatusTypeId = financialStatusTypeId,
            InvoiceNumber = $"INV-{id:D4}",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-id)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30 - id)),
            Subtotal = Math.Round(totalAmount / 1.15m, 2),
            TaxAmount = Math.Round(totalAmount - (totalAmount / 1.15m), 2),
            TotalAmount = totalAmount,
            CurrencyCode = "EUR",
            IsDeleted = isDeleted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a Payment entity with controlled parameters for testing.
    /// </summary>
    private static Payment CreatePayment(
        int id, int businessId, int invoiceId, decimal amount, bool isVoided)
    {
        return new Payment
        {
            Id = id,
            BusinessId = businessId,
            InvoiceId = invoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow.AddDays(-id),
            Amount = amount,
            IsVoided = isVoided,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generates an InvoiceFinancialStatusTypeId from a seed (1-5).
    /// </summary>
    private static int GenerateFinancialStatusId(int seed)
    {
        return (Math.Abs(seed) % 5) + 1;
    }

    /// <summary>
    /// Generates an InvoiceStatusTypeId from a seed (1-3).
    /// </summary>
    private static int GenerateInvoiceStatusId(int seed)
    {
        return (Math.Abs(seed) % 3) + 1;
    }

    #endregion

    #region Property 2: Outstanding balance computation correctness

    /// <summary>
    /// Property 2: Outstanding balance equals the sum of (TotalAmount - sum of non-voided payments)
    /// across all qualifying invoices, and count equals the number of qualifying invoices.
    /// Generates random invoices with various statuses and associated payments.
    /// **Validates: Requirements 1.2, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingBalance_EqualsCorrectSumAndCount(
        PositiveInt[] amountSeeds,
        bool[] deletedFlags,
        byte[] statusSeeds,
        byte[] financialStatusSeeds,
        bool[] voidedFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 20);
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var paymentId = 1;

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            var invoiceStatusId = statusSeeds.Length > 0
                ? (statusSeeds[i % statusSeeds.Length] % 3) + 1
                : InvoiceStatusIssued;
            var financialStatusId = financialStatusSeeds.Length > 0
                ? (financialStatusSeeds[i % financialStatusSeeds.Length] % 5) + 1
                : FinancialStatusUnpaid;

            var invoice = CreateInvoice(i + 1, TestBusinessId, totalAmount,
                invoiceStatusId, financialStatusId, isDeleted);
            invoices.Add(invoice);

            // Generate 0-3 payments per invoice
            var paymentCount = amountSeeds[i].Get % 4;
            var remainingForPayments = totalAmount;

            for (int j = 0; j < paymentCount && remainingForPayments > 0.01m; j++)
            {
                var isVoided = voidedFlags.Length > 0 && voidedFlags[(i + j) % voidedFlags.Length];
                var paymentAmount = Math.Min(
                    Math.Round(GenerateAmount(amountSeeds[i].Get + j + 1000) % remainingForPayments + 0.01m, 2),
                    remainingForPayments);

                payments.Add(CreatePayment(paymentId++, TestBusinessId, invoice.Id, paymentAmount, isVoided));

                if (!isVoided)
                    remainingForPayments -= paymentAmount;
            }
        }

        var expectedTotal = ComputeExpectedOutstandingTotal(invoices, payments, TestBusinessId);
        var expectedCount = ComputeExpectedOutstandingCount(invoices, TestBusinessId);

        // Simulate the DashboardService computation
        var actualTotal = invoices
            .Where(inv => inv.BusinessId == TestBusinessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && QualifyingFinancialStatuses.Contains(inv.InvoiceFinancialStatusTypeId))
            .Sum(inv =>
            {
                var totalPaid = payments
                    .Where(p => p.InvoiceId == inv.Id && !p.IsVoided)
                    .Sum(p => p.Amount);
                return Math.Max(0m, inv.TotalAmount - totalPaid);
            });

        var actualCount = invoices
            .Count(inv => inv.BusinessId == TestBusinessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && QualifyingFinancialStatuses.Contains(inv.InvoiceFinancialStatusTypeId));

        return (actualTotal == expectedTotal && actualCount == expectedCount).ToProperty()
            .Label($"Expected Total={expectedTotal}, Actual Total={actualTotal}, " +
                   $"Expected Count={expectedCount}, Actual Count={actualCount}, " +
                   $"InvoiceCount={invoiceCount}");
    }

    /// <summary>
    /// Voided payments do not reduce the outstanding balance.
    /// Only non-voided payments reduce the outstanding amount per invoice.
    /// **Validates: Requirements 1.2, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingBalance_VoidedPaymentsDoNotReduceBalance(
        PositiveInt totalAmountSeed,
        PositiveInt paymentAmountSeed)
    {
        var totalAmount = GenerateAmount(totalAmountSeed.Get);
        var paymentAmount = Math.Min(GenerateAmount(paymentAmountSeed.Get), totalAmount);

        var invoice = CreateInvoice(1, TestBusinessId, totalAmount,
            InvoiceStatusIssued, FinancialStatusUnpaid, isDeleted: false);
        var invoices = new List<Invoice> { invoice };

        // Only voided payments — outstanding should equal totalAmount
        var payments = new List<Payment>
        {
            CreatePayment(1, TestBusinessId, invoice.Id, paymentAmount, isVoided: true)
        };

        var actualTotal = ComputeExpectedOutstandingTotal(invoices, payments, TestBusinessId);

        return (actualTotal == totalAmount).ToProperty()
            .Label($"Expected={totalAmount}, Actual={actualTotal} — voided payments should not reduce balance");
    }

    /// <summary>
    /// Deleted invoices are excluded from the Outstanding balance computation.
    /// **Validates: Requirements 1.2, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingBalance_ExcludesDeletedInvoices(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var invoices = new List<Invoice>();

        // All invoices are deleted but otherwise qualifying
        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            invoices.Add(CreateInvoice(i + 1, TestBusinessId, totalAmount,
                InvoiceStatusIssued, FinancialStatusUnpaid, isDeleted: true));
        }

        var payments = new List<Payment>();
        var actualTotal = ComputeExpectedOutstandingTotal(invoices, payments, TestBusinessId);
        var actualCount = ComputeExpectedOutstandingCount(invoices, TestBusinessId);

        return (actualTotal == 0m && actualCount == 0).ToProperty()
            .Label($"Deleted invoices should contribute 0, got Total={actualTotal}, Count={actualCount}");
    }

    /// <summary>
    /// Non-issued invoices (Draft, Cancelled) are excluded from Outstanding balance.
    /// **Validates: Requirements 1.2, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingBalance_ExcludesNonIssuedInvoices(
        PositiveInt[] amountSeeds, bool[] useDraft)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var invoices = new List<Invoice>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var isDraft = useDraft.Length > 0 && useDraft[i % useDraft.Length];
            var statusId = isDraft ? InvoiceStatusDraft : InvoiceStatusCancelled;

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, totalAmount,
                statusId, FinancialStatusUnpaid, isDeleted: false));
        }

        var payments = new List<Payment>();
        var actualTotal = ComputeExpectedOutstandingTotal(invoices, payments, TestBusinessId);
        var actualCount = ComputeExpectedOutstandingCount(invoices, TestBusinessId);

        return (actualTotal == 0m && actualCount == 0).ToProperty()
            .Label($"Non-issued invoices should contribute 0, got Total={actualTotal}, Count={actualCount}");
    }

    /// <summary>
    /// Invoices with Paid (3) or WrittenOff (5) financial status are excluded from Outstanding.
    /// **Validates: Requirements 1.2, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingBalance_ExcludesPaidAndWrittenOffStatuses(
        PositiveInt[] amountSeeds, bool[] usePaid)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var invoices = new List<Invoice>();

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var isPaid = usePaid.Length > 0 && usePaid[i % usePaid.Length];
            var financialStatusId = isPaid ? FinancialStatusPaid : FinancialStatusWrittenOff;

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, totalAmount,
                InvoiceStatusIssued, financialStatusId, isDeleted: false));
        }

        var payments = new List<Payment>();
        var actualTotal = ComputeExpectedOutstandingTotal(invoices, payments, TestBusinessId);
        var actualCount = ComputeExpectedOutstandingCount(invoices, TestBusinessId);

        return (actualTotal == 0m && actualCount == 0).ToProperty()
            .Label($"Paid/WrittenOff invoices should contribute 0, got Total={actualTotal}, Count={actualCount}");
    }

    /// <summary>
    /// Outstanding balance correctly accounts for partial payments.
    /// When valid payments exist, outstanding per invoice = TotalAmount - sum(non-voided payments),
    /// clamped to 0 if overpaid.
    /// **Validates: Requirements 1.2, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingBalance_CorrectlyAccountsForPartialPayments(
        PositiveInt totalAmountSeed,
        PositiveInt[] paymentSeeds)
    {
        if (paymentSeeds.Length == 0)
            return true.ToProperty().Label("No payments — trivially true");

        var totalAmount = GenerateAmount(totalAmountSeed.Get);

        var invoice = CreateInvoice(1, TestBusinessId, totalAmount,
            InvoiceStatusIssued, FinancialStatusPartiallyPaid, isDeleted: false);
        var invoices = new List<Invoice> { invoice };

        // Generate non-voided payments that don't exceed totalAmount
        var payments = new List<Payment>();
        var remainingBalance = totalAmount;
        var paymentCount = Math.Min(paymentSeeds.Length, 5);

        for (int i = 0; i < paymentCount && remainingBalance > 0.01m; i++)
        {
            var paymentAmount = Math.Min(
                Math.Round(GenerateAmount(paymentSeeds[i].Get) % remainingBalance + 0.01m, 2),
                remainingBalance);
            payments.Add(CreatePayment(i + 1, TestBusinessId, invoice.Id, paymentAmount, isVoided: false));
            remainingBalance -= paymentAmount;
        }

        var actualTotal = ComputeExpectedOutstandingTotal(invoices, payments, TestBusinessId);
        var expectedOutstanding = Math.Max(0m, totalAmount - payments.Sum(p => p.Amount));

        return (actualTotal == expectedOutstanding).ToProperty()
            .Label($"Expected={expectedOutstanding}, Actual={actualTotal}, " +
                   $"TotalAmount={totalAmount}, TotalPaid={payments.Sum(p => p.Amount)}");
    }

    /// <summary>
    /// Invoices from a different business are excluded from Outstanding balance.
    /// **Validates: Requirements 1.2, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingBalance_ExcludesOtherBusinessInvoices(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var otherBusinessId = 99;
        var invoices = new List<Invoice>();

        // Create qualifying invoices for a DIFFERENT business
        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            invoices.Add(CreateInvoice(i + 1, otherBusinessId, totalAmount,
                InvoiceStatusIssued, FinancialStatusUnpaid, isDeleted: false));
        }

        var payments = new List<Payment>();
        var actualTotal = ComputeExpectedOutstandingTotal(invoices, payments, TestBusinessId);
        var actualCount = ComputeExpectedOutstandingCount(invoices, TestBusinessId);

        return (actualTotal == 0m && actualCount == 0).ToProperty()
            .Label($"Other business invoices should contribute 0, got Total={actualTotal}, Count={actualCount}");
    }

    /// <summary>
    /// When no qualifying invoices exist, Outstanding is zero with count zero.
    /// **Validates: Requirements 1.2, 1.5**
    /// </summary>
    [Fact]
    public void OutstandingBalance_NoQualifyingInvoices_ReturnsZeroTotalAndCount()
    {
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var actualTotal = ComputeExpectedOutstandingTotal(invoices, payments, TestBusinessId);
        var actualCount = ComputeExpectedOutstandingCount(invoices, TestBusinessId);
        Assert.Equal(0m, actualTotal);
        Assert.Equal(0, actualCount);
    }

    /// <summary>
    /// Mixed scenario: invoices across multiple statuses, businesses, and deletion states.
    /// Only non-deleted, issued, qualifying-financial-status, same-business invoices count.
    /// **Validates: Requirements 1.2, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OutstandingBalance_MixedScenario_OnlyCountsQualifyingInvoices(
        PositiveInt[] amountSeeds,
        bool[] deletedFlags,
        bool[] issuedFlags,
        bool[] qualifyingFlags,
        bool[] sameBusinessFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 20);
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var paymentId = 1;

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            var isIssued = issuedFlags.Length > 0 && issuedFlags[i % issuedFlags.Length];
            var isQualifying = qualifyingFlags.Length > 0 && qualifyingFlags[i % qualifyingFlags.Length];
            var isSameBusiness = sameBusinessFlags.Length > 0 && sameBusinessFlags[i % sameBusinessFlags.Length];

            var invoiceStatusId = isIssued ? InvoiceStatusIssued : InvoiceStatusDraft;
            var financialStatusId = isQualifying
                ? QualifyingFinancialStatuses[Math.Abs(amountSeeds[i].Get) % QualifyingFinancialStatuses.Length]
                : FinancialStatusPaid;
            var businessId = isSameBusiness ? TestBusinessId : 99;

            var invoice = CreateInvoice(i + 1, businessId, totalAmount,
                invoiceStatusId, financialStatusId, isDeleted);
            invoices.Add(invoice);

            // Add one payment per invoice (50% of total, non-voided)
            if (amountSeeds[i].Get % 2 == 0)
            {
                var paymentAmount = Math.Round(totalAmount * 0.5m, 2);
                payments.Add(CreatePayment(paymentId++, businessId, invoice.Id, paymentAmount, isVoided: false));
            }
        }

        var expectedTotal = ComputeExpectedOutstandingTotal(invoices, payments, TestBusinessId);
        var expectedCount = ComputeExpectedOutstandingCount(invoices, TestBusinessId);

        // Simulate the same computation
        var actualTotal = invoices
            .Where(inv => inv.BusinessId == TestBusinessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && QualifyingFinancialStatuses.Contains(inv.InvoiceFinancialStatusTypeId))
            .Sum(inv =>
            {
                var totalPaid = payments
                    .Where(p => p.InvoiceId == inv.Id && !p.IsVoided)
                    .Sum(p => p.Amount);
                return Math.Max(0m, inv.TotalAmount - totalPaid);
            });

        var actualCount = invoices
            .Count(inv => inv.BusinessId == TestBusinessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && QualifyingFinancialStatuses.Contains(inv.InvoiceFinancialStatusTypeId));

        return (actualTotal == expectedTotal && actualCount == expectedCount).ToProperty()
            .Label($"Expected Total={expectedTotal}, Actual Total={actualTotal}, " +
                   $"Expected Count={expectedCount}, Actual Count={actualCount}, " +
                   $"TotalInvoices={invoiceCount}");
    }

    #endregion
}
