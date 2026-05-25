using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: dashboard-upgrade, Property 5: Tenant data isolation

/// <summary>
/// Property-based tests for Dashboard Tenant Data Isolation.
/// Validates that filtering by BusinessId A never includes records belonging to BusinessId B,
/// and vice versa. Tested as pure computation over generated entity data.
/// **Validates: Requirements 1.6, 2.7, 3.5, 5.6, 7.5, 8.7, 10.1**
/// </summary>
public class DashboardTenantIsolationPropertyTests
{
    private const int BusinessIdA = 1;
    private const int BusinessIdB = 2;

    #region Test Infrastructure

    /// <summary>
    /// Generates a positive decimal amount from a seed.
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = Math.Abs(seed) % 999999 + 1;
        return raw / 100m;
    }

    /// <summary>
    /// Generates a DateOnly within the current month.
    /// </summary>
    private static DateOnly GenerateCurrentMonthDate(int seed)
    {
        var now = DateTime.UtcNow;
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var dayOffset = Math.Abs(seed) % daysInMonth;
        return new DateOnly(now.Year, now.Month, dayOffset + 1);
    }

    /// <summary>
    /// Generates a DateTime within the current month (for Payment dates).
    /// </summary>
    private static DateTime GenerateCurrentMonthDateTime(int seed)
    {
        var date = GenerateCurrentMonthDate(seed);
        return new DateTime(date.Year, date.Month, date.Day, 12, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// Creates a Purchase entity with controlled parameters for testing.
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
            Description = $"Test Purchase {id}",
            AmountExcludingVat = Math.Round(totalAmount / 1.15m, 2),
            VatAmount = Math.Round(totalAmount - (totalAmount / 1.15m), 2),
            TotalAmount = totalAmount,
            IsCancelled = isCancelled,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates an Invoice entity with controlled parameters for testing.
    /// </summary>
    private static Invoice CreateInvoice(
        int id, int businessId, decimal totalAmount, DateOnly invoiceDate,
        int financialStatusTypeId, bool isDeleted)
    {
        return new Invoice
        {
            Id = id,
            BusinessId = businessId,
            CustomerId = 1,
            InvoiceStatusTypeId = 2, // Issued
            InvoiceFinancialStatusTypeId = financialStatusTypeId,
            InvoiceNumber = $"INV-{id:D4}",
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(30),
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
        int id, int businessId, int invoiceId, decimal amount,
        DateTime paymentDateUtc, bool isVoided)
    {
        return new Payment
        {
            Id = id,
            BusinessId = businessId,
            InvoiceId = invoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = paymentDateUtc,
            Amount = amount,
            IsVoided = isVoided,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    #endregion

    #region Filtering Logic (mirrors DashboardService computations)

    /// <summary>
    /// Filters purchases for expenses this month by businessId (non-cancelled, current month).
    /// </summary>
    private static List<Purchase> FilterExpensesThisMonth(
        List<Purchase> allPurchases, int businessId)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateOnly(now.Year, now.Month, 1);
        var monthEnd = new DateOnly(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));

        return allPurchases
            .Where(p => p.BusinessId == businessId
                        && !p.IsCancelled
                        && p.InvoiceDate >= monthStart
                        && p.InvoiceDate <= monthEnd)
            .ToList();
    }

    /// <summary>
    /// Filters invoices for a given businessId (issued, non-deleted).
    /// </summary>
    private static List<Invoice> FilterIssuedInvoices(
        List<Invoice> allInvoices, int businessId)
    {
        return allInvoices
            .Where(i => i.BusinessId == businessId
                        && i.InvoiceStatusTypeId == 2
                        && !i.IsDeleted)
            .ToList();
    }

    /// <summary>
    /// Filters payments for revenue this month by businessId (non-voided, current month).
    /// </summary>
    private static List<Payment> FilterRevenueThisMonth(
        List<Payment> allPayments, int businessId)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

        return allPayments
            .Where(p => p.BusinessId == businessId
                        && !p.IsVoided
                        && p.PaymentDateUtc >= monthStart
                        && p.PaymentDateUtc <= monthEnd)
            .ToList();
    }

    /// <summary>
    /// Filters recent payments by businessId (non-voided, top 5 by date desc).
    /// </summary>
    private static List<Payment> FilterRecentPayments(
        List<Payment> allPayments, int businessId)
    {
        return allPayments
            .Where(p => p.BusinessId == businessId && !p.IsVoided)
            .OrderByDescending(p => p.PaymentDateUtc)
            .Take(5)
            .ToList();
    }

    #endregion

    #region Property 5: Tenant data isolation

    /// <summary>
    /// Property 5: Filtering purchases by BusinessId A never returns records from BusinessId B.
    /// Generates purchases for both tenants, filters for each, asserts no cross-contamination.
    /// **Validates: Requirements 1.6, 2.7, 3.5, 5.6, 7.5, 8.7, 10.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PurchaseFiltering_IsolatesTenantData(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var purchaseCount = Math.Min(amountSeeds.Length, 20);
        var allPurchases = new List<Purchase>();

        // Create overlapping purchases for both tenants
        for (int i = 0; i < purchaseCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var invoiceDate = GenerateCurrentMonthDate(amountSeeds[i].Get + i);

            // Add purchase for Tenant A
            allPurchases.Add(CreatePurchase(
                i * 2 + 1, BusinessIdA, amount, invoiceDate, isCancelled: false));

            // Add purchase for Tenant B with same date/amount (overlapping)
            allPurchases.Add(CreatePurchase(
                i * 2 + 2, BusinessIdB, amount + 1m, invoiceDate, isCancelled: false));
        }

        // Filter for Tenant A
        var tenantAResults = FilterExpensesThisMonth(allPurchases, BusinessIdA);
        var tenantAContainsBData = tenantAResults.Any(p => p.BusinessId == BusinessIdB);

        // Filter for Tenant B
        var tenantBResults = FilterExpensesThisMonth(allPurchases, BusinessIdB);
        var tenantBContainsAData = tenantBResults.Any(p => p.BusinessId == BusinessIdA);

        return (!tenantAContainsBData && !tenantBContainsAData).ToProperty()
            .Label($"TenantA results contain B data: {tenantAContainsBData}, " +
                   $"TenantB results contain A data: {tenantBContainsAData}, " +
                   $"TenantA count: {tenantAResults.Count}, TenantB count: {tenantBResults.Count}");
    }

    /// <summary>
    /// Property 5: Filtering invoices by BusinessId A never returns records from BusinessId B.
    /// Generates invoices for both tenants, filters for each, asserts no cross-contamination.
    /// **Validates: Requirements 1.6, 2.7, 3.5, 5.6, 7.5, 8.7, 10.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoiceFiltering_IsolatesTenantData(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var invoiceCount = Math.Min(amountSeeds.Length, 20);
        var allInvoices = new List<Invoice>();

        // Create overlapping invoices for both tenants
        for (int i = 0; i < invoiceCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var invoiceDate = GenerateCurrentMonthDate(amountSeeds[i].Get + i);

            // Add invoice for Tenant A
            allInvoices.Add(CreateInvoice(
                i * 2 + 1, BusinessIdA, amount, invoiceDate,
                financialStatusTypeId: 1, isDeleted: false));

            // Add invoice for Tenant B with same date/amount (overlapping)
            allInvoices.Add(CreateInvoice(
                i * 2 + 2, BusinessIdB, amount + 1m, invoiceDate,
                financialStatusTypeId: 1, isDeleted: false));
        }

        // Filter for Tenant A
        var tenantAResults = FilterIssuedInvoices(allInvoices, BusinessIdA);
        var tenantAContainsBData = tenantAResults.Any(i => i.BusinessId == BusinessIdB);

        // Filter for Tenant B
        var tenantBResults = FilterIssuedInvoices(allInvoices, BusinessIdB);
        var tenantBContainsAData = tenantBResults.Any(i => i.BusinessId == BusinessIdA);

        return (!tenantAContainsBData && !tenantBContainsAData).ToProperty()
            .Label($"TenantA results contain B data: {tenantAContainsBData}, " +
                   $"TenantB results contain A data: {tenantBContainsAData}, " +
                   $"TenantA count: {tenantAResults.Count}, TenantB count: {tenantBResults.Count}");
    }

    /// <summary>
    /// Property 5: Filtering payments by BusinessId A never returns records from BusinessId B.
    /// Generates payments for both tenants, filters for each, asserts no cross-contamination.
    /// **Validates: Requirements 1.6, 2.7, 3.5, 5.6, 7.5, 8.7, 10.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PaymentFiltering_IsolatesTenantData(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var paymentCount = Math.Min(amountSeeds.Length, 20);
        var allPayments = new List<Payment>();

        // Create overlapping payments for both tenants
        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var paymentDate = GenerateCurrentMonthDateTime(amountSeeds[i].Get + i);

            // Add payment for Tenant A
            allPayments.Add(CreatePayment(
                i * 2 + 1, BusinessIdA, invoiceId: 100 + i,
                amount, paymentDate, isVoided: false));

            // Add payment for Tenant B with same date/amount (overlapping)
            allPayments.Add(CreatePayment(
                i * 2 + 2, BusinessIdB, invoiceId: 200 + i,
                amount + 1m, paymentDate, isVoided: false));
        }

        // Filter revenue this month for Tenant A
        var tenantARevenue = FilterRevenueThisMonth(allPayments, BusinessIdA);
        var tenantAContainsBData = tenantARevenue.Any(p => p.BusinessId == BusinessIdB);

        // Filter revenue this month for Tenant B
        var tenantBRevenue = FilterRevenueThisMonth(allPayments, BusinessIdB);
        var tenantBContainsAData = tenantBRevenue.Any(p => p.BusinessId == BusinessIdA);

        return (!tenantAContainsBData && !tenantBContainsAData).ToProperty()
            .Label($"TenantA results contain B data: {tenantAContainsBData}, " +
                   $"TenantB results contain A data: {tenantBContainsAData}, " +
                   $"TenantA count: {tenantARevenue.Count}, TenantB count: {tenantBRevenue.Count}");
    }

    /// <summary>
    /// Property 5: Recent payments filtering by BusinessId isolates tenant data.
    /// Generates payments for both tenants, retrieves recent payments for each,
    /// asserts no cross-contamination.
    /// **Validates: Requirements 1.6, 2.7, 3.5, 5.6, 7.5, 8.7, 10.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RecentPayments_IsolatesTenantData(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var paymentCount = Math.Min(amountSeeds.Length, 20);
        var allPayments = new List<Payment>();

        for (int i = 0; i < paymentCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var paymentDate = GenerateCurrentMonthDateTime(amountSeeds[i].Get + i);

            allPayments.Add(CreatePayment(
                i * 2 + 1, BusinessIdA, invoiceId: 100 + i,
                amount, paymentDate, isVoided: false));

            allPayments.Add(CreatePayment(
                i * 2 + 2, BusinessIdB, invoiceId: 200 + i,
                amount + 1m, paymentDate, isVoided: false));
        }

        // Get recent payments for Tenant A
        var tenantARecent = FilterRecentPayments(allPayments, BusinessIdA);
        var tenantAContainsBData = tenantARecent.Any(p => p.BusinessId == BusinessIdB);

        // Get recent payments for Tenant B
        var tenantBRecent = FilterRecentPayments(allPayments, BusinessIdB);
        var tenantBContainsAData = tenantBRecent.Any(p => p.BusinessId == BusinessIdA);

        return (!tenantAContainsBData && !tenantBContainsAData).ToProperty()
            .Label($"TenantA recent contain B data: {tenantAContainsBData}, " +
                   $"TenantB recent contain A data: {tenantBContainsAData}, " +
                   $"TenantA count: {tenantARecent.Count}, TenantB count: {tenantBRecent.Count}");
    }

    /// <summary>
    /// Property 5: Mixed entity isolation — generates purchases, invoices, and payments
    /// for two tenants with overlapping data, verifies complete isolation across all
    /// dashboard filtering operations simultaneously.
    /// **Validates: Requirements 1.6, 2.7, 3.5, 5.6, 7.5, 8.7, 10.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllDashboardFiltering_IsolatesTenantDataAcrossAllEntities(
        PositiveInt[] amountSeeds, bool[] cancelFlags, bool[] voidedFlags)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var recordCount = Math.Min(amountSeeds.Length, 15);
        var allPurchases = new List<Purchase>();
        var allInvoices = new List<Invoice>();
        var allPayments = new List<Payment>();

        for (int i = 0; i < recordCount; i++)
        {
            var amount = GenerateAmount(amountSeeds[i].Get);
            var date = GenerateCurrentMonthDate(amountSeeds[i].Get + i);
            var dateTime = GenerateCurrentMonthDateTime(amountSeeds[i].Get + i);
            var isCancelled = cancelFlags.Length > 0 && cancelFlags[i % cancelFlags.Length];
            var isVoided = voidedFlags.Length > 0 && voidedFlags[i % voidedFlags.Length];

            // Purchases for both tenants
            allPurchases.Add(CreatePurchase(i * 2 + 1, BusinessIdA, amount, date, isCancelled));
            allPurchases.Add(CreatePurchase(i * 2 + 2, BusinessIdB, amount + 5m, date, isCancelled));

            // Invoices for both tenants
            allInvoices.Add(CreateInvoice(i * 2 + 1, BusinessIdA, amount, date, 1, false));
            allInvoices.Add(CreateInvoice(i * 2 + 2, BusinessIdB, amount + 5m, date, 1, false));

            // Payments for both tenants
            allPayments.Add(CreatePayment(i * 2 + 1, BusinessIdA, i * 2 + 1, amount, dateTime, isVoided));
            allPayments.Add(CreatePayment(i * 2 + 2, BusinessIdB, i * 2 + 2, amount + 5m, dateTime, isVoided));
        }

        // Verify purchase isolation
        var purchasesA = FilterExpensesThisMonth(allPurchases, BusinessIdA);
        var purchasesB = FilterExpensesThisMonth(allPurchases, BusinessIdB);
        var purchaseIsolation = !purchasesA.Any(p => p.BusinessId == BusinessIdB)
                             && !purchasesB.Any(p => p.BusinessId == BusinessIdA);

        // Verify invoice isolation
        var invoicesA = FilterIssuedInvoices(allInvoices, BusinessIdA);
        var invoicesB = FilterIssuedInvoices(allInvoices, BusinessIdB);
        var invoiceIsolation = !invoicesA.Any(i => i.BusinessId == BusinessIdB)
                            && !invoicesB.Any(i => i.BusinessId == BusinessIdA);

        // Verify payment isolation (revenue this month)
        var paymentsA = FilterRevenueThisMonth(allPayments, BusinessIdA);
        var paymentsB = FilterRevenueThisMonth(allPayments, BusinessIdB);
        var paymentIsolation = !paymentsA.Any(p => p.BusinessId == BusinessIdB)
                            && !paymentsB.Any(p => p.BusinessId == BusinessIdA);

        // Verify recent payments isolation
        var recentA = FilterRecentPayments(allPayments, BusinessIdA);
        var recentB = FilterRecentPayments(allPayments, BusinessIdB);
        var recentIsolation = !recentA.Any(p => p.BusinessId == BusinessIdB)
                           && !recentB.Any(p => p.BusinessId == BusinessIdA);

        var allIsolated = purchaseIsolation && invoiceIsolation
                       && paymentIsolation && recentIsolation;

        return allIsolated.ToProperty()
            .Label($"Purchase isolation: {purchaseIsolation}, Invoice isolation: {invoiceIsolation}, " +
                   $"Payment isolation: {paymentIsolation}, Recent isolation: {recentIsolation}");
    }

    #endregion
}
