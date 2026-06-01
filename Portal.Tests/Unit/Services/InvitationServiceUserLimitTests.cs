using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for InvitationService user limit enforcement logic.
/// Validates Requirements 5.1–5.5.
/// </summary>
public class InvitationServiceUserLimitTests : IDisposable
{
    private const int TestBusinessId = 1;
    private const string TestUserId = "superadmin-001";
    private const string TestEmail = "newuser@example.com";

    private readonly MembershipDbContext _membershipDbContext;
    private readonly Mock<IBusinessPlanRepository> _businessPlanRepoMock;
    private readonly Mock<IPlanRepository> _planRepoMock;
    private readonly InvitationService _service;

    public InvitationServiceUserLimitTests()
    {
        var options = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _membershipDbContext = new MembershipDbContext(options);
        _businessPlanRepoMock = new Mock<IBusinessPlanRepository>();
        _planRepoMock = new Mock<IPlanRepository>();

        _service = new InvitationService(
            _membershipDbContext,
            _businessPlanRepoMock.Object,
            _planRepoMock.Object);
    }

    public void Dispose()
    {
        _membershipDbContext.Database.EnsureDeleted();
        _membershipDbContext.Dispose();
    }

    #region Helpers

    private void SetupActivePlan(int maxUsers, int planId = 10)
    {
        var businessPlan = new BusinessPlan
        {
            Id = 1,
            BusinessId = TestBusinessId,
            PlanId = planId,
            StartDateUtc = DateTime.UtcNow.AddMonths(-1),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var plan = new Plan
        {
            Id = planId,
            Name = "Business",
            Slug = "business",
            MonthlyPriceEur = 29.00m,
            AnnualPriceEur = 348.00m,
            MaxUsers = maxUsers,
            IsActive = true,
            DisplayOrder = 2,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _businessPlanRepoMock
            .Setup(r => r.GetActiveByBusinessIdAsync(TestBusinessId))
            .ReturnsAsync(businessPlan);

        _planRepoMock
            .Setup(r => r.GetByIdAsync(planId))
            .ReturnsAsync(plan);
    }

    private void SeedActiveUsers(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _membershipDbContext.UserBusinesses.Add(new UserBusiness
            {
                UserId = $"user-{i:D3}",
                BusinessId = TestBusinessId,
                IsDefault = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        _membershipDbContext.SaveChanges();
    }

    private void SeedPendingInvitations(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _membershipDbContext.Invitations.Add(new Invitation
            {
                Email = $"pending{i}@example.com",
                BusinessId = TestBusinessId,
                Token = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(72),
                IsUsed = false,
                CreatedByUserId = TestUserId
            });
        }
        _membershipDbContext.SaveChanges();
    }

    private void SeedExpiredInvitations(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _membershipDbContext.Invitations.Add(new Invitation
            {
                Email = $"expired{i}@example.com",
                BusinessId = TestBusinessId,
                Token = Guid.NewGuid().ToString("N"),
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(-2), // Already expired
                IsUsed = false,
                CreatedByUserId = TestUserId
            });
        }
        _membershipDbContext.SaveChanges();
    }

    private void SeedInactiveUsers(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _membershipDbContext.UserBusinesses.Add(new UserBusiness
            {
                UserId = $"inactive-user-{i:D3}",
                BusinessId = TestBusinessId,
                IsDefault = false,
                IsActive = false,
                DeactivatedAtUtc = DateTime.UtcNow.AddDays(-10),
                CreatedAtUtc = DateTime.UtcNow.AddMonths(-3)
            });
        }
        _membershipDbContext.SaveChanges();
    }

    #endregion

    #region Requirement 5.5 — No active plan

    [Fact]
    public async Task CreateInvitationAsync_NoActivePlan_ThrowsInvalidOperationException()
    {
        // Arrange
        _businessPlanRepoMock
            .Setup(r => r.GetActiveByBusinessIdAsync(TestBusinessId))
            .ReturnsAsync((BusinessPlan?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateInvitationAsync(TestEmail, TestBusinessId, TestUserId));

        Assert.Contains("no active subscription plan", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Requirement 5.3 — Unlimited users (MaxUsers == -1)

    [Fact]
    public async Task CreateInvitationAsync_UnlimitedMaxUsers_PermitsInvitation()
    {
        // Arrange
        SetupActivePlan(maxUsers: -1);
        SeedActiveUsers(100); // Even with many users, should still permit

        // Act
        var result = await _service.CreateInvitationAsync(TestEmail, TestBusinessId, TestUserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestEmail, result.Email);
        Assert.Equal(TestBusinessId, result.BusinessId);
        Assert.False(result.IsUsed);
    }

    #endregion

    #region Requirement 5.2 — Occupied seats below limit

    [Fact]
    public async Task CreateInvitationAsync_OccupiedSeatsBelowMaxUsers_PermitsInvitation()
    {
        // Arrange: activeUsers=2, pendingInvitations=1, MaxUsers=5 → 3 < 5 → permitted
        SetupActivePlan(maxUsers: 5);
        SeedActiveUsers(2);
        SeedPendingInvitations(1);

        // Act
        var result = await _service.CreateInvitationAsync(TestEmail, TestBusinessId, TestUserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestEmail, result.Email);
        Assert.Equal(TestBusinessId, result.BusinessId);
    }

    #endregion

    #region Requirement 5.2 — Occupied seats equal to limit

    [Fact]
    public async Task CreateInvitationAsync_OccupiedSeatsEqualToMaxUsers_ThrowsInvalidOperationException()
    {
        // Arrange: activeUsers=3, pendingInvitations=2, MaxUsers=5 → 5 >= 5 → rejected
        SetupActivePlan(maxUsers: 5);
        SeedActiveUsers(3);
        SeedPendingInvitations(2);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateInvitationAsync(TestEmail, TestBusinessId, TestUserId));

        Assert.Contains("user limit", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("5", ex.Message);
    }

    #endregion

    #region Requirement 5.2 — Occupied seats exceed limit (concurrent insert edge case)

    [Fact]
    public async Task CreateInvitationAsync_OccupiedSeatsExceedMaxUsers_ThrowsInvalidOperationException()
    {
        // Arrange: activeUsers=4, pendingInvitations=2, MaxUsers=5 → 6 >= 5 → rejected
        SetupActivePlan(maxUsers: 5);
        SeedActiveUsers(4);
        SeedPendingInvitations(2);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateInvitationAsync(TestEmail, TestBusinessId, TestUserId));

        Assert.Contains("user limit", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("5", ex.Message);
    }

    #endregion

    #region Requirement 5.4 — Expired invitations NOT counted

    [Fact]
    public async Task CreateInvitationAsync_ExpiredInvitationsNotCounted_PermitsInvitation()
    {
        // Arrange: activeUsers=2, pendingInvitations=1, expiredInvitations=3, MaxUsers=5
        // Occupied seats = 2 + 1 = 3 (expired NOT counted) → 3 < 5 → permitted
        SetupActivePlan(maxUsers: 5);
        SeedActiveUsers(2);
        SeedPendingInvitations(1);
        SeedExpiredInvitations(3);

        // Act
        var result = await _service.CreateInvitationAsync(TestEmail, TestBusinessId, TestUserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestEmail, result.Email);
    }

    #endregion

    #region Requirement 5.4 — Inactive UserBusiness records NOT counted

    [Fact]
    public async Task CreateInvitationAsync_InactiveUsersNotCounted_PermitsInvitation()
    {
        // Arrange: activeUsers=2, inactiveUsers=5, pendingInvitations=1, MaxUsers=5
        // Occupied seats = 2 + 1 = 3 (inactive NOT counted) → 3 < 5 → permitted
        SetupActivePlan(maxUsers: 5);
        SeedActiveUsers(2);
        SeedInactiveUsers(5);
        SeedPendingInvitations(1);

        // Act
        var result = await _service.CreateInvitationAsync(TestEmail, TestBusinessId, TestUserId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestEmail, result.Email);
    }

    #endregion
}
