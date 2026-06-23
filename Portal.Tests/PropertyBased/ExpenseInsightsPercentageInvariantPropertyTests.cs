using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.ExpenseInsights;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: expense-categorisation-insights, Property 2: Percentage invariant

/// <summary>
/// Property-based tests for the percentage invariant of ExpenseInsightsService.GetInsightsDataAsync.
/// For any non-empty category breakdown (where total spend > 0), the sum of all PercentageOfTotal
/// values across categories SHALL equal 100.0 (within a rounding tolerance).
/// **Validates: Requirements 1.2**
/// </summary>
public class ExpenseInsightsPercentageInvariantPropertyTests
{
    private const int TestBusinessId = 1;

    /// <summary>
    /// Creates an in-memory PortalDbContext with the tenant service returning our test BusinessId.
    /// </summary>
    private static (PortalDbContext dbContext, ExpenseInsightsService service) CreateServiceWithDb()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"PercentageInvariant_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);
        var service = new ExpenseInsightsService(dbContext, tenantMock.Object);

        return (dbContext, service);
    }

    /// <summary>
    /// Seeds the required lookup and reference data (Business, ExpenseTypes, PurchaseOriginType, PurchaseType)
    /// that foreign key navigations depend on.
    /// </summary>
    private static void SeedBaseData(PortalDbContext dbContext)
    {
        dbContext.Businesses.Add(new Business
        {
            Id = TestBusinessId,
            Name = "Test Business",
            IsActive = true,
            IsDemoAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        dbContext.ExpenseTypes.Add(new ExpenseType { Id = 1, Name = "Services" });
        dbContext.ExpenseTypes.Add(new ExpenseType { Id = 2, Name = "Goods" });

        dbContext.PurchaseOriginTypes.Add(new PurchaseOriginType { Id = 1, Name = "Domestic" });
        dbContext.PurchaseTypes.Add(new PurchaseType { Id = 3, Name = "Expense" });

        dbContext.SaveChanges();
    }

    /// <summary>
    /// Property 2: Percentage invariant
    /// For any non-empty category breakdown (where total spend > 0), the sum of all
    /// PercentageOfTotal values across categories SHALL equal 100.0 within a rounding tolerance
    /// of N × 0.01 (where N is the number of categories), because each percentage is rounded to 2dp.
    ///
    /// Test approach:
    /// - Generate random purchases with positive TotalAmounts across 2+ categories within a date range
    /// - Call GetInsightsDataAsync
    /// - Verify Sum(PercentageOfTotal) ≈ 100.0
    ///
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PercentageOfTotal_SumsToApproximately100_ForNonEmptyBreakdown(
        PositiveInt categoryCountSeed, PositiveInt purchaseCountSeed)
    {
        // Generate 2 to 10 categories
        var categoryCount = (categoryCountSeed.Get % 9) + 2;
        // Generate 1 to 5 purchases per category
        var purchasesPerCategory = (purchaseCountSeed.Get % 5) + 1;

        var (dbContext, service) = CreateServiceWithDb();

        try
        {
            SeedBaseData(dbContext);

            // Create categories
            var categories = new List<ExpenseCategory>();
            for (int i = 1; i <= categoryCount; i++)
            {
                var category = new ExpenseCategory
                {
                    Id = i,
                    BusinessId = TestBusinessId,
                    Name = $"Category {i}",
                    IsActive = true,
                    ExpenseTypeId = (i % 2 == 0) ? 1 : 2,
                    CreatedAtUtc = DateTime.UtcNow
                };
                categories.Add(category);
                dbContext.ExpenseCategories.Add(category);
            }

            // Create a supplier
            dbContext.Suppliers.Add(new Supplier
            {
                Id = 1,
                BusinessId = TestBusinessId,
                Name = "Test Supplier",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });

            dbContext.SaveChanges();

            // Create purchases with varying positive amounts
            var random = new System.Random(categoryCountSeed.Get ^ purchaseCountSeed.Get);
            var purchaseId = 1;
            var invoiceDate = new DateOnly(2024, 6, 15);

            for (int catIdx = 0; catIdx < categoryCount; catIdx++)
            {
                for (int p = 0; p < purchasesPerCategory; p++)
                {
                    // Generate a positive amount between 1.00 and 10000.00
                    var amount = Math.Round((decimal)(random.NextDouble() * 9999.0 + 1.0), 2);

                    dbContext.Purchases.Add(new Purchase
                    {
                        Id = purchaseId++,
                        BusinessId = TestBusinessId,
                        SupplierId = 1,
                        ExpenseCategoryId = categories[catIdx].Id,
                        PurchaseOriginTypeId = 1,
                        PurchaseTypeId = 3,
                        InvoiceDate = invoiceDate,
                        Description = $"Purchase {purchaseId}",
                        AmountExcludingVat = amount,
                        VatAmount = 0m,
                        TotalAmount = amount,
                        IsCancelled = false,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    });
                }
            }

            dbContext.SaveChanges();

            // Act: call GetInsightsDataAsync with a custom period covering our test date
            var request = new ExpenseInsightsPeriodRequest
            {
                PeriodType = PnlPeriodType.Custom,
                CustomStartDate = new DateOnly(2024, 6, 1),
                CustomEndDate = new DateOnly(2024, 6, 30)
            };

            var result = service.GetInsightsDataAsync(request).GetAwaiter().GetResult();

            // Assert: percentages should sum to approximately 100.0
            var percentageSum = result.Categories.Sum(c => c.PercentageOfTotal);
            var categoriesReturned = result.Categories.Count;

            // Tolerance: each category can have up to ±0.005 rounding error (2dp rounding).
            // For N categories, max total deviation = N × 0.01
            // Use the simpler ±0.5 tolerance as a safe upper bound for up to 50 categories
            var tolerance = Math.Max(categoriesReturned * 0.01m, 0.5m);
            var deviation = Math.Abs(percentageSum - 100.0m);

            return (deviation <= tolerance)
                .ToProperty()
                .Label($"Categories={categoriesReturned}, Sum={percentageSum:F4}, " +
                       $"Deviation={deviation:F4}, Tolerance={tolerance:F2}");
        }
        finally
        {
            dbContext.Dispose();
        }
    }
}
