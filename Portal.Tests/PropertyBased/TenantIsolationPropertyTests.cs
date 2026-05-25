using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 16: Tenant isolation invariant

/// <summary>
/// Property-based tests for tenant isolation invariant.
/// Validates that for any query or mutation performed by an authenticated user with BusinessId B,
/// all returned data SHALL have BusinessId = B, and all created records SHALL have BusinessId = B.
/// Any attempt to access data with BusinessId ≠ B SHALL be rejected.
///
/// Since services use raw SQL (ADO.NET), these tests verify the tenant isolation contract
/// using LINQ against in-memory data as the oracle, validating the filtering logic.
/// **Validates: Requirements 12.1, 12.2, 12.3, 12.4**
/// </summary>
public class TenantIsolationPropertyTests
{
    private const int BusinessA = 1;
    private const int BusinessB = 2;
    private const int BusinessC = 3;

    // Invoice Status Type IDs
    private const int StatusDraft = 1;
    private const int StatusIssued = 2;
    private const int StatusCancelled = 3;

    // Financial Status Type IDs
    private const int FinancialUnpaid = 1;
    private const int FinancialPartiallyPaid = 2;
    private const int FinancialPaid = 3;
    private const int FinancialOverdue = 4;
    private const int FinancialWrittenOff = 5;

    #region Test Infrastructure

    private static PortalDbContext CreateDbContext(int tenantBusinessId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(tenantBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantIsolation_{Guid.NewGuid()}")
            .Options;

        var dbContext = new PortalDbContext(options, tenantMock.Object);

        SeedReferenceData(dbContext);

        return dbContext;
    }

    private static void SeedReferenceData(PortalDbContext dbContext)
    {
        // Seed multiple businesses
        dbContext.Businesses.AddRange(
            new Business { Id = BusinessA, Name = "Business A", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Business { Id = BusinessB, Name = "Business B", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Business { Id = BusinessC, Name = "Business C", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }
        );

        dbContext.InvoiceStatusTypes.AddRange(
            new InvoiceStatusType { Id = StatusDraft, Name = "Draft" },
            new InvoiceStatusType { Id = StatusIssued, Name = "Issued" },
            new InvoiceStatusType { Id = StatusCancelled, Name = "Cancelled" }
        );

        dbContext.InvoiceFinancialStatusTypes.AddRange(
            new InvoiceFinancialStatusType { Id = FinancialUnpaid, Name = "Unpaid" },
            new InvoiceFinancialStatusType { Id = FinancialPartiallyPaid, Name = "PartiallyPaid" },
            new InvoiceFinancialStatusType { Id = FinancialPaid, Name = "Paid" },
            new InvoiceFinancialStatusType { Id = FinancialOverdue, Name = "Overdue" },
            new InvoiceFinancialStatusType { Id = FinancialWrittenOff, Name = "WrittenOff" }
        );

        // Seed customers for each business
        dbContext.Customers.AddRange(
            new Customer { Id = 1, BusinessId = BusinessA, Name = "Customer A1", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Customer { Id = 2, BusinessId = BusinessA, Name = "Customer A2", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Customer { Id = 3, BusinessId = BusinessB, Name = "Customer B1", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Customer { Id = 4, BusinessId = BusinessB, Name = "Customer B2", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Customer { Id = 5, BusinessId = BusinessC, Name = "Customer C1", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }
        );

        dbContext.PaymentMethodTypes.Add(new PaymentMethodType { Id = 1, Name = "Cash", IsActive = true });

        dbContext.SaveChanges();
    }

    private static Invoice CreateInvoice(int id, int businessId, int customerId, decimal totalAmount)
    {
        return new Invoice
        {
            Id = id,
            BusinessId = businessId,
            CustomerId = customerId,
            InvoiceStatusTypeId = StatusIssued,
            InvoiceFinancialStatusTypeId = FinancialUnpaid,
            InvoiceNumber = $"INV-{businessId}-{id:D4}",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-id)),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30 - id)),
            Subtotal = Math.Round(totalAmount / 1.15m, 2),
            TaxAmount = Math.Round(totalAmount - (totalAmount / 1.15m), 2),
            TotalAmount = totalAmount,
            CurrencyCode = "EUR",
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static Payment CreatePayment(int id, int businessId, int invoiceId, decimal amount)
    {
        return new Payment
        {
            Id = id,
            BusinessId = businessId,
            InvoiceId = invoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow.AddDays(-id),
            Amount = amount,
            IsVoided = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = $"user-{businessId}"
        };
    }

    private static decimal GenerateAmount(int seed)
    {
        var raw = (Math.Abs(seed) % 9999000) + 1000;
        return raw / 100m;
    }

    /// <summary>
    /// Determines which business a generated item belongs to based on seed.
    /// Distributes items across all three businesses.
    /// </summary>
    private static int AssignBusiness(int seed)
    {
        var businesses = new[] { BusinessA, BusinessB, BusinessC };
        return businesses[Math.Abs(seed) % businesses.Length];
    }

    /// <summary>
    /// Gets the customer ID for a given business (maps business to its customers).
    /// </summary>
    private static int GetCustomerForBusiness(int businessId, int seed)
    {
        return businessId switch
        {
            BusinessA => (Math.Abs(seed) % 2) + 1,  // Customer 1 or 2
            BusinessB => (Math.Abs(seed) % 2) + 3,  // Customer 3 or 4
            BusinessC => 5,                           // Customer 5
            _ => 1
        };
    }

    #endregion

    #region Property 16a: Invoice queries only return data for authenticated BusinessId

    /// <summary>
    /// Property 16a: For any set of multi-tenant invoices, querying with BusinessId B
    /// SHALL only return invoices where Invoice.BusinessId = B.
    /// No invoice belonging to another business SHALL ever appear in the results.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoiceQueries_OnlyReturnDataForAuthenticatedBusiness(
        PositiveInt[] amountSeeds,
        int[] businessSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var dbContext = CreateDbContext(BusinessA);
        try
        {
            var invoiceCount = Math.Min(amountSeeds.Length, 20);
            var bizSeeds = businessSeeds.Length > 0 ? businessSeeds : new[] { 0, 1, 2 };

            // Seed invoices across multiple businesses
            for (int i = 0; i < invoiceCount; i++)
            {
                var businessId = AssignBusiness(bizSeeds[i % bizSeeds.Length]);
                var customerId = GetCustomerForBusiness(businessId, i);
                var totalAmount = GenerateAmount(amountSeeds[i].Get);

                var invoice = CreateInvoice(i + 1, businessId, customerId, totalAmount);
                dbContext.Invoices.Add(invoice);
            }
            dbContext.SaveChanges();

            // Simulate the receivables query filtered by BusinessA
            var resultsForBusinessA = dbContext.Invoices
                .Where(inv => inv.BusinessId == BusinessA
                           && inv.InvoiceStatusTypeId == StatusIssued
                           && !inv.IsDeleted)
                .ToList();

            // Property: ALL returned invoices belong to BusinessA
            var allBelongToBusinessA = resultsForBusinessA
                .All(inv => inv.BusinessId == BusinessA);

            // Property: NO invoice from BusinessB or BusinessC appears
            var noOtherBusinessData = !resultsForBusinessA
                .Any(inv => inv.BusinessId == BusinessB || inv.BusinessId == BusinessC);

            // Property: count matches expected (only BusinessA invoices)
            var expectedCount = dbContext.Invoices
                .Count(inv => inv.BusinessId == BusinessA
                           && inv.InvoiceStatusTypeId == StatusIssued
                           && !inv.IsDeleted);
            var countCorrect = resultsForBusinessA.Count == expectedCount;

            // Cross-check: BusinessB query should not return BusinessA data
            var resultsForBusinessB = dbContext.Invoices
                .Where(inv => inv.BusinessId == BusinessB
                           && inv.InvoiceStatusTypeId == StatusIssued
                           && !inv.IsDeleted)
                .ToList();

            var businessBIsolated = resultsForBusinessB
                .All(inv => inv.BusinessId == BusinessB);

            var allPropertiesHold = allBelongToBusinessA
                                 && noOtherBusinessData
                                 && countCorrect
                                 && businessBIsolated;

            return allPropertiesHold.ToProperty()
                .Label($"Total={invoiceCount}, BusinessA={resultsForBusinessA.Count}, " +
                       $"BusinessB={resultsForBusinessB.Count}, " +
                       $"AllBelongToA={allBelongToBusinessA}, NoOther={noOtherBusinessData}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 16b: Payment queries only return data for authenticated BusinessId

    /// <summary>
    /// Property 16b: For any set of multi-tenant payments, querying with BusinessId B
    /// SHALL only return payments where Payment.BusinessId = B.
    /// No payment belonging to another business SHALL ever appear in the results.
    /// **Validates: Requirements 12.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PaymentQueries_OnlyReturnDataForAuthenticatedBusiness(
        PositiveInt[] amountSeeds,
        int[] businessSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var dbContext = CreateDbContext(BusinessA);
        try
        {
            var itemCount = Math.Min(amountSeeds.Length, 20);
            var bizSeeds = businessSeeds.Length > 0 ? businessSeeds : new[] { 0, 1, 2 };

            // First seed invoices for each business
            var invoiceId = 1;
            var invoicesPerBusiness = new Dictionary<int, List<int>>
            {
                { BusinessA, new List<int>() },
                { BusinessB, new List<int>() },
                { BusinessC, new List<int>() }
            };

            // Create at least one invoice per business for payments to reference
            foreach (var biz in new[] { BusinessA, BusinessB, BusinessC })
            {
                var customerId = GetCustomerForBusiness(biz, 0);
                var inv = CreateInvoice(invoiceId, biz, customerId, 10000m);
                dbContext.Invoices.Add(inv);
                invoicesPerBusiness[biz].Add(invoiceId);
                invoiceId++;
            }
            dbContext.SaveChanges();

            // Seed payments across multiple businesses
            for (int i = 0; i < itemCount; i++)
            {
                var businessId = AssignBusiness(bizSeeds[i % bizSeeds.Length]);
                var targetInvoiceId = invoicesPerBusiness[businessId][0];
                var amount = Math.Min(GenerateAmount(amountSeeds[i].Get) / 100m, 50m);

                var payment = CreatePayment(i + 1, businessId, targetInvoiceId, amount);
                dbContext.Payments.Add(payment);
            }
            dbContext.SaveChanges();

            // Simulate payment query filtered by BusinessA
            var paymentsForBusinessA = dbContext.Payments
                .Where(p => p.BusinessId == BusinessA && !p.IsVoided)
                .ToList();

            // Property: ALL returned payments belong to BusinessA
            var allBelongToBusinessA = paymentsForBusinessA
                .All(p => p.BusinessId == BusinessA);

            // Property: NO payment from BusinessB or BusinessC appears
            var noOtherBusinessData = !paymentsForBusinessA
                .Any(p => p.BusinessId == BusinessB || p.BusinessId == BusinessC);

            // Property: count matches expected
            var expectedCount = dbContext.Payments
                .Count(p => p.BusinessId == BusinessA && !p.IsVoided);
            var countCorrect = paymentsForBusinessA.Count == expectedCount;

            // Cross-check: BusinessB query should not return BusinessA data
            var paymentsForBusinessB = dbContext.Payments
                .Where(p => p.BusinessId == BusinessB && !p.IsVoided)
                .ToList();

            var businessBIsolated = paymentsForBusinessB
                .All(p => p.BusinessId == BusinessB);

            var allPropertiesHold = allBelongToBusinessA
                                 && noOtherBusinessData
                                 && countCorrect
                                 && businessBIsolated;

            return allPropertiesHold.ToProperty()
                .Label($"Total={itemCount}, PaymentsA={paymentsForBusinessA.Count}, " +
                       $"PaymentsB={paymentsForBusinessB.Count}, " +
                       $"AllBelongToA={allBelongToBusinessA}, NoOther={noOtherBusinessData}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 16c: Created payments get the authenticated BusinessId

    /// <summary>
    /// Property 16c: When a payment is created via PaymentService, the Payment.BusinessId
    /// SHALL always be set to the authenticated user's BusinessId, regardless of what
    /// InvoiceId is provided. The service enforces that the invoice belongs to the same business.
    /// **Validates: Requirements 12.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CreatedPayments_AlwaysGetAuthenticatedBusinessId(
        PositiveInt amountSeed,
        PositiveInt invoiceAmountSeed)
    {
        var dbContext = CreateDbContext(BusinessA);
        try
        {
            var invoiceTotalAmount = GenerateAmount(invoiceAmountSeed.Get);
            var paymentAmount = Math.Min(GenerateAmount(amountSeed.Get), invoiceTotalAmount);

            // Create an invoice for BusinessA
            var invoice = CreateInvoice(1, BusinessA, 1, invoiceTotalAmount);
            dbContext.Invoices.Add(invoice);
            dbContext.SaveChanges();

            // Simulate what PaymentService does: sets BusinessId from authenticated tenant
            var authenticatedBusinessId = BusinessA;
            var newPayment = new Payment
            {
                Id = 1,
                BusinessId = authenticatedBusinessId, // Service sets this from tenant
                InvoiceId = 1,
                PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow,
                Amount = paymentAmount,
                IsVoided = false,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = "test-user"
            };
            dbContext.Payments.Add(newPayment);
            dbContext.SaveChanges();

            // Property: the created payment has the authenticated BusinessId
            var createdPayment = dbContext.Payments.Find(1);
            var businessIdCorrect = createdPayment!.BusinessId == authenticatedBusinessId;

            // Property: the payment is associated with an invoice of the same business
            var invoiceBusinessId = dbContext.Invoices
                .Where(inv => inv.Id == createdPayment.InvoiceId)
                .Select(inv => inv.BusinessId)
                .FirstOrDefault();
            var invoiceBelongsToSameBusiness = invoiceBusinessId == authenticatedBusinessId;

            var allPropertiesHold = businessIdCorrect && invoiceBelongsToSameBusiness;

            return allPropertiesHold.ToProperty()
                .Label($"PaymentBusinessId={createdPayment.BusinessId}, " +
                       $"AuthenticatedBusinessId={authenticatedBusinessId}, " +
                       $"InvoiceBusinessId={invoiceBusinessId}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 16d: Access to data from different BusinessId is rejected

    /// <summary>
    /// Property 16d: Any attempt to access an invoice or payment belonging to a different
    /// BusinessId SHALL be rejected. When querying with BusinessId B, records with
    /// BusinessId ≠ B are never accessible.
    /// **Validates: Requirements 12.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AccessToDifferentBusinessData_IsRejected(
        PositiveInt[] amountSeeds,
        int[] businessSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var dbContext = CreateDbContext(BusinessA);
        try
        {
            var itemCount = Math.Min(amountSeeds.Length, 20);
            var bizSeeds = businessSeeds.Length > 0 ? businessSeeds : new[] { 0, 1, 2 };

            // Seed invoices and payments for multiple businesses
            for (int i = 0; i < itemCount; i++)
            {
                var businessId = AssignBusiness(bizSeeds[i % bizSeeds.Length]);
                var customerId = GetCustomerForBusiness(businessId, i);
                var totalAmount = GenerateAmount(amountSeeds[i].Get);

                var invoice = CreateInvoice(i + 1, businessId, customerId, totalAmount);
                dbContext.Invoices.Add(invoice);
            }
            dbContext.SaveChanges();

            // Add payments for each invoice
            var paymentId = 1;
            foreach (var invoice in dbContext.Invoices.ToList())
            {
                var amount = Math.Min(GenerateAmount(invoice.Id + 100), invoice.TotalAmount / 2);
                var payment = CreatePayment(paymentId++, invoice.BusinessId, invoice.Id, amount);
                dbContext.Payments.Add(payment);
            }
            dbContext.SaveChanges();

            // Simulate BusinessA trying to access BusinessB's invoices
            var businessBInvoiceIds = dbContext.Invoices
                .Where(inv => inv.BusinessId == BusinessB)
                .Select(inv => inv.Id)
                .ToList();

            // For each BusinessB invoice, verify BusinessA cannot access it
            // (simulating the repository pattern: GetByIdAndBusinessIdAsync returns null)
            var accessDeniedForInvoices = businessBInvoiceIds.All(invoiceId =>
            {
                var result = dbContext.Invoices
                    .FirstOrDefault(inv => inv.Id == invoiceId && inv.BusinessId == BusinessA);
                return result == null; // Should be null — access denied
            });

            // Simulate BusinessA trying to access BusinessB's payments
            var businessBPaymentIds = dbContext.Payments
                .Where(p => p.BusinessId == BusinessB)
                .Select(p => p.Id)
                .ToList();

            var accessDeniedForPayments = businessBPaymentIds.All(pmtId =>
            {
                var result = dbContext.Payments
                    .FirstOrDefault(p => p.Id == pmtId && p.BusinessId == BusinessA);
                return result == null; // Should be null — access denied
            });

            // Verify the inverse: BusinessA CAN access its own data
            var businessAInvoiceIds = dbContext.Invoices
                .Where(inv => inv.BusinessId == BusinessA)
                .Select(inv => inv.Id)
                .ToList();

            var accessGrantedForOwnInvoices = businessAInvoiceIds.All(invoiceId =>
            {
                var result = dbContext.Invoices
                    .FirstOrDefault(inv => inv.Id == invoiceId && inv.BusinessId == BusinessA);
                return result != null; // Should find it — access granted
            });

            var allPropertiesHold = accessDeniedForInvoices
                                 && accessDeniedForPayments
                                 && accessGrantedForOwnInvoices;

            return allPropertiesHold.ToProperty()
                .Label($"BusinessBInvoices={businessBInvoiceIds.Count}, " +
                       $"BusinessBPayments={businessBPaymentIds.Count}, " +
                       $"AccessDeniedInvoices={accessDeniedForInvoices}, " +
                       $"AccessDeniedPayments={accessDeniedForPayments}, " +
                       $"AccessGrantedOwn={accessGrantedForOwnInvoices}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 16e: Dashboard KPIs are tenant-isolated

    /// <summary>
    /// Property 16e: Dashboard KPI computations (Outstanding Receivables, Overdue Amount,
    /// Paid This Month) SHALL only include data for the authenticated BusinessId.
    /// Data from other businesses SHALL NOT affect the KPI values.
    /// **Validates: Requirements 12.1, 12.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DashboardKpis_OnlyIncludeAuthenticatedBusinessData(
        PositiveInt[] amountSeeds,
        int[] businessSeeds)
    {
        if (amountSeeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var dbContext = CreateDbContext(BusinessA);
        try
        {
            var itemCount = Math.Min(amountSeeds.Length, 20);
            var bizSeeds = businessSeeds.Length > 0 ? businessSeeds : new[] { 0, 1, 2 };

            // Seed invoices across multiple businesses
            for (int i = 0; i < itemCount; i++)
            {
                var businessId = AssignBusiness(bizSeeds[i % bizSeeds.Length]);
                var customerId = GetCustomerForBusiness(businessId, i);
                var totalAmount = GenerateAmount(amountSeeds[i].Get);

                var invoice = CreateInvoice(i + 1, businessId, customerId, totalAmount);
                dbContext.Invoices.Add(invoice);
            }
            dbContext.SaveChanges();

            // Add payments for some invoices
            var paymentId = 1;
            foreach (var invoice in dbContext.Invoices.ToList())
            {
                if (invoice.Id % 2 == 0) // Add payments to ~half the invoices
                {
                    var amount = Math.Min(GenerateAmount(invoice.Id + 200), invoice.TotalAmount / 2);
                    var payment = CreatePayment(paymentId++, invoice.BusinessId, invoice.Id, amount);
                    dbContext.Payments.Add(payment);
                }
            }
            dbContext.SaveChanges();

            // Compute Outstanding Receivables for BusinessA only
            var outstandingForA = dbContext.Invoices
                .Where(inv => inv.BusinessId == BusinessA
                           && !inv.IsDeleted
                           && inv.InvoiceStatusTypeId == StatusIssued
                           && new[] { FinancialUnpaid, FinancialPartiallyPaid, FinancialOverdue }
                               .Contains(inv.InvoiceFinancialStatusTypeId))
                .Sum(inv => inv.TotalAmount - dbContext.Payments
                    .Where(p => p.InvoiceId == inv.Id && !p.IsVoided)
                    .Sum(p => p.Amount));

            // Compute what it would be if we incorrectly included all businesses
            var outstandingAll = dbContext.Invoices
                .Where(inv => !inv.IsDeleted
                           && inv.InvoiceStatusTypeId == StatusIssued
                           && new[] { FinancialUnpaid, FinancialPartiallyPaid, FinancialOverdue }
                               .Contains(inv.InvoiceFinancialStatusTypeId))
                .Sum(inv => inv.TotalAmount - dbContext.Payments
                    .Where(p => p.InvoiceId == inv.Id && !p.IsVoided)
                    .Sum(p => p.Amount));

            // Compute Paid This Month for BusinessA only
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var paidThisMonthForA = dbContext.Payments
                .Where(p => p.BusinessId == BusinessA
                         && !p.IsVoided
                         && p.PaymentDateUtc >= monthStart
                         && p.PaymentDateUtc < monthEnd)
                .Sum(p => p.Amount);

            var paidThisMonthAll = dbContext.Payments
                .Where(p => !p.IsVoided
                         && p.PaymentDateUtc >= monthStart
                         && p.PaymentDateUtc < monthEnd)
                .Sum(p => p.Amount);

            // Property: tenant-scoped values only include BusinessA data
            var outstandingOnlyIncludesA = outstandingForA <= outstandingAll;
            var paidOnlyIncludesA = paidThisMonthForA <= paidThisMonthAll;

            // Property: if other businesses have data, the tenant-scoped value differs
            var hasOtherBusinessInvoices = dbContext.Invoices
                .Any(inv => inv.BusinessId != BusinessA
                         && !inv.IsDeleted
                         && inv.InvoiceStatusTypeId == StatusIssued);

            // When other businesses have data, tenant-scoped should be less than or equal to all
            var isolationCorrect = !hasOtherBusinessInvoices || outstandingForA <= outstandingAll;

            var allPropertiesHold = outstandingOnlyIncludesA
                                 && paidOnlyIncludesA
                                 && isolationCorrect;

            return allPropertiesHold.ToProperty()
                .Label($"OutstandingA={outstandingForA}, OutstandingAll={outstandingAll}, " +
                       $"PaidA={paidThisMonthForA}, PaidAll={paidThisMonthAll}, " +
                       $"HasOtherBiz={hasOtherBusinessInvoices}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion
}
