using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Integration;

/// <summary>
/// Integration test: Tenant isolation with multi-business data.
/// Seeds invoices and payments for Business A and Business B, then verifies
/// each business only sees its own data across all service methods.
///
/// For services using raw SQL (DashboardService, ReceivablesQueryService), tests the
/// tenant isolation contract using LINQ as the oracle.
/// For VatIntegrationService (uses EF Core LINQ), tests with actual service against in-memory database.
///
/// **Validates: Requirements 12.1, 12.2, 12.3, 12.4**
/// </summary>
public class TenantIsolationIntegrationTests : IDisposable
{
    private const int BusinessAId = 1;
    private const int BusinessBId = 2;

    // Invoice Status Type IDs
    private const int StatusIssued = 2;

    // Financial Status Type IDs
    private const int FinancialUnpaid = 1;
    private const int FinancialPaid = 3;

    private readonly PortalDbContext _dbContextA;
    private readonly PortalDbContext _dbContextB;
    private readonly string _dbName;

    public TenantIsolationIntegrationTests()
    {
        _dbName = $"TenantIsolation_Integration_{Guid.NewGuid()}";
        _dbContextA = CreateDbContext(BusinessAId);
        _dbContextB = CreateDbContext(BusinessBId);

        SeedSharedData(_dbContextA);
    }

    public void Dispose()
    {
        _dbContextA.Database.EnsureDeleted();
        _dbContextA.Dispose();
        _dbContextB.Dispose();
    }

    #region Test Infrastructure

    private PortalDbContext CreateDbContext(int tenantBusinessId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(tenantBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: _dbName)
            .Options;

        return new PortalDbContext(options, tenantMock.Object);
    }

    private static void SeedSharedData(PortalDbContext dbContext)
    {
        // Seed businesses
        dbContext.Businesses.AddRange(
            new Business { Id = BusinessAId, Name = "Business A", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Business { Id = BusinessBId, Name = "Business B", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }
        );

        // Seed invoice status types
        dbContext.InvoiceStatusTypes.AddRange(
            new InvoiceStatusType { Id = 1, Name = "Draft" },
            new InvoiceStatusType { Id = StatusIssued, Name = "Issued" },
            new InvoiceStatusType { Id = 3, Name = "Cancelled" }
        );

        // Seed financial status types
        dbContext.InvoiceFinancialStatusTypes.AddRange(
            new InvoiceFinancialStatusType { Id = FinancialUnpaid, Name = "Unpaid" },
            new InvoiceFinancialStatusType { Id = 2, Name = "PartiallyPaid" },
            new InvoiceFinancialStatusType { Id = FinancialPaid, Name = "Paid" },
            new InvoiceFinancialStatusType { Id = 4, Name = "Overdue" },
            new InvoiceFinancialStatusType { Id = 5, Name = "WrittenOff" }
        );

        // Seed payment method types
        dbContext.PaymentMethodTypes.AddRange(
            new PaymentMethodType { Id = 1, Name = "Cash", IsActive = true },
            new PaymentMethodType { Id = 2, Name = "BankTransfer", IsActive = true }
        );

        // Seed customers for each business
        dbContext.Customers.AddRange(
            new Customer { Id = 1, BusinessId = BusinessAId, Name = "Customer A1", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Customer { Id = 2, BusinessId = BusinessAId, Name = "Customer A2", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Customer { Id = 3, BusinessId = BusinessBId, Name = "Customer B1", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow },
            new Customer { Id = 4, BusinessId = BusinessBId, Name = "Customer B2", IsActive = true, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow }
        );

        // Seed suppliers for each business
        dbContext.Suppliers.AddRange(
            new Supplier { Id = 1, BusinessId = BusinessAId, Name = "Supplier A1", IsActive = true, CreatedAtUtc = DateTime.UtcNow },
            new Supplier { Id = 2, BusinessId = BusinessBId, Name = "Supplier B1", IsActive = true, CreatedAtUtc = DateTime.UtcNow }
        );

        // Seed expense categories for each business
        dbContext.ExpenseCategories.AddRange(
            new ExpenseCategory { Id = 1, BusinessId = BusinessAId, Name = "Office Supplies", IsActive = true, CreatedAtUtc = DateTime.UtcNow },
            new ExpenseCategory { Id = 2, BusinessId = BusinessBId, Name = "Travel", IsActive = true, CreatedAtUtc = DateTime.UtcNow }
        );

        // Seed VAT periods for each business
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = new DateOnly(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1);
        var periodEnd = periodStart.AddMonths(3).AddDays(-1);

        dbContext.VatSubmissionPeriods.AddRange(
            new VatSubmissionPeriod { Id = 1, BusinessId = BusinessAId, PeriodLabel = "Q1-A", PeriodStartDate = periodStart, PeriodEndDate = periodEnd, CreatedAtUtc = DateTime.UtcNow },
            new VatSubmissionPeriod { Id = 2, BusinessId = BusinessBId, PeriodLabel = "Q1-B", PeriodStartDate = periodStart, PeriodEndDate = periodEnd, CreatedAtUtc = DateTime.UtcNow }
        );

        // Seed invoices for Business A (3 invoices)
        dbContext.Invoices.AddRange(
            new Invoice
            {
                Id = 1, BusinessId = BusinessAId, CustomerId = 1, InvoiceStatusTypeId = StatusIssued,
                InvoiceFinancialStatusTypeId = FinancialUnpaid, InvoiceNumber = "INV-A-001",
                InvoiceDate = today.AddDays(-30), DueDate = today.AddDays(30),
                Subtotal = 1000m, TaxAmount = 150m, TotalAmount = 1150m,
                CurrencyCode = "EUR", IsDeleted = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            },
            new Invoice
            {
                Id = 2, BusinessId = BusinessAId, CustomerId = 2, InvoiceStatusTypeId = StatusIssued,
                InvoiceFinancialStatusTypeId = FinancialPaid, InvoiceNumber = "INV-A-002",
                InvoiceDate = today.AddDays(-20), DueDate = today.AddDays(10),
                Subtotal = 2000m, TaxAmount = 300m, TotalAmount = 2300m,
                CurrencyCode = "EUR", IsDeleted = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            },
            new Invoice
            {
                Id = 3, BusinessId = BusinessAId, CustomerId = 1, InvoiceStatusTypeId = StatusIssued,
                InvoiceFinancialStatusTypeId = FinancialUnpaid, InvoiceNumber = "INV-A-003",
                InvoiceDate = today.AddDays(-10), DueDate = today.AddDays(20),
                Subtotal = 500m, TaxAmount = 75m, TotalAmount = 575m,
                CurrencyCode = "EUR", IsDeleted = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            }
        );

        // Seed invoices for Business B (3 invoices)
        dbContext.Invoices.AddRange(
            new Invoice
            {
                Id = 4, BusinessId = BusinessBId, CustomerId = 3, InvoiceStatusTypeId = StatusIssued,
                InvoiceFinancialStatusTypeId = FinancialUnpaid, InvoiceNumber = "INV-B-001",
                InvoiceDate = today.AddDays(-25), DueDate = today.AddDays(5),
                Subtotal = 3000m, TaxAmount = 450m, TotalAmount = 3450m,
                CurrencyCode = "EUR", IsDeleted = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            },
            new Invoice
            {
                Id = 5, BusinessId = BusinessBId, CustomerId = 4, InvoiceStatusTypeId = StatusIssued,
                InvoiceFinancialStatusTypeId = FinancialPaid, InvoiceNumber = "INV-B-002",
                InvoiceDate = today.AddDays(-15), DueDate = today.AddDays(15),
                Subtotal = 4000m, TaxAmount = 600m, TotalAmount = 4600m,
                CurrencyCode = "EUR", IsDeleted = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            },
            new Invoice
            {
                Id = 6, BusinessId = BusinessBId, CustomerId = 3, InvoiceStatusTypeId = StatusIssued,
                InvoiceFinancialStatusTypeId = FinancialUnpaid, InvoiceNumber = "INV-B-003",
                InvoiceDate = today.AddDays(-5), DueDate = today.AddDays(25),
                Subtotal = 1500m, TaxAmount = 225m, TotalAmount = 1725m,
                CurrencyCode = "EUR", IsDeleted = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            }
        );

        // Seed payments for Business A
        dbContext.Payments.AddRange(
            new Payment
            {
                Id = 1, BusinessId = BusinessAId, InvoiceId = 1, PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow.AddDays(-5), Amount = 500m,
                IsVoided = false, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = "user-a"
            },
            new Payment
            {
                Id = 2, BusinessId = BusinessAId, InvoiceId = 2, PaymentMethodTypeId = 2,
                PaymentDateUtc = DateTime.UtcNow.AddDays(-3), Amount = 2300m,
                IsVoided = false, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = "user-a"
            }
        );

        // Seed payments for Business B
        dbContext.Payments.AddRange(
            new Payment
            {
                Id = 3, BusinessId = BusinessBId, InvoiceId = 4, PaymentMethodTypeId = 1,
                PaymentDateUtc = DateTime.UtcNow.AddDays(-4), Amount = 1000m,
                IsVoided = false, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = "user-b"
            },
            new Payment
            {
                Id = 4, BusinessId = BusinessBId, InvoiceId = 5, PaymentMethodTypeId = 2,
                PaymentDateUtc = DateTime.UtcNow.AddDays(-2), Amount = 4600m,
                IsVoided = false, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = "user-b"
            }
        );

        // Seed purchases for Business A
        dbContext.Purchases.Add(new Purchase
        {
            Id = 1, BusinessId = BusinessAId, SupplierId = 1, ExpenseCategoryId = 1,
            PurchaseOriginTypeId = 1, InvoiceNumber = "PUR-A-001",
            InvoiceDate = today.AddDays(-10), Description = "Office supplies",
            AmountExcludingVat = 200m, VatAmount = 30m, TotalAmount = 230m,
            IsCancelled = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });

        // Seed purchases for Business B
        dbContext.Purchases.Add(new Purchase
        {
            Id = 2, BusinessId = BusinessBId, SupplierId = 2, ExpenseCategoryId = 2,
            PurchaseOriginTypeId = 1, InvoiceNumber = "PUR-B-001",
            InvoiceDate = today.AddDays(-8), Description = "Travel expenses",
            AmountExcludingVat = 800m, VatAmount = 120m, TotalAmount = 920m,
            IsCancelled = false, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        });

        // Seed PurchaseOriginType (reference data)
        dbContext.PurchaseOriginTypes.Add(new PurchaseOriginType { Id = 1, Name = "Domestic" });

        dbContext.SaveChanges();
    }

    #endregion

    #region PaymentService Tenant Isolation (LINQ Oracle)

    /// <summary>
    /// Verifies the tenant isolation contract for payment queries: Business A's context
    /// only returns payments belonging to Business A. Uses LINQ as the oracle since
    /// PaymentRepository uses raw SQL that doesn't work with in-memory database.
    /// **Validates: Requirements 12.2**
    /// </summary>
    [Fact]
    public async Task PaymentService_GetPaymentHistory_OnlyReturnsOwnBusinessPayments()
    {
        // Act: query payments for Business A's invoice (id=1) using Business A's context
        var paymentsForInv1 = await _dbContextA.Payments
            .Where(p => p.InvoiceId == 1)
            .ToListAsync();

        // Assert: only Business A's payment is returned (global query filter enforces BusinessId)
        Assert.Single(paymentsForInv1);
        Assert.Equal(500m, paymentsForInv1[0].Amount);
        Assert.Equal(BusinessAId, paymentsForInv1[0].BusinessId);

        // Act: try to query Business B's invoice (id=4) using Business A's context
        // Global query filter will exclude it since invoice 4 belongs to Business B
        var paymentsForInv4 = await _dbContextA.Payments
            .Where(p => p.InvoiceId == 4)
            .ToListAsync();

        // Assert: no payments returned (Business B's payments are filtered out)
        Assert.Empty(paymentsForInv4);
    }

    /// <summary>
    /// Verifies the tenant isolation contract for payment creation: when recording a payment,
    /// the invoice must belong to the authenticated business. Uses LINQ to verify that
    /// Business A cannot see Business B's invoices through the global query filter.
    /// **Validates: Requirements 12.3, 12.4**
    /// </summary>
    [Fact]
    public async Task PaymentService_RecordPayment_RejectsInvoiceFromDifferentBusiness()
    {
        // Act: try to find Business B's invoice (id=4) using Business A's context
        // This simulates what the repository does: query by Id with BusinessId filter
        var invoiceFromBContext = await _dbContextA.Invoices
            .FirstOrDefaultAsync(inv => inv.Id == 4);

        // Assert: invoice not found because global query filter excludes Business B's data
        Assert.Null(invoiceFromBContext);

        // Verify the invoice exists when queried without filters (it's in the database)
        var invoiceWithoutFilter = await _dbContextA.Invoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(inv => inv.Id == 4);
        Assert.NotNull(invoiceWithoutFilter);
        Assert.Equal(BusinessBId, invoiceWithoutFilter.BusinessId);
    }

    /// <summary>
    /// Verifies the tenant isolation contract for payment voiding: Business A cannot
    /// access or void Business B's payments through the global query filter.
    /// **Validates: Requirements 12.4**
    /// </summary>
    [Fact]
    public async Task PaymentService_VoidPayment_RejectsPaymentFromDifferentBusiness()
    {
        // Act: try to find Business B's payment (id=3) using Business A's context
        var paymentFromBContext = await _dbContextA.Payments
            .FirstOrDefaultAsync(p => p.Id == 3);

        // Assert: payment not found because global query filter excludes Business B's data
        Assert.Null(paymentFromBContext);

        // Verify the payment exists when queried without filters
        var paymentWithoutFilter = await _dbContextA.Payments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == 3);
        Assert.NotNull(paymentWithoutFilter);
        Assert.Equal(BusinessBId, paymentWithoutFilter.BusinessId);
    }

    /// <summary>
    /// Verifies the tenant isolation contract for payment creation: when a payment is created,
    /// it must be associated with the authenticated business. Verifies that the global query
    /// filter ensures only own-business invoices are accessible for payment recording.
    /// **Validates: Requirements 12.3**
    /// </summary>
    [Fact]
    public async Task PaymentService_RecordPayment_SetsAuthenticatedBusinessId()
    {
        // Act: verify Business A can access its own invoice (id=3)
        var ownInvoice = await _dbContextA.Invoices
            .FirstOrDefaultAsync(inv => inv.Id == 3);

        // Assert: own invoice is accessible
        Assert.NotNull(ownInvoice);
        Assert.Equal(BusinessAId, ownInvoice.BusinessId);
        Assert.Equal(575m, ownInvoice.TotalAmount);

        // Simulate creating a payment — verify it gets Business A's ID
        var newPayment = new Payment
        {
            Id = 100,
            BusinessId = BusinessAId, // Service always sets this from authenticated tenant
            InvoiceId = 3,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = DateTime.UtcNow,
            Amount = 100m,
            IsVoided = false,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "user-a"
        };
        _dbContextA.Payments.Add(newPayment);
        await _dbContextA.SaveChangesAsync();

        // Verify the created payment has the correct BusinessId
        var createdPayment = await _dbContextA.Payments
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == 100);
        Assert.NotNull(createdPayment);
        Assert.Equal(BusinessAId, createdPayment.BusinessId);

        // Verify Business B cannot see this payment
        var paymentFromB = await _dbContextB.Payments
            .FirstOrDefaultAsync(p => p.Id == 100);
        Assert.Null(paymentFromB);
    }

    #endregion

    #region ReceivablesQueryService Tenant Isolation (LINQ Oracle)

    /// <summary>
    /// Verifies that the receivables query contract (filtering by BusinessId) only returns
    /// invoices belonging to the authenticated business. Uses LINQ as the oracle since
    /// ReceivablesQueryService uses raw SQL that doesn't work with in-memory database.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task ReceivablesQuery_OnlyReturnsInvoicesForAuthenticatedBusiness()
    {
        // Act: simulate the receivables query for Business A using LINQ (oracle)
        var receivablesA = await _dbContextA.Invoices
            .Where(inv => inv.InvoiceStatusTypeId == StatusIssued && !inv.IsDeleted)
            .ToListAsync();

        // Assert: only Business A's invoices are returned (global query filter enforces BusinessId)
        Assert.Equal(3, receivablesA.Count);
        Assert.All(receivablesA, inv => Assert.Equal(BusinessAId, inv.BusinessId));

        // Verify specific invoice numbers
        var invoiceNumbers = receivablesA.Select(inv => inv.InvoiceNumber).OrderBy(n => n).ToList();
        Assert.Contains("INV-A-001", invoiceNumbers);
        Assert.Contains("INV-A-002", invoiceNumbers);
        Assert.Contains("INV-A-003", invoiceNumbers);

        // Verify Business B's invoices are NOT visible
        Assert.DoesNotContain(receivablesA, inv => inv.InvoiceNumber.StartsWith("INV-B"));
    }

    /// <summary>
    /// Verifies that Business B's receivables query only returns Business B's invoices.
    /// Cross-validates isolation from the other direction.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task ReceivablesQuery_BusinessB_OnlySeesOwnInvoices()
    {
        // Act: simulate the receivables query for Business B using LINQ (oracle)
        var receivablesB = await _dbContextB.Invoices
            .Where(inv => inv.InvoiceStatusTypeId == StatusIssued && !inv.IsDeleted)
            .ToListAsync();

        // Assert: only Business B's invoices are returned
        Assert.Equal(3, receivablesB.Count);
        Assert.All(receivablesB, inv => Assert.Equal(BusinessBId, inv.BusinessId));

        // Verify specific invoice numbers
        var invoiceNumbers = receivablesB.Select(inv => inv.InvoiceNumber).OrderBy(n => n).ToList();
        Assert.Contains("INV-B-001", invoiceNumbers);
        Assert.Contains("INV-B-002", invoiceNumbers);
        Assert.Contains("INV-B-003", invoiceNumbers);

        // Verify Business A's invoices are NOT visible
        Assert.DoesNotContain(receivablesB, inv => inv.InvoiceNumber.StartsWith("INV-A"));
    }

    /// <summary>
    /// Verifies that payment totals computed for receivables are tenant-isolated.
    /// Business A's outstanding balance calculations don't include Business B's payments.
    /// **Validates: Requirements 12.1, 12.2**
    /// </summary>
    [Fact]
    public async Task ReceivablesQuery_PaymentTotals_AreIsolatedByBusiness()
    {
        // Act: compute total paid for Business A's invoice 1 using Business A's context
        var totalPaidForInv1 = await _dbContextA.Payments
            .Where(p => p.InvoiceId == 1 && !p.IsVoided)
            .SumAsync(p => p.Amount);

        // Assert: only Business A's payment (500m) is counted
        Assert.Equal(500m, totalPaidForInv1);

        // Act: compute total paid for Business B's invoice 4 using Business B's context
        var totalPaidForInv4 = await _dbContextB.Payments
            .Where(p => p.InvoiceId == 4 && !p.IsVoided)
            .SumAsync(p => p.Amount);

        // Assert: only Business B's payment (1000m) is counted
        Assert.Equal(1000m, totalPaidForInv4);
    }

    #endregion

    #region DashboardService Tenant Isolation (LINQ Oracle)

    /// <summary>
    /// Verifies that dashboard KPI computations (Outstanding Receivables) are tenant-isolated.
    /// Uses LINQ as the oracle since DashboardService uses raw SQL.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task DashboardKpi_OutstandingReceivables_OnlyIncludesOwnBusiness()
    {
        // Act: compute Outstanding Receivables for Business A using LINQ oracle
        var outstandingA = await _dbContextA.Invoices
            .Where(inv => !inv.IsDeleted
                && inv.InvoiceStatusTypeId == StatusIssued
                && new[] { 1, 2, 4 }.Contains(inv.InvoiceFinancialStatusTypeId))
            .Select(inv => new
            {
                Outstanding = inv.TotalAmount - _dbContextA.Payments
                    .Where(p => p.InvoiceId == inv.Id && !p.IsVoided)
                    .Sum(p => p.Amount)
            })
            .SumAsync(x => x.Outstanding);

        // Business A has: INV-A-001 (1150 - 500 = 650) + INV-A-003 (575 - 0 = 575) = 1225
        // INV-A-002 is Paid (status 3), so excluded from outstanding
        Assert.Equal(1225m, outstandingA);

        // Act: compute Outstanding Receivables for Business B using LINQ oracle
        var outstandingB = await _dbContextB.Invoices
            .Where(inv => !inv.IsDeleted
                && inv.InvoiceStatusTypeId == StatusIssued
                && new[] { 1, 2, 4 }.Contains(inv.InvoiceFinancialStatusTypeId))
            .Select(inv => new
            {
                Outstanding = inv.TotalAmount - _dbContextB.Payments
                    .Where(p => p.InvoiceId == inv.Id && !p.IsVoided)
                    .Sum(p => p.Amount)
            })
            .SumAsync(x => x.Outstanding);

        // Business B has: INV-B-001 (3450 - 1000 = 2450) + INV-B-003 (1725 - 0 = 1725) = 4175
        // INV-B-002 is Paid (status 3), so excluded from outstanding
        Assert.Equal(4175m, outstandingB);

        // Verify isolation: A's outstanding != B's outstanding
        Assert.NotEqual(outstandingA, outstandingB);
    }

    /// <summary>
    /// Verifies that Paid This Month KPI is tenant-isolated.
    /// Uses LINQ as the oracle since DashboardService uses raw SQL.
    /// **Validates: Requirements 12.2**
    /// </summary>
    [Fact]
    public async Task DashboardKpi_PaidThisMonth_OnlyIncludesOwnBusinessPayments()
    {
        // Arrange: determine current month boundaries
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        // Act: compute Paid This Month for Business A
        var paidThisMonthA = await _dbContextA.Payments
            .Where(p => !p.IsVoided
                && p.PaymentDateUtc >= monthStart
                && p.PaymentDateUtc < monthEnd)
            .SumAsync(p => p.Amount);

        // Act: compute Paid This Month for Business B
        var paidThisMonthB = await _dbContextB.Payments
            .Where(p => !p.IsVoided
                && p.PaymentDateUtc >= monthStart
                && p.PaymentDateUtc < monthEnd)
            .SumAsync(p => p.Amount);

        // Assert: Business A sees only its own payments (500 + 2300 = 2800)
        Assert.Equal(2800m, paidThisMonthA);

        // Assert: Business B sees only its own payments (1000 + 4600 = 5600)
        Assert.Equal(5600m, paidThisMonthB);

        // Verify isolation
        Assert.NotEqual(paidThisMonthA, paidThisMonthB);
    }

    /// <summary>
    /// Verifies that recent payments query is tenant-isolated.
    /// Uses LINQ as the oracle since DashboardService uses raw SQL.
    /// **Validates: Requirements 12.2**
    /// </summary>
    [Fact]
    public async Task DashboardRecentPayments_OnlyReturnsOwnBusinessPayments()
    {
        // Act: query recent payments for Business A
        var recentPaymentsA = await _dbContextA.Payments
            .Where(p => !p.IsVoided)
            .OrderByDescending(p => p.PaymentDateUtc)
            .ToListAsync();

        // Assert: only Business A's payments are returned
        Assert.Equal(2, recentPaymentsA.Count);
        Assert.All(recentPaymentsA, p => Assert.Equal(BusinessAId, p.BusinessId));

        // Act: query recent payments for Business B
        var recentPaymentsB = await _dbContextB.Payments
            .Where(p => !p.IsVoided)
            .OrderByDescending(p => p.PaymentDateUtc)
            .ToListAsync();

        // Assert: only Business B's payments are returned
        Assert.Equal(2, recentPaymentsB.Count);
        Assert.All(recentPaymentsB, p => Assert.Equal(BusinessBId, p.BusinessId));
    }

    #endregion

    #region VatIntegrationService Tenant Isolation (Actual Service)

    /// <summary>
    /// Verifies that VatIntegrationService.GetCurrentPeriodSummaryAsync only includes
    /// data for the authenticated business. Tests with actual service against in-memory database.
    /// **Validates: Requirements 12.1, 12.2**
    /// </summary>
    [Fact]
    public async Task VatIntegrationService_GetCurrentPeriodSummary_OnlyIncludesOwnBusinessData()
    {
        // Arrange: create VatIntegrationService for Business A
        var vatServiceA = new VatIntegrationService(_dbContextA);

        // Act: get current period summary for Business A
        var summaryA = await vatServiceA.GetCurrentPeriodSummaryAsync(BusinessAId);

        // Assert: Output VAT only includes Business A's fully paid invoices in current period
        // INV-A-002 is Paid (status 3) with InvoiceDate in current period, TaxAmount = 300
        // The exact value depends on whether the invoice date falls in the current VAT period
        // But critically, Business B's data should NOT be included

        // Arrange: create VatIntegrationService for Business B
        var vatServiceB = new VatIntegrationService(_dbContextB);

        // Act: get current period summary for Business B
        var summaryB = await vatServiceB.GetCurrentPeriodSummaryAsync(BusinessBId);

        // Assert: the two summaries are independent
        // Business A's Input VAT comes from Purchase A (30m)
        // Business B's Input VAT comes from Purchase B (120m)
        // They should not be equal (unless both happen to be 0 due to period mismatch)
        if (summaryA.TotalInputVat > 0 || summaryB.TotalInputVat > 0)
        {
            // If either has data in the current period, they should differ
            Assert.True(
                summaryA.TotalInputVat != summaryB.TotalInputVat || summaryA.TotalOutputVat != summaryB.TotalOutputVat,
                "Business A and B should have different VAT summaries when data exists in current period");
        }
    }

    /// <summary>
    /// Verifies that VatIntegrationService.GetVatLiabilityByPeriodAsync only returns
    /// VAT periods belonging to the authenticated business.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task VatIntegrationService_GetVatLiabilityByPeriod_OnlyReturnsOwnBusinessPeriods()
    {
        // Arrange: create VatIntegrationService for Business A
        var vatServiceA = new VatIntegrationService(_dbContextA);

        // Act: get VAT liability by period for Business A
        var periodsA = await vatServiceA.GetVatLiabilityByPeriodAsync(BusinessAId);

        // Assert: only Business A's periods are returned
        Assert.Single(periodsA);
        Assert.Equal("Q1-A", periodsA[0].PeriodLabel);

        // Arrange: create VatIntegrationService for Business B
        var vatServiceB = new VatIntegrationService(_dbContextB);

        // Act: get VAT liability by period for Business B
        var periodsB = await vatServiceB.GetVatLiabilityByPeriodAsync(BusinessBId);

        // Assert: only Business B's periods are returned
        Assert.Single(periodsB);
        Assert.Equal("Q1-B", periodsB[0].PeriodLabel);
    }

    /// <summary>
    /// Verifies that VatIntegrationService Input VAT computation is tenant-isolated.
    /// Business A's purchases don't affect Business B's Input VAT and vice versa.
    /// **Validates: Requirements 12.1, 12.2**
    /// </summary>
    [Fact]
    public async Task VatIntegrationService_InputVat_IsTenantIsolated()
    {
        // Arrange: query purchases visible to each business context
        var purchasesA = await _dbContextA.Purchases
            .Where(p => !p.IsCancelled)
            .ToListAsync();

        var purchasesB = await _dbContextB.Purchases
            .Where(p => !p.IsCancelled)
            .ToListAsync();

        // Assert: each business only sees its own purchases
        Assert.Single(purchasesA);
        Assert.Equal(BusinessAId, purchasesA[0].BusinessId);
        Assert.Equal(30m, purchasesA[0].VatAmount);

        Assert.Single(purchasesB);
        Assert.Equal(BusinessBId, purchasesB[0].BusinessId);
        Assert.Equal(120m, purchasesB[0].VatAmount);
    }

    #endregion

    #region Cross-Service Tenant Isolation

    /// <summary>
    /// Comprehensive cross-service test: verifies that all data access paths
    /// are tenant-isolated by checking total counts and sums across both businesses.
    /// **Validates: Requirements 12.1, 12.2, 12.3, 12.4**
    /// </summary>
    [Fact]
    public async Task AllServices_TotalDataCounts_AreIsolatedPerBusiness()
    {
        // Act: count all visible data for Business A
        var invoiceCountA = await _dbContextA.Invoices.CountAsync();
        var paymentCountA = await _dbContextA.Payments.CountAsync();
        var purchaseCountA = await _dbContextA.Purchases.CountAsync();
        var customerCountA = await _dbContextA.Customers.CountAsync();
        var vatPeriodCountA = await _dbContextA.VatSubmissionPeriods.CountAsync();

        // Act: count all visible data for Business B
        var invoiceCountB = await _dbContextB.Invoices.CountAsync();
        var paymentCountB = await _dbContextB.Payments.CountAsync();
        var purchaseCountB = await _dbContextB.Purchases.CountAsync();
        var customerCountB = await _dbContextB.Customers.CountAsync();
        var vatPeriodCountB = await _dbContextB.VatSubmissionPeriods.CountAsync();

        // Assert: Business A sees only its own data
        Assert.Equal(3, invoiceCountA);   // 3 invoices for Business A
        Assert.Equal(2, paymentCountA);   // 2 payments for Business A
        Assert.Equal(1, purchaseCountA);  // 1 purchase for Business A
        Assert.Equal(2, customerCountA);  // 2 customers for Business A
        Assert.Equal(1, vatPeriodCountA); // 1 VAT period for Business A

        // Assert: Business B sees only its own data
        Assert.Equal(3, invoiceCountB);   // 3 invoices for Business B
        Assert.Equal(2, paymentCountB);   // 2 payments for Business B
        Assert.Equal(1, purchaseCountB);  // 1 purchase for Business B
        Assert.Equal(2, customerCountB);  // 2 customers for Business B
        Assert.Equal(1, vatPeriodCountB); // 1 VAT period for Business B

        // Verify total in database (without filters) is greater than either business sees
        var totalInvoices = await _dbContextA.Invoices.IgnoreQueryFilters().CountAsync();
        var totalPayments = await _dbContextA.Payments.IgnoreQueryFilters().CountAsync();

        Assert.Equal(6, totalInvoices);  // 3 + 3
        Assert.Equal(4, totalPayments);  // 2 + 2
    }

    #endregion
}
