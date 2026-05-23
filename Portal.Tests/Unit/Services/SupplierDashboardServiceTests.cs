using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for SupplierDashboardService covering KPI computation, chart data,
/// pagination, period filtering, and access control scenarios.
/// </summary>
public class SupplierDashboardServiceTests : IDisposable
{
    private const int TestBusinessId = 1;
    private const int OtherBusinessId = 99;

    private readonly PortalDbContext _dbContext;
    private readonly SupplierDashboardService _service;

    public SupplierDashboardServiceTests()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PortalDbContext(options, tenantMock.Object);

        // Seed a business and business profile
        _dbContext.Businesses.Add(new Business
        {
            Id = TestBusinessId,
            Name = "Test Business",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        _dbContext.BusinessProfiles.Add(new BusinessProfile
        {
            Id = 1,
            BusinessId = TestBusinessId,
            CompanyRegistrationNumber = "REG001",
            VatRegistrationNumber = "VAT001",
            VatRegistrationDate = new DateOnly(2023, 1, 1),
            VatPeriodLengthInMonths = 3,
            AddressLine1 = "123 Test St",
            City = "TestCity",
            PostalCode = "12345",
            Country = "TestCountry",
            Email = "test@test.com",
            CurrencySymbol = "€"
        });

        _dbContext.SaveChanges();

        _service = new SupplierDashboardService(_dbContext, tenantMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region Helper Methods

    private Supplier CreateSupplier(int id, string name, bool isActive = true, int businessId = TestBusinessId)
    {
        var supplier = new Supplier
        {
            Id = id,
            BusinessId = businessId,
            Name = name,
            IsActive = isActive,
            CreatedAtUtc = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _dbContext.Suppliers.Add(supplier);
        return supplier;
    }

    private ExpenseCategory CreateCategory(int id, string name)
    {
        var category = new ExpenseCategory
        {
            Id = id,
            BusinessId = TestBusinessId,
            Name = name,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.ExpenseCategories.Add(category);
        return category;
    }

    private VatSubmissionPeriod CreatePeriod(int id, string label, DateOnly startDate, DateOnly endDate, int businessId = TestBusinessId)
    {
        var period = new VatSubmissionPeriod
        {
            Id = id,
            BusinessId = businessId,
            PeriodLabel = label,
            PeriodStartDate = startDate,
            PeriodEndDate = endDate,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.VatSubmissionPeriods.Add(period);
        return period;
    }

    private Purchase CreatePurchase(int id, int supplierId, int categoryId, DateOnly invoiceDate,
        decimal amountExclVat, decimal vatAmount, bool isCancelled = false, int? periodId = null)
    {
        var purchase = new Purchase
        {
            Id = id,
            BusinessId = TestBusinessId,
            SupplierId = supplierId,
            ExpenseCategoryId = categoryId,
            PurchaseOriginTypeId = 1,
            InvoiceNumber = $"INV-{id:D3}",
            InvoiceDate = invoiceDate,
            Description = $"Purchase {id}",
            AmountExcludingVat = amountExclVat,
            VatAmount = vatAmount,
            TotalAmount = amountExclVat + vatAmount,
            IsCancelled = isCancelled,
            VatSubmissionPeriodId = periodId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Purchases.Add(purchase);
        return purchase;
    }

    #endregion

    [Fact]
    public async Task GetDashboardAsync_SupplierNotBelongingToCurrentBusiness_ReturnsEmptySupplierName()
    {
        // Arrange: supplier belongs to a different business — global query filter will exclude it
        var otherBusiness = new Business
        {
            Id = OtherBusinessId,
            Name = "Other Business",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Businesses.Add(otherBusiness);

        var supplier = new Supplier
        {
            Id = 100,
            BusinessId = OtherBusinessId,
            Name = "Other Supplier",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Suppliers.Add(supplier);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDashboardAsync(100, null, 1);

        // Assert: supplier not found due to global query filter — returns empty name
        Assert.Equal(string.Empty, result.SupplierName);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task GetDashboardAsync_SupplierWithNoPurchases_ReturnsZeroValueKpis()
    {
        // Arrange
        CreateSupplier(1, "Empty Supplier");
        CreateCategory(1, "General");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDashboardAsync(1, null, 1);

        // Assert
        Assert.Equal(0m, result.TotalSpend);
        Assert.Equal(0, result.TotalPurchases);
        Assert.Equal(0m, result.AverageMonthlySpend);
        Assert.Empty(result.Purchases);
        Assert.Equal("Empty Supplier", result.SupplierName);
    }

    [Fact]
    public async Task GetDashboardAsync_PeriodDropdown_AlwaysHasAllTimeAsFirstOptionConcept()
    {
        // Arrange: create periods in non-chronological order
        CreateSupplier(1, "Test Supplier");
        CreatePeriod(2, "Jun-Aug 2024", new DateOnly(2024, 6, 1), new DateOnly(2024, 8, 31));
        CreatePeriod(1, "Mar-May 2024", new DateOnly(2024, 3, 1), new DateOnly(2024, 5, 31));
        CreatePeriod(3, "Sep-Nov 2024", new DateOnly(2024, 9, 1), new DateOnly(2024, 11, 30));
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDashboardAsync(1, null, 1);

        // Assert: periods are ordered by PeriodStartDate ascending
        // "All Time" is represented by SelectedPeriodId being null (the view adds "All Time" as first option)
        Assert.Null(result.SelectedPeriodId);
        Assert.Equal(3, result.Periods.Count);
        Assert.Equal("Mar-May 2024", result.Periods[0].Label);
        Assert.Equal("Jun-Aug 2024", result.Periods[1].Label);
        Assert.Equal("Sep-Nov 2024", result.Periods[2].Label);
    }

    [Fact]
    public async Task GetDashboardAsync_SpendShare_IncludesOthersSliceWhenMoreThan5OtherSuppliers()
    {
        // Arrange: create 7 suppliers (1 current + 6 others, so > 5 others triggers "Others" slice)
        CreateSupplier(1, "Current Supplier");
        CreateSupplier(2, "Supplier A");
        CreateSupplier(3, "Supplier B");
        CreateSupplier(4, "Supplier C");
        CreateSupplier(5, "Supplier D");
        CreateSupplier(6, "Supplier E");
        CreateSupplier(7, "Supplier F");
        CreateCategory(1, "General");

        // Current supplier purchases
        CreatePurchase(1, 1, 1, new DateOnly(2024, 3, 15), 500m, 100m);

        // Other suppliers purchases
        CreatePurchase(2, 2, 1, new DateOnly(2024, 3, 15), 600m, 120m);
        CreatePurchase(3, 3, 1, new DateOnly(2024, 3, 15), 500m, 100m);
        CreatePurchase(4, 4, 1, new DateOnly(2024, 3, 15), 400m, 80m);
        CreatePurchase(5, 5, 1, new DateOnly(2024, 3, 15), 300m, 60m);
        CreatePurchase(6, 6, 1, new DateOnly(2024, 3, 15), 200m, 40m);
        CreatePurchase(7, 7, 1, new DateOnly(2024, 3, 15), 100m, 20m);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDashboardAsync(1, null, 1);

        // Assert: should have current supplier + top 5 others + "Others" aggregate = 7 slices
        Assert.Equal(7, result.SpendShareData.Count);

        var currentSlice = result.SpendShareData.First(s => s.IsCurrentSupplier);
        Assert.Equal("Current Supplier", currentSlice.SupplierName);
        Assert.Equal(500m, currentSlice.Amount);

        var othersSlice = result.SpendShareData.First(s => s.SupplierName == "Others");
        Assert.Equal(100m, othersSlice.Amount); // Supplier F (the 6th other) goes into "Others"
    }

    [Fact]
    public async Task GetDashboardAsync_SpendShare_HandlesCurrentSupplierWithZeroSpendInSelectedPeriod()
    {
        // Arrange: current supplier has no purchases in the selected period
        CreateSupplier(1, "Zero Spend Supplier");
        CreateSupplier(2, "Active Supplier");
        CreateCategory(1, "General");
        CreatePeriod(1, "Mar-May 2024", new DateOnly(2024, 3, 1), new DateOnly(2024, 5, 31));

        // Only other supplier has purchases in this period
        CreatePurchase(1, 2, 1, new DateOnly(2024, 4, 10), 1000m, 200m, periodId: 1);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDashboardAsync(1, 1, 1);

        // Assert: current supplier appears with zero amount
        var currentSlice = result.SpendShareData.First(s => s.IsCurrentSupplier);
        Assert.Equal("Zero Spend Supplier", currentSlice.SupplierName);
        Assert.Equal(0m, currentSlice.Amount);
        Assert.True(currentSlice.IsCurrentSupplier);
    }

    [Fact]
    public async Task GetDashboardAsync_MonthlyChart_ShowsCorrectAbbreviatedMonthLabelsFor3MonthPeriod()
    {
        // Arrange
        CreateSupplier(1, "Test Supplier");
        CreateCategory(1, "General");
        CreatePeriod(1, "Mar-May 2024", new DateOnly(2024, 3, 1), new DateOnly(2024, 5, 31));

        CreatePurchase(1, 1, 1, new DateOnly(2024, 3, 10), 100m, 20m, periodId: 1);
        CreatePurchase(2, 1, 1, new DateOnly(2024, 4, 15), 200m, 40m, periodId: 1);
        CreatePurchase(3, 1, 1, new DateOnly(2024, 5, 20), 300m, 60m, periodId: 1);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDashboardAsync(1, 1, 1);

        // Assert: 3 bars with abbreviated month labels
        Assert.Equal(3, result.MonthlySpendData.Count);
        Assert.Equal("Mar", result.MonthlySpendData[0].MonthLabel);
        Assert.Equal("Apr", result.MonthlySpendData[1].MonthLabel);
        Assert.Equal("May", result.MonthlySpendData[2].MonthLabel);
        Assert.Equal(100m, result.MonthlySpendData[0].Amount);
        Assert.Equal(200m, result.MonthlySpendData[1].Amount);
        Assert.Equal(300m, result.MonthlySpendData[2].Amount);
    }

    [Fact]
    public async Task GetDashboardAsync_PeriodChart_MarksSelectedPeriodBarAsIsSelected()
    {
        // Arrange
        CreateSupplier(1, "Test Supplier");
        CreateCategory(1, "General");
        CreatePeriod(1, "Mar-May 2024", new DateOnly(2024, 3, 1), new DateOnly(2024, 5, 31));
        CreatePeriod(2, "Jun-Aug 2024", new DateOnly(2024, 6, 1), new DateOnly(2024, 8, 31));
        CreatePeriod(3, "Sep-Nov 2024", new DateOnly(2024, 9, 1), new DateOnly(2024, 11, 30));

        CreatePurchase(1, 1, 1, new DateOnly(2024, 4, 10), 500m, 100m, periodId: 1);
        CreatePurchase(2, 1, 1, new DateOnly(2024, 7, 10), 300m, 60m, periodId: 2);

        await _dbContext.SaveChangesAsync();

        // Act: select period 2
        var result = await _service.GetDashboardAsync(1, 2, 1);

        // Assert: only period 2 is marked as selected
        Assert.Equal(3, result.PeriodSpendData.Count);
        Assert.False(result.PeriodSpendData[0].IsSelected);
        Assert.True(result.PeriodSpendData[1].IsSelected);
        Assert.False(result.PeriodSpendData[2].IsSelected);
    }

    [Fact]
    public async Task GetDashboardAsync_TableRows_EmptyWhenSupplierHasNoNonCancelledPurchases()
    {
        // Arrange: supplier has only cancelled purchases
        CreateSupplier(1, "Test Supplier");
        CreateCategory(1, "General");

        CreatePurchase(1, 1, 1, new DateOnly(2024, 3, 10), 100m, 20m, isCancelled: true);
        CreatePurchase(2, 1, 1, new DateOnly(2024, 4, 10), 200m, 40m, isCancelled: true);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDashboardAsync(1, null, 1);

        // Assert
        Assert.Empty(result.Purchases);
        Assert.Equal(0, result.TotalRecords);
        Assert.Equal(0m, result.TotalSpend);
        Assert.Equal(0, result.TotalPurchases);
    }

    [Fact]
    public async Task GetDashboardAsync_InvalidPeriodId_TreatedAsAllTime()
    {
        // Arrange: period 999 does not exist
        CreateSupplier(1, "Test Supplier");
        CreateCategory(1, "General");
        CreatePeriod(1, "Mar-May 2024", new DateOnly(2024, 3, 1), new DateOnly(2024, 5, 31));

        CreatePurchase(1, 1, 1, new DateOnly(2024, 3, 10), 100m, 20m, periodId: 1);
        CreatePurchase(2, 1, 1, new DateOnly(2024, 6, 10), 200m, 40m); // no period

        await _dbContext.SaveChangesAsync();

        // Act: pass invalid periodId
        var result = await _service.GetDashboardAsync(1, 999, 1);

        // Assert: treated as "All Time" — both purchases included
        Assert.Null(result.SelectedPeriodId);
        Assert.Equal(300m, result.TotalSpend);
        Assert.Equal(2, result.TotalPurchases);
    }

    [Fact]
    public async Task GetDashboardAsync_PageClampedTo1_WhenRequestedPageIsLessThan1()
    {
        // Arrange
        CreateSupplier(1, "Test Supplier");
        CreateCategory(1, "General");

        CreatePurchase(1, 1, 1, new DateOnly(2024, 3, 10), 100m, 20m);

        await _dbContext.SaveChangesAsync();

        // Act: request page 0 (invalid)
        var result = await _service.GetDashboardAsync(1, null, 0);

        // Assert: page clamped to 1
        Assert.Equal(1, result.CurrentPage);
        Assert.Single(result.Purchases);
    }

    [Fact]
    public async Task GetDashboardAsync_PageClampedToLastPage_WhenRequestedPageExceedsTotalPages()
    {
        // Arrange: 3 purchases = 1 page (page size is 10)
        CreateSupplier(1, "Test Supplier");
        CreateCategory(1, "General");

        CreatePurchase(1, 1, 1, new DateOnly(2024, 3, 10), 100m, 20m);
        CreatePurchase(2, 1, 1, new DateOnly(2024, 4, 10), 200m, 40m);
        CreatePurchase(3, 1, 1, new DateOnly(2024, 5, 10), 300m, 60m);

        await _dbContext.SaveChangesAsync();

        // Act: request page 50 (way beyond total pages)
        var result = await _service.GetDashboardAsync(1, null, 50);

        // Assert: page clamped to last page (1)
        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(1, result.TotalPages);
        Assert.Equal(3, result.Purchases.Count);
    }
}
