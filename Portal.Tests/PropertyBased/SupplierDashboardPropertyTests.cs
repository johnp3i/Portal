using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

/// <summary>
/// Property-based tests for SupplierDashboardService using FsCheck + xUnit.
/// Each property test validates correctness invariants across randomly generated inputs.
/// </summary>
public class SupplierDashboardPropertyTests
{
    private const int TestBusinessId = 1;
    private const int OtherBusinessId = 99;
    private const int DefaultSupplierId = 1;
    private const int DefaultCategoryId = 1;

    #region Test Infrastructure

    private static (PortalDbContext dbContext, SupplierDashboardService service) CreateTestContext(int businessId = TestBusinessId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(businessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new PortalDbContext(options, tenantMock.Object);

        // Seed required entities
        dbContext.Businesses.Add(new Business
        {
            Id = TestBusinessId,
            Name = "Test Business",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        dbContext.BusinessProfiles.Add(new BusinessProfile
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

        dbContext.Suppliers.Add(new Supplier
        {
            Id = DefaultSupplierId,
            BusinessId = TestBusinessId,
            Name = "Test Supplier",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        dbContext.ExpenseCategories.Add(new ExpenseCategory
        {
            Id = DefaultCategoryId,
            BusinessId = TestBusinessId,
            Name = "General",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        dbContext.SaveChanges();

        var service = new SupplierDashboardService(dbContext, tenantMock.Object);
        return (dbContext, service);
    }

    private static DateOnly GenerateDate(int seed)
    {
        // Generate dates between 2023-01-01 and 2025-12-31
        var baseDate = new DateOnly(2023, 1, 1);
        var daysToAdd = Math.Abs(seed) % 1095; // ~3 years of days
        return baseDate.AddDays(daysToAdd);
    }

    private static decimal GenerateAmount(int seed)
    {
        // Generate positive amounts between 0.01 and 9999.99
        var raw = Math.Abs(seed) % 999999 + 1;
        return raw / 100m;
    }

    private static Purchase CreatePurchase(
        int id, int supplierId, int businessId, int categoryId,
        DateOnly invoiceDate, decimal amount, bool isCancelled,
        int? periodId = null)
    {
        return new Purchase
        {
            Id = id,
            BusinessId = businessId,
            SupplierId = supplierId,
            ExpenseCategoryId = categoryId,
            PurchaseOriginTypeId = 1,
            InvoiceNumber = $"INV-{id:D4}",
            InvoiceDate = invoiceDate,
            Description = $"Purchase {id}",
            AmountExcludingVat = amount,
            VatAmount = Math.Round(amount * 0.15m, 2),
            TotalAmount = amount + Math.Round(amount * 0.15m, 2),
            IsCancelled = isCancelled,
            VatSubmissionPeriodId = periodId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    #endregion

    #region Property 1: Cancelled Purchase Exclusion

    /// <summary>
    /// Property 1: Cancelled Purchase Exclusion
    /// For any set of purchases with mixed IsCancelled values, all computed metrics
    /// only reflect purchases where IsCancelled = false.
    /// **Validates: Requirements 5.2, 5.3, 10.2, 13.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void CancelledPurchases_NeverAppearInMetrics(PositiveInt[] amountSeeds, bool[] cancelFlags)
    {
        if (amountSeeds.Length == 0) return;

        var (dbContext, service) = CreateTestContext();
        try
        {
            var purchaseCount = Math.Min(amountSeeds.Length, 15);
            var flags = cancelFlags.Length > 0 ? cancelFlags : new[] { false };

            for (int i = 0; i < purchaseCount; i++)
            {
                var isCancelled = flags[i % flags.Length];
                var amount = GenerateAmount(amountSeeds[i].Get);
                var date = GenerateDate(amountSeeds[i].Get + i);
                var purchase = CreatePurchase(
                    i + 1, DefaultSupplierId, TestBusinessId, DefaultCategoryId,
                    date, amount, isCancelled);
                dbContext.Purchases.Add(purchase);
            }
            dbContext.SaveChanges();

            var result = service.GetDashboardAsync(DefaultSupplierId, null, 1).Result;

            // Compute expected values from non-cancelled purchases only
            var nonCancelled = dbContext.Purchases
                .Where(p => p.SupplierId == DefaultSupplierId && !p.IsCancelled && p.BusinessId == TestBusinessId)
                .ToList();

            var expectedTotalSpend = nonCancelled.Sum(p => p.AmountExcludingVat);
            var expectedCount = nonCancelled.Count;

            Assert.Equal(expectedTotalSpend, result.TotalSpend);
            Assert.Equal(expectedCount, result.TotalPurchases);

            // All table rows should only be non-cancelled
            Assert.Equal(Math.Min(10, expectedCount), result.Purchases.Count);

            // Monthly chart bar totals should equal total spend
            var chartTotal = result.MonthlySpendData.Sum(m => m.Amount);
            Assert.Equal(expectedTotalSpend, chartTotal);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 2: Business Scoping Invariant

    /// <summary>
    /// Property 2: Business Scoping Invariant
    /// All returned data only contains records belonging to the authenticated user's BusinessId.
    /// **Validates: Requirements 3.3, 13.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public void AllData_ScopedToCurrentBusiness(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0) return;

        var (dbContext, service) = CreateTestContext();
        try
        {
            // Add other business
            dbContext.Businesses.Add(new Business
            {
                Id = OtherBusinessId,
                Name = "Other Business",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            // Add supplier for other business (won't be visible due to query filter)
            dbContext.Suppliers.Add(new Supplier
            {
                Id = 50,
                BusinessId = OtherBusinessId,
                Name = "Other Supplier",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

            dbContext.ExpenseCategories.Add(new ExpenseCategory
            {
                Id = 50,
                BusinessId = OtherBusinessId,
                Name = "Other Category",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

            var purchaseCount = Math.Min(amountSeeds.Length, 10);

            // Add purchases for test business
            for (int i = 0; i < purchaseCount; i++)
            {
                var amount = GenerateAmount(amountSeeds[i].Get);
                var date = GenerateDate(amountSeeds[i].Get + i);
                dbContext.Purchases.Add(CreatePurchase(
                    i + 1, DefaultSupplierId, TestBusinessId, DefaultCategoryId,
                    date, amount, false));
            }

            // Add purchases for other business (should never appear)
            for (int i = 0; i < purchaseCount; i++)
            {
                var amount = GenerateAmount(amountSeeds[i].Get + 1000);
                var date = GenerateDate(amountSeeds[i].Get + i + 500);
                dbContext.Purchases.Add(CreatePurchase(
                    100 + i + 1, 50, OtherBusinessId, 50,
                    date, amount, false));
            }
            dbContext.SaveChanges();

            var result = service.GetDashboardAsync(DefaultSupplierId, null, 1).Result;

            // KPIs should only reflect test business purchases
            var expectedSpend = dbContext.Purchases
                .Where(p => p.BusinessId == TestBusinessId && p.SupplierId == DefaultSupplierId && !p.IsCancelled)
                .Sum(p => p.AmountExcludingVat);

            Assert.Equal(expectedSpend, result.TotalSpend);
            Assert.Equal(purchaseCount, result.TotalPurchases);

            // Spend share should not include other business suppliers
            Assert.DoesNotContain(result.SpendShareData, s => s.SupplierName == "Other Supplier");

            // Period options should not include other business periods
            // (none were added for other business, so just verify count is 0)
            Assert.Empty(result.Periods);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 3: Period Filter Scoping

    /// <summary>
    /// Property 3: Period Filter Scoping
    /// When a periodId is selected, all metrics only include purchases with that VatSubmissionPeriodId.
    /// When periodId is null, all non-cancelled purchases are included.
    /// **Validates: Requirements 5.5, 6.2, 6.3, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public void PeriodFilter_CorrectlyScopesAllMetrics(PositiveInt[] amountSeeds, bool selectPeriod)
    {
        if (amountSeeds.Length == 0) return;

        var (dbContext, service) = CreateTestContext();
        try
        {
            // Create two periods
            var period1 = new VatSubmissionPeriod
            {
                Id = 1,
                BusinessId = TestBusinessId,
                PeriodLabel = "Mar-May 2024",
                PeriodStartDate = new DateOnly(2024, 3, 1),
                PeriodEndDate = new DateOnly(2024, 5, 31),
                CreatedAtUtc = DateTime.UtcNow
            };
            var period2 = new VatSubmissionPeriod
            {
                Id = 2,
                BusinessId = TestBusinessId,
                PeriodLabel = "Jun-Aug 2024",
                PeriodStartDate = new DateOnly(2024, 6, 1),
                PeriodEndDate = new DateOnly(2024, 8, 31),
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.VatSubmissionPeriods.AddRange(period1, period2);

            var purchaseCount = Math.Min(amountSeeds.Length, 12);

            for (int i = 0; i < purchaseCount; i++)
            {
                var amount = GenerateAmount(amountSeeds[i].Get);
                var periodId = (i % 2 == 0) ? 1 : 2;
                var date = periodId == 1
                    ? new DateOnly(2024, 3, 1).AddDays(i % 90)
                    : new DateOnly(2024, 6, 1).AddDays(i % 90);
                dbContext.Purchases.Add(CreatePurchase(
                    i + 1, DefaultSupplierId, TestBusinessId, DefaultCategoryId,
                    date, amount, false, periodId));
            }
            dbContext.SaveChanges();

            int? filterPeriodId = selectPeriod ? 1 : null;
            var result = service.GetDashboardAsync(DefaultSupplierId, filterPeriodId, 1).Result;

            var allPurchases = dbContext.Purchases
                .Where(p => p.SupplierId == DefaultSupplierId && !p.IsCancelled && p.BusinessId == TestBusinessId)
                .ToList();

            if (selectPeriod)
            {
                var periodPurchases = allPurchases.Where(p => p.VatSubmissionPeriodId == 1).ToList();
                Assert.Equal(periodPurchases.Sum(p => p.AmountExcludingVat), result.TotalSpend);
                Assert.Equal(periodPurchases.Count, result.TotalPurchases);
            }
            else
            {
                Assert.Equal(allPurchases.Sum(p => p.AmountExcludingVat), result.TotalSpend);
                Assert.Equal(allPurchases.Count, result.TotalPurchases);
            }
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 4: Total Spend Computation

    /// <summary>
    /// Property 4: Total Spend Computation
    /// TotalSpend equals the sum of AmountExcludingVat for all non-cancelled purchases.
    /// Equals zero when the list is empty.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public void TotalSpend_EqualsSumOfAmountExcludingVat(PositiveInt[] amountSeeds)
    {
        var (dbContext, service) = CreateTestContext();
        try
        {
            var purchaseCount = Math.Min(amountSeeds.Length, 15);

            for (int i = 0; i < purchaseCount; i++)
            {
                var amount = GenerateAmount(amountSeeds[i].Get);
                var date = GenerateDate(amountSeeds[i].Get + i);
                dbContext.Purchases.Add(CreatePurchase(
                    i + 1, DefaultSupplierId, TestBusinessId, DefaultCategoryId,
                    date, amount, false));
            }
            dbContext.SaveChanges();

            var result = service.GetDashboardAsync(DefaultSupplierId, null, 1).Result;

            var expectedSpend = dbContext.Purchases
                .Where(p => p.SupplierId == DefaultSupplierId && !p.IsCancelled && p.BusinessId == TestBusinessId)
                .Sum(p => p.AmountExcludingVat);

            Assert.Equal(expectedSpend, result.TotalSpend);

            if (purchaseCount == 0)
                Assert.Equal(0m, result.TotalSpend);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 5: Total Purchases Count

    /// <summary>
    /// Property 5: Total Purchases Count
    /// TotalPurchases equals the count of non-cancelled purchases.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public void TotalPurchases_EqualsCountOfNonCancelled(PositiveInt[] amountSeeds)
    {
        var (dbContext, service) = CreateTestContext();
        try
        {
            var purchaseCount = Math.Min(amountSeeds.Length, 15);

            for (int i = 0; i < purchaseCount; i++)
            {
                var amount = GenerateAmount(amountSeeds[i].Get);
                var date = GenerateDate(amountSeeds[i].Get + i);
                dbContext.Purchases.Add(CreatePurchase(
                    i + 1, DefaultSupplierId, TestBusinessId, DefaultCategoryId,
                    date, amount, false));
            }
            dbContext.SaveChanges();

            var result = service.GetDashboardAsync(DefaultSupplierId, null, 1).Result;

            Assert.Equal(purchaseCount, result.TotalPurchases);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 6: Average Monthly Spend Computation

    /// <summary>
    /// Property 6: Average Monthly Spend Computation
    /// AverageMonthlySpend equals TotalSpend / distinctMonths where distinctMonths
    /// is the count of unique (year, month) pairs. Zero when no purchases exist.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public void AverageMonthlySpend_EqualsSpendDividedByDistinctMonths(PositiveInt[] amountSeeds)
    {
        var (dbContext, service) = CreateTestContext();
        try
        {
            var purchaseCount = Math.Min(amountSeeds.Length, 15);

            for (int i = 0; i < purchaseCount; i++)
            {
                var amount = GenerateAmount(amountSeeds[i].Get);
                var date = GenerateDate(amountSeeds[i].Get + i);
                dbContext.Purchases.Add(CreatePurchase(
                    i + 1, DefaultSupplierId, TestBusinessId, DefaultCategoryId,
                    date, amount, false));
            }
            dbContext.SaveChanges();

            var result = service.GetDashboardAsync(DefaultSupplierId, null, 1).Result;

            if (purchaseCount == 0)
            {
                Assert.Equal(0m, result.AverageMonthlySpend);
            }
            else
            {
                var purchases = dbContext.Purchases
                    .Where(p => p.SupplierId == DefaultSupplierId && !p.IsCancelled && p.BusinessId == TestBusinessId)
                    .ToList();

                var totalSpend = purchases.Sum(p => p.AmountExcludingVat);
                var distinctMonths = purchases
                    .Select(p => new { p.InvoiceDate.Year, p.InvoiceDate.Month })
                    .Distinct()
                    .Count();

                var expectedAvg = distinctMonths > 0 ? totalSpend / distinctMonths : 0m;
                Assert.Equal(expectedAvg, result.AverageMonthlySpend);
            }
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 7: Spend Share Ranking and Aggregation

    /// <summary>
    /// Property 7: Spend Share Ranking and Aggregation
    /// Exactly one slice for the current supplier, at most 5 slices for other suppliers
    /// ordered by descending spend, an "Others" slice when more than 5 other suppliers exist.
    /// Sum of all slices equals total spend across all suppliers.
    /// **Validates: Requirements 7.2, 13.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public void SpendShare_CorrectStructureAndAggregation(PositiveInt supplierCount)
    {
        var numOtherSuppliers = (supplierCount.Get % 8) + 1; // 1 to 8 other suppliers

        var (dbContext, service) = CreateTestContext();
        try
        {
            // Create other suppliers
            for (int i = 0; i < numOtherSuppliers; i++)
            {
                dbContext.Suppliers.Add(new Supplier
                {
                    Id = 10 + i,
                    BusinessId = TestBusinessId,
                    Name = $"Supplier {(char)('A' + i)}",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            // Add purchases for current supplier
            dbContext.Purchases.Add(CreatePurchase(
                1, DefaultSupplierId, TestBusinessId, DefaultCategoryId,
                new DateOnly(2024, 3, 15), 500m, false));

            // Add purchases for other suppliers with decreasing amounts
            for (int i = 0; i < numOtherSuppliers; i++)
            {
                var amount = (numOtherSuppliers - i) * 100m;
                dbContext.Purchases.Add(CreatePurchase(
                    10 + i, 10 + i, TestBusinessId, DefaultCategoryId,
                    new DateOnly(2024, 3, 15), amount, false));
            }
            dbContext.SaveChanges();

            var result = service.GetDashboardAsync(DefaultSupplierId, null, 1).Result;

            // Exactly one slice for current supplier
            var currentSlices = result.SpendShareData.Where(s => s.IsCurrentSupplier).ToList();
            Assert.Single(currentSlices);

            // At most 5 non-current, non-Others slices
            var otherSlices = result.SpendShareData
                .Where(s => !s.IsCurrentSupplier && s.SupplierName != "Others")
                .ToList();
            Assert.True(otherSlices.Count <= 5);

            // "Others" slice exists only when more than 5 other suppliers
            var othersSlice = result.SpendShareData.FirstOrDefault(s => s.SupplierName == "Others");
            if (numOtherSuppliers > 5)
            {
                Assert.NotNull(othersSlice);
                // Others amount = sum of remaining suppliers beyond top 5
                var allOtherAmounts = Enumerable.Range(0, numOtherSuppliers)
                    .Select(i => (numOtherSuppliers - i) * 100m)
                    .OrderByDescending(a => a)
                    .ToList();
                var expectedOthers = allOtherAmounts.Skip(5).Sum();
                Assert.Equal(expectedOthers, othersSlice.Amount);
            }
            else
            {
                Assert.Null(othersSlice);
            }

            // Sum of all slices equals total spend across all suppliers
            var totalSliceAmount = result.SpendShareData.Sum(s => s.Amount);
            var totalDbSpend = dbContext.Purchases
                .Where(p => !p.IsCancelled && p.BusinessId == TestBusinessId)
                .Sum(p => p.AmountExcludingVat);
            Assert.Equal(totalDbSpend, totalSliceAmount);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 8: Monthly Spend Bar Values

    /// <summary>
    /// Property 8: Monthly Spend Bar Values
    /// Each MonthlySpendBar.Amount equals the sum of AmountExcludingVat for purchases in that month.
    /// **Validates: Requirements 8.2, 8.3, 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public void MonthlySpendBars_SumCorrectlyPerMonth(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0) return;

        var (dbContext, service) = CreateTestContext();
        try
        {
            var purchaseCount = Math.Min(amountSeeds.Length, 15);

            for (int i = 0; i < purchaseCount; i++)
            {
                var amount = GenerateAmount(amountSeeds[i].Get);
                var date = GenerateDate(amountSeeds[i].Get + i);
                dbContext.Purchases.Add(CreatePurchase(
                    i + 1, DefaultSupplierId, TestBusinessId, DefaultCategoryId,
                    date, amount, false));
            }
            dbContext.SaveChanges();

            var result = service.GetDashboardAsync(DefaultSupplierId, null, 1).Result;

            // Verify each bar matches the sum for that month
            var purchases = dbContext.Purchases
                .Where(p => p.SupplierId == DefaultSupplierId && !p.IsCancelled && p.BusinessId == TestBusinessId)
                .ToList();

            var expectedByMonth = purchases
                .GroupBy(p => new { p.InvoiceDate.Year, p.InvoiceDate.Month })
                .ToDictionary(
                    g => new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM"),
                    g => g.Sum(p => p.AmountExcludingVat));

            foreach (var bar in result.MonthlySpendData)
            {
                Assert.True(expectedByMonth.ContainsKey(bar.MonthLabel),
                    $"Unexpected month label: {bar.MonthLabel}");
                Assert.Equal(expectedByMonth[bar.MonthLabel], bar.Amount);
            }

            // Total of all bars equals total spend
            Assert.Equal(purchases.Sum(p => p.AmountExcludingVat),
                result.MonthlySpendData.Sum(b => b.Amount));
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 9: Period Spend Bar Values

    /// <summary>
    /// Property 9: Period Spend Bar Values
    /// Each PeriodSpendBar.Amount equals the sum of AmountExcludingVat for non-cancelled
    /// purchases assigned to that period. Periods with no purchases show zero.
    /// **Validates: Requirements 9.2, 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public void PeriodSpendBars_SumCorrectlyPerPeriod(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0) return;

        var (dbContext, service) = CreateTestContext();
        try
        {
            // Create 3 periods
            dbContext.VatSubmissionPeriods.AddRange(
                new VatSubmissionPeriod
                {
                    Id = 1, BusinessId = TestBusinessId, PeriodLabel = "P1",
                    PeriodStartDate = new DateOnly(2024, 1, 1),
                    PeriodEndDate = new DateOnly(2024, 3, 31),
                    CreatedAtUtc = DateTime.UtcNow
                },
                new VatSubmissionPeriod
                {
                    Id = 2, BusinessId = TestBusinessId, PeriodLabel = "P2",
                    PeriodStartDate = new DateOnly(2024, 4, 1),
                    PeriodEndDate = new DateOnly(2024, 6, 30),
                    CreatedAtUtc = DateTime.UtcNow
                },
                new VatSubmissionPeriod
                {
                    Id = 3, BusinessId = TestBusinessId, PeriodLabel = "P3",
                    PeriodStartDate = new DateOnly(2024, 7, 1),
                    PeriodEndDate = new DateOnly(2024, 9, 30),
                    CreatedAtUtc = DateTime.UtcNow
                });

            var purchaseCount = Math.Min(amountSeeds.Length, 12);

            // Assign purchases to periods 1 and 2 only (period 3 should show zero)
            for (int i = 0; i < purchaseCount; i++)
            {
                var amount = GenerateAmount(amountSeeds[i].Get);
                var periodId = (i % 2 == 0) ? 1 : 2;
                var date = periodId == 1
                    ? new DateOnly(2024, 2, 1).AddDays(i % 28)
                    : new DateOnly(2024, 5, 1).AddDays(i % 28);
                dbContext.Purchases.Add(CreatePurchase(
                    i + 1, DefaultSupplierId, TestBusinessId, DefaultCategoryId,
                    date, amount, false, periodId));
            }
            dbContext.SaveChanges();

            var result = service.GetDashboardAsync(DefaultSupplierId, null, 1).Result;

            // Verify 3 period bars exist
            Assert.Equal(3, result.PeriodSpendData.Count);

            // Verify amounts per period
            var purchases = dbContext.Purchases
                .Where(p => p.SupplierId == DefaultSupplierId && !p.IsCancelled && p.BusinessId == TestBusinessId)
                .ToList();

            foreach (var bar in result.PeriodSpendData)
            {
                var expectedAmount = purchases
                    .Where(p => p.VatSubmissionPeriodId == bar.PeriodId)
                    .Sum(p => p.AmountExcludingVat);
                Assert.Equal(expectedAmount, bar.Amount);
            }

            // Period 3 should be zero
            Assert.Equal(0m, result.PeriodSpendData.First(b => b.PeriodId == 3).Amount);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 10: Purchases Table Sorting

    /// <summary>
    /// Property 10: Purchases Table Sorting
    /// The returned Purchases list is sorted by InvoiceDate ascending for any page.
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public void PurchasesTable_SortedByInvoiceDateAscending(PositiveInt[] amountSeeds)
    {
        if (amountSeeds.Length == 0) return;

        var (dbContext, service) = CreateTestContext();
        try
        {
            var purchaseCount = Math.Min(amountSeeds.Length, 15);

            for (int i = 0; i < purchaseCount; i++)
            {
                var amount = GenerateAmount(amountSeeds[i].Get);
                var date = GenerateDate(amountSeeds[i].Get + i);
                dbContext.Purchases.Add(CreatePurchase(
                    i + 1, DefaultSupplierId, TestBusinessId, DefaultCategoryId,
                    date, amount, false));
            }
            dbContext.SaveChanges();

            // Test page 1
            var result = service.GetDashboardAsync(DefaultSupplierId, null, 1).Result;

            // Verify sorting
            for (int i = 1; i < result.Purchases.Count; i++)
            {
                Assert.True(result.Purchases[i].InvoiceDate >= result.Purchases[i - 1].InvoiceDate,
                    $"Purchases not sorted: {result.Purchases[i - 1].InvoiceDate} > {result.Purchases[i].InvoiceDate}");
            }

            // If there are multiple pages, test page 2 as well
            if (purchaseCount > 10)
            {
                var result2 = service.GetDashboardAsync(DefaultSupplierId, null, 2).Result;
                for (int i = 1; i < result2.Purchases.Count; i++)
                {
                    Assert.True(result2.Purchases[i].InvoiceDate >= result2.Purchases[i - 1].InvoiceDate,
                        $"Page 2 not sorted: {result2.Purchases[i - 1].InvoiceDate} > {result2.Purchases[i].InvoiceDate}");
                }
            }
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 11: Pagination Correctness

    /// <summary>
    /// Property 11: Pagination Correctness
    /// The returned page contains exactly min(10, N - (P-1)*10) records (or 0 if P exceeds total pages).
    /// Pagination info correctly reports the range.
    /// **Validates: Requirements 10.4, 10.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public void Pagination_ReturnsCorrectPageSizeAndInfo(PositiveInt recordCount, PositiveInt pageNum)
    {
        var n = (recordCount.Get % 25) + 1; // 1 to 25 records
        var p = (pageNum.Get % 5) + 1; // page 1 to 5

        var (dbContext, service) = CreateTestContext();
        try
        {
            for (int i = 0; i < n; i++)
            {
                var date = new DateOnly(2024, 1, 1).AddDays(i);
                dbContext.Purchases.Add(CreatePurchase(
                    i + 1, DefaultSupplierId, TestBusinessId, DefaultCategoryId,
                    date, (i + 1) * 10m, false));
            }
            dbContext.SaveChanges();

            var result = service.GetDashboardAsync(DefaultSupplierId, null, p).Result;

            var totalPages = (int)Math.Ceiling(n / 10.0);
            var clampedPage = Math.Max(1, Math.Min(p, totalPages));

            var expectedRecordsOnPage = Math.Min(10, n - (clampedPage - 1) * 10);

            Assert.Equal(expectedRecordsOnPage, result.Purchases.Count);
            Assert.Equal(clampedPage, result.CurrentPage);
            Assert.Equal(totalPages, result.TotalPages);
            Assert.Equal(n, result.TotalRecords);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 12: Period Dropdown Ordering

    /// <summary>
    /// Property 12: Period Dropdown Ordering
    /// The Periods list is ordered by PeriodStartDate ascending.
    /// **Validates: Requirements 6.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public void PeriodDropdown_OrderedByStartDateAscending(PositiveInt[] periodSeeds)
    {
        if (periodSeeds.Length == 0) return;

        var (dbContext, service) = CreateTestContext();
        try
        {
            var periodCount = Math.Min(periodSeeds.Length, 8);

            for (int i = 0; i < periodCount; i++)
            {
                // Generate random start dates spread across years
                var baseDate = new DateOnly(2023, 1, 1);
                var daysOffset = Math.Abs(periodSeeds[i].Get) % 730; // ~2 years
                var startDate = baseDate.AddDays(daysOffset);
                var endDate = startDate.AddMonths(3).AddDays(-1);

                dbContext.VatSubmissionPeriods.Add(new VatSubmissionPeriod
                {
                    Id = i + 1,
                    BusinessId = TestBusinessId,
                    PeriodLabel = $"Period {i + 1}",
                    PeriodStartDate = startDate,
                    PeriodEndDate = endDate,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
            dbContext.SaveChanges();

            var result = service.GetDashboardAsync(DefaultSupplierId, null, 1).Result;

            // Verify periods are ordered by start date ascending
            Assert.Equal(periodCount, result.Periods.Count);

            var expectedOrder = dbContext.VatSubmissionPeriods
                .Where(p => p.BusinessId == TestBusinessId)
                .OrderBy(p => p.PeriodStartDate)
                .Select(p => p.Id)
                .ToList();

            for (int i = 0; i < result.Periods.Count; i++)
            {
                Assert.Equal(expectedOrder[i], result.Periods[i].Id);
            }

            // Verify ordering invariant: each period's ID matches the sorted order
            // (SelectedPeriodId is null representing "All Time" as default)
            Assert.Null(result.SelectedPeriodId);
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion
}
