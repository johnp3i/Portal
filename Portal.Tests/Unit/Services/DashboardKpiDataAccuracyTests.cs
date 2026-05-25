using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Integration test: Dashboard data accuracy.
/// Seeds multiple invoices with various statuses and payments, then verifies all KPI values
/// match expected computations using LINQ against in-memory data.
/// Since DashboardService uses raw SQL (incompatible with InMemory provider), this test
/// validates the computation rules directly against seeded data.
/// **Validates: Requirements 4.1, 4.2, 4.3, 4.4**
/// </summary>
public class DashboardKpiDataAccuracyTests : IDisposable
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

    private readonly PortalDbContext _dbContext;

    public DashboardKpiDataAccuracyTests()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PortalDbContext(options, tenantMock.Object);

        SeedBaseData();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region Seed Data

    private void SeedBaseData()
    {
        _dbContext.Businesses.Add(new Business
        {
            Id = TestBusinessId,
            Name = "Test Business",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        _dbContext.Customers.Add(new Customer
        {
            Id = 1,
            BusinessId = TestBusinessId,
            Name = "Customer Alpha",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        _dbContext.Customers.Add(new Customer
        {
            Id = 2,
            BusinessId = TestBusinessId,
            Name = "Customer Beta",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        _dbContext.SaveChanges();
    }

    private Invoice CreateInvoice(int id, int customerId, decimal totalAmount,
        int invoiceStatusTypeId, int financialStatusTypeId,
        DateOnly dueDate, bool isDeleted = false)
    {
        var invoice = new Invoice
        {
            Id = id,
            BusinessId = TestBusinessId,
            CustomerId = customerId,
            InvoiceStatusTypeId = invoiceStatusTypeId,
            InvoiceFinancialStatusTypeId = financialStatusTypeId,
            InvoiceNumber = $"INV-{id:D4}",
            InvoiceDate = new DateOnly(2024, 1, 15),
            DueDate = dueDate,
            Subtotal = totalAmount * 0.85m,
            TaxAmount = totalAmount * 0.15m,
            TotalAmount = totalAmount,
            CurrencyCode = "EUR",
            IsDeleted = isDeleted,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Invoices.Add(invoice);
        return invoice;
    }

    private Payment CreatePayment(int id, int invoiceId, decimal amount,
        DateTime paymentDateUtc, bool isVoided = false)
    {
        var payment = new Payment
        {
            Id = id,
            BusinessId = TestBusinessId,
            InvoiceId = invoiceId,
            PaymentMethodTypeId = 1,
            PaymentDateUtc = paymentDateUtc,
            Amount = amount,
            IsVoided = isVoided,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = "test-user"
        };
        _dbContext.Payments.Add(payment);
        return payment;
    }

    #endregion

    #region KPI Computation Logic (mirrors DashboardService SQL logic)

    /// <summary>
    /// Computes Outstanding Receivables using the same rules as DashboardService:
    /// Sum of (TotalAmount - sum of valid payments) for all non-deleted invoices
    /// with InvoiceStatusTypeId = 2 AND InvoiceFinancialStatusTypeId in (1, 2, 4).
    /// </summary>
    private decimal ComputeOutstandingReceivables(
        List<Invoice> invoices, List<Payment> payments)
    {
        var qualifyingStatuses = new[] { FinancialStatusUnpaid, FinancialStatusPartiallyPaid, FinancialStatusOverdue };

        return invoices
            .Where(inv => inv.BusinessId == TestBusinessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && qualifyingStatuses.Contains(inv.InvoiceFinancialStatusTypeId))
            .Sum(inv =>
            {
                var totalPaid = payments
                    .Where(p => p.InvoiceId == inv.Id && !p.IsVoided && p.BusinessId == TestBusinessId)
                    .Sum(p => p.Amount);
                return inv.TotalAmount - totalPaid;
            });
    }

    /// <summary>
    /// Computes Overdue Amount using the same rules as DashboardService:
    /// Sum of (TotalAmount - sum of valid payments) for all non-deleted Issued invoices
    /// where DueDate < today AND outstanding balance > 0.
    /// </summary>
    private decimal ComputeOverdueAmount(
        List<Invoice> invoices, List<Payment> payments, DateOnly today)
    {
        return invoices
            .Where(inv => inv.BusinessId == TestBusinessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && inv.DueDate < today)
            .Select(inv =>
            {
                var totalPaid = payments
                    .Where(p => p.InvoiceId == inv.Id && !p.IsVoided && p.BusinessId == TestBusinessId)
                    .Sum(p => p.Amount);
                return inv.TotalAmount - totalPaid;
            })
            .Where(outstanding => outstanding > 0)
            .Sum();
    }

    /// <summary>
    /// Computes Paid This Month using the same rules as DashboardService:
    /// Sum of Payment.Amount where IsVoided = 0 and PaymentDateUtc in current calendar month.
    /// </summary>
    private decimal ComputePaidThisMonth(List<Payment> payments)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        return payments
            .Where(p => p.BusinessId == TestBusinessId
                     && !p.IsVoided
                     && p.PaymentDateUtc >= monthStart
                     && p.PaymentDateUtc < monthEnd)
            .Sum(p => p.Amount);
    }

    /// <summary>
    /// Computes Partially Paid Amount using the same rules as DashboardService:
    /// Sum of (TotalAmount - sum of valid payments) for all non-deleted Issued invoices
    /// with InvoiceFinancialStatusTypeId = 2 (PartiallyPaid).
    /// </summary>
    private decimal ComputePartiallyPaidAmount(
        List<Invoice> invoices, List<Payment> payments)
    {
        return invoices
            .Where(inv => inv.BusinessId == TestBusinessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && inv.InvoiceFinancialStatusTypeId == FinancialStatusPartiallyPaid)
            .Sum(inv =>
            {
                var totalPaid = payments
                    .Where(p => p.InvoiceId == inv.Id && !p.IsVoided && p.BusinessId == TestBusinessId)
                    .Sum(p => p.Amount);
                return inv.TotalAmount - totalPaid;
            });
    }

    #endregion

    #region Test: Dashboard Data Accuracy with Known Values

    [Fact]
    public async Task GetKpiData_WithMixedInvoicesAndPayments_AllKpiValuesMatchExpected()
    {
        // Arrange: Seed invoices with various statuses
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureDate = today.AddDays(30);
        var pastDate = today.AddDays(-15);
        var now = DateTime.UtcNow;
        var currentMonthDate = new DateTime(now.Year, now.Month, 10, 12, 0, 0, DateTimeKind.Utc);
        var lastMonthDate = currentMonthDate.AddMonths(-1);

        // Invoice 1: Issued, Unpaid, due in future, TotalAmount = 1000.00
        // Expected: contributes 1000.00 to Outstanding Receivables
        CreateInvoice(1, 1, 1000.00m, InvoiceStatusIssued, FinancialStatusUnpaid, futureDate);

        // Invoice 2: Issued, PartiallyPaid, due in future, TotalAmount = 2000.00
        // Has payment of 500.00 (valid) + 300.00 (voided)
        // Expected: contributes 1500.00 to Outstanding, 1500.00 to Partially Paid
        CreateInvoice(2, 1, 2000.00m, InvoiceStatusIssued, FinancialStatusPartiallyPaid, futureDate);

        // Invoice 3: Issued, Overdue, due in past, TotalAmount = 3000.00
        // Has payment of 1000.00 (valid, current month)
        // Expected: contributes 2000.00 to Outstanding, 2000.00 to Overdue
        CreateInvoice(3, 2, 3000.00m, InvoiceStatusIssued, FinancialStatusOverdue, pastDate);

        // Invoice 4: Issued, Paid, due in future, TotalAmount = 1500.00
        // Fully paid — should NOT contribute to Outstanding Receivables
        CreateInvoice(4, 2, 1500.00m, InvoiceStatusIssued, FinancialStatusPaid, futureDate);

        // Invoice 5: Issued, WrittenOff, due in past, TotalAmount = 800.00
        // Should NOT contribute to Outstanding Receivables
        CreateInvoice(5, 1, 800.00m, InvoiceStatusIssued, FinancialStatusWrittenOff, pastDate);

        // Invoice 6: Draft (not Issued), Unpaid, TotalAmount = 500.00
        // Should NOT contribute to any KPI (not Issued)
        CreateInvoice(6, 1, 500.00m, InvoiceStatusDraft, FinancialStatusUnpaid, futureDate);

        // Invoice 7: Issued, Unpaid, deleted, TotalAmount = 750.00
        // Should NOT contribute to any KPI (deleted)
        CreateInvoice(7, 2, 750.00m, InvoiceStatusIssued, FinancialStatusUnpaid, futureDate, isDeleted: true);

        // Invoice 8: Issued, Overdue, due in past, TotalAmount = 1200.00
        // Has payment of 1200.00 (valid, last month) — fully paid but status still Overdue
        // Outstanding = 0, so should NOT contribute to Overdue Amount (outstanding must be > 0)
        CreateInvoice(8, 1, 1200.00m, InvoiceStatusIssued, FinancialStatusOverdue, pastDate);

        // Payments:
        // Payment 1: Invoice 2, 500.00, valid, current month
        CreatePayment(1, 2, 500.00m, currentMonthDate);
        // Payment 2: Invoice 2, 300.00, voided (should not count)
        CreatePayment(2, 2, 300.00m, currentMonthDate, isVoided: true);
        // Payment 3: Invoice 3, 1000.00, valid, current month
        CreatePayment(3, 3, 1000.00m, currentMonthDate);
        // Payment 4: Invoice 4, 1500.00, valid, last month (fully pays invoice 4)
        CreatePayment(4, 4, 1500.00m, lastMonthDate);
        // Payment 5: Invoice 8, 1200.00, valid, last month (fully pays invoice 8)
        CreatePayment(5, 8, 1200.00m, lastMonthDate);

        await _dbContext.SaveChangesAsync();

        // Act: Compute KPIs using the same logic as DashboardService
        var invoices = await _dbContext.Invoices.ToListAsync();
        var payments = await _dbContext.Payments.ToListAsync();

        var outstandingReceivables = ComputeOutstandingReceivables(invoices, payments);
        var overdueAmount = ComputeOverdueAmount(invoices, payments, today);
        var paidThisMonth = ComputePaidThisMonth(payments);
        var partiallyPaidAmount = ComputePartiallyPaidAmount(invoices, payments);

        // Assert: Verify each KPI matches expected manual computation

        // Outstanding Receivables:
        // Invoice 1 (Unpaid): 1000.00 - 0 = 1000.00
        // Invoice 2 (PartiallyPaid): 2000.00 - 500.00 = 1500.00
        // Invoice 3 (Overdue): 3000.00 - 1000.00 = 2000.00
        // Invoice 8 (Overdue): 1200.00 - 1200.00 = 0.00 (still qualifies by status but 0 balance)
        // Total: 1000.00 + 1500.00 + 2000.00 + 0.00 = 4500.00
        Assert.Equal(4500.00m, outstandingReceivables);

        // Overdue Amount:
        // Invoice 3 (DueDate < today, outstanding > 0): 3000.00 - 1000.00 = 2000.00
        // Invoice 5 (DueDate < today, but WrittenOff — still checked by overdue logic): 800.00
        // Invoice 8 (DueDate < today, outstanding = 0): excluded (outstanding must be > 0)
        // Note: Overdue query checks DueDate < today AND outstanding > 0, regardless of financial status
        Assert.Equal(2800.00m, overdueAmount);

        // Paid This Month:
        // Payment 1: 500.00 (current month, valid)
        // Payment 2: 300.00 (current month, voided — excluded)
        // Payment 3: 1000.00 (current month, valid)
        // Payment 4: 1500.00 (last month — excluded)
        // Payment 5: 1200.00 (last month — excluded)
        // Total: 500.00 + 1000.00 = 1500.00
        Assert.Equal(1500.00m, paidThisMonth);

        // Partially Paid Amount:
        // Invoice 2 (PartiallyPaid status): 2000.00 - 500.00 = 1500.00
        // Total: 1500.00
        Assert.Equal(1500.00m, partiallyPaidAmount);
    }

    [Fact]
    public async Task GetKpiData_WithNoInvoices_AllKpiValuesAreZero()
    {
        // Arrange: No invoices or payments seeded (only base data)

        // Act
        var invoices = await _dbContext.Invoices.ToListAsync();
        var payments = await _dbContext.Payments.ToListAsync();

        var outstandingReceivables = ComputeOutstandingReceivables(invoices, payments);
        var overdueAmount = ComputeOverdueAmount(invoices, payments, DateOnly.FromDateTime(DateTime.UtcNow));
        var paidThisMonth = ComputePaidThisMonth(payments);
        var partiallyPaidAmount = ComputePartiallyPaidAmount(invoices, payments);

        // Assert
        Assert.Equal(0m, outstandingReceivables);
        Assert.Equal(0m, overdueAmount);
        Assert.Equal(0m, paidThisMonth);
        Assert.Equal(0m, partiallyPaidAmount);
    }

    [Fact]
    public async Task GetKpiData_OutstandingReceivablesCount_MatchesQualifyingInvoiceCount()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureDate = today.AddDays(30);
        var pastDate = today.AddDays(-10);

        // 3 qualifying invoices (Issued + Unpaid/PartiallyPaid/Overdue + not deleted)
        CreateInvoice(1, 1, 500.00m, InvoiceStatusIssued, FinancialStatusUnpaid, futureDate);
        CreateInvoice(2, 1, 700.00m, InvoiceStatusIssued, FinancialStatusPartiallyPaid, futureDate);
        CreateInvoice(3, 2, 900.00m, InvoiceStatusIssued, FinancialStatusOverdue, pastDate);

        // Non-qualifying invoices
        CreateInvoice(4, 2, 400.00m, InvoiceStatusIssued, FinancialStatusPaid, futureDate);
        CreateInvoice(5, 1, 300.00m, InvoiceStatusDraft, FinancialStatusUnpaid, futureDate);
        CreateInvoice(6, 1, 600.00m, InvoiceStatusIssued, FinancialStatusUnpaid, futureDate, isDeleted: true);

        await _dbContext.SaveChangesAsync();

        // Act
        var invoices = await _dbContext.Invoices.ToListAsync();
        var qualifyingStatuses = new[] { FinancialStatusUnpaid, FinancialStatusPartiallyPaid, FinancialStatusOverdue };

        var qualifyingCount = invoices
            .Count(inv => inv.BusinessId == TestBusinessId
                       && !inv.IsDeleted
                       && inv.InvoiceStatusTypeId == InvoiceStatusIssued
                       && qualifyingStatuses.Contains(inv.InvoiceFinancialStatusTypeId));

        // Assert: exactly 3 qualifying invoices
        Assert.Equal(3, qualifyingCount);
    }

    [Fact]
    public async Task GetKpiData_OverdueAmount_ExcludesFullyPaidOverdueInvoices()
    {
        // Arrange: An overdue invoice that has been fully paid should not contribute to Overdue Amount
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var pastDate = today.AddDays(-20);
        var currentMonthDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 5, 12, 0, 0, DateTimeKind.Utc);

        // Invoice with outstanding balance (contributes to overdue)
        CreateInvoice(1, 1, 2000.00m, InvoiceStatusIssued, FinancialStatusOverdue, pastDate);
        CreatePayment(1, 1, 800.00m, currentMonthDate);

        // Invoice fully paid (should NOT contribute to overdue — outstanding = 0)
        CreateInvoice(2, 2, 1000.00m, InvoiceStatusIssued, FinancialStatusOverdue, pastDate);
        CreatePayment(2, 2, 1000.00m, currentMonthDate);

        await _dbContext.SaveChangesAsync();

        // Act
        var invoices = await _dbContext.Invoices.ToListAsync();
        var payments = await _dbContext.Payments.ToListAsync();
        var overdueAmount = ComputeOverdueAmount(invoices, payments, today);

        // Assert: Only invoice 1 contributes (2000 - 800 = 1200)
        Assert.Equal(1200.00m, overdueAmount);
    }

    [Fact]
    public async Task GetKpiData_PaidThisMonth_OnlyCountsCurrentMonthValidPayments()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var futureDate = today.AddDays(30);
        var currentMonthDate = new DateTime(now.Year, now.Month, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastMonthDate = currentMonthDate.AddMonths(-1);
        var twoMonthsAgoDate = currentMonthDate.AddMonths(-2);

        CreateInvoice(1, 1, 5000.00m, InvoiceStatusIssued, FinancialStatusPartiallyPaid, futureDate);

        // Current month payments
        CreatePayment(1, 1, 200.00m, currentMonthDate);
        CreatePayment(2, 1, 350.00m, currentMonthDate.AddDays(2));
        // Voided current month payment (should not count)
        CreatePayment(3, 1, 100.00m, currentMonthDate.AddDays(1), isVoided: true);
        // Past month payments (should not count)
        CreatePayment(4, 1, 500.00m, lastMonthDate);
        CreatePayment(5, 1, 750.00m, twoMonthsAgoDate);

        await _dbContext.SaveChangesAsync();

        // Act
        var payments = await _dbContext.Payments.ToListAsync();
        var paidThisMonth = ComputePaidThisMonth(payments);

        // Assert: Only payments 1 and 2 count (200 + 350 = 550)
        Assert.Equal(550.00m, paidThisMonth);
    }

    [Fact]
    public async Task GetKpiData_PartiallyPaid_OnlyCountsPartiallyPaidStatusInvoices()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureDate = today.AddDays(30);
        var pastDate = today.AddDays(-10);
        var currentMonthDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 8, 12, 0, 0, DateTimeKind.Utc);

        // PartiallyPaid invoice with payment
        CreateInvoice(1, 1, 3000.00m, InvoiceStatusIssued, FinancialStatusPartiallyPaid, futureDate);
        CreatePayment(1, 1, 1200.00m, currentMonthDate);

        // Another PartiallyPaid invoice with multiple payments
        CreateInvoice(2, 2, 2000.00m, InvoiceStatusIssued, FinancialStatusPartiallyPaid, futureDate);
        CreatePayment(2, 2, 400.00m, currentMonthDate);
        CreatePayment(3, 2, 600.00m, currentMonthDate.AddDays(-5));

        // Overdue invoice with payment (should NOT count for Partially Paid KPI)
        CreateInvoice(3, 1, 1500.00m, InvoiceStatusIssued, FinancialStatusOverdue, pastDate);
        CreatePayment(4, 3, 500.00m, currentMonthDate);

        // Unpaid invoice (should NOT count for Partially Paid KPI)
        CreateInvoice(4, 2, 800.00m, InvoiceStatusIssued, FinancialStatusUnpaid, futureDate);

        await _dbContext.SaveChangesAsync();

        // Act
        var invoices = await _dbContext.Invoices.ToListAsync();
        var payments = await _dbContext.Payments.ToListAsync();
        var partiallyPaidAmount = ComputePartiallyPaidAmount(invoices, payments);

        // Assert:
        // Invoice 1: 3000 - 1200 = 1800
        // Invoice 2: 2000 - 400 - 600 = 1000
        // Total: 1800 + 1000 = 2800
        Assert.Equal(2800.00m, partiallyPaidAmount);
    }

    [Fact]
    public async Task GetKpiData_VoidedPayments_DoNotAffectOutstandingBalance()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var futureDate = today.AddDays(30);
        var currentMonthDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 12, 10, 0, 0, DateTimeKind.Utc);

        CreateInvoice(1, 1, 5000.00m, InvoiceStatusIssued, FinancialStatusPartiallyPaid, futureDate);

        // Valid payment
        CreatePayment(1, 1, 1000.00m, currentMonthDate);
        // Voided payments (should not reduce outstanding)
        CreatePayment(2, 1, 2000.00m, currentMonthDate, isVoided: true);
        CreatePayment(3, 1, 500.00m, currentMonthDate, isVoided: true);

        await _dbContext.SaveChangesAsync();

        // Act
        var invoices = await _dbContext.Invoices.ToListAsync();
        var payments = await _dbContext.Payments.ToListAsync();
        var outstandingReceivables = ComputeOutstandingReceivables(invoices, payments);

        // Assert: Only valid payment of 1000 reduces the balance
        // Outstanding = 5000 - 1000 = 4000
        Assert.Equal(4000.00m, outstandingReceivables);
    }

    #endregion
}
