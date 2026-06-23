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

// Feature: expense-categorisation-insights, Property 9: Tenant isolation invariant

/// <summary>
/// Property-based tests for ExpenseInsightsService tenant isolation.
/// Validates that for any BusinessId and for any set of purchases spanning multiple businesses,
/// the service SHALL return only data where Purchase.BusinessId matches the current tenant's
/// BusinessId — never including records from other tenants.
/// **Validates: Requirements 13.1, 13.2, 13.3, 13.4**
/// </summary>
public class ExpenseInsightsTenantIsolationPropertyTests
{
    private const int TenantABusinessId = 1;
    private const int TenantBBusinessId = 2;

    #region Test Infrastructure

    /// <summary>
    /// Holds the generated test scenario for a single property test case.
    /// </summary>
    private record TestScenario(
        List<PurchaseData> TenantAPurchases,
        List<PurchaseData> TenantBPurchases,
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
    /// Generates a TestScenario with random purchases for two different tenants.
    /// Both tenants have purchases with overlapping dates and categories.
    /// </summary>
    private static Gen<TestScenario> TestScenarioGen =>
        from tenantACount in Gen.Choose(1, 10)
        from tenantBCount in Gen.Choose(1, 10)
        from aCategoryIds in Gen.ArrayOf(tenantACount, Gen.Choose(1, 5))
        from aSupplierIds in Gen.ArrayOf(tenantACount, Gen.Choose(1, 3))
        from aAmountSeeds in Gen.ArrayOf(tenantACount, Gen.Choose(1, 999999))
        from aDateSeeds in Gen.ArrayOf(tenantACount, Gen.Choose(0, 1825))
        from aCancelFlags in Gen.ArrayOf(tenantACount, Gen.Elements(true, false))
        from bCategoryIds in Gen.ArrayOf(tenantBCount, Gen.Choose(1, 5))
        from bSupplierIds in Gen.ArrayOf(tenantBCount, Gen.Choose(1, 3))
        from bAmountSeeds in Gen.ArrayOf(tenantBCount, Gen.Choose(1, 999999))
        from bDateSeeds in Gen.ArrayOf(tenantBCount, Gen.Choose(0, 1825))
        from bCancelFlags in Gen.ArrayOf(tenantBCount, Gen.Elements(true, false))
        from startDateSeed in Gen.Choose(0, 1000)
        from rangeDays in Gen.Choose(1, 365)
        select BuildScenario(
            tenantACount, aCategoryIds, aSupplierIds, aAmountSeeds, aDateSeeds, aCancelFlags,
            tenantBCount, bCategoryIds, bSupplierIds, bAmountSeeds, bDateSeeds, bCancelFlags,
            startDateSeed, rangeDays);

    private static TestScenario BuildScenario(
        int tenantACount, int[] aCategoryIds, int[] aSupplierIds,
        int[] aAmountSeeds, int[] aDateSeeds, bool[] aCancelFlags,
        int tenantBCount, int[] bCategoryIds, int[] bSupplierIds,
        int[] bAmountSeeds, int[] bDateSeeds, bool[] bCancelFlags,
        int startDateSeed, int rangeDays)
    {
        var startDate = GenerateDateOnly(startDateSeed);
        var endDate = startDate.AddDays(rangeDays);

        var tenantAPurchases = new List<PurchaseData>();
        for (int i = 0; i < tenantACount; i++)
        {
            tenantAPurchases.Add(new PurchaseData(
                ExpenseCategoryId: aCategoryIds[i],
                SupplierId: aSupplierIds[i],
                TotalAmount: GenerateAmount(aAmountSeeds[i]),
                InvoiceDate: GenerateDateOnly(aDateSeeds[i]),
                IsCancelled: aCancelFlags[i]));
        }

        var tenantBPurchases = new List<PurchaseData>();
        for (int i = 0; i < tenantBCount; i++)
        {
            tenantBPurchases.Add(new PurchaseData(
                ExpenseCategoryId: bCategoryIds[i],
                SupplierId: bSupplierIds[i],
                TotalAmount: GenerateAmount(bAmountSeeds[i]),
                InvoiceDate: GenerateDateOnly(bDateSeeds[i]),
                IsCancelled: bCancelFlags[i]));
        }

        return new TestScenario(tenantAPurchases, tenantBPurchases, startDate, endDate);
    }

    /// <summary>
    /// Creates an in-memory PortalDbContext seeded with purchases for two tenants.
    /// Returns the DbContext ready for querying via the service.
    /// </summary>
    private static PortalDbContext CreateSeededDbContext(TestScenario scenario, Mock<ICurrentTenantService> tenantMock)
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantIsolation_{Guid.NewGuid()}")
            .Options;

        var dbContext = new PortalDbContext(options, tenantMock.Object);

        // Seed both businesses
        dbContext.Businesses.Add(new Business
        {
            Id = TenantABusinessId,
            Name = "Business A",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        dbContext.Businesses.Add(new Business
        {
            Id = TenantBBusinessId,
            Name = "Business B",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        // Seed ExpenseTypes
        dbContext.ExpenseTypes.Add(new ExpenseType { Id = 1, Name = "Services" });
        dbContext.ExpenseTypes.Add(new ExpenseType { Id = 2, Name = "Goods" });

        // Seed Suppliers for both businesses (1-3 for A, 4-6 for B)
        for (int i = 1; i <= 3; i++)
        {
            dbContext.Suppliers.Add(new Supplier
            {
                Id = i,
                BusinessId = TenantABusinessId,
                Name = $"Supplier A-{i}",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
            dbContext.Suppliers.Add(new Supplier
            {
                Id = i + 3,
                BusinessId = TenantBBusinessId,
                Name = $"Supplier B-{i}",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // Seed ExpenseCategories for both businesses (1-5 for A, 6-10 for B)
        for (int i = 1; i <= 5; i++)
        {
            int? expenseTypeId = i <= 2 ? 1 : i <= 4 ? 2 : (int?)null;
            dbContext.ExpenseCategories.Add(new ExpenseCategory
            {
                Id = i,
                BusinessId = TenantABusinessId,
                Name = $"Category A-{i}",
                IsActive = true,
                ExpenseTypeId = expenseTypeId,
                CreatedAtUtc = DateTime.UtcNow
            });
            dbContext.ExpenseCategories.Add(new ExpenseCategory
            {
                Id = i + 5,
                BusinessId = TenantBBusinessId,
                Name = $"Category B-{i}",
                IsActive = true,
                ExpenseTypeId = expenseTypeId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // Seed lookup tables
        dbContext.PurchaseOriginTypes.Add(new PurchaseOriginType { Id = 1, Name = "Domestic" });
        dbContext.PurchaseTypes.Add(new PurchaseType { Id = 3, Name = "Expense" });

        // Seed Tenant A Purchases
        var purchaseId = 1;
        for (int i = 0; i < scenario.TenantAPurchases.Count; i++)
        {
            var p = scenario.TenantAPurchases[i];
            dbContext.Purchases.Add(new Purchase
            {
                Id = purchaseId++,
                BusinessId = TenantABusinessId,
                SupplierId = p.SupplierId,
                ExpenseCategoryId = p.ExpenseCategoryId,
                PurchaseOriginTypeId = 1,
                PurchaseTypeId = 3,
                InvoiceNumber = $"A-INV-{i + 1:D4}",
                InvoiceDate = p.InvoiceDate,
                Description = $"Tenant A purchase {i + 1}",
                AmountExcludingVat = Math.Round(p.TotalAmount / 1.15m, 2),
                VatAmount = Math.Round(p.TotalAmount - (p.TotalAmount / 1.15m), 2),
                TotalAmount = p.TotalAmount,
                IsCancelled = p.IsCancelled,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        // Seed Tenant B Purchases (use category IDs 6-10 and supplier IDs 4-6)
        for (int i = 0; i < scenario.TenantBPurchases.Count; i++)
        {
            var p = scenario.TenantBPurchases[i];
            dbContext.Purchases.Add(new Purchase
            {
                Id = purchaseId++,
                BusinessId = TenantBBusinessId,
                SupplierId = p.SupplierId + 3, // Map to B's supplier IDs (4-6)
                ExpenseCategoryId = p.ExpenseCategoryId + 5, // Map to B's category IDs (6-10)
                PurchaseOriginTypeId = 1,
                PurchaseTypeId = 3,
                InvoiceNumber = $"B-INV-{i + 1:D4}",
                InvoiceDate = p.InvoiceDate,
                Description = $"Tenant B purchase {i + 1}",
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
    /// Computes the expected total spend for Tenant A only (non-cancelled, in-range purchases).
    /// This is the oracle for verifying tenant isolation.
    /// </summary>
    private static decimal ComputeExpectedTenantATotal(TestScenario scenario)
    {
        return scenario.TenantAPurchases
            .Where(p => !p.IsCancelled
                        && p.InvoiceDate >= scenario.StartDate
                        && p.InvoiceDate <= scenario.EndDate)
            .Sum(p => p.TotalAmount);
    }

    /// <summary>
    /// Computes the expected total spend for Tenant B only (non-cancelled, in-range purchases).
    /// Used to verify that this amount is never present in Tenant A's results.
    /// </summary>
    private static decimal ComputeExpectedTenantBTotal(TestScenario scenario)
    {
        return scenario.TenantBPurchases
            .Where(p => !p.IsCancelled
                        && p.InvoiceDate >= scenario.StartDate
                        && p.InvoiceDate <= scenario.EndDate)
            .Sum(p => p.TotalAmount);
    }

    #endregion

    #region Property 9: Tenant isolation invariant

    /// <summary>
    /// For any set of purchases spanning two businesses, when ICurrentTenantService
    /// is configured with BusinessId=1 (Tenant A), the service SHALL return only
    /// Tenant A's data — total spend equals exactly the sum of Tenant A's non-cancelled
    /// in-range purchases and never includes Tenant B's data.
    /// **Validates: Requirements 13.1, 13.2, 13.3, 13.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetInsightsData_ReturnsOnlyCurrentTenantPurchases()
    {
        return Prop.ForAll(
            TestScenarioGen.ToArbitrary(),
            scenario =>
            {
                // Arrange — set current tenant to Business A
                var tenantMock = new Mock<ICurrentTenantService>();
                tenantMock.Setup(t => t.CurrentBusinessId).Returns(TenantABusinessId);

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

                // Compute expected totals
                var expectedTenantATotal = ComputeExpectedTenantATotal(scenario);
                var actualTotal = result.Categories.Sum(c => c.TotalSpend);

                // Assert: actual total matches exactly what Tenant A should have
                var totalMatchesTenantA = actualTotal == expectedTenantATotal;

                return totalMatchesTenantA
                    .ToProperty()
                    .Label($"ExpectedTenantA={expectedTenantATotal}, ActualTotal={actualTotal}, " +
                           $"TenantAPurchases={scenario.TenantAPurchases.Count}, " +
                           $"TenantBPurchases={scenario.TenantBPurchases.Count}");
            });
    }

    /// <summary>
    /// For any set of purchases spanning two businesses, Tenant B's non-cancelled
    /// in-range purchases SHALL be completely excluded from Tenant A's results.
    /// When Tenant B has data in the same date range, the service SHALL NOT include
    /// any of that data in the response for Tenant A.
    /// **Validates: Requirements 13.1, 13.2, 13.3, 13.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetInsightsData_ExcludesOtherTenantPurchasesCompletely()
    {
        return Prop.ForAll(
            TestScenarioGen.ToArbitrary(),
            scenario =>
            {
                // Arrange — set current tenant to Business A
                var tenantMock = new Mock<ICurrentTenantService>();
                tenantMock.Setup(t => t.CurrentBusinessId).Returns(TenantABusinessId);

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

                // Compute Tenant B's expected total (should be excluded)
                var tenantBTotal = ComputeExpectedTenantBTotal(scenario);
                var tenantATotal = ComputeExpectedTenantATotal(scenario);
                var actualTotal = result.Categories.Sum(c => c.TotalSpend);

                // If Tenant B has data in range, verify it's NOT included
                // The actual total should equal Tenant A's total, NOT A + B
                var combinedTotal = tenantATotal + tenantBTotal;
                var doesNotIncludeTenantB = actualTotal == tenantATotal;

                // Additional check: no category IDs from Tenant B (6-10) appear in results
                var tenantBCategoryIds = Enumerable.Range(6, 5).ToHashSet();
                var noTenantBCategories = !result.Categories.Any(c => tenantBCategoryIds.Contains(c.ExpenseCategoryId));

                var allPass = doesNotIncludeTenantB && noTenantBCategories;

                return allPass
                    .ToProperty()
                    .Label($"ActualTotal={actualTotal}, TenantATotal={tenantATotal}, " +
                           $"TenantBTotal={tenantBTotal}, CombinedTotal={combinedTotal}, " +
                           $"ExcludesTenantB={doesNotIncludeTenantB}, NoBCategoryIds={noTenantBCategories}");
            });
    }

    /// <summary>
    /// Symmetric isolation: when switching the current tenant to Business B,
    /// the service SHALL return only Tenant B's data and exclude all of Tenant A's records.
    /// This verifies isolation works in both directions.
    /// **Validates: Requirements 13.1, 13.2, 13.3, 13.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetInsightsData_SymmetricIsolation_TenantBExcludesTenantA()
    {
        return Prop.ForAll(
            TestScenarioGen.ToArbitrary(),
            scenario =>
            {
                // Arrange — set current tenant to Business B
                var tenantMock = new Mock<ICurrentTenantService>();
                tenantMock.Setup(t => t.CurrentBusinessId).Returns(TenantBBusinessId);

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

                // Expected: only Tenant B's non-cancelled in-range purchases
                var expectedTenantBTotal = ComputeExpectedTenantBTotal(scenario);
                var actualTotal = result.Categories.Sum(c => c.TotalSpend);

                // Verify total matches Tenant B only
                var totalMatchesTenantB = actualTotal == expectedTenantBTotal;

                // Verify no Tenant A category IDs (1-5) appear in results
                var tenantACategoryIds = Enumerable.Range(1, 5).ToHashSet();
                var noTenantACategories = !result.Categories.Any(c => tenantACategoryIds.Contains(c.ExpenseCategoryId));

                var allPass = totalMatchesTenantB && noTenantACategories;

                return allPass
                    .ToProperty()
                    .Label($"ActualTotal={actualTotal}, ExpectedTenantB={expectedTenantBTotal}, " +
                           $"TotalMatchesB={totalMatchesTenantB}, NoACategoryIds={noTenantACategories}");
            });
    }

    /// <summary>
    /// For any generated multi-tenant scenario, when ICurrentTenantService returns BusinessId=0
    /// (no active tenant), the service SHALL return an empty result set regardless of how many
    /// purchases exist in the database.
    /// **Validates: Requirements 13.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GetInsightsData_ZeroBusinessId_ReturnsEmptyResult()
    {
        return Prop.ForAll(
            TestScenarioGen.ToArbitrary(),
            scenario =>
            {
                // Arrange — set current tenant to 0 (no active business)
                var tenantMock = new Mock<ICurrentTenantService>();
                tenantMock.Setup(t => t.CurrentBusinessId).Returns(0);

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

                // Assert: empty results regardless of data in DB
                var isEmpty = result.Categories.Count == 0;
                var totalIsZero = result.Summary.TotalSpend == 0;
                var hasNoData = !result.HasData;

                var allPass = isEmpty && totalIsZero && hasNoData;

                return allPass
                    .ToProperty()
                    .Label($"IsEmpty={isEmpty}, TotalIsZero={totalIsZero}, " +
                           $"HasNoData={hasNoData}, TotalPurchasesInDb=" +
                           $"{scenario.TenantAPurchases.Count + scenario.TenantBPurchases.Count}");
            });
    }

    #endregion
}
