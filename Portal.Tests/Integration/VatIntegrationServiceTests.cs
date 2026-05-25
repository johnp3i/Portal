using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Integration;

/// <summary>
/// Integration tests for VatIntegrationService using in-memory EF Core database.
/// Seeds invoices, payments, and purchases across VAT periods and verifies
/// Output/Input/Net VAT calculations and Output/Input ratio.
/// **Validates: Requirements 6.1, 6.2, 6.3, 6.4**
/// </summary>
public class VatIntegrationServiceTests : IDisposable
{
    private const int TestBusinessId = 1;
    private readonly PortalDbContext _dbContext;
    private readonly VatIntegrationService _service;

    public VatIntegrationServiceTests()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"VatIntegration_{Guid.NewGuid()}")
            .Options;

        _dbContext = new PortalDbContext(options, tenantMock.Object);
        _service = new VatIntegrationService(_dbContext);

        SeedReferenceData();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region Test Infrastructure

    private void SeedReferenceData()
    {
        _dbContext.Businesses.Add(new Business
        {
            Id = TestBusinessId,
            Name = "Test Business",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        _dbContext.InvoiceStatusTypes.AddRange(
            new InvoiceStatusType { Id = 1, Name = "Draft" },
            new InvoiceStatusType { Id = 2, Name = "Issued" },
            new InvoiceStatusType { Id = 3, Name = "Cancelled" }
        );

        _dbContext.InvoiceFinancialStatusTypes.AddRange(
            new InvoiceFinancialStatusType { Id = 1, Name = "Unpaid" },
            new InvoiceFinancialStatusType { Id = 2, Name = "PartiallyPaid" },
            new InvoiceFinancialStatusType { Id = 3, Name = "Paid" },
            new InvoiceFinancialStatusType { Id = 4, Name = "Overdue" },
            new InvoiceFinancialStatusType { Id = 5, Name = "WrittenOff" }
        );

        _dbContext.Customers.Add(new Customer
        {
            Id = 1,
            BusinessId = TestBusinessId,
            Name = "Test Customer",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        _dbContext.Suppliers.Add(new Supplier
        {
            Id = 1,
            BusinessId = TestBusinessId,
            Name = "Test Supplier",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        _dbContext.ExpenseCategories.Add(new ExpenseCategory
        {
            Id = 1,
            BusinessId = TestBusinessId,
            Name = "Office Supplies",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        _dbContext.PurchaseOriginTypes.Add(new PurchaseOriginType
        {
            Id = 1,
            Name = "Domestic"
        });

        _dbContext.SaveChanges();
    }

    private VatSubmissionPeriod CreateCurrentPeriod()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = new DateOnly(today.Year, today.Month, 1);
        var periodEnd = periodStart.AddMonths(2).AddDays(-1);

        var period = new VatSubmissionPeriod
        {
            Id = 1,
            BusinessId = TestBusinessId,
            PeriodStartDate = periodStart,
            PeriodEndDate = periodEnd,
            PeriodLabel = $"{periodStart:MMM} - {periodEnd:MMM yyyy}",
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.VatSubmissionPeriods.Add(period);
        _dbContext.SaveChanges();

        return period;
    }

    private Invoice CreateFullyPaidInvoice(int id, decimal taxAmount, DateOnly invoiceDate)
    {
        var invoice = new Invoice
        {
            Id = id,
            BusinessId = TestBusinessId,
            CustomerId = 1,
            InvoiceStatusTypeId = 2, // Issued
            InvoiceFinancialStatusTypeId = 3, // Paid
            InvoiceNumber = $"INV-{id:D4}",
            InvoiceDate = invoiceDate,
            DueDate = invoiceDate.AddDays(30),
            Subtotal = taxAmount * 5, // Arbitrary subtotal
            TaxAmount = taxAmount,
            TotalAmount = (taxAmount * 5) + taxAmount,
            CurrencyCode = "EUR",
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Invoices.Add(invoice);
        return invoice;
    }

    private Purchase CreatePurchase(int id, decimal vatAmount, DateOnly invoiceDate, bool isCancelled = false)
    {
        var purchase = new Purchase
        {
            Id = id,
            BusinessId = TestBusinessId,
            SupplierId = 1,
            ExpenseCategoryId = 1,
            PurchaseOriginTypeId = 1,
            InvoiceNumber = $"PUR-{id:D4}",
            InvoiceDate = invoiceDate,
            Description = $"Purchase {id}",
            AmountExcludingVat = vatAmount * 5,
            VatAmount = vatAmount,
            TotalAmount = (vatAmount * 5) + vatAmount,
            IsCancelled = isCancelled,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Purchases.Add(purchase);
        return purchase;
    }

    #endregion

    #region Requirement 6.1: Output VAT Collected

    /// <summary>
    /// Verifies Output VAT = sum of TaxAmount for fully paid invoices (InvoiceFinancialStatusTypeId = 3)
    /// with InvoiceDate within the current VAT period.
    /// **Validates: Requirement 6.1**
    /// </summary>
    [Fact]
    public async Task GetCurrentPeriodSummary_OutputVat_SumsFullyPaidInvoiceTaxAmounts()
    {
        // Arrange
        var period = CreateCurrentPeriod();
        var dateInPeriod = period.PeriodStartDate.AddDays(10);

        // Create fully paid invoices within the period
        CreateFullyPaidInvoice(1, 100.00m, dateInPeriod);
        CreateFullyPaidInvoice(2, 250.50m, dateInPeriod.AddDays(5));
        CreateFullyPaidInvoice(3, 75.25m, dateInPeriod.AddDays(15));

        // Create an invoice that is NOT fully paid (should be excluded)
        _dbContext.Invoices.Add(new Invoice
        {
            Id = 4,
            BusinessId = TestBusinessId,
            CustomerId = 1,
            InvoiceStatusTypeId = 2,
            InvoiceFinancialStatusTypeId = 1, // Unpaid — should NOT count
            InvoiceNumber = "INV-0004",
            InvoiceDate = dateInPeriod,
            DueDate = dateInPeriod.AddDays(30),
            Subtotal = 500m,
            TaxAmount = 200.00m,
            TotalAmount = 700m,
            CurrencyCode = "EUR",
            IsDeleted = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        // Create a fully paid invoice OUTSIDE the period (should be excluded)
        CreateFullyPaidInvoice(5, 999.99m, period.PeriodStartDate.AddDays(-10));

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentPeriodSummaryAsync(TestBusinessId);

        // Assert
        var expectedOutputVat = 100.00m + 250.50m + 75.25m;
        Assert.Equal(expectedOutputVat, result.TotalOutputVat);
    }

    /// <summary>
    /// Verifies that deleted invoices are excluded from Output VAT calculation.
    /// **Validates: Requirement 6.1**
    /// </summary>
    [Fact]
    public async Task GetCurrentPeriodSummary_OutputVat_ExcludesDeletedInvoices()
    {
        // Arrange
        var period = CreateCurrentPeriod();
        var dateInPeriod = period.PeriodStartDate.AddDays(5);

        CreateFullyPaidInvoice(1, 150.00m, dateInPeriod);

        // Create a deleted fully paid invoice (should be excluded)
        _dbContext.Invoices.Add(new Invoice
        {
            Id = 2,
            BusinessId = TestBusinessId,
            CustomerId = 1,
            InvoiceStatusTypeId = 2,
            InvoiceFinancialStatusTypeId = 3, // Paid
            InvoiceNumber = "INV-0002",
            InvoiceDate = dateInPeriod,
            DueDate = dateInPeriod.AddDays(30),
            Subtotal = 500m,
            TaxAmount = 300.00m,
            TotalAmount = 800m,
            CurrencyCode = "EUR",
            IsDeleted = true, // Deleted — should NOT count
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentPeriodSummaryAsync(TestBusinessId);

        // Assert — only the non-deleted invoice counts
        Assert.Equal(150.00m, result.TotalOutputVat);
    }

    #endregion

    #region Requirement 6.2: Input VAT

    /// <summary>
    /// Verifies Input VAT = sum of VatAmount for non-cancelled purchases
    /// with InvoiceDate within the current VAT period.
    /// **Validates: Requirement 6.2**
    /// </summary>
    [Fact]
    public async Task GetCurrentPeriodSummary_InputVat_SumsNonCancelledPurchaseVatAmounts()
    {
        // Arrange
        var period = CreateCurrentPeriod();
        var dateInPeriod = period.PeriodStartDate.AddDays(5);

        // Create non-cancelled purchases within the period
        CreatePurchase(1, 50.00m, dateInPeriod);
        CreatePurchase(2, 120.75m, dateInPeriod.AddDays(10));
        CreatePurchase(3, 30.25m, dateInPeriod.AddDays(20));

        // Create a cancelled purchase (should be excluded)
        CreatePurchase(4, 500.00m, dateInPeriod.AddDays(3), isCancelled: true);

        // Create a purchase OUTSIDE the period (should be excluded)
        CreatePurchase(5, 999.99m, period.PeriodStartDate.AddDays(-5));

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentPeriodSummaryAsync(TestBusinessId);

        // Assert
        var expectedInputVat = 50.00m + 120.75m + 30.25m;
        Assert.Equal(expectedInputVat, result.TotalInputVat);
    }

    /// <summary>
    /// Verifies that cancelled purchases are excluded from Input VAT calculation.
    /// **Validates: Requirement 6.2**
    /// </summary>
    [Fact]
    public async Task GetCurrentPeriodSummary_InputVat_ExcludesCancelledPurchases()
    {
        // Arrange
        var period = CreateCurrentPeriod();
        var dateInPeriod = period.PeriodStartDate.AddDays(10);

        CreatePurchase(1, 80.00m, dateInPeriod);
        CreatePurchase(2, 200.00m, dateInPeriod, isCancelled: true); // Cancelled

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentPeriodSummaryAsync(TestBusinessId);

        // Assert — only non-cancelled purchase counts
        Assert.Equal(80.00m, result.TotalInputVat);
    }

    #endregion

    #region Requirement 6.3: Net VAT Payable

    /// <summary>
    /// Verifies Net VAT Payable = Output VAT - Input VAT.
    /// **Validates: Requirement 6.3**
    /// </summary>
    [Fact]
    public async Task GetCurrentPeriodSummary_NetVatPayable_EqualsOutputMinusInput()
    {
        // Arrange
        var period = CreateCurrentPeriod();
        var dateInPeriod = period.PeriodStartDate.AddDays(10);

        // Output VAT: 100 + 200 = 300
        CreateFullyPaidInvoice(1, 100.00m, dateInPeriod);
        CreateFullyPaidInvoice(2, 200.00m, dateInPeriod.AddDays(5));

        // Input VAT: 50 + 75 = 125
        CreatePurchase(1, 50.00m, dateInPeriod);
        CreatePurchase(2, 75.00m, dateInPeriod.AddDays(3));

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentPeriodSummaryAsync(TestBusinessId);

        // Assert
        Assert.Equal(300.00m, result.TotalOutputVat);
        Assert.Equal(125.00m, result.TotalInputVat);
        Assert.Equal(175.00m, result.NetVatPayable); // 300 - 125 = 175
    }

    /// <summary>
    /// Verifies Net VAT Payable can be negative when Input exceeds Output.
    /// **Validates: Requirement 6.3**
    /// </summary>
    [Fact]
    public async Task GetCurrentPeriodSummary_NetVatPayable_CanBeNegative()
    {
        // Arrange
        var period = CreateCurrentPeriod();
        var dateInPeriod = period.PeriodStartDate.AddDays(10);

        // Output VAT: 50
        CreateFullyPaidInvoice(1, 50.00m, dateInPeriod);

        // Input VAT: 200
        CreatePurchase(1, 200.00m, dateInPeriod);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentPeriodSummaryAsync(TestBusinessId);

        // Assert
        Assert.Equal(50.00m, result.TotalOutputVat);
        Assert.Equal(200.00m, result.TotalInputVat);
        Assert.Equal(-150.00m, result.NetVatPayable); // 50 - 200 = -150
    }

    #endregion

    #region Requirement 6.4: VAT Liability by Period

    /// <summary>
    /// Verifies GetVatLiabilityByPeriodAsync returns correct Output/Input/Net for multiple periods.
    /// **Validates: Requirement 6.4**
    /// </summary>
    [Fact]
    public async Task GetVatLiabilityByPeriod_ReturnsCorrectValuesForMultiplePeriods()
    {
        // Arrange — create 3 VAT periods
        var period1 = new VatSubmissionPeriod
        {
            Id = 1,
            BusinessId = TestBusinessId,
            PeriodStartDate = new DateOnly(2024, 1, 1),
            PeriodEndDate = new DateOnly(2024, 3, 31),
            PeriodLabel = "Jan - Mar 2024",
            CreatedAtUtc = DateTime.UtcNow
        };
        var period2 = new VatSubmissionPeriod
        {
            Id = 2,
            BusinessId = TestBusinessId,
            PeriodStartDate = new DateOnly(2024, 4, 1),
            PeriodEndDate = new DateOnly(2024, 6, 30),
            PeriodLabel = "Apr - Jun 2024",
            CreatedAtUtc = DateTime.UtcNow
        };
        var period3 = new VatSubmissionPeriod
        {
            Id = 3,
            BusinessId = TestBusinessId,
            PeriodStartDate = new DateOnly(2024, 7, 1),
            PeriodEndDate = new DateOnly(2024, 9, 30),
            PeriodLabel = "Jul - Sep 2024",
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.VatSubmissionPeriods.AddRange(period1, period2, period3);

        // Period 1: Output=300, Input=100, Net=200
        CreateFullyPaidInvoice(1, 300.00m, new DateOnly(2024, 2, 15));
        CreatePurchase(1, 100.00m, new DateOnly(2024, 1, 20));

        // Period 2: Output=150, Input=250, Net=-100
        CreateFullyPaidInvoice(2, 150.00m, new DateOnly(2024, 5, 10));
        CreatePurchase(2, 250.00m, new DateOnly(2024, 4, 5));

        // Period 3: Output=500, Input=0, Net=500
        CreateFullyPaidInvoice(3, 500.00m, new DateOnly(2024, 8, 20));

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetVatLiabilityByPeriodAsync(TestBusinessId);

        // Assert
        Assert.Equal(3, result.Count);

        // Period 1
        var p1 = result.First(r => r.PeriodLabel == "Jan - Mar 2024");
        Assert.Equal(300.00m, p1.OutputVat);
        Assert.Equal(100.00m, p1.InputVat);
        Assert.Equal(200.00m, p1.NetPayable);

        // Period 2
        var p2 = result.First(r => r.PeriodLabel == "Apr - Jun 2024");
        Assert.Equal(150.00m, p2.OutputVat);
        Assert.Equal(250.00m, p2.InputVat);
        Assert.Equal(-100.00m, p2.NetPayable);

        // Period 3
        var p3 = result.First(r => r.PeriodLabel == "Jul - Sep 2024");
        Assert.Equal(500.00m, p3.OutputVat);
        Assert.Equal(0.00m, p3.InputVat);
        Assert.Equal(500.00m, p3.NetPayable);
    }

    /// <summary>
    /// Verifies GetVatLiabilityByPeriodAsync returns at most 6 periods.
    /// **Validates: Requirement 6.4**
    /// </summary>
    [Fact]
    public async Task GetVatLiabilityByPeriod_ReturnsAtMostSixPeriods()
    {
        // Arrange — create 8 periods
        for (int i = 0; i < 8; i++)
        {
            var startDate = new DateOnly(2023, 1, 1).AddMonths(i * 3);
            _dbContext.VatSubmissionPeriods.Add(new VatSubmissionPeriod
            {
                Id = i + 1,
                BusinessId = TestBusinessId,
                PeriodStartDate = startDate,
                PeriodEndDate = startDate.AddMonths(3).AddDays(-1),
                PeriodLabel = $"Period {i + 1}",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetVatLiabilityByPeriodAsync(TestBusinessId);

        // Assert — at most 6 periods returned
        Assert.True(result.Count <= 6);
    }

    #endregion

    #region Output/Input Ratio (removed — property no longer exists in VatSummaryDto)

    // OutputInputRatio was removed from VatSummaryDto as part of the dashboard upgrade.
    // The ratio is no longer computed or stored. These tests are retained as documentation
    // but the assertions now verify the new HasData property instead.

    /// <summary>
    /// Verifies HasData is true when a valid period with data exists.
    /// </summary>
    [Fact]
    public async Task GetCurrentPeriodSummary_HasData_TrueWhenPeriodExists()
    {
        // Arrange
        var period = CreateCurrentPeriod();
        var dateInPeriod = period.PeriodStartDate.AddDays(10);

        // Output VAT: 400
        CreateFullyPaidInvoice(1, 400.00m, dateInPeriod);

        // Input VAT: 200
        CreatePurchase(1, 200.00m, dateInPeriod);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentPeriodSummaryAsync(TestBusinessId);

        // Assert
        Assert.True(result.HasData);
    }

    /// <summary>
    /// Verifies HasData is true when Input VAT is zero but period exists.
    /// </summary>
    [Fact]
    public async Task GetCurrentPeriodSummary_HasData_TrueWhenInputIsZero()
    {
        // Arrange
        var period = CreateCurrentPeriod();
        var dateInPeriod = period.PeriodStartDate.AddDays(10);

        // Output VAT: 500 (no purchases, so Input = 0)
        CreateFullyPaidInvoice(1, 500.00m, dateInPeriod);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentPeriodSummaryAsync(TestBusinessId);

        // Assert
        Assert.Equal(500.00m, result.TotalOutputVat);
        Assert.Equal(0m, result.TotalInputVat);
        Assert.True(result.HasData);
    }

    #endregion

    #region No Active Period

    /// <summary>
    /// Verifies that when no VAT period covers the current date, all values are zero.
    /// </summary>
    [Fact]
    public async Task GetCurrentPeriodSummary_NoPeriod_ReturnsZeroValues()
    {
        // Arrange — no VatSubmissionPeriod seeded that covers today

        // Act
        var result = await _service.GetCurrentPeriodSummaryAsync(TestBusinessId);

        // Assert
        Assert.Equal(0m, result.TotalOutputVat);
        Assert.Equal(0m, result.TotalInputVat);
        Assert.Equal(0m, result.NetVatPayable);
        Assert.False(result.HasData);
        Assert.Equal("No active period", result.PeriodLabel);
    }

    #endregion

    #region Period Label

    /// <summary>
    /// Verifies the PeriodLabel is correctly returned from the current period.
    /// </summary>
    [Fact]
    public async Task GetCurrentPeriodSummary_ReturnsPeriodLabel()
    {
        // Arrange
        var period = CreateCurrentPeriod();

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetCurrentPeriodSummaryAsync(TestBusinessId);

        // Assert
        Assert.Equal(period.PeriodLabel, result.PeriodLabel);
    }

    #endregion
}
