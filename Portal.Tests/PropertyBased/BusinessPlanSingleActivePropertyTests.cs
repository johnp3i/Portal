using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-plans, Property 5: Single active plan per business

/// <summary>
/// Property-based tests for single active plan per business constraint.
/// For any business, the system SHALL permit at most one BusinessPlan record with IsActive = 1
/// at any given time. Attempting to insert a second active plan for the same business SHALL be rejected.
/// This tests the invariant logic that enforces the filtered unique index
/// UX_BusinessPlan_BusinessId_IsActive WHERE IsActive = 1.
/// **Validates: Requirements 3.9**
/// </summary>
public class BusinessPlanSingleActivePropertyTests
{
    /// <summary>
    /// Represents a simplified BusinessPlan record for constraint validation.
    /// </summary>
    private record BusinessPlanRecord(int BusinessId, int PlanId, bool IsActive);

    /// <summary>
    /// Validates the business rule: at most one BusinessPlan with IsActive = 1 per business.
    /// Returns true if the constraint is satisfied (0 or 1 active plans per business).
    /// </summary>
    private static bool HasAtMostOneActivePlanPerBusiness(IEnumerable<BusinessPlanRecord> plans, int businessId)
    {
        return plans.Count(p => p.BusinessId == businessId && p.IsActive) <= 1;
    }

    /// <summary>
    /// Simulates the filtered unique index constraint check.
    /// Returns true if inserting a new active plan for the given business would violate the constraint.
    /// </summary>
    private static bool WouldViolateConstraint(IEnumerable<BusinessPlanRecord> existingPlans, int businessId, bool newIsActive)
    {
        if (!newIsActive)
            return false; // Inactive plans never violate the constraint

        // A violation occurs if there is already an active plan for this business
        return existingPlans.Any(p => p.BusinessId == businessId && p.IsActive);
    }

    #region Property 5a: A single active plan per business satisfies the constraint

    /// <summary>
    /// Property 5a: For any business with exactly one active plan, the constraint is satisfied.
    /// A business is allowed to have one active plan at any time.
    /// **Validates: Requirements 3.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleActivePlan_SatisfiesConstraint(PositiveInt businessId, PositiveInt planId)
    {
        var plans = new List<BusinessPlanRecord>
        {
            new(businessId.Get, planId.Get, IsActive: true)
        };

        var result = HasAtMostOneActivePlanPerBusiness(plans, businessId.Get);

        return result.ToProperty()
            .Label($"BusinessId={businessId.Get}: Single active plan should satisfy constraint");
    }

    #endregion

    #region Property 5b: Multiple active plans for the same business violates the constraint

    /// <summary>
    /// Property 5b: For any business with more than one active plan, the constraint is violated.
    /// The filtered unique index prevents this state from occurring.
    /// **Validates: Requirements 3.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleActivePlans_ViolatesConstraint(PositiveInt businessId, PositiveInt planId1, PositiveInt planId2)
    {
        var plans = new List<BusinessPlanRecord>
        {
            new(businessId.Get, planId1.Get, IsActive: true),
            new(businessId.Get, planId2.Get, IsActive: true)
        };

        var result = HasAtMostOneActivePlanPerBusiness(plans, businessId.Get);

        return (!result).ToProperty()
            .Label($"BusinessId={businessId.Get}: Two active plans should violate constraint");
    }

    #endregion

    #region Property 5c: Inactive plans do not affect the constraint

    /// <summary>
    /// Property 5c: For any business, adding inactive plans (IsActive = 0) does not violate
    /// the single active plan constraint. Multiple inactive plans are permitted.
    /// **Validates: Requirements 3.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InactivePlans_DoNotAffectConstraint(PositiveInt businessId, PositiveInt planCount)
    {
        // Create multiple inactive plans for the same business
        var count = (planCount.Get % 10) + 1; // 1 to 10 inactive plans
        var plans = Enumerable.Range(1, count)
            .Select(i => new BusinessPlanRecord(businessId.Get, i, IsActive: false))
            .ToList();

        var result = HasAtMostOneActivePlanPerBusiness(plans, businessId.Get);

        return result.ToProperty()
            .Label($"BusinessId={businessId.Get}: {count} inactive plans should satisfy constraint");
    }

    #endregion

    #region Property 5d: One active plan plus multiple inactive plans satisfies the constraint

    /// <summary>
    /// Property 5d: For any business with exactly one active plan and any number of inactive plans,
    /// the constraint is satisfied. Historical (deactivated) plans are allowed alongside one active plan.
    /// **Validates: Requirements 3.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OneActivePlusInactivePlans_SatisfiesConstraint(PositiveInt businessId, PositiveInt inactiveCount)
    {
        var count = (inactiveCount.Get % 10) + 1; // 1 to 10 inactive plans
        var plans = new List<BusinessPlanRecord>
        {
            new(businessId.Get, 100, IsActive: true) // The single active plan
        };

        // Add multiple inactive (historical) plans
        for (int i = 1; i <= count; i++)
        {
            plans.Add(new BusinessPlanRecord(businessId.Get, i, IsActive: false));
        }

        var result = HasAtMostOneActivePlanPerBusiness(plans, businessId.Get);

        return result.ToProperty()
            .Label($"BusinessId={businessId.Get}: 1 active + {count} inactive plans should satisfy constraint");
    }

    #endregion

    #region Property 5e: Inserting a second active plan would violate the constraint

    /// <summary>
    /// Property 5e: For any business that already has an active plan, attempting to insert
    /// another active plan SHALL be detected as a constraint violation.
    /// **Validates: Requirements 3.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InsertingSecondActivePlan_WouldViolateConstraint(PositiveInt businessId, PositiveInt existingPlanId)
    {
        var existingPlans = new List<BusinessPlanRecord>
        {
            new(businessId.Get, existingPlanId.Get, IsActive: true)
        };

        var wouldViolate = WouldViolateConstraint(existingPlans, businessId.Get, newIsActive: true);

        return wouldViolate.ToProperty()
            .Label($"BusinessId={businessId.Get}: Inserting second active plan should be detected as violation");
    }

    #endregion

    #region Property 5f: Inserting an inactive plan never violates the constraint

    /// <summary>
    /// Property 5f: For any business (regardless of existing active plans), inserting an inactive
    /// plan (IsActive = 0) SHALL never violate the constraint.
    /// **Validates: Requirements 3.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InsertingInactivePlan_NeverViolatesConstraint(PositiveInt businessId, PositiveInt existingPlanId)
    {
        var existingPlans = new List<BusinessPlanRecord>
        {
            new(businessId.Get, existingPlanId.Get, IsActive: true)
        };

        var wouldViolate = WouldViolateConstraint(existingPlans, businessId.Get, newIsActive: false);

        return (!wouldViolate).ToProperty()
            .Label($"BusinessId={businessId.Get}: Inserting inactive plan should never violate constraint");
    }

    #endregion

    #region Property 5g: Different businesses can each have one active plan

    /// <summary>
    /// Property 5g: For any set of distinct business IDs, each business having exactly one active
    /// plan satisfies the constraint for all businesses. The constraint is per-business, not global.
    /// **Validates: Requirements 3.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DifferentBusinesses_EachCanHaveOneActivePlan(PositiveInt businessCount)
    {
        var count = (businessCount.Get % 20) + 2; // 2 to 21 businesses
        var plans = Enumerable.Range(1, count)
            .Select(bizId => new BusinessPlanRecord(bizId, PlanId: 1, IsActive: true))
            .ToList();

        // Verify constraint holds for each business individually
        var allSatisfied = Enumerable.Range(1, count)
            .All(bizId => HasAtMostOneActivePlanPerBusiness(plans, bizId));

        return allSatisfied.ToProperty()
            .Label($"Each of {count} businesses with one active plan should satisfy constraint");
    }

    #endregion
}
