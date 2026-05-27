using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: audit-system-administration, Property 7: Tenant isolation — all results have current BusinessId
// Feature: audit-system-administration, Property 8: AND-logic filter correctness
// Feature: audit-system-administration, Property 9: Descending timestamp ordering
// Feature: audit-system-administration, Property 10: Pagination math correctness
// Feature: audit-system-administration, Property 11: PageSize clamping

/// <summary>
/// Property-based tests for AuditLogQueryService.
/// Validates tenant isolation, filter AND-logic, ordering, pagination math, and PageSize clamping.
/// Uses an in-memory PortalDbContext seeded with AuditLog records for multiple businesses.
/// **Validates: Requirements 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9**
/// </summary>
public class AuditLogQueryServicePropertyTests
{
    private const int BusinessId1 = 1;
    private const int BusinessId2 = 2;

    private static readonly string[] ValidActions = { "Insert", "Update", "Delete" };
    private static readonly string[] TableNames = { "Invoice", "Payment", "Customer", "Purchase" };

    #region Test Infrastructure

    /// <summary>
    /// Creates an in-memory PortalDbContext scoped to the given tenant BusinessId.
    /// Each call uses a unique database name to ensure test isolation.
    /// </summary>
    private static PortalDbContext CreateDbContext(int tenantBusinessId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(tenantBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"AuditLogQueryService_{Guid.NewGuid()}")
            .Options;

        return new PortalDbContext(options, tenantMock.Object);
    }

    /// <summary>
    /// Creates an AuditLogQueryService wired to the given DbContext and tenant.
    /// </summary>
    private static AuditLogQueryService CreateService(PortalDbContext dbContext, int tenantBusinessId)
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        tenantMock.Setup(t => t.CurrentBusinessId).Returns(tenantBusinessId);

        var repository = new AuditLogQueryRepository(dbContext);
        return new AuditLogQueryService(repository, tenantMock.Object);
    }

    /// <summary>
    /// Seeds AuditLog records directly into the DbContext, bypassing the global query filter
    /// by using IgnoreQueryFilters-equivalent raw Add (EF in-memory always allows direct Add).
    /// Records for both BusinessId1 and BusinessId2 are seeded to test isolation.
    /// </summary>
    private static void SeedAuditLogs(PortalDbContext dbContext, List<AuditLog> logs)
    {
        dbContext.AuditLogs.AddRange(logs);
        dbContext.SaveChanges();
    }

    /// <summary>
    /// Builds an AuditLog record with controlled parameters.
    /// </summary>
    private static AuditLog MakeLog(
        long id,
        int businessId,
        string action,
        string tableName,
        string? userId,
        DateTime timestamp)
    {
        return new AuditLog
        {
            Id = id,
            BusinessId = businessId,
            Action = action,
            TableName = tableName,
            RecordId = id.ToString(),
            UserId = userId,
            Timestamp = timestamp
        };
    }

    #endregion

    #region Property 7: Tenant isolation — all results have current BusinessId

    /// <summary>
    /// Property 7: Every record returned by GetAuditLogsAsync has BusinessId equal to the
    /// current tenant's value. Records belonging to other businesses never appear.
    /// **Validates: Requirements 2.2, 2.7**
    /// </summary>
    // Feature: audit-system-administration, Property 7: every record in the result has BusinessId equal to the current tenant's value; no records from other businesses appear
    [Property(MaxTest = 100)]
    public Property Property7_AllResults_HaveCurrentTenantBusinessId(
        PositiveInt[] seeds)
    {
        if (seeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var dbContext = CreateDbContext(BusinessId1);
        try
        {
            var count = Math.Min(seeds.Length, 20);
            var logs = new List<AuditLog>();
            var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int i = 0; i < count; i++)
            {
                var action = ValidActions[Math.Abs(seeds[i].Get) % ValidActions.Length];
                var table = TableNames[Math.Abs(seeds[i].Get) % TableNames.Length];
                var ts = baseTime.AddMinutes(i);

                // Alternate records between the two businesses
                logs.Add(MakeLog(i * 2 + 1, BusinessId1, action, table, null, ts));
                logs.Add(MakeLog(i * 2 + 2, BusinessId2, action, table, null, ts.AddSeconds(1)));
            }

            SeedAuditLogs(dbContext, logs);

            var service = CreateService(dbContext, BusinessId1);
            var filter = new AuditLogFilter { PageNumber = 1, PageSize = 100 };
            var result = service.GetAuditLogsAsync(filter).GetAwaiter().GetResult();

            var allBelongToTenant = result.Items.All(r => r.BusinessId == BusinessId1);
            var noOtherTenantData = !result.Items.Any(r => r.BusinessId == BusinessId2);

            return (allBelongToTenant && noOtherTenantData).ToProperty()
                .Label($"Items={result.Items.Count}, AllBelongToTenant={allBelongToTenant}, " +
                       $"NoOtherTenantData={noOtherTenantData}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 8: AND-logic filter correctness

    /// <summary>
    /// Property 8: For any combination of filter parameters, every item in the result satisfies
    /// all specified conditions simultaneously (AND logic), and the result is a subset of the
    /// unfiltered result for the same tenant.
    /// **Validates: Requirements 2.3, 2.4, 2.5, 2.6**
    /// </summary>
    // Feature: audit-system-administration, Property 8: for any combination of filter parameters, the result set satisfies all specified conditions simultaneously (AND logic) and is a subset of the unfiltered result
    [Property(MaxTest = 100)]
    public Property Property8_FilteredResults_SatisfyAllConditionsSimultaneously(
        PositiveInt[] seeds,
        bool filterByTable,
        bool filterByAction,
        bool filterByUser,
        bool filterByDateFrom,
        bool filterByDateTo)
    {
        if (seeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var dbContext = CreateDbContext(BusinessId1);
        try
        {
            var count = Math.Min(seeds.Length, 30);
            var logs = new List<AuditLog>();
            var baseTime = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

            for (int i = 0; i < count; i++)
            {
                var action = ValidActions[i % ValidActions.Length];
                var table = TableNames[i % TableNames.Length];
                var userId = (i % 3 == 0) ? "user-alpha" : (i % 3 == 1) ? "user-beta" : null;
                var ts = baseTime.AddDays(i);

                logs.Add(MakeLog(i + 1, BusinessId1, action, table, userId, ts));
            }

            SeedAuditLogs(dbContext, logs);

            // Build a filter using the first record's values as anchors
            var anchor = logs.First();
            var filter = new AuditLogFilter
            {
                PageNumber = 1,
                PageSize = 100,
                TableName = filterByTable ? anchor.TableName : null,
                Action = filterByAction ? anchor.Action : null,
                UserId = filterByUser ? anchor.UserId : null,
                DateFrom = filterByDateFrom ? baseTime.AddDays(-1) : (DateTime?)null,
                DateTo = filterByDateTo ? baseTime.AddDays(count + 1) : (DateTime?)null
            };

            var service = CreateService(dbContext, BusinessId1);
            var filteredResult = service.GetAuditLogsAsync(filter).GetAwaiter().GetResult();

            // Verify each item satisfies all active filter conditions
            var allSatisfyFilters = filteredResult.Items.All(item =>
            {
                if (filterByTable && filter.TableName != null && item.TableName != filter.TableName)
                    return false;
                if (filterByAction && filter.Action != null && item.Action != filter.Action)
                    return false;
                if (filterByUser && filter.UserId != null && item.UserId != filter.UserId)
                    return false;
                if (filterByDateFrom && filter.DateFrom.HasValue && item.Timestamp < filter.DateFrom.Value)
                    return false;
                if (filterByDateTo && filter.DateTo.HasValue && item.Timestamp > filter.DateTo.Value)
                    return false;
                return true;
            });

            // Verify filtered result is a subset of the unfiltered result
            var unfilteredFilter = new AuditLogFilter { PageNumber = 1, PageSize = 200 };
            var unfilteredResult = service.GetAuditLogsAsync(unfilteredFilter).GetAwaiter().GetResult();
            var unfilteredIds = unfilteredResult.Items.Select(r => r.Id).ToHashSet();
            var isSubset = filteredResult.Items.All(r => unfilteredIds.Contains(r.Id));

            return (allSatisfyFilters && isSubset).ToProperty()
                .Label($"FilteredCount={filteredResult.Items.Count}, UnfilteredCount={unfilteredResult.Items.Count}, " +
                       $"AllSatisfyFilters={allSatisfyFilters}, IsSubset={isSubset}, " +
                       $"Filters: table={filterByTable}, action={filterByAction}, user={filterByUser}, " +
                       $"dateFrom={filterByDateFrom}, dateTo={filterByDateTo}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 9: Descending timestamp ordering

    /// <summary>
    /// Property 9: For any result set, all consecutive pairs satisfy
    /// items[i].Timestamp >= items[i+1].Timestamp (descending order).
    /// **Validates: Requirements 2.8**
    /// </summary>
    // Feature: audit-system-administration, Property 9: for any result set, all consecutive pairs satisfy items[i].Timestamp >= items[i+1].Timestamp (descending order)
    [Property(MaxTest = 100)]
    public Property Property9_Results_AreOrderedByTimestampDescending(
        PositiveInt[] seeds)
    {
        if (seeds.Length == 0)
            return true.ToProperty().Label("Empty input — trivially true");

        var dbContext = CreateDbContext(BusinessId1);
        try
        {
            var count = Math.Min(seeds.Length, 25);
            var logs = new List<AuditLog>();
            var baseTime = new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

            // Create records with deliberately shuffled timestamps
            for (int i = 0; i < count; i++)
            {
                // Use seed to create non-sequential timestamps
                var offsetMinutes = Math.Abs(seeds[i].Get) % (count * 10);
                var ts = baseTime.AddMinutes(offsetMinutes);
                var action = ValidActions[i % ValidActions.Length];
                var table = TableNames[i % TableNames.Length];

                logs.Add(MakeLog(i + 1, BusinessId1, action, table, null, ts));
            }

            SeedAuditLogs(dbContext, logs);

            var service = CreateService(dbContext, BusinessId1);
            var filter = new AuditLogFilter { PageNumber = 1, PageSize = 100 };
            var result = service.GetAuditLogsAsync(filter).GetAwaiter().GetResult();

            if (result.Items.Count <= 1)
                return true.ToProperty().Label("0 or 1 items — ordering trivially satisfied");

            // Verify all consecutive pairs are in descending order
            var allPairsDescending = true;
            for (int i = 0; i < result.Items.Count - 1; i++)
            {
                if (result.Items[i].Timestamp < result.Items[i + 1].Timestamp)
                {
                    allPairsDescending = false;
                    break;
                }
            }

            return allPairsDescending.ToProperty()
                .Label($"Items={result.Items.Count}, AllPairsDescending={allPairsDescending}, " +
                       $"First={result.Items.First().Timestamp:O}, Last={result.Items.Last().Timestamp:O}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 10: Pagination math correctness

    /// <summary>
    /// Property 10: For any valid PageNumber and PageSize (after clamping):
    /// items.Count &lt;= PageSize; TotalPages == Math.Ceiling(TotalCount / (double)PageSize);
    /// pages do not overlap; when PageNumber &gt; TotalPages, items is empty and
    /// TotalCount/TotalPages are still correct.
    /// **Validates: Requirements 2.5, 2.6, 2.9**
    /// </summary>
    // Feature: audit-system-administration, Property 10: for any valid PageNumber and PageSize (after clamping): items.Count <= PageSize; TotalPages == Math.Ceiling(TotalCount / (double)PageSize); pages do not overlap; when PageNumber > TotalPages, items is empty and TotalCount/TotalPages are still correct
    [Property(MaxTest = 100)]
    public Property Property10_Pagination_MathIsCorrect(
        PositiveInt totalCountSeed,
        PositiveInt pageSizeSeed,
        PositiveInt pageNumberSeed)
    {
        var totalCount = totalCountSeed.Get % 51;   // 0–50 records
        var pageSize = (pageSizeSeed.Get % 20) + 1; // 1–20 (within valid range)
        var pageNumber = (pageNumberSeed.Get % 10) + 1; // 1–10

        var dbContext = CreateDbContext(BusinessId1);
        try
        {
            var baseTime = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            var logs = new List<AuditLog>();

            for (int i = 0; i < totalCount; i++)
            {
                logs.Add(MakeLog(i + 1, BusinessId1, "Insert", "Invoice", null,
                    baseTime.AddMinutes(i)));
            }

            SeedAuditLogs(dbContext, logs);

            var service = CreateService(dbContext, BusinessId1);
            var filter = new AuditLogFilter
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = service.GetAuditLogsAsync(filter).GetAwaiter().GetResult();

            // Property: items.Count <= PageSize
            var itemsWithinPageSize = result.Items.Count <= pageSize;

            // Property: TotalPages == Math.Ceiling(TotalCount / (double)PageSize)
            var expectedTotalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)pageSize);
            var totalPagesCorrect = result.TotalPages == expectedTotalPages;

            // Property: TotalCount is accurate
            var totalCountCorrect = result.TotalCount == totalCount;

            // Property: when PageNumber > TotalPages, items is empty but TotalCount/TotalPages remain correct
            var beyondLastPageCorrect = true;
            if (pageNumber > expectedTotalPages && totalCount > 0)
            {
                beyondLastPageCorrect = result.Items.Count == 0
                                     && result.TotalCount == totalCount
                                     && result.TotalPages == expectedTotalPages;
            }

            var allHold = itemsWithinPageSize && totalPagesCorrect
                       && totalCountCorrect && beyondLastPageCorrect;

            return allHold.ToProperty()
                .Label($"TotalCount={totalCount}, PageSize={pageSize}, PageNumber={pageNumber}, " +
                       $"Items={result.Items.Count}, TotalPages={result.TotalPages} (expected {expectedTotalPages}), " +
                       $"ItemsWithinPageSize={itemsWithinPageSize}, TotalPagesCorrect={totalPagesCorrect}, " +
                       $"TotalCountCorrect={totalCountCorrect}, BeyondLastPageCorrect={beyondLastPageCorrect}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    /// <summary>
    /// Property 10b: Pages do not overlap — the set of record IDs on page N and page N+1
    /// are disjoint for any valid consecutive pages.
    /// **Validates: Requirements 2.5, 2.6**
    /// </summary>
    // Feature: audit-system-administration, Property 10: pages do not overlap
    [Property(MaxTest = 100)]
    public Property Property10b_ConsecutivePages_DoNotOverlap(
        PositiveInt totalCountSeed,
        PositiveInt pageSizeSeed)
    {
        var totalCount = (totalCountSeed.Get % 40) + 2; // 2–41 records (ensure at least 2)
        var pageSize = (pageSizeSeed.Get % 10) + 1;     // 1–10

        var dbContext = CreateDbContext(BusinessId1);
        try
        {
            var baseTime = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc);
            var logs = new List<AuditLog>();

            for (int i = 0; i < totalCount; i++)
            {
                logs.Add(MakeLog(i + 1, BusinessId1, "Update", "Customer", null,
                    baseTime.AddMinutes(i)));
            }

            SeedAuditLogs(dbContext, logs);

            var service = CreateService(dbContext, BusinessId1);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            if (totalPages < 2)
                return true.ToProperty().Label("Only 1 page — overlap check trivially satisfied");

            // Collect IDs from all pages and verify no overlaps
            var allPageIds = new List<HashSet<long>>();
            for (int p = 1; p <= totalPages; p++)
            {
                var filter = new AuditLogFilter { PageNumber = p, PageSize = pageSize };
                var result = service.GetAuditLogsAsync(filter).GetAwaiter().GetResult();
                allPageIds.Add(result.Items.Select(r => r.Id).ToHashSet());
            }

            var noOverlap = true;
            for (int i = 0; i < allPageIds.Count - 1; i++)
            {
                if (allPageIds[i].Overlaps(allPageIds[i + 1]))
                {
                    noOverlap = false;
                    break;
                }
            }

            // Also verify union of all pages equals total count
            var allIds = allPageIds.SelectMany(s => s).ToHashSet();
            var unionEqualsTotal = allIds.Count == totalCount;

            return (noOverlap && unionEqualsTotal).ToProperty()
                .Label($"TotalCount={totalCount}, PageSize={pageSize}, TotalPages={totalPages}, " +
                       $"NoOverlap={noOverlap}, UnionEqualsTotal={unionEqualsTotal} (union={allIds.Count})");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion

    #region Property 11: PageSize clamping

    /// <summary>
    /// Property 11: PageSize &lt; 1 is clamped to 1; PageSize &gt; 100 is clamped to 100;
    /// values in [1, 100] are used as-is.
    /// **Validates: Requirements 2.5**
    /// </summary>
    // Feature: audit-system-administration, Property 11: PageSize < 1 is clamped to 1; PageSize > 100 is clamped to 100; values in [1, 100] are used as-is
    [Property(MaxTest = 100)]
    public Property Property11_PageSize_IsClamped(
        int rawPageSize,
        PositiveInt totalCountSeed)
    {
        var totalCount = (totalCountSeed.Get % 50) + 10; // 10–59 records (enough to observe clamping)

        var dbContext = CreateDbContext(BusinessId1);
        try
        {
            var baseTime = new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            var logs = new List<AuditLog>();

            for (int i = 0; i < totalCount; i++)
            {
                logs.Add(MakeLog(i + 1, BusinessId1, "Insert", "Invoice", null,
                    baseTime.AddMinutes(i)));
            }

            SeedAuditLogs(dbContext, logs);

            var service = CreateService(dbContext, BusinessId1);
            var filter = new AuditLogFilter
            {
                PageNumber = 1,
                PageSize = rawPageSize
            };

            var result = service.GetAuditLogsAsync(filter).GetAwaiter().GetResult();

            // Determine the expected effective page size after clamping
            var expectedEffectivePageSize = Math.Clamp(rawPageSize, 1, 100);

            // The result's PageSize must equal the clamped value
            var pageSizeIsCorrectlyClamped = result.PageSize == expectedEffectivePageSize;

            // items.Count must not exceed the effective page size
            var itemsWithinEffectivePageSize = result.Items.Count <= expectedEffectivePageSize;

            // Specific boundary checks:
            // PageSize < 1 → effective = 1 → at most 1 item returned
            var belowMinCorrect = rawPageSize >= 1 || result.Items.Count <= 1;

            // PageSize > 100 → effective = 100 → at most 100 items returned
            var aboveMaxCorrect = rawPageSize <= 100 || result.Items.Count <= 100;

            // PageSize in [1, 100] → used as-is → items.Count <= rawPageSize
            var inRangeCorrect = rawPageSize < 1 || rawPageSize > 100
                || result.Items.Count <= rawPageSize;

            var allHold = pageSizeIsCorrectlyClamped
                       && itemsWithinEffectivePageSize
                       && belowMinCorrect
                       && aboveMaxCorrect
                       && inRangeCorrect;

            return allHold.ToProperty()
                .Label($"RawPageSize={rawPageSize}, EffectivePageSize={expectedEffectivePageSize}, " +
                       $"ResultPageSize={result.PageSize}, Items={result.Items.Count}, " +
                       $"PageSizeClamped={pageSizeIsCorrectlyClamped}, " +
                       $"ItemsWithinEffective={itemsWithinEffectivePageSize}, " +
                       $"BelowMinCorrect={belowMinCorrect}, AboveMaxCorrect={aboveMaxCorrect}, " +
                       $"InRangeCorrect={inRangeCorrect}");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    /// <summary>
    /// Property 11 (boundary): PageSize = 0 clamps to 1; PageSize = 101 clamps to 100;
    /// PageSize = 50 is used as-is. Verifies the three canonical boundary cases explicitly.
    /// **Validates: Requirements 2.5**
    /// </summary>
    // Feature: audit-system-administration, Property 11: PageSize clamping boundary cases
    [Property(MaxTest = 100)]
    public Property Property11b_PageSize_BoundaryCases(PositiveInt totalCountSeed)
    {
        var totalCount = (totalCountSeed.Get % 90) + 10; // 10–99 records

        var dbContext = CreateDbContext(BusinessId1);
        try
        {
            var baseTime = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);
            var logs = new List<AuditLog>();

            for (int i = 0; i < totalCount; i++)
            {
                logs.Add(MakeLog(i + 1, BusinessId1, "Delete", "Payment", null,
                    baseTime.AddMinutes(i)));
            }

            SeedAuditLogs(dbContext, logs);

            var service = CreateService(dbContext, BusinessId1);

            // Case 1: PageSize = 0 → clamped to 1
            var resultZero = service.GetAuditLogsAsync(
                new AuditLogFilter { PageNumber = 1, PageSize = 0 }).GetAwaiter().GetResult();
            var zeroClampedTo1 = resultZero.PageSize == 1 && resultZero.Items.Count <= 1;

            // Case 2: PageSize = -5 → clamped to 1
            var resultNeg = service.GetAuditLogsAsync(
                new AuditLogFilter { PageNumber = 1, PageSize = -5 }).GetAwaiter().GetResult();
            var negClampedTo1 = resultNeg.PageSize == 1 && resultNeg.Items.Count <= 1;

            // Case 3: PageSize = 101 → clamped to 100
            var result101 = service.GetAuditLogsAsync(
                new AuditLogFilter { PageNumber = 1, PageSize = 101 }).GetAwaiter().GetResult();
            var over100ClampedTo100 = result101.PageSize == 100 && result101.Items.Count <= 100;

            // Case 4: PageSize = 50 → used as-is
            var result50 = service.GetAuditLogsAsync(
                new AuditLogFilter { PageNumber = 1, PageSize = 50 }).GetAwaiter().GetResult();
            var inRangeUsedAsIs = result50.PageSize == 50 && result50.Items.Count <= 50;

            var allHold = zeroClampedTo1 && negClampedTo1 && over100ClampedTo100 && inRangeUsedAsIs;

            return allHold.ToProperty()
                .Label($"TotalCount={totalCount}, " +
                       $"ZeroClampedTo1={zeroClampedTo1} (PageSize={resultZero.PageSize}, Items={resultZero.Items.Count}), " +
                       $"NegClampedTo1={negClampedTo1} (PageSize={resultNeg.PageSize}, Items={resultNeg.Items.Count}), " +
                       $"Over100ClampedTo100={over100ClampedTo100} (PageSize={result101.PageSize}, Items={result101.Items.Count}), " +
                       $"InRangeUsedAsIs={inRangeUsedAsIs} (PageSize={result50.PageSize}, Items={result50.Items.Count})");
        }
        finally
        {
            dbContext.Database.EnsureDeleted();
            dbContext.Dispose();
        }
    }

    #endregion
}
