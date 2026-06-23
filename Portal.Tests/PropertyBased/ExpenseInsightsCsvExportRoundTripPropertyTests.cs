using System.Text;
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

// Feature: expense-categorisation-insights, Property 8: CSV export round-trip

/// <summary>
/// Property-based tests for ExpenseInsightsService CSV export round-trip correctness.
/// Validates that for any non-empty category breakdown, the generated CSV SHALL:
/// - Contain exactly one header row plus one data row per category
/// - Parse back into the same number of categories with matching CategoryName, TotalSpend (to 2dp), and BudgetStatus values
/// - Use UTF-8 encoding and comma delimiters
/// **Validates: Requirements 10.1, 10.2, 10.4**
/// </summary>
public class ExpenseInsightsCsvExportRoundTripPropertyTests
{
    private const int TestBusinessId = 1;
    private const string ExpectedHeader = "Category Name,Expense Type,Total Spend,Percentage of Total,Month-Over-Month Variance,Budget Limit,Budget Status";

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
        DateOnly InvoiceDate);

    /// <summary>
    /// Generates a positive decimal amount from a seed (range: 0.01 to 9999.99).
    /// </summary>
    private static decimal GenerateAmount(int seed)
    {
        var raw = (Math.Abs(seed) % 999999) + 1;
        return raw / 100m;
    }

    /// <summary>
    /// Generates a TestScenario with random purchases that are guaranteed to be within the date range.
    /// At least 1 purchase is always generated to ensure a non-empty breakdown.
    /// </summary>
    private static Gen<TestScenario> TestScenarioGen =>
        from purchaseCount in Gen.Choose(1, 12)
        from categoryIds in Gen.ArrayOf(purchaseCount, Gen.Choose(1, 5))
        from supplierIds in Gen.ArrayOf(purchaseCount, Gen.Choose(1, 3))
        from amountSeeds in Gen.ArrayOf(purchaseCount, Gen.Choose(1, 999999))
        from dayOffsets in Gen.ArrayOf(purchaseCount, Gen.Choose(0, 27))
        select BuildScenario(purchaseCount, categoryIds, supplierIds, amountSeeds, dayOffsets);

    private static TestScenario BuildScenario(
        int purchaseCount, int[] categoryIds, int[] supplierIds,
        int[] amountSeeds, int[] dayOffsets)
    {
        // Use a fixed date range (2024-03-01 to 2024-03-28) so all purchases fall within range
        var startDate = new DateOnly(2024, 3, 1);
        var endDate = new DateOnly(2024, 3, 28);

        var purchases = new List<PurchaseData>();
        for (int i = 0; i < purchaseCount; i++)
        {
            purchases.Add(new PurchaseData(
                ExpenseCategoryId: categoryIds[i],
                SupplierId: supplierIds[i],
                TotalAmount: GenerateAmount(amountSeeds[i]),
                InvoiceDate: startDate.AddDays(dayOffsets[i])));
        }

        return new TestScenario(purchases, startDate, endDate);
    }

    /// <summary>
    /// Creates an in-memory PortalDbContext seeded with the test business, categories, suppliers,
    /// and purchases needed for the CSV export test.
    /// </summary>
    private static PortalDbContext CreateSeededDbContext(TestScenario scenario, Mock<ICurrentTenantService> tenantMock)
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"CsvRoundTrip_{Guid.NewGuid()}")
            .Options;

        var dbContext = new PortalDbContext(options, tenantMock.Object);

        // Seed the business (needed for GetBusinessNameAsync)
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

        // Seed Purchases (all non-cancelled, within date range)
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
                IsCancelled = false,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        dbContext.SaveChanges();
        return dbContext;
    }

    /// <summary>
    /// Parses a CSV field respecting RFC 4180 quoting rules.
    /// Quoted fields have surrounding quotes removed and internal "" unescaped to ".
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        while (i < line.Length)
        {
            if (inQuotes)
            {
                if (line[i] == '"')
                {
                    // Check for escaped quote ""
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i += 2;
                    }
                    else
                    {
                        // End of quoted field
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    current.Append(line[i]);
                    i++;
                }
            }
            else
            {
                if (line[i] == '"')
                {
                    inQuotes = true;
                    i++;
                }
                else if (line[i] == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                    i++;
                }
                else
                {
                    current.Append(line[i]);
                    i++;
                }
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    #endregion

    #region Property 8: CSV export round-trip

    /// <summary>
    /// For any non-empty category breakdown, the generated CSV SHALL contain exactly
    /// one header row plus one data row per category.
    /// **Validates: Requirements 10.1, 10.2, 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CsvExport_HasCorrectRowCount()
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
                var exportResult = service.ExportCsvAsync(request).GetAwaiter().GetResult();

                // Parse CSV
                var csvContent = Encoding.UTF8.GetString(exportResult.Content);
                var lines = csvContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                // Also get the insights data directly to know expected category count
                // Need fresh context since the previous one might have tracking issues
                using var dbContext2 = CreateSeededDbContext(scenario, tenantMock);
                var service2 = new ExpenseInsightsService(dbContext2, tenantMock.Object);
                var insightsData = service2.GetInsightsDataAsync(request).GetAwaiter().GetResult();

                var expectedRowCount = insightsData.Categories.Count + 1; // +1 for header
                var actualRowCount = lines.Length;

                var rowCountMatches = actualRowCount == expectedRowCount;

                return rowCountMatches
                    .ToProperty()
                    .Label($"ExpectedRows={expectedRowCount}, ActualRows={actualRowCount}, " +
                           $"Categories={insightsData.Categories.Count}");
            });
    }

    /// <summary>
    /// For any non-empty category breakdown, the CSV header SHALL exactly match
    /// the defined column specification.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CsvExport_HasCorrectHeader()
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
                var exportResult = service.ExportCsvAsync(request).GetAwaiter().GetResult();

                // Parse CSV
                var csvContent = Encoding.UTF8.GetString(exportResult.Content);
                var lines = csvContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                var headerCorrect = lines.Length > 0 && lines[0] == ExpectedHeader;

                return headerCorrect
                    .ToProperty()
                    .Label($"Header='{(lines.Length > 0 ? lines[0] : "(empty)")}', " +
                           $"Expected='{ExpectedHeader}'");
            });
    }

    /// <summary>
    /// For any non-empty category breakdown, parsed CSV data rows SHALL contain
    /// matching CategoryName, TotalSpend (to 2dp), and BudgetStatus values when
    /// compared to GetInsightsDataAsync results.
    /// **Validates: Requirements 10.1, 10.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CsvExport_RoundTripsMatchInsightsData()
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

                // Get insights data first
                var insightsData = service.GetInsightsDataAsync(request).GetAwaiter().GetResult();

                // Get CSV export (needs a fresh context since the previous was tracked)
                using var dbContext2 = CreateSeededDbContext(scenario, tenantMock);
                var service2 = new ExpenseInsightsService(dbContext2, tenantMock.Object);
                var exportResult = service2.ExportCsvAsync(request).GetAwaiter().GetResult();

                // Parse CSV
                var csvContent = Encoding.UTF8.GetString(exportResult.Content);
                var lines = csvContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                if (insightsData.Categories.Count == 0)
                {
                    // Edge case: empty breakdown means only header row
                    return (lines.Length == 1)
                        .ToProperty()
                        .Label("EmptyBreakdown: expected header only");
                }

                // Skip header, parse data rows
                var dataRows = lines.Skip(1).Select(ParseCsvLine).ToList();

                // Verify count
                if (dataRows.Count != insightsData.Categories.Count)
                {
                    return false
                        .ToProperty()
                        .Label($"DataRowCount={dataRows.Count} != CategoryCount={insightsData.Categories.Count}");
                }

                // Verify each row matches the corresponding category
                var allMatch = true;
                var mismatchInfo = "";

                for (int i = 0; i < insightsData.Categories.Count; i++)
                {
                    var category = insightsData.Categories[i];
                    var row = dataRows[i];

                    // CSV columns: Category Name[0], Expense Type[1], Total Spend[2], Percentage[3], Variance[4], Budget Limit[5], Budget Status[6]
                    if (row.Count < 7)
                    {
                        allMatch = false;
                        mismatchInfo = $"Row {i} has {row.Count} fields (expected 7)";
                        break;
                    }

                    var csvCategoryName = row[0];
                    var csvTotalSpend = row[2];
                    var csvBudgetStatus = row[6];

                    // Verify CategoryName matches
                    if (csvCategoryName != category.CategoryName)
                    {
                        allMatch = false;
                        mismatchInfo = $"Row {i} CategoryName: '{csvCategoryName}' != '{category.CategoryName}'";
                        break;
                    }

                    // Verify TotalSpend (to 2dp)
                    var expectedSpend = category.TotalSpend.ToString("F2");
                    if (csvTotalSpend != expectedSpend)
                    {
                        allMatch = false;
                        mismatchInfo = $"Row {i} TotalSpend: '{csvTotalSpend}' != '{expectedSpend}'";
                        break;
                    }

                    // Verify BudgetStatus
                    if (csvBudgetStatus != category.BudgetStatus)
                    {
                        allMatch = false;
                        mismatchInfo = $"Row {i} BudgetStatus: '{csvBudgetStatus}' != '{category.BudgetStatus}'";
                        break;
                    }
                }

                return allMatch
                    .ToProperty()
                    .Label($"Categories={insightsData.Categories.Count}, " +
                           $"DataRows={dataRows.Count}, " +
                           $"AllMatch={allMatch}" +
                           (string.IsNullOrEmpty(mismatchInfo) ? "" : $", Mismatch={mismatchInfo}"));
            });
    }

    /// <summary>
    /// For any non-empty category breakdown, the CSV output SHALL be valid UTF-8 encoded content
    /// using comma delimiters.
    /// **Validates: Requirements 10.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CsvExport_IsValidUtf8WithCommaDelimiters()
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
                var exportResult = service.ExportCsvAsync(request).GetAwaiter().GetResult();

                // Verify UTF-8 encoding (should not throw)
                var isValidUtf8 = true;
                try
                {
                    var decoded = Encoding.UTF8.GetString(exportResult.Content);
                    // Verify it's not empty
                    isValidUtf8 = !string.IsNullOrEmpty(decoded);
                }
                catch
                {
                    isValidUtf8 = false;
                }

                // Verify content type
                var contentTypeCorrect = exportResult.ContentType == "text/csv";

                // Verify all lines have comma delimiters (header has 6 commas for 7 columns)
                var csvContent = Encoding.UTF8.GetString(exportResult.Content);
                var lines = csvContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                var allLinesHaveCommas = lines.All(line => line.Contains(','));

                // Verify header has exactly 6 commas (7 fields)
                var headerCommaCount = lines.Length > 0 ? lines[0].Count(c => c == ',') : 0;
                var headerFieldCountCorrect = headerCommaCount == 6;

                var allPass = isValidUtf8 && contentTypeCorrect && allLinesHaveCommas && headerFieldCountCorrect;

                return allPass
                    .ToProperty()
                    .Label($"UTF8={isValidUtf8}, ContentType={contentTypeCorrect}, " +
                           $"AllCommas={allLinesHaveCommas}, HeaderFields={headerCommaCount + 1}");
            });
    }

    #endregion
}
