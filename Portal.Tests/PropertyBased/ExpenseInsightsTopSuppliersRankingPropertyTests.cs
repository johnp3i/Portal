using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.ExpenseInsights;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: expense-categorisation-insights, Property 6: Top suppliers ranking

/// <summary>
/// Property-based tests for ExpenseInsightsService top suppliers ranking.
/// Validates that for any set of non-cancelled purchases within a category,
/// the top suppliers list SHALL:
/// - Contain at most 3 entries
/// - Be ordered by TotalSpend descending, with SupplierId ascending as tie-breaker
/// - Each supplier's PercentageOfCategory equals (supplierSpend / categoryTotal) × 100 rounded to 1 decimal place
/// - Only include suppliers with spend > 0
/// **Validates: Requirements 8.1, 8.3, 8.4**
/// </summary>
public class ExpenseInsightsTopSuppliersRankingPropertyTests
{
    private const int TestBusinessId = 1;
    private const int TestCategoryId = 1;

    #region Test Infrastructure

    /// <summary>
    /// Holds the generated test scenario for a single property test case.
    /// </summary>
    private record TestScenario(
        List<PurchaseData> Purchases,
        DateOnly StartDate,
        DateOnly EndDate);

    /// <summary>
    /// Minimal data structure for generating purchase records.
    /// </summary>
    private record PurchaseData(
        int SupplierId,
        decimal TotalAmount,
        DateOnly InvoiceDate,
        bool IsCancelled);

    /// <summary>
    /// Generates a positive decimal amount from a seed (range: 0.01 to 9999.99).
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = (Math.Abs(seed) % 999999) + 1;
        return raw / 100m;
    }

    /// <summary>
    /// Generates a TestScenario with random purchases for a single category across 1-6 suppliers.
    /// All purchases fall within the date range to ensure they are included in the aggregation.
    /// </summary>
    private static Gen<TestScenario> TestScenarioGen =>
        from purchaseCount in Gen.Choose(1, 12)
        from supplierIds in Gen.ArrayOf(purchaseCount, Gen.Choose(1, 6))
        from amountSeeds in Gen.ArrayOf(purchaseCount, Gen.Choose(1, 999999))
        from cancelFlags in Gen.ArrayOf(purchaseCount, Gen.Elements(true, false))
        from startYear in Gen.Choose(2021, 2024)
        from startMonth in Gen.Choose(1, 12)
        select BuildScenario(purchaseCount, supplierIds, amountSeeds, cancelFlags, startYear, startMonth);

    private static TestScenario BuildScenario(
        int purchaseCount, int[] supplierIds, int[] amountSeeds,
        bool[] cancelFlags, int startYear, int startMonth)
    {
        var startDate = new DateOnly(startYear, startMonth, 1);
        var endDate = startDate.AddDays(30);

        var purchases = new List<PurchaseData>();
        for (int i = 0; i < purchaseCount; i++)
        {
            // Place purchases within the date range (offset by index days, capped to range)
            var invoiceDate = startDate.AddDays(i % 28);
            purchases.Add(new PurchaseData(
                SupplierId: supplierIds[i],
                TotalAmount: GenerateAmount(amountSeeds[i]),
                InvoiceDate: invoiceDate,
                IsCancelled: cancelFlags[i]));
        }

        return new TestScenario(purchases, startDate, endDate);
    }

    /// <summary>
    /// Creates an in-memory PortalDbContext seeded with the test scenario data.
    /// All purchases belong to a single category (TestCategoryId) to test supplier ranking within that category.
    /// </summary>
    private static PortalDbContext CreateSeededDbContext(TestScenario scenario, Mock<ICurrentTenantService> tenantMock)
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"TopSuppliers_{Guid.NewGuid()}")
            .Options;

        var dbContext = new PortalDbContext(options, tenantMock.Object);

        // Seed the business
        dbContext.Businesses.Add(new Business
        {
            Id = TestBusinessId,
            Name = "Test Business",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        // Seed ExpenseType
        dbContext.ExpenseTypes.Add(new ExpenseType { Id = 1, Name = "Services" });

        // Seed Suppliers (1 through 6)
        for (int i = 1; i <= 6; i++)
        {
            dbContext.Suppliers.Add(new Supplier
            {
                Id = i,
                BusinessId = TestBusinessId,
                Name = $"Supplier {i}",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // Seed single ExpenseCategory
        dbContext.ExpenseCategories.Add(new ExpenseCategory
        {
            Id = TestCategoryId,
            BusinessId = TestBusinessId,
            Name = "Test Category",
            IsActive = true,
            ExpenseTypeId = 1,
            CreatedAtUtc = DateTime.UtcNow
        });

        // Seed PurchaseOriginType and PurchaseType (required FK references)
        dbContext.PurchaseOriginTypes.Add(new PurchaseOriginType { Id = 1, Name = "Domestic" });
        dbContext.PurchaseTypes.Add(new PurchaseType { Id = 3, Name = "Expense" });

        // Seed Purchases — all in single category
        for (int i = 0; i < scenario.Purchases.Count; i++)
        {
            var p = scenario.Purchases[i];
            dbContext.Purchases.Add(new Purchase
            {
                Id = i + 1,
                BusinessId = TestBusinessId,
                SupplierId = p.SupplierId,
                ExpenseCategoryId = TestCategoryId,
                PurchaseOriginTypeId = 1,
                PurchaseTypeId = 3,
                InvoiceNumber = $"INV-{i + 1:D4}",
                InvoiceDate = p.InvoiceDate,
                Description = $"Test purchase {i + 1}",
                AmountExcludingVat = Math.Round(p.TotalAmount / 1.15m, 2),
                VatAmount = Math.Round(p.TotalAmount - (p.TotalAmount / 1.15m), 2),
                TotalAmount = p.TotalAmount,
                IsCancelled = p.IsCancelled,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        dbContext.SaveChanges();
        return dbContext;
    }

    /// <summary>
    /// Computes the expected top suppliers from the raw purchase data (oracle/reference implementation).
    /// Filters to non-cancelled purchases within date range, groups by SupplierId, orders by spend
    /// descending with SupplierId ascending as tie-breaker, takes top 3.
    /// </summary>
    private static List<(int SupplierId, decimal TotalSpend, decimal Percentage)> ComputeExpectedTopSuppliers(TestScenario scenario)
    {
        var filtered = scenario.Purchases
            .Where(p => !p.IsCancelled
                        && p.InvoiceDate >= scenario.StartDate
                        && p.InvoiceDate <= scenario.EndDate)
            .ToList();

        var categoryTotal = filtered.Sum(p => p.TotalAmount);
        if (categoryTotal == 0)
            return new List<(int, decimal, decimal)>();

        return filtered
            .GroupBy(p => p.SupplierId)
            .Select(g => (
                SupplierId: g.Key,
                TotalSpend: g.Sum(p => p.TotalAmount),
                Percentage: Math.Round((g.Sum(p => p.TotalAmount) / categoryTotal) * 100m, 1)))
            .Where(s => s.TotalSpend > 0)
            .OrderByDescending(s => s.TotalSpend)
            .ThenBy(s => s.SupplierId)
            .Take(3)
            .ToList();
    }

    #endregion

    #region Property 6: Top suppliers ranking

    /// <summary>
    /// For any set of non-cancelled purchases within a category, the top suppliers list
    /// SHALL contain at most 3 entries.
    /// **Validates: Requirements 8.1, 8.3, 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopSuppliers_ContainsAtMost3Entries()
    {
        return Prop.ForAll(
            TestScenarioGen.ToArbitrary(),
            scenario =>
            {
                // Arrange
                var tenantMock = new Mock<ICurrentTenantService>();
                tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

                using var dbContext = CreateSeededDbContext(scenario, tenantMock);
                var service = new ExpenseInsightsService(dbContext, tenantMock.Object);

                var request = new ExpenseInsightsPeriodRequest
                {
                    PeriodType = PnlPeriodType.Custom,
                    CustomStartDate = scenario.StartDate,
                    CustomEndDate = scenario.EndDate
                };

                // Act
                var result = service.GetInsightsDataAsync(request).GetAwaiter().GetResult();

                // Assert: each category's TopSuppliers has at most 3 entries
                var allCategoriesHaveAtMost3 = result.Categories
                    .All(c => c.TopSuppliers.Count <= 3);

                return allCategoriesHaveAtMost3
                    .ToProperty()
                    .Label($"Categories={result.Categories.Count}, " +
                           $"TopSupplierCounts=[{string.Join(", ", result.Categories.Select(c => c.TopSuppliers.Count))}]");
            });
    }

    /// <summary>
    /// For any set of non-cancelled purchases within a category, the top suppliers list
    /// SHALL be ordered by TotalSpend descending, with SupplierId ascending as tie-breaker.
    /// **Validates: Requirements 8.1, 8.3, 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopSuppliers_AreOrderedBySpendDescThenSupplierIdAsc()
    {
        return Prop.ForAll(
            TestScenarioGen.ToArbitrary(),
            scenario =>
            {
                // Arrange
                var tenantMock = new Mock<ICurrentTenantService>();
                tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

                using var dbContext = CreateSeededDbContext(scenario, tenantMock);
                var service = new ExpenseInsightsService(dbContext, tenantMock.Object);

                var request = new ExpenseInsightsPeriodRequest
                {
                    PeriodType = PnlPeriodType.Custom,
                    CustomStartDate = scenario.StartDate,
                    CustomEndDate = scenario.EndDate
                };

                // Act
                var result = service.GetInsightsDataAsync(request).GetAwaiter().GetResult();

                // Assert: ordering is correct for each category
                var allOrdered = true;
                foreach (var category in result.Categories)
                {
                    var suppliers = category.TopSuppliers;
                    for (int i = 1; i < suppliers.Count; i++)
                    {
                        var prev = suppliers[i - 1];
                        var curr = suppliers[i];

                        // TotalSpend must be descending
                        if (curr.TotalSpend > prev.TotalSpend)
                        {
                            allOrdered = false;
                            break;
                        }

                        // When TotalSpend is equal, SupplierId must be ascending
                        if (curr.TotalSpend == prev.TotalSpend && curr.SupplierId < prev.SupplierId)
                        {
                            allOrdered = false;
                            break;
                        }
                    }

                    if (!allOrdered) break;
                }

                return allOrdered
                    .ToProperty()
                    .Label($"Categories={result.Categories.Count}, " +
                           $"Suppliers=[{string.Join("; ", result.Categories.Select(c => string.Join(",", c.TopSuppliers.Select(s => $"S{s.SupplierId}:{s.TotalSpend}"))))}]");
            });
    }

    /// <summary>
    /// For any set of non-cancelled purchases within a category, each supplier's
    /// PercentageOfCategory SHALL equal (supplierSpend / categoryTotal) × 100 rounded to 1 decimal place.
    /// **Validates: Requirements 8.1, 8.3, 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopSuppliers_PercentageOfCategoryIsCorrect()
    {
        return Prop.ForAll(
            TestScenarioGen.ToArbitrary(),
            scenario =>
            {
                // Arrange
                var tenantMock = new Mock<ICurrentTenantService>();
                tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

                using var dbContext = CreateSeededDbContext(scenario, tenantMock);
                var service = new ExpenseInsightsService(dbContext, tenantMock.Object);

                var request = new ExpenseInsightsPeriodRequest
                {
                    PeriodType = PnlPeriodType.Custom,
                    CustomStartDate = scenario.StartDate,
                    CustomEndDate = scenario.EndDate
                };

                // Act
                var result = service.GetInsightsDataAsync(request).GetAwaiter().GetResult();

                // Assert: percentages match the formula
                var allPercentagesCorrect = true;
                foreach (var category in result.Categories)
                {
                    var categoryTotal = category.TotalSpend;
                    if (categoryTotal == 0) continue;

                    foreach (var supplier in category.TopSuppliers)
                    {
                        var expectedPercentage = Math.Round((supplier.TotalSpend / categoryTotal) * 100m, 1);
                        if (supplier.PercentageOfCategory != expectedPercentage)
                        {
                            allPercentagesCorrect = false;
                            break;
                        }
                    }

                    if (!allPercentagesCorrect) break;
                }

                return allPercentagesCorrect
                    .ToProperty()
                    .Label($"Categories={result.Categories.Count}, " +
                           $"Percentages=[{string.Join("; ", result.Categories.Select(c => string.Join(",", c.TopSuppliers.Select(s => $"S{s.SupplierId}:{s.PercentageOfCategory}%"))))}]");
            });
    }

    /// <summary>
    /// For any set of non-cancelled purchases within a category, the top suppliers list
    /// SHALL only include suppliers with spend > 0.
    /// **Validates: Requirements 8.1, 8.3, 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopSuppliers_OnlyIncludesSuppliersWithPositiveSpend()
    {
        return Prop.ForAll(
            TestScenarioGen.ToArbitrary(),
            scenario =>
            {
                // Arrange
                var tenantMock = new Mock<ICurrentTenantService>();
                tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

                using var dbContext = CreateSeededDbContext(scenario, tenantMock);
                var service = new ExpenseInsightsService(dbContext, tenantMock.Object);

                var request = new ExpenseInsightsPeriodRequest
                {
                    PeriodType = PnlPeriodType.Custom,
                    CustomStartDate = scenario.StartDate,
                    CustomEndDate = scenario.EndDate
                };

                // Act
                var result = service.GetInsightsDataAsync(request).GetAwaiter().GetResult();

                // Assert: all suppliers have TotalSpend > 0
                var allPositive = result.Categories
                    .SelectMany(c => c.TopSuppliers)
                    .All(s => s.TotalSpend > 0);

                return allPositive
                    .ToProperty()
                    .Label($"Categories={result.Categories.Count}, " +
                           $"AllPositiveSpend={allPositive}");
            });
    }

    /// <summary>
    /// For any set of non-cancelled purchases within a category, the top suppliers ranking
    /// SHALL match the expected reference computation (comprehensive end-to-end validation).
    /// **Validates: Requirements 8.1, 8.3, 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TopSuppliers_MatchesExpectedRanking()
    {
        return Prop.ForAll(
            TestScenarioGen.ToArbitrary(),
            scenario =>
            {
                // Arrange
                var tenantMock = new Mock<ICurrentTenantService>();
                tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

                using var dbContext = CreateSeededDbContext(scenario, tenantMock);
                var service = new ExpenseInsightsService(dbContext, tenantMock.Object);

                var request = new ExpenseInsightsPeriodRequest
                {
                    PeriodType = PnlPeriodType.Custom,
                    CustomStartDate = scenario.StartDate,
                    CustomEndDate = scenario.EndDate
                };

                // Act
                var result = service.GetInsightsDataAsync(request).GetAwaiter().GetResult();

                // Compute expected
                var expected = ComputeExpectedTopSuppliers(scenario);

                // Get actual suppliers from the first (only) category, or empty if no categories
                var actualSuppliers = result.Categories.FirstOrDefault()?.TopSuppliers
                    ?? new List<TopSupplierDto>();

                // Assert 1: Same count
                var countMatches = actualSuppliers.Count == expected.Count;

                // Assert 2: Same supplier IDs in same order
                var idsMatch = actualSuppliers.Select(s => s.SupplierId)
                    .SequenceEqual(expected.Select(e => e.SupplierId));

                // Assert 3: Same spend values
                var spendsMatch = actualSuppliers.Select(s => s.TotalSpend)
                    .SequenceEqual(expected.Select(e => e.TotalSpend));

                // Assert 4: Same percentages
                var percentagesMatch = actualSuppliers.Select(s => s.PercentageOfCategory)
                    .SequenceEqual(expected.Select(e => e.Percentage));

                var allPass = countMatches && idsMatch && spendsMatch && percentagesMatch;

                return allPass
                    .ToProperty()
                    .Label($"Purchases={scenario.Purchases.Count}, " +
                           $"ExpectedSuppliers={expected.Count}, ActualSuppliers={actualSuppliers.Count}, " +
                           $"CountMatch={countMatches}, IdsMatch={idsMatch}, " +
                           $"SpendsMatch={spendsMatch}, PercentagesMatch={percentagesMatch}");
            });
    }

    #endregion
}
