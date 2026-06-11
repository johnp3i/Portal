using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: demo-access-invitations, Property 10: Revocation state transition

/// <summary>
/// Property-based tests for DemoInvitationService.RevokeAsync state transition.
/// Validates that revoking an invitation with status 'sent' or 'accessed' sets status
/// to 'revoked' and RevokedAtUtc within tolerance of UtcNow.
/// **Validates: Requirements 11.3**
/// </summary>
public class DemoRevocationStateTransitionPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a DemoInvitationService with mocked dependencies configured for revocation testing.
    /// Returns the service and the mocked repository for verification.
    /// </summary>
    private static (DemoInvitationService Service, Mock<DemoInvitationRepository> RepoMock) CreateServiceWithMocks()
    {
        var mockRepo = new Mock<DemoInvitationRepository>(MockBehavior.Loose, new object[] { null! });
        var mockEmailService = new Mock<IEmailService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockLogger = new Mock<ILogger<DemoInvitationService>>();

        var membershipOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: $"MembershipTest_{Guid.NewGuid()}")
            .Options;
        var membershipDbContext = new MembershipDbContext(membershipOptions);

        // Setup UpdateStatusAsync to complete successfully
        mockRepo
            .Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        var service = new DemoInvitationService(
            mockRepo.Object,
            mockEmailService.Object,
            mockHttpContextAccessor.Object,
            mockLogger.Object,
            membershipDbContext);

        return (service, mockRepo);
    }

    #endregion

    #region Property 10: Revocation State Transition

    /// <summary>
    /// Property 10a: RevokeAsync calls repository with status 'revoked'.
    /// For any random invitation ID, calling RevokeAsync results in the repository being
    /// called with status = "revoked".
    /// **Validates: Requirements 11.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RevokeAsync_CallsRepository_WithStatusRevoked(PositiveInt invitationIdSeed)
    {
        var invitationId = (invitationIdSeed.Get % 10000) + 1;

        var (service, repoMock) = CreateServiceWithMocks();

        // Act
        service.RevokeAsync(invitationId).GetAwaiter().GetResult();

        // Assert: UpdateStatusAsync was called with status = "revoked"
        var calledWithRevokedStatus = false;
        try
        {
            repoMock.Verify(
                r => r.UpdateStatusAsync(invitationId, "revoked", It.IsAny<DateTime?>()),
                Times.Once());
            calledWithRevokedStatus = true;
        }
        catch
        {
            calledWithRevokedStatus = false;
        }

        return calledWithRevokedStatus.ToProperty()
            .Label($"invitationId={invitationId}, calledWithRevokedStatus={calledWithRevokedStatus}");
    }

    /// <summary>
    /// Property 10b: RevokeAsync sets RevokedAtUtc within a reasonable tolerance of DateTime.UtcNow.
    /// For any random invitation ID, calling RevokeAsync results in the repository being
    /// called with a RevokedAtUtc value within 5 seconds of the current UTC time.
    /// **Validates: Requirements 11.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RevokeAsync_SetsRevokedAtUtc_WithinToleranceOfUtcNow(PositiveInt invitationIdSeed)
    {
        var invitationId = (invitationIdSeed.Get % 10000) + 1;
        var tolerance = TimeSpan.FromSeconds(5);

        var (service, repoMock) = CreateServiceWithMocks();

        var beforeCall = DateTime.UtcNow;

        // Act
        service.RevokeAsync(invitationId).GetAwaiter().GetResult();

        var afterCall = DateTime.UtcNow;

        // Assert: UpdateStatusAsync was called with RevokedAtUtc within tolerance
        repoMock.Verify(
            r => r.UpdateStatusAsync(
                invitationId,
                "revoked",
                It.Is<DateTime?>(dt =>
                    dt != null &&
                    dt.Value >= beforeCall.Add(-tolerance) &&
                    dt.Value <= afterCall.Add(tolerance))),
            Times.Once());

        return true.ToProperty()
            .Label($"invitationId={invitationId}, beforeCall={beforeCall}, afterCall={afterCall}");
    }

    /// <summary>
    /// Property 10c: RevokeAsync calls repository exactly once per invocation.
    /// For any random invitation ID, RevokeAsync invokes UpdateStatusAsync exactly once
    /// with the correct invitation ID.
    /// **Validates: Requirements 11.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RevokeAsync_CallsUpdateStatusAsync_ExactlyOnce(PositiveInt invitationIdSeed)
    {
        var invitationId = (invitationIdSeed.Get % 10000) + 1;

        var (service, repoMock) = CreateServiceWithMocks();

        // Act
        service.RevokeAsync(invitationId).GetAwaiter().GetResult();

        // Assert: UpdateStatusAsync was called exactly once with the correct ID
        var calledExactlyOnce = false;
        try
        {
            repoMock.Verify(
                r => r.UpdateStatusAsync(invitationId, It.IsAny<string>(), It.IsAny<DateTime?>()),
                Times.Once());
            calledExactlyOnce = true;
        }
        catch
        {
            calledExactlyOnce = false;
        }

        // Verify no other ID was passed
        var noOtherIdCalled = false;
        try
        {
            repoMock.Verify(
                r => r.UpdateStatusAsync(It.Is<int>(id => id != invitationId), It.IsAny<string>(), It.IsAny<DateTime?>()),
                Times.Never());
            noOtherIdCalled = true;
        }
        catch
        {
            noOtherIdCalled = false;
        }

        return (calledExactlyOnce && noOtherIdCalled).ToProperty()
            .Label($"invitationId={invitationId}, calledExactlyOnce={calledExactlyOnce}, noOtherIdCalled={noOtherIdCalled}");
    }

    #endregion
}
