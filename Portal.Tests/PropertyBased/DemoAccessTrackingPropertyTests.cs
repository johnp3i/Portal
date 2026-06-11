using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: demo-access-invitations, Property 4: Access tracking invariants

/// <summary>
/// Property-based tests for DemoInvitationService.ValidateAndTrackAccessAsync access tracking.
/// Validates that valid access increments AccessCount by 1, sets LastAccessedAtUtc,
/// and sets FirstAccessedAtUtc on first access with status → 'accessed'.
/// **Validates: Requirements 9.1, 9.2**
/// </summary>
public class DemoAccessTrackingPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a DemoInvitationService with mocked dependencies configured for access tracking testing.
    /// Returns the service and the mocked repository for verification.
    /// </summary>
    private static (DemoInvitationService Service, Mock<DemoInvitationRepository> RepoMock) CreateServiceWithMocks(
        DemoInvitation invitationToReturn)
    {
        var mockRepo = new Mock<DemoInvitationRepository>(MockBehavior.Loose, new object[] { null! });
        var mockEmailService = new Mock<IEmailService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockLogger = new Mock<ILogger<DemoInvitationService>>();

        var membershipOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: $"MembershipTest_{Guid.NewGuid()}")
            .Options;
        var membershipDbContext = new MembershipDbContext(membershipOptions);

        // Setup GetByTokenAsync to return the provided invitation
        mockRepo
            .Setup(r => r.GetByTokenAsync(invitationToReturn.Token))
            .ReturnsAsync(invitationToReturn);

        // Setup UpdateAccessTrackingAsync to complete successfully
        mockRepo
            .Setup(r => r.UpdateAccessTrackingAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        // Setup UpdateStatusAsync to complete successfully (in case of expired path)
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

    #region Property 4: Access Tracking Invariants

    /// <summary>
    /// Property 4a: Valid access increments AccessCount by exactly 1.
    /// For any valid invitation (status 'sent' or 'accessed', future expiry) with any initial AccessCount,
    /// after ValidateAndTrackAccessAsync the returned invitation has AccessCount = initial + 1.
    /// **Validates: Requirements 9.1, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidAccess_IncrementsAccessCount_ByOne(
        PositiveInt accessCountSeed,
        PositiveInt invitationIdSeed,
        NonEmptyString tokenSeed)
    {
        var initialAccessCount = accessCountSeed.Get % 1000; // 0 to 999
        var invitationId = (invitationIdSeed.Get % 10000) + 1;
        var token = tokenSeed.Get.Replace("\0", "a"); // Ensure valid string

        var invitation = new DemoInvitation
        {
            Id = invitationId,
            BusinessId = 1000,
            Token = token,
            RecipientEmail = "prospect@example.com",
            RecipientName = "Test User",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Status = "sent",
            CreatedByUserId = "admin-001",
            FirstAccessedAtUtc = initialAccessCount > 0 ? DateTime.UtcNow.AddDays(-1) : null,
            LastAccessedAtUtc = initialAccessCount > 0 ? DateTime.UtcNow.AddHours(-1) : null,
            AccessCount = initialAccessCount,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
        };

        var (service, _) = CreateServiceWithMocks(invitation);

        // Act
        var result = service.ValidateAndTrackAccessAsync(token).GetAwaiter().GetResult();

        // Assert
        var isValid = result.IsValid;
        var accessCountIncremented = result.Invitation!.AccessCount == initialAccessCount + 1;

        return (isValid && accessCountIncremented).ToProperty()
            .Label($"token={token}, initialAccessCount={initialAccessCount}, " +
                   $"resultAccessCount={result.Invitation?.AccessCount}, isValid={isValid}");
    }

    /// <summary>
    /// Property 4b: Valid access sets LastAccessedAtUtc to a recent timestamp.
    /// For any valid invitation, after ValidateAndTrackAccessAsync the returned invitation
    /// has LastAccessedAtUtc set (not null) and within a reasonable tolerance of UtcNow.
    /// **Validates: Requirements 9.1, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidAccess_SetsLastAccessedAtUtc(
        PositiveInt accessCountSeed,
        PositiveInt invitationIdSeed,
        NonEmptyString tokenSeed)
    {
        var initialAccessCount = accessCountSeed.Get % 1000;
        var invitationId = (invitationIdSeed.Get % 10000) + 1;
        var token = tokenSeed.Get.Replace("\0", "a");

        var invitation = new DemoInvitation
        {
            Id = invitationId,
            BusinessId = 1000,
            Token = token,
            RecipientEmail = "prospect@example.com",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Status = "accessed",
            CreatedByUserId = "admin-001",
            FirstAccessedAtUtc = DateTime.UtcNow.AddDays(-2),
            LastAccessedAtUtc = DateTime.UtcNow.AddHours(-3),
            AccessCount = initialAccessCount,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
        };

        var beforeCall = DateTime.UtcNow;
        var (service, _) = CreateServiceWithMocks(invitation);

        // Act
        var result = service.ValidateAndTrackAccessAsync(token).GetAwaiter().GetResult();
        var afterCall = DateTime.UtcNow;

        // Assert
        var lastAccessedSet = result.Invitation!.LastAccessedAtUtc != null;
        var withinTolerance = result.Invitation!.LastAccessedAtUtc >= beforeCall.AddSeconds(-1)
                           && result.Invitation!.LastAccessedAtUtc <= afterCall.AddSeconds(1);

        return (result.IsValid && lastAccessedSet && withinTolerance).ToProperty()
            .Label($"LastAccessedAtUtc={result.Invitation?.LastAccessedAtUtc}, " +
                   $"beforeCall={beforeCall}, afterCall={afterCall}, " +
                   $"lastAccessedSet={lastAccessedSet}, withinTolerance={withinTolerance}");
    }

    /// <summary>
    /// Property 4c: First access sets FirstAccessedAtUtc and status becomes 'accessed'.
    /// For any valid invitation where FirstAccessedAtUtc was null (first access),
    /// after ValidateAndTrackAccessAsync the FirstAccessedAtUtc is set and Status = 'accessed'.
    /// **Validates: Requirements 9.1, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FirstAccess_SetsFirstAccessedAtUtc_AndStatusBecomesAccessed(
        PositiveInt invitationIdSeed,
        NonEmptyString tokenSeed)
    {
        var invitationId = (invitationIdSeed.Get % 10000) + 1;
        var token = tokenSeed.Get.Replace("\0", "a");

        // First access scenario: FirstAccessedAtUtc is null, status is 'sent'
        var invitation = new DemoInvitation
        {
            Id = invitationId,
            BusinessId = 1000,
            Token = token,
            RecipientEmail = "prospect@example.com",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Status = "sent",
            CreatedByUserId = "admin-001",
            FirstAccessedAtUtc = null,
            LastAccessedAtUtc = null,
            AccessCount = 0,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        };

        var beforeCall = DateTime.UtcNow;
        var (service, repoMock) = CreateServiceWithMocks(invitation);

        // Act
        var result = service.ValidateAndTrackAccessAsync(token).GetAwaiter().GetResult();
        var afterCall = DateTime.UtcNow;

        // Assert
        var firstAccessSet = result.Invitation!.FirstAccessedAtUtc != null;
        var firstAccessWithinTolerance = result.Invitation!.FirstAccessedAtUtc >= beforeCall.AddSeconds(-1)
                                      && result.Invitation!.FirstAccessedAtUtc <= afterCall.AddSeconds(1);
        var statusIsAccessed = result.Invitation!.Status == "accessed";

        return (result.IsValid && firstAccessSet && firstAccessWithinTolerance && statusIsAccessed).ToProperty()
            .Label($"FirstAccessedAtUtc={result.Invitation?.FirstAccessedAtUtc}, " +
                   $"Status={result.Invitation?.Status}, " +
                   $"firstAccessSet={firstAccessSet}, statusIsAccessed={statusIsAccessed}");
    }

    /// <summary>
    /// Property 4d: Repository UpdateAccessTrackingAsync called with isFirstAccess=true on first access.
    /// For any valid invitation with FirstAccessedAtUtc null, the repository is called with isFirstAccess=true.
    /// **Validates: Requirements 9.1, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FirstAccess_CallsRepository_WithIsFirstAccessTrue(
        PositiveInt invitationIdSeed,
        NonEmptyString tokenSeed)
    {
        var invitationId = (invitationIdSeed.Get % 10000) + 1;
        var token = tokenSeed.Get.Replace("\0", "a");

        var invitation = new DemoInvitation
        {
            Id = invitationId,
            BusinessId = 1000,
            Token = token,
            RecipientEmail = "prospect@example.com",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Status = "sent",
            CreatedByUserId = "admin-001",
            FirstAccessedAtUtc = null,
            LastAccessedAtUtc = null,
            AccessCount = 0,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        };

        var (service, repoMock) = CreateServiceWithMocks(invitation);

        // Act
        service.ValidateAndTrackAccessAsync(token).GetAwaiter().GetResult();

        // Assert: UpdateAccessTrackingAsync was called with isFirstAccess=true
        var updateCalledWithFirstAccess = false;
        try
        {
            repoMock.Verify(
                r => r.UpdateAccessTrackingAsync(invitationId, It.IsAny<DateTime>(), true),
                Times.Once());
            updateCalledWithFirstAccess = true;
        }
        catch
        {
            updateCalledWithFirstAccess = false;
        }

        return updateCalledWithFirstAccess.ToProperty()
            .Label($"invitationId={invitationId}, token={token}, " +
                   $"updateCalledWithFirstAccess={updateCalledWithFirstAccess}");
    }

    /// <summary>
    /// Property 4e: Subsequent access (non-first) does NOT reset FirstAccessedAtUtc and calls with isFirstAccess=false.
    /// For any valid invitation where FirstAccessedAtUtc was already set,
    /// repository is called with isFirstAccess=false and the original FirstAccessedAtUtc is preserved.
    /// **Validates: Requirements 9.1, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SubsequentAccess_CallsRepository_WithIsFirstAccessFalse(
        PositiveInt invitationIdSeed,
        PositiveInt accessCountSeed,
        NonEmptyString tokenSeed)
    {
        var invitationId = (invitationIdSeed.Get % 10000) + 1;
        var initialAccessCount = (accessCountSeed.Get % 100) + 1; // At least 1 previous access
        var token = tokenSeed.Get.Replace("\0", "a");
        var originalFirstAccess = DateTime.UtcNow.AddDays(-2);

        var invitation = new DemoInvitation
        {
            Id = invitationId,
            BusinessId = 1000,
            Token = token,
            RecipientEmail = "prospect@example.com",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Status = "accessed",
            CreatedByUserId = "admin-001",
            FirstAccessedAtUtc = originalFirstAccess,
            LastAccessedAtUtc = DateTime.UtcNow.AddHours(-1),
            AccessCount = initialAccessCount,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
        };

        var (service, repoMock) = CreateServiceWithMocks(invitation);

        // Act
        var result = service.ValidateAndTrackAccessAsync(token).GetAwaiter().GetResult();

        // Assert: UpdateAccessTrackingAsync was called with isFirstAccess=false
        var updateCalledWithNotFirstAccess = false;
        try
        {
            repoMock.Verify(
                r => r.UpdateAccessTrackingAsync(invitationId, It.IsAny<DateTime>(), false),
                Times.Once());
            updateCalledWithNotFirstAccess = true;
        }
        catch
        {
            updateCalledWithNotFirstAccess = false;
        }

        // Assert: FirstAccessedAtUtc is preserved (not changed)
        var firstAccessPreserved = result.Invitation!.FirstAccessedAtUtc == originalFirstAccess;

        return (updateCalledWithNotFirstAccess && firstAccessPreserved).ToProperty()
            .Label($"invitationId={invitationId}, initialAccessCount={initialAccessCount}, " +
                   $"updateCalledWithNotFirstAccess={updateCalledWithNotFirstAccess}, " +
                   $"firstAccessPreserved={firstAccessPreserved}");
    }

    #endregion
}
