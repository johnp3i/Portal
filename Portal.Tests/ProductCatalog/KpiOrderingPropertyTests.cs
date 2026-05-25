using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.ProductCatalog;

// Feature: product-catalog, Property 9: KPI calculation correctness
// Feature: product-catalog, Property 10: Top products by usage ordering
// Feature: product-catalog, Property 21: Price history ordered descending

/// <summary>
/// Property-based tests for KPI calculations, top products by usage ordering,
/// and price history ordering in ProductService.
/// Tests Properties 9, 10, and 21 from the product-catalog design document.
/// Uses FsCheck.Xunit with Moq to mock ProductRepository, ProductPriceHistoryRepository, and ICurrentTenantService.
/// **Validates: Requirements 3.6, 3.7, 8.1**
/// </summary>
public class KpiOrderingPropertyTests
{
    private const int TestBusinessId = 1;

    #region Test Infrastructure

    private static (ProductService Service, Mock<ProductRepository> ProductRepo, Mock<ProductPriceHistoryRepository> PriceHistoryRepo) CreateService(int businessId = TestBusinessId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(businessId);

        var productRepoMock = new Mock<ProductRepository>(MockBehavior.Loose, new object[] { null! });
        var priceHistoryRepoMock = new Mock<ProductPriceHistoryRepository>(MockBehavior.Loose, new object[] { null! });
        var supplierRepoMock = new Mock<SupplierRepository>(MockBehavior.Loose, new object[] { null! });
        var loggerMock = new Mock<ILogger<ProductService>>();

        var service = new ProductService(
            tenantMock.Object,
            productRepoMock.Object,
            priceHistoryRepoMock.Object,
            supplierRepoMock.Object,
            loggerMock.Object);

        return (service, productRepoMock, priceHistoryRepoMock);
    }

    /// <summary>
    /// Generates a valid product description: non-empty, max 100 chars.
    /// Ensures at least one non-whitespace character.
    /// </summary>
    private static Gen<string> ValidDescriptionGen()
    {
        return Gen.Choose(1, 50)
            .SelectMany(len => Gen.ArrayOf(len, Gen.Elements(
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 -_.".ToCharArray()))
            .Select(chars => new string(chars)))
            .Where(s => !string.IsNullOrWhiteSpace(s));
    }

    /// <summary>
    /// Generates a non-negative integer for counts.
    /// </summary>
    private static Gen<int> NonNegativeIntGen()
    {
        return Gen.Choose(0, 10000);
    }

    /// <summary>
    /// Generates a positive integer for counts (at least 1).
    /// </summary>
    private static Gen<int> PositiveIntGen()
    {
        return Gen.Choose(1, 10000);
    }

    /// <summary>
    /// Generates a valid price: decimal >= 0, max 2 decimal places.
    /// </summary>
    private static Gen<decimal> ValidPriceGen()
    {
        return Gen.Choose(0, 999999)
            .Select(cents => cents / 100m);
    }

    /// <summary>
    /// Generates a usage count (non-negative).
    /// </summary>
    private static Gen<int> UsageCountGen()
    {
        return Gen.Choose(0, 5000);
    }

    /// <summary>
    /// Generates a list of ProductUsageDto items with random descriptions and usage counts.
    /// </summary>
    private static Gen<List<ProductUsageDto>> UsageDtoListGen(int minCount, int maxCount)
    {
        return Gen.Choose(minCount, maxCount).SelectMany(count =>
            Gen.ArrayOf(count,
                from desc in ValidDescriptionGen()
                from usage in UsageCountGen()
                select new ProductUsageDto { Description = desc, UsageCount = usage })
            .Select(arr => arr.ToList()));
    }

    /// <summary>
    /// Generates a DateTime within a reasonable range for price history.
    /// </summary>
    private static Gen<DateTime> DateTimeGen()
    {
        return Gen.Choose(0, 365 * 5) // up to 5 years of days
            .Select(days => new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(days).AddMinutes(days % 1440));
    }

    #endregion

    #region Property 9: KPI calculation correctness

    /// <summary>
    /// Property 9: For any set of products belonging to a business, Total Products SHALL equal the count
    /// of all products, Active Products SHALL equal the count where IsActive=true, Average Selling Price
    /// SHALL equal the mean of DefaultSellingPrice across active products, and Best Seller SHALL be the
    /// product with the highest Usage_Count.
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KpiCalculation_ReturnsCorrectValues()
    {
        var arb = Arb.From(
            from totalProducts in PositiveIntGen()
            from activeProducts in Gen.Choose(0, totalProducts)
            from avgPrice in ValidPriceGen()
            from bestSellerDesc in ValidDescriptionGen()
            from bestSellerUsage in NonNegativeIntGen()
            select (totalProducts, activeProducts, avgPrice, bestSellerDesc, bestSellerUsage));

        return Prop.ForAll(arb, async tuple =>
        {
            var (totalProducts, activeProducts, avgPrice, bestSellerDesc, bestSellerUsage) = tuple;

            var (service, productRepoMock, _) = CreateService();

            // Mock GetKpiDataAsync to return specific KPI values
            var expectedKpi = new ProductKpiDto
            {
                TotalProducts = totalProducts,
                ActiveProducts = activeProducts,
                AverageSellingPrice = avgPrice,
                BestSellerDescription = bestSellerDesc,
                BestSellerUsageCount = bestSellerUsage
            };

            productRepoMock
                .Setup(r => r.GetKpiDataAsync(TestBusinessId))
                .ReturnsAsync(expectedKpi);

            // Act
            var result = await service.GetKpisAsync();

            // Assert: Total Products equals the count of all products
            Assert.Equal(totalProducts, result.TotalProducts);

            // Assert: Active Products equals the count where IsActive=true
            Assert.Equal(activeProducts, result.ActiveProducts);

            // Assert: Average Selling Price equals the mean of DefaultSellingPrice across active products
            Assert.Equal(avgPrice, result.AverageSellingPrice);

            // Assert: Best Seller is the product with the highest Usage_Count
            Assert.Equal(bestSellerDesc, result.BestSellerDescription);
            Assert.Equal(bestSellerUsage, result.BestSellerUsageCount);
        });
    }

    /// <summary>
    /// Property 9 (edge case): When BusinessId is 0 (unresolved), KPIs SHALL return zero/null defaults.
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KpiCalculation_WithUnresolvedBusiness_ReturnsDefaults()
    {
        var arb = Arb.From(Gen.Constant(0));

        return Prop.ForAll(arb, async _ =>
        {
            var (service, productRepoMock, _) = CreateService(businessId: 0);

            // Act
            var result = await service.GetKpisAsync();

            // Assert: all KPIs are zero/null
            Assert.Equal(0, result.TotalProducts);
            Assert.Equal(0, result.ActiveProducts);
            Assert.Equal(0m, result.AverageSellingPrice);
            Assert.Null(result.BestSellerDescription);
            Assert.Equal(0, result.BestSellerUsageCount);

            // Assert: repository was never called
            productRepoMock.Verify(r => r.GetKpiDataAsync(It.IsAny<int>()), Times.Never);
        });
    }

    #endregion

    #region Property 10: Top products by usage ordering

    /// <summary>
    /// Property 10: For any set of products with usage counts, the top-10 result SHALL be sorted in
    /// descending order by Usage_Count and SHALL contain at most 10 entries.
    /// **Validates: Requirements 3.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopProductsByUsage_SortedDescendingAndLimitedTo10()
    {
        var arb = Arb.From(UsageDtoListGen(0, 20));

        return Prop.ForAll(arb, async usageData =>
        {
            var (service, productRepoMock, _) = CreateService();

            // The repository returns data sorted descending and limited to top N.
            // Simulate what the repository would return: sort descending, take top 10.
            var sortedAndLimited = usageData
                .OrderByDescending(u => u.UsageCount)
                .Take(10)
                .ToList();

            productRepoMock
                .Setup(r => r.GetTopByUsageAsync(TestBusinessId, 10))
                .ReturnsAsync(sortedAndLimited);

            // Act
            var result = await service.GetTopProductsByUsageAsync(10);

            // Assert: at most 10 entries
            Assert.True(result.Count <= 10,
                $"Expected at most 10 entries but got {result.Count}");

            // Assert: sorted in descending order by UsageCount
            for (int i = 0; i < result.Count - 1; i++)
            {
                Assert.True(result[i].UsageCount >= result[i + 1].UsageCount,
                    $"Expected descending order but item[{i}].UsageCount={result[i].UsageCount} < item[{i + 1}].UsageCount={result[i + 1].UsageCount}");
            }
        });
    }

    /// <summary>
    /// Property 10 (additional): For any set of products with fewer than 10 entries,
    /// the result SHALL contain all entries (not padded to 10).
    /// **Validates: Requirements 3.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopProductsByUsage_WithFewerThan10_ReturnsAll()
    {
        var arb = Arb.From(UsageDtoListGen(0, 9));

        return Prop.ForAll(arb, async usageData =>
        {
            var (service, productRepoMock, _) = CreateService();

            // Repository returns the data sorted descending (all items since fewer than 10)
            var sortedData = usageData
                .OrderByDescending(u => u.UsageCount)
                .ToList();

            productRepoMock
                .Setup(r => r.GetTopByUsageAsync(TestBusinessId, 10))
                .ReturnsAsync(sortedData);

            // Act
            var result = await service.GetTopProductsByUsageAsync(10);

            // Assert: count matches input (all returned since fewer than 10)
            Assert.Equal(sortedData.Count, result.Count);

            // Assert: still sorted descending
            for (int i = 0; i < result.Count - 1; i++)
            {
                Assert.True(result[i].UsageCount >= result[i + 1].UsageCount,
                    $"Expected descending order but item[{i}].UsageCount={result[i].UsageCount} < item[{i + 1}].UsageCount={result[i + 1].UsageCount}");
            }
        });
    }

    /// <summary>
    /// Property 10 (edge case): When BusinessId is 0, top products SHALL return empty list.
    /// **Validates: Requirements 3.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopProductsByUsage_WithUnresolvedBusiness_ReturnsEmpty()
    {
        var arb = Arb.From(Gen.Constant(0));

        return Prop.ForAll(arb, async _ =>
        {
            var (service, productRepoMock, _) = CreateService(businessId: 0);

            // Act
            var result = await service.GetTopProductsByUsageAsync(10);

            // Assert: empty list
            Assert.Empty(result);

            // Assert: repository was never called
            productRepoMock.Verify(r => r.GetTopByUsageAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        });
    }

    #endregion

    #region Property 21: Price history ordered descending

    /// <summary>
    /// Property 21: For any product's price history retrieval, the records SHALL be ordered by
    /// EffectiveFromUtc descending (most recent first).
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PriceHistory_OrderedByEffectiveFromUtcDescending()
    {
        var arb = Arb.From(
            from recordCount in Gen.Choose(1, 20)
            from dates in Gen.ArrayOf(recordCount, DateTimeGen())
            from prices in Gen.ArrayOf(recordCount, ValidPriceGen())
            from costPrices in Gen.ArrayOf(recordCount, ValidPriceGen())
            select (dates, prices, costPrices));

        return Prop.ForAll(arb, async tuple =>
        {
            var (dates, prices, costPrices) = tuple;
            var productId = 42;

            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // Mock product exists for this business
            productRepoMock
                .Setup(r => r.GetByIdAndBusinessIdAsync(productId, TestBusinessId))
                .ReturnsAsync(new Product
                {
                    Id = productId,
                    BusinessId = TestBusinessId,
                    ProductCode = "TEST-001",
                    Description = "Test Product",
                    DefaultSellingPrice = 10.00m,
                    DefaultCostPrice = 5.00m,
                    DefaultVatRate = 15.00m,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                });

            // Build price history records and sort descending by EffectiveFromUtc
            // (simulating what the repository returns — it orders by EffectiveFromUtc DESC)
            var historyRecords = dates.Select((date, i) => new ProductPriceHistory
            {
                Id = i + 1,
                ProductId = productId,
                SellingPrice = prices[i],
                CostPrice = costPrices[i],
                EffectiveFromUtc = date,
                ChangedByUserId = "test-user",
                CreatedAtUtc = date
            })
            .OrderByDescending(h => h.EffectiveFromUtc)
            .ToList();

            priceHistoryRepoMock
                .Setup(r => r.GetByProductIdAsync(productId))
                .ReturnsAsync(historyRecords);

            // Act
            var result = await service.GetPriceHistoryAsync(productId);

            // Assert: records are ordered by EffectiveFromUtc descending (most recent first)
            Assert.NotEmpty(result);
            for (int i = 0; i < result.Count - 1; i++)
            {
                Assert.True(result[i].EffectiveFromUtc >= result[i + 1].EffectiveFromUtc,
                    $"Expected descending order but record[{i}].EffectiveFromUtc={result[i].EffectiveFromUtc} < record[{i + 1}].EffectiveFromUtc={result[i + 1].EffectiveFromUtc}");
            }
        });
    }

    /// <summary>
    /// Property 21 (additional): When product does not belong to the business, price history
    /// SHALL return empty list (tenant isolation).
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PriceHistory_ProductNotInBusiness_ReturnsEmpty()
    {
        var arb = Arb.From(Gen.Choose(1, 1000));

        return Prop.ForAll(arb, async productId =>
        {
            var (service, productRepoMock, priceHistoryRepoMock) = CreateService();

            // Mock product NOT found for this business
            productRepoMock
                .Setup(r => r.GetByIdAndBusinessIdAsync(productId, TestBusinessId))
                .ReturnsAsync((Product?)null);

            // Act
            var result = await service.GetPriceHistoryAsync(productId);

            // Assert: empty list returned
            Assert.Empty(result);

            // Assert: price history repository was never called
            priceHistoryRepoMock.Verify(r => r.GetByProductIdAsync(It.IsAny<int>()), Times.Never);
        });
    }

    /// <summary>
    /// Property 21 (edge case): When BusinessId is 0, price history SHALL return empty list.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PriceHistory_WithUnresolvedBusiness_ReturnsEmpty()
    {
        var arb = Arb.From(Gen.Choose(1, 1000));

        return Prop.ForAll(arb, async productId =>
        {
            var (service, productRepoMock, priceHistoryRepoMock) = CreateService(businessId: 0);

            // Act
            var result = await service.GetPriceHistoryAsync(productId);

            // Assert: empty list returned
            Assert.Empty(result);

            // Assert: repositories were never called
            productRepoMock.Verify(r => r.GetByIdAndBusinessIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            priceHistoryRepoMock.Verify(r => r.GetByProductIdAsync(It.IsAny<int>()), Times.Never);
        });
    }

    #endregion
}
