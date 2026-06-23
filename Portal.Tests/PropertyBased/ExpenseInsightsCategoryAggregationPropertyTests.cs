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

// Feature: expense-categorisation-insights, Property 1: Category aggregation correctness

/// <summary>
/// Property-based tests for ExpenseInsightsService category aggregation.
/// Validates that for any set of Purchase records (with varied IsCancelled flags,
/// InvoiceDates, and ExpenseCategoryIds) and for any valid date range, the category
/// breakdown SHALL:
/// - Include only non-cancelled purchases whose InvoiceDate falls within [startDate, endDate]
/// - Group correctly by ExpenseCategoryId
/// - Sum TotalAmount correctly per group
/// - Be ordered by TotalSpend descending
/// **Validates: Requirements 1.1, 1.4, 1.5**
/// </summary>
public class ExpenseInsightsCategoryAggregationPropertyTests
{
    private const int TestBusinessId = 1;

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
        int ExpenseCategoryId,
        int SupplierId,
        decimal TotalAmount,
        DateOnly InvoiceDate,
        bool IsCancelled);

    /// <summary>
    /// Generates a valid DateOnly from a seed within a reasonable range (2020-2025).
    /// </summary>
    private static DateOnly GenerateDateOnly(int seed)
    {
        var baseDays = new DateOnly(2020, 1, 1).DayNumber;
        // Range of ~5 years = ~1826 days
        var dayOffset = Math.Abs(seed) % 1826;
        return DateOnly.FromDayNumber(baseDays + dayOffset);
    }

    /// <summary>
    /// Generates a positive decimal amount from a seed (range: 0.01 to 9999.99).
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = (Math.Abs(seed) % 999999) + 1;
        return raw / 100m;
    }

    /// <summary>
    /// Generates a TestScenario with random purchases and a date range.
    /// </summary>
    private static Gen<TestScenario> TestScenarioGen =>
        from purchaseCount in Gen.Choose(0, 15)
        from categoryIds in Gen.ArrayOf(purchaseCount, Gen.Choose(1, 5))
        from supplierIds in Gen.ArrayOf(purchaseCount, Gen.Choose(1, 3))
        from amountSeeds in Gen.ArrayOf(purchaseCount, Gen.Choose(1, 999999))
        from dateSeeds in Gen.ArrayOf(purchaseCount, Gen.Choose(0, 1825))
        from cancelFlags in Gen.ArrayOf(purchaseCount, Gen.Elements(true, false))
        from startDateSeed in Gen.Choose(0, 1000)
        from rangeDays in Gen.Choose(1, 180)
        select BuildScenario(purchaseCount, categoryIds, supplierIds, amountSeeds, dateSeeds, cancelFlags, startDateSeed, rangeDays);

    private static TestScenario BuildScenario(
        int purchaseCount, int[] categoryIds, int[] supplierIds,
        int[] amountSeeds, int[] dateSeeds, bool[] cancelFlags,
        int startDateSeed, int rangeDays)
    {
        var startDate = GenerateDateOnly(startDateSeed);
        var endDate = startDate.AddDays(rangeDays);

        var purchases = new List<PurchaseData>();
        for (int i = 0; i < purchaseCount; i++)
        {
            purchases.Add(new PurchaseData(
                ExpenseCategoryId: categoryIds[i],
                SupplierId: supplierIds[i],
                TotalAmount: GenerateAmount(amountSeeds[i]),
                InvoiceDate: GenerateDateOnly(dateSeeds[i]),
                IsCancelled: cancelFlags[i]));
        }

        return new TestScenario(purchases, startDate, endDate);
    }

    /// <summary>
    /// Creates an in-memory PortalDbContext with the test business seeded, along with
    /// required lookup data (ExpenseTypes, Suppliers, ExpenseCategories, Purchases).
    /// Returns the DbContext ready for querying via the service.
    /// </summary>
    private static PortalDbContext CreateSeededDbContext(TestScenario scenario, Mock<ICurrentTenantService> tenantMock)
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"CatAggregation_{Guid.NewGuid()}")
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

        // Seed ExpenseTypes
        dbContext.ExpenseTypes.Add(new ExpenseType { Id = 1, Name = "Services" });
        dbContext.ExpenseTypes.Add(new ExpenseType { Id = 2, Name = "Goods" });

        // Seed Suppliers (1 through 3)
        for (int i = 1; i <= 3; i++)
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

        // Seed ExpenseCategories (1 through 5)
        // Alternate ExpenseTypeId: 1 (Services), 2 (Goods), null (Uncategorised)
        for (int i = 1; i <= 5; i++)
        {
            int? expenseTypeId = i <= 2 ? 1 : i <= 4 ? 2 : (int?)null;
            dbContext.ExpenseCategories.Add(new ExpenseCategory
            {
                Id = i,
                BusinessId = TestBusinessId,
                Name = $"Category {i}",
                IsActive = true,
                ExpenseTypeId = expenseTypeId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // Seed PurchaseOriginType and PurchaseType (required FK references)
        dbContext.PurchaseOriginTypes.Add(new PurchaseOriginType { Id = 1, Name = "Domestic" });
        dbContext.PurchaseTypes.Add(new PurchaseType { Id = 3, Name = "Expense" });

        // Seed Purchases
        for (int i = 0; i < scenario.Purchases.Count; i++)
        {
            var p = scenario.Purchases[i];
            dbContext.Purchases.Add(new Purchase
            {
                Id = i + 1,
                BusinessId = TestBusinessId,
                SupplierId = p.SupplierId,
                ExpenseCategoryId = p.ExpenseCategoryId,
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
    /// Computes the expected breakdown from raw purchase data (our oracle/reference implementation).
    /// Filters to non-cancelled purchases within [startDate, endDate], groups by ExpenseCategoryId,
    /// sums TotalAmount per group, orders by TotalSpend descending.
    /// </summary>
    private static List<(int CategoryId, decimal TotalSpend)> ComputeExpectedBreakdown(TestScenario scenario)
    {
        var filtered = scenario.Purchases
            .Where(p => !p.IsCancelled
                        && p.InvoiceDate >= scenario.StartDate
                        && p.InvoiceDate <= scenario.EndDate)
            .ToList();

        if (!filtered.Any())
            return new List<(int, decimal)>();

        return filtered
            .GroupBy(p => p.ExpenseCategoryId)
            .Select(g => (CategoryId: g.Key, TotalSpend: g.Sum(p => p.TotalAmount)))
            .OrderByDescending(x => x.TotalSpend)
            .ToList();
    }

    #endregion

    #region Property 1: Category aggregation correctness

    /// <summary>
    /// For any set of purchases and any valid date range, the category breakdown
    /// SHALL include only non-cancelled purchases whose InvoiceDate falls within
    /// [startDate, endDate], group correctly by ExpenseCategoryId, sum TotalAmount
    /// correctly per group, and be ordered by TotalSpend descending.
    /// **Validates: Requirements 1.1, 1.4, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CategoryBreakdown_MatchesExpectedAggregation()
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
                var expected = ComputeExpectedBreakdown(scenario);

                // Assert 1: Same number of categories
                var categoryCountMatches = result.Categories.Count == expected.Count;

                // Assert 2: Same category IDs in same order
                var actualCategoryIds = result.Categories.Select(c => c.ExpenseCategoryId).ToList();
                var expectedCategoryIds = expected.Select(e => e.CategoryId).ToList();
                var categoryIdsMatch = actualCategoryIds.SequenceEqual(expectedCategoryIds);

                // Assert 3: TotalSpend per category matches
                var spendsMatch = true;
                for (int i = 0; i < expected.Count && i < result.Categories.Count; i++)
                {
                    if (result.Categories[i].TotalSpend != expected[i].TotalSpend)
                    {
                        spendsMatch = false;
                        break;
                    }
                }

                // Assert 4: Ordered by TotalSpend descending
                var isDescending = true;
                for (int i = 1; i < result.Categories.Count; i++)
                {
                    if (result.Categories[i].TotalSpend > result.Categories[i - 1].TotalSpend)
                    {
                        isDescending = false;
                        break;
                    }
                }

                var allPass = categoryCountMatches && categoryIdsMatch && spendsMatch && isDescending;

                return allPass
                    .ToProperty()
                    .Label($"Purchases={scenario.Purchases.Count}, " +
                           $"DateRange=[{scenario.StartDate}..{scenario.EndDate}], " +
                           $"ExpectedCategories={expected.Count}, ActualCategories={result.Categories.Count}, " +
                           $"CountMatch={categoryCountMatches}, IdsMatch={categoryIdsMatch}, " +
                           $"SpendsMatch={spendsMatch}, Descending={isDescending}");
            });
    }

    /// <summary>
    /// For any set of purchases, cancelled purchases SHALL never appear in the breakdown
    /// (their amounts are excluded from all sums).
    /// **Validates: Requirements 1.1, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CancelledPurchases_AreExcludedFromBreakdown()
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

                // Compute total spend from cancelled purchases that are in range
                var cancelledInRangeSpend = scenario.Purchases
                    .Where(p => p.IsCancelled
                                && p.InvoiceDate >= scenario.StartDate
                                && p.InvoiceDate <= scenario.EndDate)
                    .Sum(p => p.TotalAmount);

                // Expected total from non-cancelled in range
                var expectedTotal = scenario.Purchases
                    .Where(p => !p.IsCancelled
                                && p.InvoiceDate >= scenario.StartDate
                                && p.InvoiceDate <= scenario.EndDate)
                    .Sum(p => p.TotalAmount);

                var actualTotal = result.Categories.Sum(c => c.TotalSpend);

                // The actual total should equal expected (non-cancelled) total
                var totalCorrect = actualTotal == expectedTotal;

                return totalCorrect
                    .ToProperty()
                    .Label($"ExpectedTotal={expectedTotal}, ActualTotal={actualTotal}, " +
                           $"CancelledInRange={cancelledInRangeSpend}");
            });
    }

    /// <summary>
    /// For any set of purchases, purchases outside the date range [startDate, endDate]
    /// SHALL not appear in the breakdown regardless of their cancellation status.
    /// **Validates: Requirements 1.1, 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PurchasesOutsideDateRange_AreExcludedFromBreakdown()
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

                // Compute: only non-cancelled purchases within [startDate, endDate]
                var expectedInRange = scenario.Purchases
                    .Where(p => !p.IsCancelled
                                && p.InvoiceDate >= scenario.StartDate
                                && p.InvoiceDate <= scenario.EndDate)
                    .Sum(p => p.TotalAmount);

                var actualTotal = result.Categories.Sum(c => c.TotalSpend);

                var correct = actualTotal == expectedInRange;

                return correct
                    .ToProperty()
                    .Label($"ExpectedInRange={expectedInRange}, ActualTotal={actualTotal}, " +
                           $"TotalPurchases={scenario.Purchases.Count}");
            });
    }

    /// <summary>
    /// The result is always ordered by TotalSpend descending — for any non-empty breakdown,
    /// each category's TotalSpend is greater than or equal to the next category's TotalSpend.
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CategoryBreakdown_IsAlwaysOrderedByTotalSpendDescending()
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

                // Assert: each item's TotalSpend >= next item's TotalSpend
                var isDescending = true;
                for (int i = 1; i < result.Categories.Count; i++)
                {
                    if (result.Categories[i].TotalSpend > result.Categories[i - 1].TotalSpend)
                    {
                        isDescending = false;
                        break;
                    }
                }

                return isDescending
                    .ToProperty()
                    .Label($"Categories={result.Categories.Count}, " +
                           $"Spends=[{string.Join(", ", result.Categories.Select(c => c.TotalSpend))}]");
            });
    }

    #endregion
}
