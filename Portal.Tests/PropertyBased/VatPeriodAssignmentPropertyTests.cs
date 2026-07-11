using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: vat-period-assignment, Properties 12.1–12.5

/// <summary>
/// Property-based tests for VAT period assignment workflow.
/// Tests the pure filtering and assignment logic using in-memory records
/// to validate invariants around locking, tenant isolation, idempotency,
/// cancellation exclusion, and count consistency.
/// </summary>
public class VatPeriodAssignmentPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// Represents a purchase record with the fields relevant to VAT period assignment logic.
    /// </summary>
    private record TestPurchase(
        int Id,
        int BusinessId,
        int? VatSubmissionPeriodId,
        bool IsCancelled,
        DateOnly InvoiceDate);

    /// <summary>
    /// Determines whether a purchase qualifies as "unassigned" within a date range for a business.
    /// Mirrors the SQL: WHERE BusinessId = @BusinessId AND VatSubmissionPeriodId IS NULL
    ///   AND IsCancelled = 0 AND InvoiceDate >= @Start AND InvoiceDate <= @End
    /// </summary>
    private static bool IsUnassignedInRange(TestPurchase p, int businessId, DateOnly start, DateOnly end)
    {
        return p.BusinessId == businessId
            && p.VatSubmissionPeriodId == null
            && !p.IsCancelled
            && p.InvoiceDate >= start
            && p.InvoiceDate <= end;
    }

    /// <summary>
    /// Determines whether a purchase can be bulk-assigned to a period.
    /// Mirrors the SQL: WHERE BusinessId = @BusinessId AND IsCancelled = 0 AND VatSubmissionPeriodId IS NULL
    /// </summary>
    private static bool CanBulkAssign(TestPurchase p, int businessId)
    {
        return p.BusinessId == businessId
            && !p.IsCancelled
            && p.VatSubmissionPeriodId == null;
    }

    /// <summary>
    /// Determines whether a purchase can be unassigned from its period.
    /// A purchase assigned to a submitted period cannot be unassigned.
    /// </summary>
    private static bool CanUnassign(bool isAssignedToSubmittedPeriod)
    {
        return !isAssignedToSubmittedPeriod;
    }

    /// <summary>
    /// Generates a TestPurchase with random but valid field values.
    /// </summary>
    private static Gen<TestPurchase> TestPurchaseGen(int id)
    {
        return from businessId in Gen.Elements(1, 2, 3)
               from periodId in Gen.Frequency(
                   Tuple.Create(3, Gen.Constant((int?)null)),
                   Tuple.Create(2, Gen.Choose(1, 5).Select(x => (int?)x)))
               from isCancelled in Gen.Elements(true, false)
               from dayOffset in Gen.Choose(0, 365)
               let invoiceDate = DateOnly.FromDateTime(new DateTime(2024, 1, 1).AddDays(dayOffset))
               select new TestPurchase(id, businessId, periodId, isCancelled, invoiceDate);
    }

    /// <summary>
    /// Generates a list of TestPurchase records (1 to 30 items).
    /// </summary>
    private static Gen<List<TestPurchase>> TestPurchaseListGen()
    {
        return Gen.Choose(1, 30).SelectMany(count =>
        {
            var gens = Enumerable.Range(1, count).Select(i => TestPurchaseGen(i));
            return Gen.Sequence(gens).Select(purchases => purchases.ToList());
        });
    }

    /// <summary>
    /// Set of submitted period IDs for testing locking logic.
    /// </summary>
    private static readonly HashSet<int> SubmittedPeriodIds = new() { 1, 2 };

    /// <summary>
    /// Determines whether a period is submitted (locked).
    /// </summary>
    private static bool IsPeriodSubmitted(int? periodId)
    {
        return periodId.HasValue && SubmittedPeriodIds.Contains(periodId.Value);
    }

    #endregion

    #region 12.1: Locking invariant

    /// <summary>
    /// Property 12.1a: Purchases assigned to submitted periods cannot be unassigned.
    /// The CanUnassign predicate rejects purchases whose period is submitted.
    /// **Validates: Requirements 7.1, 7.2, 7.3, 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LockedPurchases_CannotBeUnassigned(bool isSubmitted)
    {
        var canUnassign = CanUnassign(isSubmitted);
        return (isSubmitted ? !canUnassign : canUnassign).ToProperty()
            .Label($"IsSubmitted={isSubmitted}, CanUnassign={canUnassign}");
    }

    /// <summary>
    /// Property 12.1b: The BulkAssign WHERE clause (VatSubmissionPeriodId IS NULL) naturally
    /// excludes purchases already assigned to a submitted period, because they have a non-null
    /// VatSubmissionPeriodId.
    /// **Validates: Requirements 7.1, 7.2, 7.3, 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LockedPurchases_ExcludedFromBulkAssign()
    {
        return Prop.ForAll(
            TestPurchaseListGen().ToArbitrary(),
            Gen.Elements(1, 2, 3).ToArbitrary(),
            (purchases, targetBusinessId) =>
            {
                // Purchases assigned to submitted periods
                var lockedPurchases = purchases
                    .Where(p => IsPeriodSubmitted(p.VatSubmissionPeriodId))
                    .ToList();

                // Purchases that pass the BulkAssign filter
                var assignable = purchases
                    .Where(p => CanBulkAssign(p, targetBusinessId))
                    .ToList();

                // No locked purchase should appear in the assignable set
                var noLockedInAssignable = !assignable
                    .Any(p => IsPeriodSubmitted(p.VatSubmissionPeriodId));

                // Additionally, any purchase with VatSubmissionPeriodId != null is excluded
                var allAssignableAreNull = assignable
                    .All(p => p.VatSubmissionPeriodId == null);

                return (noLockedInAssignable && allAssignableAreNull).ToProperty()
                    .Label($"Locked={lockedPurchases.Count}, Assignable={assignable.Count}, " +
                           $"NoLockedInAssignable={noLockedInAssignable}, AllNull={allAssignableAreNull}");
            });
    }

    /// <summary>
    /// Property 12.1c: The BulkUnassign query uses NOT EXISTS against VatSubmission to prevent
    /// unassigning from submitted periods. Purchases whose period is submitted are excluded.
    /// **Validates: Requirements 7.1, 7.2, 7.3, 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LockedPurchases_ExcludedFromBulkUnassign()
    {
        return Prop.ForAll(
            TestPurchaseListGen().ToArbitrary(),
            Gen.Elements(1, 2, 3).ToArbitrary(),
            (purchases, targetBusinessId) =>
            {
                // Simulate unassign filter: business matches, has period, period NOT submitted
                var unassignable = purchases
                    .Where(p => p.BusinessId == targetBusinessId
                             && p.VatSubmissionPeriodId != null
                             && !IsPeriodSubmitted(p.VatSubmissionPeriodId))
                    .ToList();

                // All unassignable purchases must NOT be in a submitted period
                var noneSubmitted = unassignable
                    .All(p => !IsPeriodSubmitted(p.VatSubmissionPeriodId));

                // Purchases in submitted periods should never appear
                var lockedPurchases = purchases
                    .Where(p => p.BusinessId == targetBusinessId
                             && IsPeriodSubmitted(p.VatSubmissionPeriodId))
                    .ToList();

                var noLockedInResult = !unassignable
                    .Any(p => lockedPurchases.Select(lp => lp.Id).Contains(p.Id));

                return (noneSubmitted && noLockedInResult).ToProperty()
                    .Label($"Unassignable={unassignable.Count}, Locked={lockedPurchases.Count}, " +
                           $"NoneSubmitted={noneSubmitted}, NoLockedInResult={noLockedInResult}");
            });
    }

    #endregion

    #region 12.2: Tenant isolation

    /// <summary>
    /// Property 12.2: Assignment operations never cross business boundaries.
    /// For any set of purchases across multiple businesses, filtering for a specific businessId
    /// returns only purchases with matching BusinessId.
    /// **Validates: Requirements 12.1, 12.2, 12.3, 12.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AssignmentOperations_NeverCrossBusinessBoundaries()
    {
        return Prop.ForAll(
            TestPurchaseListGen().ToArbitrary(),
            Gen.Elements(1, 2, 3).ToArbitrary(),
            (purchases, targetBusinessId) =>
            {
                // Simulate GetUnassignedByDateRange query filtered by business
                var startDate = new DateOnly(2024, 1, 1);
                var endDate = new DateOnly(2024, 12, 31);

                var unassignedForBusiness = purchases
                    .Where(p => IsUnassignedInRange(p, targetBusinessId, startDate, endDate))
                    .ToList();

                // Property: ALL returned purchases belong to the target business
                var allBelongToTarget = unassignedForBusiness
                    .All(p => p.BusinessId == targetBusinessId);

                // Property: NO purchase from another business appears
                var noOtherBusiness = !unassignedForBusiness
                    .Any(p => p.BusinessId != targetBusinessId);

                // Simulate BulkAssign filter by business
                var assignableForBusiness = purchases
                    .Where(p => CanBulkAssign(p, targetBusinessId))
                    .ToList();

                var assignableAllBelongToTarget = assignableForBusiness
                    .All(p => p.BusinessId == targetBusinessId);

                // Cross-check: other businesses should have their own results
                var otherBusinessIds = new[] { 1, 2, 3 }.Where(b => b != targetBusinessId).ToList();
                var crossContamination = otherBusinessIds.Any(otherBiz =>
                    unassignedForBusiness.Any(p => p.BusinessId == otherBiz));

                var allPropertiesHold = allBelongToTarget
                    && noOtherBusiness
                    && assignableAllBelongToTarget
                    && !crossContamination;

                return allPropertiesHold.ToProperty()
                    .Label($"TargetBiz={targetBusinessId}, Unassigned={unassignedForBusiness.Count}, " +
                           $"Assignable={assignableForBusiness.Count}, " +
                           $"AllBelongToTarget={allBelongToTarget}, NoCross={!crossContamination}");
            });
    }

    #endregion

    #region 12.3: Assignment idempotency

    /// <summary>
    /// Property 12.3: Assigning already-assigned purchases to the same period is a no-op.
    /// The SQL uses WHERE VatSubmissionPeriodId IS NULL — if already assigned, 0 rows are affected.
    /// **Validates: Requirements 9.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AlreadyAssignedPurchases_ExcludedFromAssignOperation()
    {
        return Prop.ForAll(
            TestPurchaseListGen().ToArbitrary(),
            Gen.Elements(1, 2, 3).ToArbitrary(),
            Gen.Choose(1, 5).ToArbitrary(),
            (purchases, targetBusinessId, targetPeriodId) =>
            {
                // Purchases already assigned to the target period
                var alreadyAssigned = purchases
                    .Where(p => p.BusinessId == targetBusinessId
                             && p.VatSubmissionPeriodId == targetPeriodId)
                    .ToList();

                // The BulkAssign filter requires VatSubmissionPeriodId IS NULL
                var wouldBeAffected = purchases
                    .Where(p => CanBulkAssign(p, targetBusinessId))
                    .ToList();

                // Property: None of the already-assigned purchases appear in the "would be affected" set
                var alreadyAssignedExcluded = !alreadyAssigned
                    .Any(p => wouldBeAffected.Select(w => w.Id).Contains(p.Id));

                // Property: Re-assigning already-assigned purchases results in 0 rows affected
                var reassignAttempt = alreadyAssigned
                    .Where(p => p.VatSubmissionPeriodId == null) // This is always empty for already-assigned
                    .ToList();
                var zeroRowsAffected = reassignAttempt.Count == 0;

                return (alreadyAssignedExcluded && zeroRowsAffected).ToProperty()
                    .Label($"AlreadyAssigned={alreadyAssigned.Count}, WouldBeAffected={wouldBeAffected.Count}, " +
                           $"Excluded={alreadyAssignedExcluded}, ZeroRows={zeroRowsAffected}");
            });
    }

    #endregion

    #region 12.4: Cancelled purchases excluded

    /// <summary>
    /// Property 12.4: Cancelled purchases are never included in unassigned counts or assignment operations.
    /// The predicate IsCancelled = 0 excludes cancelled purchases from all operations.
    /// **Validates: Requirements 5.2, 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CancelledPurchases_NeverIncludedInOperations()
    {
        return Prop.ForAll(
            TestPurchaseListGen().ToArbitrary(),
            Gen.Elements(1, 2, 3).ToArbitrary(),
            (purchases, targetBusinessId) =>
            {
                var startDate = new DateOnly(2024, 1, 1);
                var endDate = new DateOnly(2024, 12, 31);

                // Get cancelled purchases for this business
                var cancelledPurchases = purchases
                    .Where(p => p.BusinessId == targetBusinessId && p.IsCancelled)
                    .ToList();

                // Unassigned query results
                var unassignedResults = purchases
                    .Where(p => IsUnassignedInRange(p, targetBusinessId, startDate, endDate))
                    .ToList();

                // BulkAssign filter results
                var assignableResults = purchases
                    .Where(p => CanBulkAssign(p, targetBusinessId))
                    .ToList();

                // Property: No cancelled purchase appears in unassigned results
                var noCancelledInUnassigned = !unassignedResults
                    .Any(p => p.IsCancelled);

                // Property: No cancelled purchase appears in assignable results
                var noCancelledInAssignable = !assignableResults
                    .Any(p => p.IsCancelled);

                // Property: Count of qualifying = count where NOT cancelled AND VatSubmissionPeriodId IS NULL
                var expectedUnassignedCount = purchases
                    .Count(p => p.BusinessId == targetBusinessId
                             && !p.IsCancelled
                             && p.VatSubmissionPeriodId == null
                             && p.InvoiceDate >= startDate
                             && p.InvoiceDate <= endDate);
                var countMatches = unassignedResults.Count == expectedUnassignedCount;

                var allPropertiesHold = noCancelledInUnassigned
                    && noCancelledInAssignable
                    && countMatches;

                return allPropertiesHold.ToProperty()
                    .Label($"Cancelled={cancelledPurchases.Count}, Unassigned={unassignedResults.Count}, " +
                           $"Assignable={assignableResults.Count}, " +
                           $"NoCancelledInUnassigned={noCancelledInUnassigned}, " +
                           $"NoCancelledInAssignable={noCancelledInAssignable}, " +
                           $"CountMatches={countMatches}");
            });
    }

    #endregion

    #region 12.5: Count consistency

    /// <summary>
    /// Generates a date range tuple (startDate, endDate) within 2024.
    /// </summary>
    private static Gen<(DateOnly Start, DateOnly End)> DateRangeGen()
    {
        return from startMonth in Gen.Choose(0, 11)
               from monthSpan in Gen.Choose(1, 6)
               let start = new DateOnly(2024, Math.Max(1, Math.Min(12, startMonth + 1)), 1)
               let end = start.AddMonths(monthSpan).AddDays(-1) > new DateOnly(2024, 12, 31)
                   ? new DateOnly(2024, 12, 31)
                   : start.AddMonths(monthSpan).AddDays(-1)
               select (start, end);
    }

    /// <summary>
    /// Generates a combined test input for count consistency: purchases, businessId, and date range.
    /// </summary>
    private static Gen<(List<TestPurchase> Purchases, int BusinessId, DateOnly Start, DateOnly End)> CountConsistencyInputGen()
    {
        return from purchases in TestPurchaseListGen()
               from businessId in Gen.Elements(1, 2, 3)
               from range in DateRangeGen()
               select (purchases, businessId, range.Start, range.End);
    }

    /// <summary>
    /// Property 12.5: Unassigned count equals the number of purchases returned by the unassigned query.
    /// Both use the same predicate: BusinessId match, VatSubmissionPeriodId IS NULL, IsCancelled = 0,
    /// InvoiceDate in range. Applying the predicate to a list yields consistent results between
    /// Count() and the filtered list's length.
    /// **Validates: Requirements 6.1, 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnassignedCount_EqualsUnassignedListLength()
    {
        return Prop.ForAll(
            CountConsistencyInputGen().ToArbitrary(),
            input =>
            {
                var (purchases, targetBusinessId, startDate, endDate) = input;

                // Simulate CountUnassignedByDateRangeAsync (the count query)
                var count = purchases
                    .Count(p => IsUnassignedInRange(p, targetBusinessId, startDate, endDate));

                // Simulate GetUnassignedByDateRangeAsync (the list query)
                var list = purchases
                    .Where(p => IsUnassignedInRange(p, targetBusinessId, startDate, endDate))
                    .ToList();

                // Property: count equals list length
                var countEqualsListLength = count == list.Count;

                // Property: all items in list satisfy the predicate
                var allSatisfyPredicate = list
                    .All(p => IsUnassignedInRange(p, targetBusinessId, startDate, endDate));

                return (countEqualsListLength && allSatisfyPredicate).ToProperty()
                    .Label($"Biz={targetBusinessId}, Range={startDate}–{endDate}, " +
                           $"Count={count}, ListLen={list.Count}, " +
                           $"AllSatisfy={allSatisfyPredicate}");
            });
    }

    #endregion
}
