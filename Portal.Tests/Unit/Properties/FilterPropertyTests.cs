// Feature: purchase-expense-tracking, Property 8: Filter correctness
using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;

namespace Portal.Tests.Unit.Properties;

/// <summary>
/// Property 8: Filter correctness — Random purchase sets + random filter combinations,
/// verify all returned purchases satisfy all criteria and no qualifying purchase is excluded.
/// **Validates: Requirements 3.6**
/// </summary>
public class FilterPropertyTests
{
    private const int TestBusinessId = 1;

    /// <summary>
    /// Applies the same filtering logic as PurchaseRepository.GetFilteredAsync in-memory.
    /// This is the reference implementation we test against.
    /// </summary>
    private static List<Purchase> ApplyFilter(
        List<Purchase> purchases,
        int businessId,
        int? supplierId,
        int? expenseCategoryId,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
        var query = purchases.Where(p => p.BusinessId == businessId);

        if (supplierId.HasValue)
            query = query.Where(p => p.SupplierId == supplierId.Value);

        if (expenseCategoryId.HasValue)
            query = query.Where(p => p.ExpenseCategoryId == expenseCategoryId.Value);

        if (dateFrom.HasValue)
            query = query.Where(p => p.InvoiceDate >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(p => p.InvoiceDate <= dateTo.Value);

        return query.ToList();
    }

    /// <summary>
    /// Checks whether a single purchase satisfies all specified filter criteria.
    /// </summary>
    private static bool SatisfiesAllCriteria(
        Purchase purchase,
        int businessId,
        int? supplierId,
        int? expenseCategoryId,
        DateOnly? dateFrom,
        DateOnly? dateTo)
    {
        if (purchase.BusinessId != businessId)
            return false;

        if (supplierId.HasValue && purchase.SupplierId != supplierId.Value)
            return false;

        if (expenseCategoryId.HasValue && purchase.ExpenseCategoryId != expenseCategoryId.Value)
            return false;

        if (dateFrom.HasValue && purchase.InvoiceDate < dateFrom.Value)
            return false;

        if (dateTo.HasValue && purchase.InvoiceDate > dateTo.Value)
            return false;

        return true;
    }
}
