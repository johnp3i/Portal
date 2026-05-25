using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Models;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: revenue-control, Property 15: Pagination respects page size and total count

/// <summary>
/// Property-based tests for pagination behavior across paginated queries
/// (overdue invoices, recent payments, receivables).
/// Validates that PagedResult&lt;T&gt; respects page size limits and TotalCount accuracy.
/// **Validates: Requirements 7.4, 8.4, 9.7**
/// </summary>
public class PaginationPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// Simulates the pagination logic used by all paginated services.
    /// Given a total data set of N items and a request for page P with page size M,
    /// produces a PagedResult that mirrors what the services return.
    /// </summary>
    private static PagedResult<T> SimulatePagination<T>(List<T> allItems, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;

        var totalCount = allItems.Count;
        var offset = (page - 1) * pageSize;
        var items = allItems.Skip(offset).Take(pageSize).ToList();

        return new PagedResult<T>
        {
            Items = items,
            CurrentPage = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Generates a list of dummy items of the specified count.
    /// </summary>
    private static List<string> GenerateItems(int count)
    {
        return Enumerable.Range(1, count).Select(i => $"Item-{i}").ToList();
    }

    #endregion

    #region Property 15: Pagination respects page size and total count

    /// <summary>
    /// Property 15a: Each page contains at most M items (page size).
    /// For any total count N and page size M, no page ever returns more than M items.
    /// **Validates: Requirements 7.4, 8.4, 9.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EachPage_ContainsAtMostPageSizeItems(PositiveInt totalCountSeed, PositiveInt pageSizeSeed, PositiveInt pageSeed)
    {
        // Generate realistic values: totalCount 0-200, pageSize 1-50, page 1-20
        var totalCount = totalCountSeed.Get % 201; // 0 to 200
        var pageSize = (pageSizeSeed.Get % 50) + 1; // 1 to 50
        var page = (pageSeed.Get % 20) + 1; // 1 to 20

        var allItems = GenerateItems(totalCount);
        var result = SimulatePagination(allItems, page, pageSize);

        return (result.Items.Count <= pageSize).ToProperty()
            .Label($"Page {page} has {result.Items.Count} items but pageSize is {pageSize} " +
                   $"(totalCount={totalCount})");
    }

    /// <summary>
    /// Property 15b: TotalCount always equals the actual number of matching records N.
    /// For any data set of N items, the reported TotalCount is exactly N regardless of page or pageSize.
    /// **Validates: Requirements 7.4, 8.4, 9.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TotalCount_EqualsActualRecordCount(PositiveInt totalCountSeed, PositiveInt pageSizeSeed, PositiveInt pageSeed)
    {
        var totalCount = totalCountSeed.Get % 201; // 0 to 200
        var pageSize = (pageSizeSeed.Get % 50) + 1; // 1 to 50
        var page = (pageSeed.Get % 20) + 1; // 1 to 20

        var allItems = GenerateItems(totalCount);
        var result = SimulatePagination(allItems, page, pageSize);

        return (result.TotalCount == totalCount).ToProperty()
            .Label($"TotalCount={result.TotalCount} but expected {totalCount} " +
                   $"(page={page}, pageSize={pageSize})");
    }

    /// <summary>
    /// Property 15c: TotalPages is computed correctly as ceiling(TotalCount / PageSize).
    /// **Validates: Requirements 7.4, 8.4, 9.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TotalPages_IsCorrectCeiling(PositiveInt totalCountSeed, PositiveInt pageSizeSeed)
    {
        var totalCount = totalCountSeed.Get % 201; // 0 to 200
        var pageSize = (pageSizeSeed.Get % 50) + 1; // 1 to 50

        var allItems = GenerateItems(totalCount);
        var result = SimulatePagination(allItems, 1, pageSize);

        var expectedTotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return (result.TotalPages == expectedTotalPages).ToProperty()
            .Label($"TotalPages={result.TotalPages} but expected {expectedTotalPages} " +
                   $"(totalCount={totalCount}, pageSize={pageSize})");
    }

    /// <summary>
    /// Property 15d: The last page contains the remainder items.
    /// For N total items and page size M, the last page contains N - (TotalPages - 1) * M items
    /// (unless N is zero, in which case the last page is empty).
    /// **Validates: Requirements 7.4, 8.4, 9.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LastPage_ContainsCorrectRemainder(PositiveInt totalCountSeed, PositiveInt pageSizeSeed)
    {
        var totalCount = totalCountSeed.Get % 201; // 0 to 200
        var pageSize = (pageSizeSeed.Get % 50) + 1; // 1 to 50

        var allItems = GenerateItems(totalCount);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        if (totalCount == 0)
        {
            var result = SimulatePagination(allItems, 1, pageSize);
            return (result.Items.Count == 0).ToProperty()
                .Label("Empty data set should return 0 items");
        }

        var lastPageResult = SimulatePagination(allItems, totalPages, pageSize);
        var expectedLastPageCount = totalCount - (totalPages - 1) * pageSize;

        return (lastPageResult.Items.Count == expectedLastPageCount).ToProperty()
            .Label($"Last page (page {totalPages}) has {lastPageResult.Items.Count} items " +
                   $"but expected {expectedLastPageCount} (totalCount={totalCount}, pageSize={pageSize})");
    }

    /// <summary>
    /// Property 15e: Pages beyond TotalPages return zero items.
    /// Requesting a page number greater than TotalPages returns an empty items list
    /// while TotalCount remains accurate.
    /// **Validates: Requirements 7.4, 8.4, 9.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BeyondLastPage_ReturnsEmptyItems(PositiveInt totalCountSeed, PositiveInt pageSizeSeed, PositiveInt extraPagesSeed)
    {
        var totalCount = (totalCountSeed.Get % 100) + 1; // 1 to 100 (ensure at least 1 item)
        var pageSize = (pageSizeSeed.Get % 50) + 1; // 1 to 50
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var beyondPage = totalPages + (extraPagesSeed.Get % 10) + 1; // at least 1 page beyond

        var allItems = GenerateItems(totalCount);
        var result = SimulatePagination(allItems, beyondPage, pageSize);

        var itemsEmpty = result.Items.Count == 0;
        var totalCountPreserved = result.TotalCount == totalCount;

        return (itemsEmpty && totalCountPreserved).ToProperty()
            .Label($"Page {beyondPage} (beyond totalPages={totalPages}): " +
                   $"items.Count={result.Items.Count} (expected 0), " +
                   $"TotalCount={result.TotalCount} (expected {totalCount})");
    }

    /// <summary>
    /// Property 15f: Sum of all page item counts equals TotalCount.
    /// Iterating through all pages and summing item counts yields exactly N.
    /// **Validates: Requirements 7.4, 8.4, 9.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AllPages_SumToTotalCount(PositiveInt totalCountSeed, PositiveInt pageSizeSeed)
    {
        var totalCount = totalCountSeed.Get % 101; // 0 to 100
        var pageSize = (pageSizeSeed.Get % 25) + 1; // 1 to 25

        var allItems = GenerateItems(totalCount);
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling((double)totalCount / pageSize);

        var sumOfItems = 0;
        for (int p = 1; p <= totalPages; p++)
        {
            var result = SimulatePagination(allItems, p, pageSize);
            sumOfItems += result.Items.Count;
        }

        return (sumOfItems == totalCount).ToProperty()
            .Label($"Sum of items across {totalPages} pages = {sumOfItems}, expected {totalCount} " +
                   $"(pageSize={pageSize})");
    }

    /// <summary>
    /// Property 15g: HasPreviousPage and HasNextPage navigation flags are correct.
    /// HasPreviousPage is true only when CurrentPage > 1.
    /// HasNextPage is true only when CurrentPage &lt; TotalPages.
    /// **Validates: Requirements 7.4, 8.4, 9.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NavigationFlags_AreCorrect(PositiveInt totalCountSeed, PositiveInt pageSizeSeed, PositiveInt pageSeed)
    {
        var totalCount = (totalCountSeed.Get % 100) + 1; // 1 to 100
        var pageSize = (pageSizeSeed.Get % 25) + 1; // 1 to 25
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var page = (pageSeed.Get % totalPages) + 1; // 1 to totalPages (valid page)

        var allItems = GenerateItems(totalCount);
        var result = SimulatePagination(allItems, page, pageSize);

        var expectedHasPrevious = page > 1;
        var expectedHasNext = page < totalPages;

        var previousCorrect = result.HasPreviousPage == expectedHasPrevious;
        var nextCorrect = result.HasNextPage == expectedHasNext;

        return (previousCorrect && nextCorrect).ToProperty()
            .Label($"Page {page}/{totalPages}: HasPrevious={result.HasPreviousPage} (expected {expectedHasPrevious}), " +
                   $"HasNext={result.HasNextPage} (expected {expectedHasNext})");
    }

    #endregion
}
