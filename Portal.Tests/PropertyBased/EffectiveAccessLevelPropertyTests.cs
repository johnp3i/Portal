using FsCheck;
using FsCheck.Xunit;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-permission-gating, Property 7: Effective access level computation

/// <summary>
/// Property-based tests for effective access level resolution.
/// For any (planAccessLevel, userAccessLevel) pair drawn from {full, readonly, none},
/// the effective access level returned by PlanCheckService SHALL equal the more restrictive
/// of the two, where the ordering is none &lt; readonly &lt; full.
/// **Validates: Requirements 5.2, 5.4**
/// </summary>
public class EffectiveAccessLevelPropertyTests
{
    private static readonly string[] AllLevels = AccessLevels.All;

    #region Property 7: Effective access level is the more restrictive of plan and user levels

    /// <summary>
    /// Property 7: For any combination of plan-level and user-level access drawn from {full, readonly, none},
    /// ResolveEffectiveAccessLevel SHALL return the more restrictive of the two.
    /// **Validates: Requirements 5.2, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EffectiveLevel_IsMoreRestrictiveOfPlanAndUser(PositiveInt planSeed, PositiveInt userSeed)
    {
        var planLevel = AllLevels[planSeed.Get % AllLevels.Length];
        var userLevel = AllLevels[userSeed.Get % AllLevels.Length];

        var result = PlanCheckService.ResolveEffectiveAccessLevel(planLevel, userLevel);
        var expected = ComputeExpected(planLevel, userLevel);

        return (result == expected).ToProperty()
            .Label($"Plan={planLevel}, User={userLevel} → Expected={expected}, Got={result}");
    }

    /// <summary>
    /// Property 7a: The effective level is never less restrictive than the plan level.
    /// **Validates: Requirements 5.2, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EffectiveLevel_NeverLessRestrictiveThanPlanLevel(PositiveInt planSeed, PositiveInt userSeed)
    {
        var planLevel = AllLevels[planSeed.Get % AllLevels.Length];
        var userLevel = AllLevels[userSeed.Get % AllLevels.Length];

        var result = PlanCheckService.ResolveEffectiveAccessLevel(planLevel, userLevel);

        var resultRank = GetRank(result);
        var planRank = GetRank(planLevel);

        return (resultRank <= planRank).ToProperty()
            .Label($"Plan={planLevel}(rank={planRank}), User={userLevel}, Result={result}(rank={resultRank}) — result should not exceed plan rank");
    }

    /// <summary>
    /// Property 7b: The effective level is never less restrictive than the user level.
    /// **Validates: Requirements 5.2, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EffectiveLevel_NeverLessRestrictiveThanUserLevel(PositiveInt planSeed, PositiveInt userSeed)
    {
        var planLevel = AllLevels[planSeed.Get % AllLevels.Length];
        var userLevel = AllLevels[userSeed.Get % AllLevels.Length];

        var result = PlanCheckService.ResolveEffectiveAccessLevel(planLevel, userLevel);

        var resultRank = GetRank(result);
        var userRank = GetRank(userLevel);

        return (resultRank <= userRank).ToProperty()
            .Label($"Plan={planLevel}, User={userLevel}(rank={userRank}), Result={result}(rank={resultRank}) — result should not exceed user rank");
    }

    #endregion

    #region Exhaustive verification of all 9 combinations

    /// <summary>
    /// Exhaustive test covering all 9 combinations of (plan, user) access levels.
    /// Ensures deterministic correctness across the full input space.
    /// **Validates: Requirements 5.2, 5.4**
    /// </summary>
    [Fact]
    public void AllCombinations_CorrectlyResolved()
    {
        // full × full → full
        Assert.Equal(AccessLevels.Full, PlanCheckService.ResolveEffectiveAccessLevel("full", "full"));

        // full × readonly → readonly
        Assert.Equal(AccessLevels.ReadOnly, PlanCheckService.ResolveEffectiveAccessLevel("full", "readonly"));

        // full × none → none
        Assert.Equal(AccessLevels.None, PlanCheckService.ResolveEffectiveAccessLevel("full", "none"));

        // readonly × full → readonly
        Assert.Equal(AccessLevels.ReadOnly, PlanCheckService.ResolveEffectiveAccessLevel("readonly", "full"));

        // readonly × readonly → readonly
        Assert.Equal(AccessLevels.ReadOnly, PlanCheckService.ResolveEffectiveAccessLevel("readonly", "readonly"));

        // readonly × none → none
        Assert.Equal(AccessLevels.None, PlanCheckService.ResolveEffectiveAccessLevel("readonly", "none"));

        // none × full → none
        Assert.Equal(AccessLevels.None, PlanCheckService.ResolveEffectiveAccessLevel("none", "full"));

        // none × readonly → none
        Assert.Equal(AccessLevels.None, PlanCheckService.ResolveEffectiveAccessLevel("none", "readonly"));

        // none × none → none
        Assert.Equal(AccessLevels.None, PlanCheckService.ResolveEffectiveAccessLevel("none", "none"));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Reference implementation of the "more restrictive wins" rule.
    /// </summary>
    private static string ComputeExpected(string planLevel, string userLevel)
    {
        if (planLevel == AccessLevels.None || userLevel == AccessLevels.None)
            return AccessLevels.None;

        if (planLevel == AccessLevels.ReadOnly || userLevel == AccessLevels.ReadOnly)
            return AccessLevels.ReadOnly;

        return AccessLevels.Full;
    }

    /// <summary>
    /// Returns a rank value for an access level: none=0, readonly=1, full=2.
    /// Higher rank means more permissive.
    /// </summary>
    private static int GetRank(string level)
    {
        return level switch
        {
            "none" => 0,
            "readonly" => 1,
            "full" => 2,
            _ => -1
        };
    }

    #endregion
}
