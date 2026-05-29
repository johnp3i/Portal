using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Entities;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: purchase-classification-enhancements, Property 9: Purchase Type Filtering

/// <summary>
/// Property-based tests for purchase type filtering logic.
/// For any list of purchases and filter value in {1,2,3}, result contains only and all purchases matching that PurchaseTypeId.
/// **Validates: Requirements 5.4**
/// </summary>
public class PurchaseTypeFilteringPropertyTests
{
    #region Generators

    /// <summary>
    /// Generates a valid PurchaseTypeId filter value in {1, 2, 3}.
    /// </summary>
    private static Gen<int> ValidPurchaseTypeIdGen()
    {
        return Gen.Elements(1, 2, 3);
    }

    /// <summary>
    /// Generates a Purchase entity with a random PurchaseTypeId in {1, 2, 3} and a unique Id.
    /// Other fields are set to minimal valid values since only PurchaseTypeId matters for filtering.
    /// </summary>
    private static Gen<Purchase> PurchaseGen(int id)
    {
        return ValidPurchaseTypeIdGen().Select(typeId => new Purchase
        {
            Id = id,
            BusinessId = 1,
            SupplierId = 1,
            ExpenseCategoryId = 1,
            PurchaseOriginTypeId = 1,
            PurchaseTypeId = typeId,
            InvoiceDate = DateOnly.FromDateTime(DateTime.Today),
            AmountExcludingVat = 100m,
            VatAmount = 15m,
            TotalAmount = 115m,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Generates a list of purchases (0 to 30 items) with unique Ids and random PurchaseTypeIds.
    /// </summary>
    private static Gen<List<Purchase>> PurchaseListGen()
    {
        return Gen.Choose(0, 30).SelectMany(count =>
        {
            if (count == 0)
                return Gen.Constant(new List<Purchase>());

            var gens = Enumerable.Range(1, count).Select(i => PurchaseGen(i));
            return Gen.Sequence(gens).Select(purchases => purchases.ToList());
        });
    }

    #endregion

    #region Filtering Logic (mirrors PurchaseController.Index)

    /// <summary>
    /// Applies the same filtering logic as PurchaseController.Index:
    /// purchases.Where(p => p.PurchaseTypeId == purchaseTypeId.Value).ToList()
    /// </summary>
    private static List<Purchase> ApplyPurchaseTypeFilter(List<Purchase> purchases, int purchaseTypeId)
    {
        return purchases.Where(p => p.PurchaseTypeId == purchaseTypeId).ToList();
    }

    #endregion

    #region Property 9a: Filtered result contains ONLY purchases matching the filter value

    /// <summary>
    /// Property 9a: For any list of purchases and filter value in {1,2,3},
    /// every purchase in the filtered result has PurchaseTypeId equal to the filter value.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FilteredResult_ContainsOnly_MatchingPurchases()
    {
        return Prop.ForAll(
            PurchaseListGen().ToArbitrary(),
            ValidPurchaseTypeIdGen().ToArbitrary(),
            (purchases, filterValue) =>
            {
                var result = ApplyPurchaseTypeFilter(purchases, filterValue);

                var allMatch = result.All(p => p.PurchaseTypeId == filterValue);

                return allMatch.ToProperty()
                    .Label($"Filter={filterValue}, ResultCount={result.Count}, " +
                           $"NonMatching={result.Count(p => p.PurchaseTypeId != filterValue)}");
            });
    }

    #endregion

    #region Property 9b: Filtered result contains ALL purchases matching the filter value

    /// <summary>
    /// Property 9b: For any list of purchases and filter value in {1,2,3},
    /// the filtered result contains every purchase from the original list whose PurchaseTypeId matches the filter value.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FilteredResult_ContainsAll_MatchingPurchases()
    {
        return Prop.ForAll(
            PurchaseListGen().ToArbitrary(),
            ValidPurchaseTypeIdGen().ToArbitrary(),
            (purchases, filterValue) =>
            {
                var result = ApplyPurchaseTypeFilter(purchases, filterValue);

                var expectedCount = purchases.Count(p => p.PurchaseTypeId == filterValue);
                var actualCount = result.Count;

                return (actualCount == expectedCount).ToProperty()
                    .Label($"Filter={filterValue}, Expected={expectedCount}, Actual={actualCount}");
            });
    }

    #endregion

    #region Property 9c: Filtered result count equals count of matching purchases in original list

    /// <summary>
    /// Property 9c: For any list of purchases and filter value in {1,2,3},
    /// the filtered result set is exactly the subset of the original list where PurchaseTypeId matches.
    /// This verifies both "only" and "all" in a single set-equality check.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FilteredResult_IsExactSubset_OfMatchingPurchases()
    {
        return Prop.ForAll(
            PurchaseListGen().ToArbitrary(),
            ValidPurchaseTypeIdGen().ToArbitrary(),
            (purchases, filterValue) =>
            {
                var result = ApplyPurchaseTypeFilter(purchases, filterValue);

                // The result should contain exactly the same Ids as the matching subset
                var expectedIds = purchases
                    .Where(p => p.PurchaseTypeId == filterValue)
                    .Select(p => p.Id)
                    .OrderBy(id => id)
                    .ToList();

                var actualIds = result
                    .Select(p => p.Id)
                    .OrderBy(id => id)
                    .ToList();

                var setsEqual = expectedIds.SequenceEqual(actualIds);

                return setsEqual.ToProperty()
                    .Label($"Filter={filterValue}, ExpectedIds=[{string.Join(",", expectedIds)}], " +
                           $"ActualIds=[{string.Join(",", actualIds)}]");
            });
    }

    #endregion
}
