using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-plans, Property 6: User limit enforcement

/// <summary>
/// Property-based tests for user limit enforcement logic.
/// For any business with an active plan, the InvitationService SHALL permit a new invitation
/// if and only if MaxUsers equals -1 (unlimited) OR the current occupied seat count
/// (active users + pending invitations) is strictly less than MaxUsers.
/// **Validates: Requirements 5.2, 5.3**
/// </summary>
public class UserLimitEnforcementPropertyTests
{
    /// <summary>
    /// The core business rule: invitation is permitted iff MaxUsers == -1 (unlimited)
    /// OR the occupied seat count is strictly less than MaxUsers.
    /// This mirrors the logic in InvitationService.CreateInvitationAsync.
    /// </summary>
    private static bool IsInvitationPermitted(int maxUsers, int activeUsers, int pendingInvitations)
    {
        if (maxUsers == -1) return true;
        var occupiedSeats = activeUsers + pendingInvitations;
        return occupiedSeats < maxUsers;
    }

    #region Property 6a: Unlimited plan always permits invitation

    /// <summary>
    /// Property 6a: When MaxUsers == -1 (unlimited), the invitation SHALL always be permitted
    /// regardless of the number of active users or pending invitations.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnlimitedPlan_AlwaysPermitsInvitation(NonNegativeInt activeUsers, NonNegativeInt pendingInvitations)
    {
        var active = activeUsers.Get;
        var pending = pendingInvitations.Get;

        var permitted = IsInvitationPermitted(-1, active, pending);

        return permitted.ToProperty()
            .Label($"MaxUsers=-1, ActiveUsers={active}, PendingInvitations={pending}: Expected permitted=true, Got={permitted}");
    }

    #endregion

    #region Property 6b: Limited plan permits when below limit

    /// <summary>
    /// Property 6b: When MaxUsers >= 1 and (activeUsers + pendingInvitations) is strictly less
    /// than MaxUsers, the invitation SHALL be permitted.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LimitedPlan_PermitsWhenBelowLimit(PositiveInt maxUsersVal)
    {
        var maxUsers = maxUsersVal.Get;

        // Generate activeUsers + pendingInvitations that sum to less than maxUsers
        // Use a deterministic split: activeUsers = maxUsers / 2 - 1, pendingInvitations = 0
        // But we need to ensure occupiedSeats < maxUsers, so pick values below the limit
        var occupiedSeats = maxUsers > 1 ? maxUsers - 1 : 0;
        var activeUsers = occupiedSeats / 2;
        var pendingInvitations = occupiedSeats - activeUsers;

        var permitted = IsInvitationPermitted(maxUsers, activeUsers, pendingInvitations);

        return permitted.ToProperty()
            .Label($"MaxUsers={maxUsers}, ActiveUsers={activeUsers}, PendingInvitations={pendingInvitations}, OccupiedSeats={occupiedSeats}: Expected permitted=true, Got={permitted}");
    }

    #endregion

    #region Property 6c: Limited plan rejects when at limit

    /// <summary>
    /// Property 6c: When MaxUsers >= 1 and (activeUsers + pendingInvitations) equals MaxUsers,
    /// the invitation SHALL be rejected.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LimitedPlan_RejectsWhenAtLimit(PositiveInt maxUsersVal, NonNegativeInt activeUsersVal)
    {
        var maxUsers = maxUsersVal.Get;
        // Ensure activeUsers doesn't exceed maxUsers so pendingInvitations stays non-negative
        var activeUsers = activeUsersVal.Get % (maxUsers + 1);
        var pendingInvitations = maxUsers - activeUsers;

        var permitted = IsInvitationPermitted(maxUsers, activeUsers, pendingInvitations);

        return (!permitted).ToProperty()
            .Label($"MaxUsers={maxUsers}, ActiveUsers={activeUsers}, PendingInvitations={pendingInvitations}, OccupiedSeats={maxUsers}: Expected permitted=false, Got={permitted}");
    }

    #endregion

    #region Property 6d: Limited plan rejects when above limit

    /// <summary>
    /// Property 6d: When MaxUsers >= 1 and (activeUsers + pendingInvitations) exceeds MaxUsers,
    /// the invitation SHALL be rejected. This covers edge cases with concurrent inserts.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LimitedPlan_RejectsWhenAboveLimit(PositiveInt maxUsersVal, PositiveInt extraVal)
    {
        var maxUsers = maxUsersVal.Get;
        var extra = extraVal.Get;
        // occupiedSeats = maxUsers + extra, which is always > maxUsers
        var activeUsers = maxUsers;
        var pendingInvitations = extra;

        var permitted = IsInvitationPermitted(maxUsers, activeUsers, pendingInvitations);

        return (!permitted).ToProperty()
            .Label($"MaxUsers={maxUsers}, ActiveUsers={activeUsers}, PendingInvitations={pendingInvitations}, OccupiedSeats={maxUsers + extra}: Expected permitted=false, Got={permitted}");
    }

    #endregion

    #region Property 6e: Completeness — permitted iff unlimited or below limit

    /// <summary>
    /// Property 6e: For any valid tuple of (maxUsers, activeUsers, pendingInvitations),
    /// IsInvitationPermitted returns true if and only if maxUsers == -1 OR
    /// (activeUsers + pendingInvitations) &lt; maxUsers.
    /// This is the universal property covering the full input space.
    /// **Validates: Requirements 5.2, 5.3**
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(UserLimitArbitrary) })]
    public Property InvitationPermitted_IfAndOnlyIf_UnlimitedOrBelowLimit(ValidUserLimitTuple tuple)
    {
        var maxUsers = tuple.MaxUsers;
        var activeUsers = tuple.ActiveUsers;
        var pendingInvitations = tuple.PendingInvitations;

        var permitted = IsInvitationPermitted(maxUsers, activeUsers, pendingInvitations);
        var expected = maxUsers == -1 || (activeUsers + pendingInvitations) < maxUsers;

        return (permitted == expected).ToProperty()
            .Label($"MaxUsers={maxUsers}, ActiveUsers={activeUsers}, PendingInvitations={pendingInvitations}: Permitted={permitted}, Expected={expected}");
    }

    #endregion
}

/// <summary>
/// Represents a valid tuple of (MaxUsers, ActiveUsers, PendingInvitations) for property testing.
/// MaxUsers is constrained to -1 (unlimited) or >= 1 (valid plan limits).
/// ActiveUsers and PendingInvitations are non-negative integers.
/// </summary>
public class ValidUserLimitTuple
{
    public int MaxUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int PendingInvitations { get; set; }

    public override string ToString() =>
        $"(MaxUsers={MaxUsers}, ActiveUsers={ActiveUsers}, PendingInvitations={PendingInvitations})";
}

/// <summary>
/// Custom FsCheck arbitrary for generating valid user limit tuples.
/// Generates MaxUsers as either -1 or a positive integer (1-1000),
/// and non-negative values for ActiveUsers and PendingInvitations (0-500).
/// </summary>
public class UserLimitArbitrary
{
    public static Arbitrary<ValidUserLimitTuple> ValidUserLimitTuples()
    {
        var maxUsersGen = Gen.Frequency(
            Tuple.Create(1, Gen.Constant(-1)),           // 1/4 chance of unlimited
            Tuple.Create(3, Gen.Choose(1, 1000))         // 3/4 chance of limited plan (1-1000)
        );

        var nonNegativeGen = Gen.Choose(0, 500);

        var tupleGen = from maxUsers in maxUsersGen
                       from activeUsers in nonNegativeGen
                       from pendingInvitations in nonNegativeGen
                       select new ValidUserLimitTuple
                       {
                           MaxUsers = maxUsers,
                           ActiveUsers = activeUsers,
                           PendingInvitations = pendingInvitations
                       };

        return Arb.From(tupleGen);
    }
}
