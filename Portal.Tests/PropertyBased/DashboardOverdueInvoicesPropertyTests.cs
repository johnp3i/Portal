using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: dashboard-upgrade, Property 9: Overdue invoices filtering, ordering, and cap

/// <summary>
/// Property-based tests for Dashboard "Overdue Invoices" computation.
/// Validates that overdue invoices contain only invoices where DueDate &lt; today AND outstanding &gt; 0,
/// ordered by DueDate ascending, capped at 10 rows, and the warning banner total equals the sum of
/// ALL overdue outstanding balances (not just the displayed 10).
/// Tested as a pure computation over generated invoice and payment data.
/// **Validates: Requirements 5.1, 5.4, 5.7**
/// </summary>
public class DashboardOverdueInvoicesPropertyTests
{
    private const int TestBusinessId = 1;

    #region Test Infrastructure

    /// <summary>
    /// Computes the outstanding balance for a single invoice given a list of payments.
    /// Outstanding = TotalAmount - sum of non-voided payments for that invoice.
    /// </summary>
    private static decimal ComputeOutstandingBalance(Invoice invoice, List<Payment> payments)
    {
        var paidAmount = payments
            .Where(p => p.InvoiceId == invoice.Id && !p.IsVoided)
            .Sum(p => p.Amount);

        var outstanding = invoice.TotalAmount - paidAmount;
        return outstanding > 0 ? outstanding : 0m;
    }

    /// <summary>
    /// Determines if an invoice qualifies as overdue:
    /// - InvoiceStatusTypeId = 2 (Issued)
    /// - IsDeleted = false
    /// - DueDate &lt; today (UTC)
    /// - Outstanding balance &gt; 0
    /// </summary>
    private static bool IsOverdue(Invoice invoice, List<Payment> payments, DateOnly today)
    {
        return invoice.BusinessId == TestBusinessId
               && invoice.InvoiceStatusTypeId == 2
               && !invoice.IsDeleted
               && invoice.DueDate < today
               && ComputeOutstandingBalance(invoice, payments) > 0;
    }

    /// <summary>
    /// Computes the full list of overdue invoices (no cap), ordered by DueDate ascending.
    /// </summary>
    private static List<(Invoice Invoice, decimal Outstanding)> ComputeAllOverdueInvoices(
        List<Invoice> invoices, List<Payment> payments, DateOnly today)
    {
        return invoices
            .Where(inv => IsOverdue(inv, payments, today))
            .OrderBy(inv => inv.DueDate)
            .Select(inv => (inv, ComputeOutstandingBalance(inv, payments)))
            .ToList();
    }

    /// <summary>
    /// Computes the displayed overdue invoices (capped at 10), ordered by DueDate ascending.
    /// </summary>
    private static List<(Invoice Invoice, decimal Outstanding)> ComputeDisplayedOverdueInvoices(
        List<Invoice> invoices, List<Payment> payments, DateOnly today)
    {
        return ComputeAllOverdueInvoices(invoices, payments, today)
            .Take(10)
            .ToList();
    }

    /// <summary>
    /// Computes the warning banner total: sum of ALL overdue outstanding balances (not just displayed 10).
    /// </summary>
    private static decimal ComputeWarningBannerTotal(
        List<Invoice> invoices, List<Payment> payments, DateOnly today)
    {
        return ComputeAllOverdueInvoices(invoices, payments, today)
            .Sum(x => x.Outstanding);
    }

    /// <summary>
    /// Generates a DateOnly in the past (before today) from a seed.
    /// </summary>
    private static DateOnly GeneratePastDate(int seed)
    {
        var daysAgo = (Math.Abs(seed) % 365) + 1; // 1 to 365 days ago
        return DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysAgo));
    }

    /// <summary>
    /// Generates a DateOnly in the future (today or later) from a seed.
    /// </summary>
    private static DateOnly GenerateFutureOrTodayDate(int seed)
    {
        var daysAhead = Math.Abs(seed) % 365; // 0 to 364 days ahead (0 = today)
        return DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
    }

    /// <summary>
    /// Generates a positive decimal amount from a seed.
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = (Math.Abs(seed) % 999999) + 100; // Ensure meaningful amounts
        return raw / 100m;
    }

    /// <summary>
    /// Creates an Invoice entity with controlled parameters for testing.
    /// </summary>
    private static Invoice CreateInvoice(
        int id, int businessId, decimal totalAmount, DateOnly dueDate,
        int statusTypeId = 2, bool isDeleted = false)
    {
        return new Invoice
        {
            Id = id,
            BusinessId = businessId,
            CustomerId = 1,
            InvoiceStatusTypeId = statusTypeId,
            InvoiceFinancialStatusTypeId = 1,
            InvoiceNumber = $"INV-{id:D4}",
            InvoiceDate = dueDate.AddDays(-30),
            DueDate = dueDate,
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
        int id, int businessId, int invoiceId, decimal amount, bool isVoided = false)
    {
        return new Payment
        {
            Id = id,
            BusinessId = businessId,
            InvoiceId = invoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow.AddDays(-5),
            Amount = amount,
            IsVoided = isVoided,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    #endregion

    #region Property 9: Overdue invoices filtering, ordering, and cap

    /// <summary>
    /// Property 9a: Overdue invoices result contains only invoices where DueDate &lt; today
    /// AND outstanding balance &gt; 0 AND IsDeleted = 0 AND InvoiceStatusTypeId = 2.
    /// **Validates: Requirements 5.1, 5.4, 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_ContainsOnlyQualifyingInvoices(
        PositiveInt[] amountSeeds, bool[] pastDueFlags, bool[] partialPaymentFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 20);
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var paymentId = 1;

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var isPastDue = pastDueFlags.Length > 0 && pastDueFlags[i % pastDueFlags.Length];
            var hasPartialPayment = partialPaymentFlags.Length > 0 && partialPaymentFlags[i % partialPaymentFlags.Length];

            var dueDate = isPastDue
                ? GeneratePastDate(amountSeeds[i].Get + i)
                : GenerateFutureOrTodayDate(amountSeeds[i].Get + i);

            var invoice = CreateInvoice(i + 1, TestBusinessId, totalAmount, dueDate);
            invoices.Add(invoice);

            // Add a partial payment for some invoices (less than total so outstanding > 0)
            if (hasPartialPayment)
            {
                var paymentAmount = Math.Round(totalAmount * 0.3m, 2); // 30% paid
                payments.Add(CreatePayment(paymentId++, TestBusinessId, invoice.Id, paymentAmount));
            }
        }

        var displayed = ComputeDisplayedOverdueInvoices(invoices, payments, today);

        // Assert: all displayed invoices must be overdue
        var allQualify = displayed.All(x =>
            x.Invoice.DueDate < today
            && x.Outstanding > 0
            && x.Invoice.InvoiceStatusTypeId == 2
            && !x.Invoice.IsDeleted
            && x.Invoice.BusinessId == TestBusinessId);

        return allQualify.ToProperty()
            .Label($"All {displayed.Count} displayed invoices should qualify as overdue");
    }

    /// <summary>
    /// Property 9b: Overdue invoices are ordered by DueDate ascending.
    /// **Validates: Requirements 5.1, 5.4, 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_OrderedByDueDateAscending(
        PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 20);
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Create all invoices with past due dates (so they all qualify as overdue)
        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var dueDate = GeneratePastDate(amountSeeds[i].Get + i);

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, totalAmount, dueDate));
        }

        var displayed = ComputeDisplayedOverdueInvoices(invoices, payments, today);

        // Assert: ordered by DueDate ascending
        var isOrdered = true;
        for (int i = 1; i < displayed.Count; i++)
        {
            if (displayed[i].Invoice.DueDate < displayed[i - 1].Invoice.DueDate)
            {
                isOrdered = false;
                break;
            }
        }

        return isOrdered.ToProperty()
            .Label($"Displayed {displayed.Count} invoices should be ordered by DueDate ascending");
    }

    /// <summary>
    /// Property 9c: Overdue invoices result is capped at 10 rows.
    /// **Validates: Requirements 5.1, 5.4, 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_CappedAtTenRows(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        // Generate more than 10 invoices to test the cap
        var invoiceCount = Math.Min(amountSeeds.Length, 20) + 10;
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (int i = 0; i < invoiceCount; i++)
        {
            var seed = i < amountSeeds.Length ? amountSeeds[i].Get : (i * 7 + 13);
            var totalAmount = GenerateAmount(seed);
            var dueDate = GeneratePastDate(seed + i); // All past due

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, totalAmount, dueDate));
        }

        var displayed = ComputeDisplayedOverdueInvoices(invoices, payments, today);

        return (displayed.Count <= 10).ToProperty()
            .Label($"Displayed count={displayed.Count} should be <= 10 " +
                   $"(total overdue={ComputeAllOverdueInvoices(invoices, payments, today).Count})");
    }

    /// <summary>
    /// Property 9d: Warning banner total equals sum of ALL overdue outstanding balances,
    /// not just the displayed 10.
    /// **Validates: Requirements 5.1, 5.4, 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_WarningBannerTotalEqualsAllOverdueBalances(
        PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        // Generate more than 10 invoices to ensure cap is exercised
        var invoiceCount = Math.Min(amountSeeds.Length, 15) + 10;
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var paymentId = 1;

        for (int i = 0; i < invoiceCount; i++)
        {
            var seed = i < amountSeeds.Length ? amountSeeds[i].Get : (i * 11 + 7);
            var totalAmount = GenerateAmount(seed);
            var dueDate = GeneratePastDate(seed + i); // All past due

            var invoice = CreateInvoice(i + 1, TestBusinessId, totalAmount, dueDate);
            invoices.Add(invoice);

            // Add partial payments to some invoices
            if (i % 3 == 0)
            {
                var paymentAmount = Math.Round(totalAmount * 0.4m, 2);
                payments.Add(CreatePayment(paymentId++, TestBusinessId, invoice.Id, paymentAmount));
            }
        }

        var allOverdue = ComputeAllOverdueInvoices(invoices, payments, today);
        var displayed = allOverdue.Take(10).ToList();
        var warningBannerTotal = ComputeWarningBannerTotal(invoices, payments, today);

        // The banner total should be the sum of ALL overdue balances
        var expectedTotal = allOverdue.Sum(x => x.Outstanding);

        // The banner total should NOT equal just the displayed 10 sum (when there are more than 10)
        var displayedTotal = displayed.Sum(x => x.Outstanding);

        var bannerEqualsAll = warningBannerTotal == expectedTotal;
        var bannerDiffersFromDisplayedWhenMoreThan10 =
            allOverdue.Count <= 10 || warningBannerTotal != displayedTotal || warningBannerTotal == expectedTotal;

        return (bannerEqualsAll && bannerDiffersFromDisplayedWhenMoreThan10).ToProperty()
            .Label($"Banner total={warningBannerTotal}, Expected (all overdue)={expectedTotal}, " +
                   $"Displayed total={displayedTotal}, " +
                   $"All overdue count={allOverdue.Count}, Displayed count={displayed.Count}");
    }

    /// <summary>
    /// Property 9e: Invoices that are not past due (DueDate >= today) are excluded from overdue results.
    /// **Validates: Requirements 5.1, 5.4, 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_ExcludesNonPastDueInvoices(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Create all invoices with future or today due dates (none should be overdue)
        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var dueDate = GenerateFutureOrTodayDate(amountSeeds[i].Get + i);

            invoices.Add(CreateInvoice(i + 1, TestBusinessId, totalAmount, dueDate));
        }

        var displayed = ComputeDisplayedOverdueInvoices(invoices, payments, today);
        var bannerTotal = ComputeWarningBannerTotal(invoices, payments, today);

        return (displayed.Count == 0 && bannerTotal == 0m).ToProperty()
            .Label($"No invoices should be overdue when all DueDates >= today, " +
                   $"but got count={displayed.Count}, bannerTotal={bannerTotal}");
    }

    /// <summary>
    /// Property 9f: Invoices with outstanding balance = 0 (fully paid) are excluded from overdue results.
    /// **Validates: Requirements 5.1, 5.4, 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_ExcludesFullyPaidInvoices(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Create all invoices past due but fully paid
        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var dueDate = GeneratePastDate(amountSeeds[i].Get + i);

            var invoice = CreateInvoice(i + 1, TestBusinessId, totalAmount, dueDate);
            invoices.Add(invoice);

            // Pay the full amount
            payments.Add(CreatePayment(i + 1, TestBusinessId, invoice.Id, totalAmount));
        }

        var displayed = ComputeDisplayedOverdueInvoices(invoices, payments, today);
        var bannerTotal = ComputeWarningBannerTotal(invoices, payments, today);

        return (displayed.Count == 0 && bannerTotal == 0m).ToProperty()
            .Label($"No invoices should be overdue when all are fully paid, " +
                   $"but got count={displayed.Count}, bannerTotal={bannerTotal}");
    }

    /// <summary>
    /// Property 9g: Deleted invoices and non-issued invoices are excluded from overdue results.
    /// **Validates: Requirements 5.1, 5.4, 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_ExcludesDeletedAndNonIssuedInvoices(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var dueDate = GeneratePastDate(amountSeeds[i].Get + i);

            if (i % 2 == 0)
            {
                // Deleted invoice (should be excluded)
                invoices.Add(CreateInvoice(i + 1, TestBusinessId, totalAmount, dueDate,
                    statusTypeId: 2, isDeleted: true));
            }
            else
            {
                // Non-issued invoice (Draft = 1, should be excluded)
                invoices.Add(CreateInvoice(i + 1, TestBusinessId, totalAmount, dueDate,
                    statusTypeId: 1, isDeleted: false));
            }
        }

        var displayed = ComputeDisplayedOverdueInvoices(invoices, payments, today);
        var bannerTotal = ComputeWarningBannerTotal(invoices, payments, today);

        return (displayed.Count == 0 && bannerTotal == 0m).ToProperty()
            .Label($"No invoices should be overdue when all are deleted or non-issued, " +
                   $"but got count={displayed.Count}, bannerTotal={bannerTotal}");
    }

    /// <summary>
    /// Property 9h: Voided payments do not reduce outstanding balance (invoice remains overdue).
    /// **Validates: Requirements 5.1, 5.4, 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_VoidedPaymentsDoNotReduceOutstanding(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 15);
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Create past-due invoices with only voided payments (should still be overdue)
        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var dueDate = GeneratePastDate(amountSeeds[i].Get + i);

            var invoice = CreateInvoice(i + 1, TestBusinessId, totalAmount, dueDate);
            invoices.Add(invoice);

            // Add a voided payment for the full amount (should not count)
            payments.Add(CreatePayment(i + 1, TestBusinessId, invoice.Id, totalAmount, isVoided: true));
        }

        var displayed = ComputeDisplayedOverdueInvoices(invoices, payments, today);

        // All invoices should still be overdue since voided payments don't count
        return (displayed.Count == invoiceCount || displayed.Count == 10).ToProperty()
            .Label($"All {invoiceCount} invoices should be overdue (voided payments ignored), " +
                   $"displayed={displayed.Count} (capped at 10)");
    }

    /// <summary>
    /// Mixed scenario: invoices with varying due dates, payment states, deletion flags, and statuses.
    /// Verifies filtering, ordering, cap, and banner total all hold simultaneously.
    /// **Validates: Requirements 5.1, 5.4, 5.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OverdueInvoices_MixedScenario_AllPropertiesHold(
        PositiveInt[] amountSeeds, bool[] pastDueFlags, bool[] deletedFlags, bool[] fullPayFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 25);
        var invoices = new List<Invoice>();
        var payments = new List<Payment>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var paymentId = 1;

        for (int i = 0; i < invoiceCount; i++)
        {
            var totalAmount = GenerateAmount(amountSeeds[i].Get);
            var isPastDue = pastDueFlags.Length > 0 && pastDueFlags[i % pastDueFlags.Length];
            var isDeleted = deletedFlags.Length > 0 && deletedFlags[i % deletedFlags.Length];
            var isFullyPaid = fullPayFlags.Length > 0 && fullPayFlags[i % fullPayFlags.Length];

            var dueDate = isPastDue
                ? GeneratePastDate(amountSeeds[i].Get + i)
                : GenerateFutureOrTodayDate(amountSeeds[i].Get + i);

            var invoice = CreateInvoice(i + 1, TestBusinessId, totalAmount, dueDate,
                statusTypeId: 2, isDeleted: isDeleted);
            invoices.Add(invoice);

            if (isFullyPaid)
            {
                payments.Add(CreatePayment(paymentId++, TestBusinessId, invoice.Id, totalAmount));
            }
        }

        var allOverdue = ComputeAllOverdueInvoices(invoices, payments, today);
        var displayed = ComputeDisplayedOverdueInvoices(invoices, payments, today);
        var bannerTotal = ComputeWarningBannerTotal(invoices, payments, today);

        // Property 1: All displayed are qualifying overdue invoices
        var allQualify = displayed.All(x =>
            x.Invoice.DueDate < today
            && x.Outstanding > 0
            && x.Invoice.InvoiceStatusTypeId == 2
            && !x.Invoice.IsDeleted);

        // Property 2: Ordered by DueDate ascending
        var isOrdered = true;
        for (int i = 1; i < displayed.Count; i++)
        {
            if (displayed[i].Invoice.DueDate < displayed[i - 1].Invoice.DueDate)
            {
                isOrdered = false;
                break;
            }
        }

        // Property 3: Capped at 10
        var isCapped = displayed.Count <= 10;

        // Property 4: Banner total equals sum of ALL overdue balances
        var expectedBannerTotal = allOverdue.Sum(x => x.Outstanding);
        var bannerCorrect = bannerTotal == expectedBannerTotal;

        return (allQualify && isOrdered && isCapped && bannerCorrect).ToProperty()
            .Label($"AllQualify={allQualify}, Ordered={isOrdered}, Capped={isCapped}, " +
                   $"BannerCorrect={bannerCorrect}, " +
                   $"Displayed={displayed.Count}, AllOverdue={allOverdue.Count}, " +
                   $"BannerTotal={bannerTotal}, ExpectedTotal={expectedBannerTotal}");
    }

    #endregion
}
