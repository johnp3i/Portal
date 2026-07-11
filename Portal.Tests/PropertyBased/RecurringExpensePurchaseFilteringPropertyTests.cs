using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: recurring-expense-validation, Properties 2, 3, 4, 8: Purchase filtering logic

/// <summary>
/// Property-based tests for recurring expense purchase filtering logic.
/// Tests the pure filtering predicates that mirror the SQL WHERE clauses used
/// by CountQualifyingPurchasesAsync and tenant-scoped queries.
/// **Validates: Requirements 2.2, 2.3, 11.1, 11.2, 14.1, 14.2**
/// </summary>
public class RecurringExpensePurchaseFilteringPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// A lightweight record representing a purchase for in-memory filtering tests.
    /// Contains only the fields relevant to the filtering predicates.
    /// </summary>
    private record TestPurchase(
        int BusinessId,
        int SupplierId,
        int ExpenseCategoryId,
        bool IsCancelled,
        DateOnly InvoiceDate,
        decimal AmountExcludingVat);

    /// <summary>
    /// The filtering predicate matching the SQL WHERE clause for qualifying purchase counts.
    /// This mirrors the logic in PurchaseRepository.CountQualifyingPurchasesAsync.
    /// </summary>
    private static bool IsQualifyingPurchase(
        TestPurchase purchase,
        int businessId,
        int supplierId,
        int? expenseCategoryId,
        DateOnly startDate,
        DateOnly endDate)
    {
        if (purchase.BusinessId != businessId) return false;
        if (purchase.SupplierId != supplierId) return false;
        if (purchase.IsCancelled) return false;
        if (expenseCategoryId.HasValue && purchase.ExpenseCategoryId != expenseCategoryId.Value) return false;
        if (purchase.InvoiceDate < startDate || purchase.InvoiceDate > endDate) return false;
        return true;
    }

    /// <summary>
    /// Counts qualifying purchases from a list using the filtering predicate.
    /// </summary>
    private static int CountQualifying(
        List<TestPurchase> purchases,
        int businessId,
        int supplierId,
        int? expenseCategoryId,
        DateOnly startDate,
        DateOnly endDate)
    {
        return purchases.Count(p => IsQualifyingPurchase(p, businessId, supplierId, expenseCategoryId, startDate, endDate));
    }

    #endregion

    #region Generators

    private const int TargetBusinessId = 1;
    private const int TargetSupplierId = 10;
    private const int TargetCategoryId = 100;
    private const int OtherCategoryId = 200;

    private static readonly DateOnly PeriodStart = new(2024, 3, 1);
    private static readonly DateOnly PeriodEnd = new(2024, 5, 31);

    /// <summary>
    /// Generates a TestPurchase with a random IsCancelled flag for the target business/supplier/category within the period.
    /// </summary>
    private static Gen<TestPurchase> PurchaseWithRandomCancelledGen()
    {
        return Arb.Generate<bool>().SelectMany(isCancelled =>
            Gen.Choose(0, 91).SelectMany(dayOffset =>
                Gen.Choose(100, 10000).Select(amountSeed =>
                    new TestPurchase(
                        BusinessId: TargetBusinessId,
                        SupplierId: TargetSupplierId,
                        ExpenseCategoryId: TargetCategoryId,
                        IsCancelled: isCancelled,
                        InvoiceDate: PeriodStart.AddDays(dayOffset),
                        AmountExcludingVat: amountSeed / 100m))));
    }

    /// <summary>
    /// Generates a list of purchases with random cancelled flags.
    /// </summary>
    private static Gen<List<TestPurchase>> MixedCancelledPurchaseListGen()
    {
        return Gen.Choose(1, 30).SelectMany(count =>
        {
            var gens = Enumerable.Range(0, count).Select(_ => PurchaseWithRandomCancelledGen());
            return Gen.Sequence(gens).Select(ps => ps.ToList());
        });
    }

    /// <summary>
    /// Generates a TestPurchase with a random ExpenseCategoryId for the target business/supplier.
    /// </summary>
    private static Gen<TestPurchase> PurchaseWithRandomCategoryGen()
    {
        return Gen.Elements(TargetCategoryId, OtherCategoryId, 300, 400).SelectMany(categoryId =>
            Gen.Choose(0, 91).SelectMany(dayOffset =>
                Gen.Choose(100, 10000).Select(amountSeed =>
                    new TestPurchase(
                        BusinessId: TargetBusinessId,
                        SupplierId: TargetSupplierId,
                        ExpenseCategoryId: categoryId,
                        IsCancelled: false,
                        InvoiceDate: PeriodStart.AddDays(dayOffset),
                        AmountExcludingVat: amountSeed / 100m))));
    }

    /// <summary>
    /// Generates a list of purchases with varied category IDs.
    /// </summary>
    private static Gen<List<TestPurchase>> VariedCategoryPurchaseListGen()
    {
        return Gen.Choose(1, 30).SelectMany(count =>
        {
            var gens = Enumerable.Range(0, count).Select(_ => PurchaseWithRandomCategoryGen());
            return Gen.Sequence(gens).Select(ps => ps.ToList());
        });
    }

    /// <summary>
    /// Generates a TestPurchase assigned to a random business ID from a set of IDs.
    /// </summary>
    private static Gen<TestPurchase> PurchaseWithRandomBusinessIdGen(int[] businessIds)
    {
        return Gen.Elements(businessIds).SelectMany(bizId =>
            Gen.Choose(0, 91).SelectMany(dayOffset =>
                Gen.Choose(100, 10000).Select(amountSeed =>
                    new TestPurchase(
                        BusinessId: bizId,
                        SupplierId: TargetSupplierId,
                        ExpenseCategoryId: TargetCategoryId,
                        IsCancelled: false,
                        InvoiceDate: PeriodStart.AddDays(dayOffset),
                        AmountExcludingVat: amountSeed / 100m))));
    }

    /// <summary>
    /// Generates a list of purchases distributed across multiple business IDs.
    /// </summary>
    private static Gen<List<TestPurchase>> MultiTenantPurchaseListGen()
    {
        var businessIds = new[] { 1, 2, 3, 4, 5 };
        return Gen.Choose(5, 40).SelectMany(count =>
        {
            var gens = Enumerable.Range(0, count).Select(_ => PurchaseWithRandomBusinessIdGen(businessIds));
            return Gen.Sequence(gens).Select(ps => ps.ToList());
        });
    }

    #endregion

    #region Property 2: Qualifying purchase count excludes cancelled purchases

    /// <summary>
    /// Property 2: Qualifying purchase count excludes cancelled purchases.
    /// For any mixed set of cancelled and non-cancelled purchases from the same
    /// supplier/business/category/period, the qualifying count equals exactly the
    /// count of non-cancelled purchases.
    /// **Validates: Requirements 14.1, 14.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CancelledPurchases_AreExcluded_FromQualifyingCount()
    {
        return Prop.ForAll(
            MixedCancelledPurchaseListGen().ToArbitrary(),
            (purchases) =>
            {
                var qualifyingCount = CountQualifying(
                    purchases,
                    TargetBusinessId,
                    TargetSupplierId,
                    expenseCategoryId: TargetCategoryId,
                    PeriodStart,
                    PeriodEnd);

                var expectedNonCancelledCount = purchases.Count(p => !p.IsCancelled);

                return (qualifyingCount == expectedNonCancelledCount).ToProperty()
                    .Label($"Total={purchases.Count}, Cancelled={purchases.Count(p => p.IsCancelled)}, " +
                           $"NonCancelled={expectedNonCancelledCount}, QualifyingCount={qualifyingCount}");
            });
    }

    /// <summary>
    /// Property 2 (corollary): No cancelled purchase ever appears in qualifying results.
    /// For any set of purchases, filtering out cancelled ones means every qualifying
    /// purchase has IsCancelled = false.
    /// **Validates: Requirements 14.1, 14.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CancelledPurchases_NeverQualify()
    {
        return Prop.ForAll(
            MixedCancelledPurchaseListGen().ToArbitrary(),
            (purchases) =>
            {
                var qualifying = purchases.Where(p =>
                    IsQualifyingPurchase(p, TargetBusinessId, TargetSupplierId, TargetCategoryId, PeriodStart, PeriodEnd))
                    .ToList();

                var noCancelledInResults = qualifying.All(p => !p.IsCancelled);

                return noCancelledInResults.ToProperty()
                    .Label($"Qualifying={qualifying.Count}, " +
                           $"CancelledInResults={qualifying.Count(p => p.IsCancelled)}");
            });
    }

    #endregion

    #region Property 3: Category-scoped rules only count category-matched purchases

    /// <summary>
    /// Property 3: When a rule has a specific ExpenseCategoryId, only purchases
    /// matching that category are counted. Purchases from the same supplier with
    /// different categories are excluded.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CategoryScopedRule_OnlyCountsMatchingCategory()
    {
        return Prop.ForAll(
            VariedCategoryPurchaseListGen().ToArbitrary(),
            (purchases) =>
            {
                // Rule has a specific category: TargetCategoryId
                var qualifyingCount = CountQualifying(
                    purchases,
                    TargetBusinessId,
                    TargetSupplierId,
                    expenseCategoryId: TargetCategoryId,
                    PeriodStart,
                    PeriodEnd);

                var expectedCount = purchases.Count(p => p.ExpenseCategoryId == TargetCategoryId);

                return (qualifyingCount == expectedCount).ToProperty()
                    .Label($"Total={purchases.Count}, " +
                           $"MatchingCategory={expectedCount}, " +
                           $"OtherCategories={purchases.Count(p => p.ExpenseCategoryId != TargetCategoryId)}, " +
                           $"QualifyingCount={qualifyingCount}");
            });
    }

    /// <summary>
    /// Property 3 (corollary): With a category-scoped rule, no purchase with a
    /// different category appears in qualifying results.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CategoryScopedRule_ExcludesNonMatchingCategories()
    {
        return Prop.ForAll(
            VariedCategoryPurchaseListGen().ToArbitrary(),
            (purchases) =>
            {
                var qualifying = purchases.Where(p =>
                    IsQualifyingPurchase(p, TargetBusinessId, TargetSupplierId, TargetCategoryId, PeriodStart, PeriodEnd))
                    .ToList();

                var allMatchCategory = qualifying.All(p => p.ExpenseCategoryId == TargetCategoryId);

                return allMatchCategory.ToProperty()
                    .Label($"Qualifying={qualifying.Count}, " +
                           $"WrongCategory={qualifying.Count(p => p.ExpenseCategoryId != TargetCategoryId)}");
            });
    }

    #endregion

    #region Property 4: Category-null rules count all purchases from supplier

    /// <summary>
    /// Property 4: When a rule has ExpenseCategoryId = null, ALL non-cancelled
    /// purchases from the specified supplier are counted, regardless of their category.
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CategoryNullRule_CountsAllPurchasesFromSupplier()
    {
        return Prop.ForAll(
            VariedCategoryPurchaseListGen().ToArbitrary(),
            (purchases) =>
            {
                // Rule has null category — should count all non-cancelled purchases from supplier
                var qualifyingCount = CountQualifying(
                    purchases,
                    TargetBusinessId,
                    TargetSupplierId,
                    expenseCategoryId: null, // null = any category
                    PeriodStart,
                    PeriodEnd);

                // All purchases in the generated list are non-cancelled, from the target supplier/business, within period
                var expectedCount = purchases.Count;

                return (qualifyingCount == expectedCount).ToProperty()
                    .Label($"Total={purchases.Count}, " +
                           $"DistinctCategories={purchases.Select(p => p.ExpenseCategoryId).Distinct().Count()}, " +
                           $"QualifyingCount={qualifyingCount}, Expected={expectedCount}");
            });
    }

    /// <summary>
    /// Property 4 (comparison): For the same set of purchases, a category-null rule
    /// always counts >= a category-scoped rule.
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CategoryNullRule_CountsAtLeastAsMuch_AsCategoryScopedRule()
    {
        return Prop.ForAll(
            VariedCategoryPurchaseListGen().ToArbitrary(),
            (purchases) =>
            {
                var nullCategoryCount = CountQualifying(
                    purchases,
                    TargetBusinessId,
                    TargetSupplierId,
                    expenseCategoryId: null,
                    PeriodStart,
                    PeriodEnd);

                var scopedCategoryCount = CountQualifying(
                    purchases,
                    TargetBusinessId,
                    TargetSupplierId,
                    expenseCategoryId: TargetCategoryId,
                    PeriodStart,
                    PeriodEnd);

                return (nullCategoryCount >= scopedCategoryCount).ToProperty()
                    .Label($"NullCategory={nullCategoryCount}, " +
                           $"ScopedCategory={scopedCategoryCount}");
            });
    }

    #endregion

    #region Property 8: Tenant isolation on all queries

    /// <summary>
    /// Property 8: A count for businessId X only includes purchases where BusinessId == X.
    /// No cross-tenant data appears in any filtered result.
    /// **Validates: Requirements 11.1, 11.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TenantIsolation_QueryForBusinessX_OnlyIncludesBusinessX()
    {
        return Prop.ForAll(
            MultiTenantPurchaseListGen().ToArbitrary(),
            (purchases) =>
            {
                // Query for each business ID and verify isolation
                var businessIds = new[] { 1, 2, 3, 4, 5 };
                var allIsolated = true;
                var violationDetails = "";

                foreach (var queryBusinessId in businessIds)
                {
                    var qualifying = purchases.Where(p =>
                        IsQualifyingPurchase(p, queryBusinessId, TargetSupplierId, null, PeriodStart, PeriodEnd))
                        .ToList();

                    var crossTenantItems = qualifying.Where(p => p.BusinessId != queryBusinessId).ToList();

                    if (crossTenantItems.Count > 0)
                    {
                        allIsolated = false;
                        violationDetails += $"Business {queryBusinessId} got {crossTenantItems.Count} foreign items; ";
                    }
                }

                return allIsolated.ToProperty()
                    .Label($"TotalPurchases={purchases.Count}, " +
                           $"Violations={violationDetails}");
            });
    }

    /// <summary>
    /// Property 8 (completeness): A count for businessId X includes ALL qualifying
    /// purchases for that business — no records are incorrectly excluded.
    /// **Validates: Requirements 11.1, 11.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TenantIsolation_QueryForBusinessX_IncludesAllFromBusinessX()
    {
        return Prop.ForAll(
            MultiTenantPurchaseListGen().ToArbitrary(),
            (purchases) =>
            {
                var businessIds = new[] { 1, 2, 3, 4, 5 };
                var allComplete = true;
                var details = "";

                foreach (var queryBusinessId in businessIds)
                {
                    var qualifyingCount = CountQualifying(
                        purchases,
                        queryBusinessId,
                        TargetSupplierId,
                        expenseCategoryId: null,
                        PeriodStart,
                        PeriodEnd);

                    // Expected: all non-cancelled purchases from this business, target supplier, in period
                    var expectedCount = purchases.Count(p =>
                        p.BusinessId == queryBusinessId &&
                        p.SupplierId == TargetSupplierId &&
                        !p.IsCancelled &&
                        p.InvoiceDate >= PeriodStart &&
                        p.InvoiceDate <= PeriodEnd);

                    if (qualifyingCount != expectedCount)
                    {
                        allComplete = false;
                        details += $"Business {queryBusinessId}: expected={expectedCount}, actual={qualifyingCount}; ";
                    }
                }

                return allComplete.ToProperty()
                    .Label($"TotalPurchases={purchases.Count}, Issues={details}");
            });
    }

    /// <summary>
    /// Property 8 (sum invariant): The sum of qualifying counts across all tenants
    /// equals the total non-cancelled purchases from the target supplier within the period.
    /// This ensures no duplication or loss across tenant boundaries.
    /// **Validates: Requirements 11.1, 11.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TenantIsolation_SumAcrossTenants_EqualsTotal()
    {
        return Prop.ForAll(
            MultiTenantPurchaseListGen().ToArbitrary(),
            (purchases) =>
            {
                var businessIds = new[] { 1, 2, 3, 4, 5 };

                var sumOfTenantCounts = businessIds.Sum(bizId =>
                    CountQualifying(purchases, bizId, TargetSupplierId, null, PeriodStart, PeriodEnd));

                var totalQualifying = purchases.Count(p =>
                    p.SupplierId == TargetSupplierId &&
                    !p.IsCancelled &&
                    p.InvoiceDate >= PeriodStart &&
                    p.InvoiceDate <= PeriodEnd);

                return (sumOfTenantCounts == totalQualifying).ToProperty()
                    .Label($"SumOfTenants={sumOfTenantCounts}, TotalQualifying={totalQualifying}");
            });
    }

    #endregion
}
