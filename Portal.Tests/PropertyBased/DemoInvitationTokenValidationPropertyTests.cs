using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: demo-access-invitations, Property 3: Validation valid vs expired

/// <summary>
/// Property-based tests for DemoInvitationService.ValidateAndTrackAccessAsync() token validation logic.
/// Validates that tokens with status 'sent' or 'accessed' and future expiry return IsValid=true,
/// while tokens with past expiry return IsValid=false with ErrorReason="expired" and status updated.
/// **Validates: Requirements 7.2, 7.4**
/// </summary>
public class DemoInvitationTokenValidationPropertyTests
{
    private static readonly string[] ValidStatuses = { "sent", "accessed" };

    /// <summary>
    /// Creates a DemoInvitationService with a mocked repository configured to return
    /// the specified invitation when GetByTokenAsync is called.
    /// Returns the service and repository mock for verification.
    /// </summary>
    private static (DemoInvitationService Service, Mock<DemoInvitationRepository> RepoMock) CreateServiceWithMocks(
        DemoInvitation? invitationToReturn)
    {
        var portalDbOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var portalDbContext = new PortalDbContext(portalDbOptions, Mock.Of<ICurrentTenantService>());

        var mockRepository = new Mock<DemoInvitationRepository>(portalDbContext) { CallBase = false };

        var mockEmailService = new Mock<IEmailService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockLogger = new Mock<ILogger<DemoInvitationService>>();

        var dbContextOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var membershipDbContext = new MembershipDbContext(dbContextOptions);

        // Setup GetByTokenAsync to return the provided invitation
        mockRepository
            .Setup(r => r.GetByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(invitationToReturn);

        // Setup UpdateStatusAsync to complete successfully
        mockRepository
            .Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        // Setup UpdateAccessTrackingAsync to complete successfully
        mockRepository
            .Setup(r => r.UpdateAccessTrackingAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        var service = new DemoInvitationService(
            mockRepository.Object,
            mockEmailService.Object,
            mockHttpContextAccessor.Object,
            mockLogger.Object,
            membershipDbContext);

        return (service, mockRepository);
    }

    /// <summary>
    /// Property 3a: Valid tokens (status 'sent' or 'accessed', ExpiresAtUtc in the future)
    /// return IsValid=true from ValidateAndTrackAccessAsync.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidToken_WithFutureExpiry_ReturnsIsValidTrue(
        PositiveInt idSeed,
        PositiveInt businessIdSeed,
        PositiveInt minutesAheadSeed,
        bool useAccessedStatus,
        PositiveInt accessCountSeed)
    {
        // Generate invitation data
        var id = (idSeed.Get % 10000) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var status = useAccessedStatus ? "accessed" : "sent";
        var minutesAhead = (minutesAheadSeed.Get % 43200) + 1; // 1 minute to 30 days ahead
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(minutesAhead);
        var accessCount = accessCountSeed.Get % 100;
        var token = $"test-token-{id}";

        var invitation = new DemoInvitation
        {
            Id = id,
            BusinessId = businessId,
            Token = token,
            RecipientEmail = $"prospect{id}@example.com",
            RecipientName = $"Prospect {id}",
            ExpiresAtUtc = expiresAtUtc,
            Status = status,
            CreatedByUserId = "admin-user-1",
            AccessCount = accessCount,
            FirstAccessedAtUtc = useAccessedStatus ? DateTime.UtcNow.AddHours(-1) : null,
            LastAccessedAtUtc = useAccessedStatus ? DateTime.UtcNow.AddMinutes(-30) : null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        var (service, _) = CreateServiceWithMocks(invitation);

        // Act
        var result = service.ValidateAndTrackAccessAsync(token).GetAwaiter().GetResult();

        return result.IsValid.ToProperty()
            .Label($"Token with status='{status}', expiresIn={minutesAhead}min should be valid but got IsValid={result.IsValid}");
    }

    /// <summary>
    /// Property 3b: Expired tokens (ExpiresAtUtc in the past) return IsValid=false
    /// with ErrorReason="expired".
    /// **Validates: Requirements 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpiredToken_ReturnsIsValidFalse_WithExpiredReason(
        PositiveInt idSeed,
        PositiveInt businessIdSeed,
        PositiveInt minutesPastSeed,
        bool useAccessedStatus)
    {
        // Generate invitation data with past expiry
        var id = (idSeed.Get % 10000) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var status = useAccessedStatus ? "accessed" : "sent";
        var minutesPast = (minutesPastSeed.Get % 43200) + 1; // 1 minute to 30 days in the past
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(-minutesPast);
        var token = $"expired-token-{id}";

        var invitation = new DemoInvitation
        {
            Id = id,
            BusinessId = businessId,
            Token = token,
            RecipientEmail = $"expired{id}@example.com",
            RecipientName = $"Expired User {id}",
            ExpiresAtUtc = expiresAtUtc,
            Status = status,
            CreatedByUserId = "admin-user-1",
            AccessCount = 0,
            FirstAccessedAtUtc = null,
            LastAccessedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-7)
        };

        var (service, _) = CreateServiceWithMocks(invitation);

        // Act
        var result = service.ValidateAndTrackAccessAsync(token).GetAwaiter().GetResult();

        var isNotValid = !result.IsValid;
        var hasExpiredReason = result.ErrorReason == "expired";

        return (isNotValid && hasExpiredReason).ToProperty()
            .Label($"Token with expiresAtUtc={minutesPast}min ago, status='{status}' should be invalid with reason='expired' " +
                   $"but got IsValid={result.IsValid}, ErrorReason='{result.ErrorReason}'");
    }

    /// <summary>
    /// Property 3c: When an expired token is validated, UpdateStatusAsync is called
    /// with status 'expired' to persist the state transition.
    /// **Validates: Requirements 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpiredToken_UpdatesStatusToExpired(
        PositiveInt idSeed,
        PositiveInt businessIdSeed,
        PositiveInt minutesPastSeed,
        bool useAccessedStatus)
    {
        // Generate invitation data with past expiry
        var id = (idSeed.Get % 10000) + 1;
        var businessId = (businessIdSeed.Get % 1000) + 1;
        var status = useAccessedStatus ? "accessed" : "sent";
        var minutesPast = (minutesPastSeed.Get % 43200) + 1;
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(-minutesPast);
        var token = $"expire-update-{id}";

        var invitation = new DemoInvitation
        {
            Id = id,
            BusinessId = businessId,
            Token = token,
            RecipientEmail = $"update{id}@example.com",
            RecipientName = null,
            ExpiresAtUtc = expiresAtUtc,
            Status = status,
            CreatedByUserId = "admin-user-1",
            AccessCount = 0,
            FirstAccessedAtUtc = null,
            LastAccessedAtUtc = null,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3)
        };

        var (service, repoMock) = CreateServiceWithMocks(invitation);

        // Act
        service.ValidateAndTrackAccessAsync(token).GetAwaiter().GetResult();

        // Assert: UpdateStatusAsync was called with "expired"
        var statusUpdateCalled = false;
        try
        {
            repoMock.Verify(r => r.UpdateStatusAsync(id, "expired", It.IsAny<DateTime?>()), Times.Once());
            statusUpdateCalled = true;
        }
        catch
        {
            statusUpdateCalled = false;
        }

        return statusUpdateCalled.ToProperty()
            .Label($"UpdateStatusAsync should be called with status='expired' for invitation id={id} " +
                   $"(original status='{status}', expired {minutesPast}min ago)");
    }
}
