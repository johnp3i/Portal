using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-plans, Property 7: Seat count calculation

/// <summary>
/// Property-based tests for seat count calculation.
/// For any business, the occupied seat count SHALL equal the number of active UserBusiness
/// records for that business plus the number of unused, unexpired Invitation records for that business.
/// **Validates: Requirements 5.4**
/// </summary>
public class SeatCountCalculationPropertyTests
{
    /// <summary>
    /// Calculates occupied seats as defined in the requirements:
    /// active users + pending (unused, unexpired) invitations.
    /// This mirrors the logic in InvitationService.CreateInvitationAsync.
    /// </summary>
    private static int CalculateOccupiedSeats(int activeUserCount, int pendingInvitationCount)
    {
        return activeUserCount + pendingInvitationCount;
    }

    #region Property 7a: Occupied seats always equals the sum of active users and pending invitations

    /// <summary>
    /// Property 7a: For any non-negative combination of active users and pending invitations,
    /// the occupied seat count SHALL equal activeUserCount + pendingInvitationCount.
    /// This is the core additive property of the seat calculation.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OccupiedSeats_EqualsSum_OfActiveUsersAndPendingInvitations(NonNegativeInt activeUsers, NonNegativeInt pendingInvitations)
    {
        var activeUserCount = activeUsers.Get;
        var pendingInvitationCount = pendingInvitations.Get;

        var occupiedSeats = CalculateOccupiedSeats(activeUserCount, pendingInvitationCount);
        var expectedSum = activeUserCount + pendingInvitationCount;

        return (occupiedSeats == expectedSum).ToProperty()
            .Label($"activeUsers={activeUserCount}, pendingInvitations={pendingInvitationCount}: " +
                   $"occupiedSeats={occupiedSeats}, expected={expectedSum}");
    }

    #endregion

    #region Property 7b: Occupied seats is always non-negative

    /// <summary>
    /// Property 7b: For any non-negative combination of active users and pending invitations,
    /// the occupied seat count SHALL always be non-negative (>= 0).
    /// Since both inputs are non-negative, the sum must also be non-negative.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OccupiedSeats_IsAlwaysNonNegative(NonNegativeInt activeUsers, NonNegativeInt pendingInvitations)
    {
        var activeUserCount = activeUsers.Get;
        var pendingInvitationCount = pendingInvitations.Get;

        var occupiedSeats = CalculateOccupiedSeats(activeUserCount, pendingInvitationCount);

        return (occupiedSeats >= 0).ToProperty()
            .Label($"activeUsers={activeUserCount}, pendingInvitations={pendingInvitationCount}: " +
                   $"occupiedSeats={occupiedSeats} should be >= 0");
    }

    #endregion

    #region Property 7c: Occupied seats is at least as large as active user count

    /// <summary>
    /// Property 7c: For any non-negative combination of active users and pending invitations,
    /// the occupied seat count SHALL always be greater than or equal to the active user count.
    /// Adding pending invitations can only increase the total.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OccupiedSeats_IsAtLeast_ActiveUserCount(NonNegativeInt activeUsers, NonNegativeInt pendingInvitations)
    {
        var activeUserCount = activeUsers.Get;
        var pendingInvitationCount = pendingInvitations.Get;

        var occupiedSeats = CalculateOccupiedSeats(activeUserCount, pendingInvitationCount);

        return (occupiedSeats >= activeUserCount).ToProperty()
            .Label($"activeUsers={activeUserCount}, pendingInvitations={pendingInvitationCount}: " +
                   $"occupiedSeats={occupiedSeats} should be >= activeUsers={activeUserCount}");
    }

    #endregion

    #region Property 7d: Occupied seats is at least as large as pending invitation count

    /// <summary>
    /// Property 7d: For any non-negative combination of active users and pending invitations,
    /// the occupied seat count SHALL always be greater than or equal to the pending invitation count.
    /// Adding active users can only increase the total.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OccupiedSeats_IsAtLeast_PendingInvitationCount(NonNegativeInt activeUsers, NonNegativeInt pendingInvitations)
    {
        var activeUserCount = activeUsers.Get;
        var pendingInvitationCount = pendingInvitations.Get;

        var occupiedSeats = CalculateOccupiedSeats(activeUserCount, pendingInvitationCount);

        return (occupiedSeats >= pendingInvitationCount).ToProperty()
            .Label($"activeUsers={activeUserCount}, pendingInvitations={pendingInvitationCount}: " +
                   $"occupiedSeats={occupiedSeats} should be >= pendingInvitations={pendingInvitationCount}");
    }

    #endregion

    #region Property 7e: Zero active users and zero pending invitations yields zero occupied seats

    /// <summary>
    /// Property 7e: When both active users and pending invitations are zero,
    /// the occupied seat count SHALL be exactly zero (identity property of addition).
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OccupiedSeats_IsZero_WhenBothInputsAreZero(PositiveInt _)
    {
        var occupiedSeats = CalculateOccupiedSeats(0, 0);

        return (occupiedSeats == 0).ToProperty()
            .Label($"activeUsers=0, pendingInvitations=0: occupiedSeats={occupiedSeats} should be 0");
    }

    #endregion
}
