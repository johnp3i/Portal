using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for DemoInvitationService covering token generation, collision retry,
/// validation for missing/revoked/expired tokens, revoke already-revoked no-op,
/// and email failure still persists invitation.
/// Validates Requirements 4.1, 4.4, 6.4, 7.3, 7.4, 7.5, 11.3.
/// </summary>
public class DemoInvitationServiceTests
{
    private readonly Mock<DemoInvitationRepository> _repositoryMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<ILogger<DemoInvitationService>> _loggerMock;
    private readonly MembershipDbContext _membershipDbContext;
    private readonly DemoInvitationService _service;

    public DemoInvitationServiceTests()
    {
        var tenantServiceMock = new Mock<ICurrentTenantService>();
        tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(1);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var portalDbContext = new PortalDbContext(options, tenantServiceMock.Object);

        _repositoryMock = new Mock<DemoInvitationRepository>(MockBehavior.Loose, portalDbContext);
        _emailServiceMock = new Mock<IEmailService>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _loggerMock = new Mock<ILogger<DemoInvitationService>>();

        var membershipOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _membershipDbContext = new MembershipDbContext(membershipOptions);

        _service = new DemoInvitationService(
            _repositoryMock.Object,
            _emailServiceMock.Object,
            _httpContextAccessorMock.Object,
            _loggerMock.Object,
            _membershipDbContext);
    }

    #region GenerateToken — Non-null/Non-empty (Req 4.1)

    [Fact]
    public void GenerateToken_ReturnsNonNullString()
    {
        // Act
        var token = _service.GenerateToken();

        // Assert
        Assert.NotNull(token);
    }

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        // Act
        var token = _service.GenerateToken();

        // Assert
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GenerateToken_ReturnsUrlSafeBase64WithoutPadding()
    {
        // Act
        var token = _service.GenerateToken();

        // Assert — should only contain [A-Za-z0-9_-] with no '=' padding
        Assert.DoesNotContain("=", token);
        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
    }

    #endregion

    #region Token Collision Retry (Req 4.4)

    [Fact]
    public async Task CreateAsync_TokenCollisionRetry_SucceedsAfterRetries()
    {
        // Arrange — first 2 token lookups find existing records, third returns null (no collision)
        var callCount = 0;
        _repositoryMock
            .Setup(r => r.GetByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= 2)
                {
                    return new DemoInvitation
                    {
                        Id = callCount,
                        Token = "existing-token",
                        Status = "sent",
                        RecipientEmail = "existing@test.com",
                        CreatedByUserId = "user-1",
                        ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                        CreatedAtUtc = DateTime.UtcNow
                    };
                }
                return null; // No collision on 3rd attempt
            });

        _repositoryMock
            .Setup(r => r.GetDemoBusinessesAsync())
            .ReturnsAsync(new List<Business>
            {
                new Business { Id = 1000, Name = "Demo Business", IsDemoAccount = true }
            });

        _repositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<DemoInvitation>(), It.IsAny<List<DemoInvitationPermission>>()))
            .Returns(Task.CompletedTask);

        _emailServiceMock
            .Setup(e => e.SendDemoInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var request = new CreateDemoInvitationRequest
        {
            BusinessId = 1000,
            RecipientEmail = "prospect@test.com",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Permissions = new List<ModulePermissionEntry>
            {
                new ModulePermissionEntry { Module = "quotation", AccessLevel = AccessLevels.Full }
            }
        };

        // Act
        var result = await _service.CreateAsync(request, "superadmin-001");

        // Assert — should succeed after retries
        Assert.NotNull(result);
        Assert.Equal("sent", result.Status);
        Assert.Equal(3, callCount); // 2 collisions + 1 success
    }

    [Fact]
    public async Task CreateAsync_TokenCollisionExhausted_ThrowsInvalidOperationException()
    {
        // Arrange — all 3 token attempts collide
        _repositoryMock
            .Setup(r => r.GetByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new DemoInvitation
            {
                Id = 1,
                Token = "existing-token",
                Status = "sent",
                RecipientEmail = "existing@test.com",
                CreatedByUserId = "user-1",
                ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
                CreatedAtUtc = DateTime.UtcNow
            });

        _repositoryMock
            .Setup(r => r.GetDemoBusinessesAsync())
            .ReturnsAsync(new List<Business>
            {
                new Business { Id = 1000, Name = "Demo Business", IsDemoAccount = true }
            });

        var request = new CreateDemoInvitationRequest
        {
            BusinessId = 1000,
            RecipientEmail = "prospect@test.com",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Permissions = new List<ModulePermissionEntry>
            {
                new ModulePermissionEntry { Module = "quotation", AccessLevel = AccessLevels.Full }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(request, "superadmin-001"));

        Assert.Contains("3 attempts", exception.Message);
        _repositoryMock.Verify(r => r.GetByTokenAsync(It.IsAny<string>()), Times.Exactly(3));
    }

    #endregion

    #region ValidateAndTrackAccessAsync — Missing Token (Req 7.3)

    [Fact]
    public async Task ValidateAndTrackAccessAsync_NonExistentToken_ReturnsInvalid()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByTokenAsync("non-existent-token"))
            .ReturnsAsync((DemoInvitation?)null);

        // Act
        var result = await _service.ValidateAndTrackAccessAsync("non-existent-token");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("invalid", result.ErrorReason);
        Assert.Null(result.Invitation);
    }

    #endregion

    #region ValidateAndTrackAccessAsync — Revoked Token (Req 7.5)

    [Fact]
    public async Task ValidateAndTrackAccessAsync_RevokedToken_ReturnsRevokedError()
    {
        // Arrange
        var revokedInvitation = new DemoInvitation
        {
            Id = 10,
            Token = "revoked-token",
            Status = "revoked",
            RecipientEmail = "prospect@test.com",
            CreatedByUserId = "superadmin-001",
            BusinessId = 1000,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            RevokedAtUtc = DateTime.UtcNow.AddHours(-1),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
        };

        _repositoryMock
            .Setup(r => r.GetByTokenAsync("revoked-token"))
            .ReturnsAsync(revokedInvitation);

        // Act
        var result = await _service.ValidateAndTrackAccessAsync("revoked-token");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("revoked", result.ErrorReason);
        Assert.NotNull(result.Invitation);
        Assert.Equal(10, result.Invitation!.Id);
    }

    #endregion

    #region ValidateAndTrackAccessAsync — Expired Token (Req 7.4)

    [Fact]
    public async Task ValidateAndTrackAccessAsync_ExpiredToken_ReturnsExpiredError()
    {
        // Arrange
        var expiredInvitation = new DemoInvitation
        {
            Id = 20,
            Token = "expired-token",
            Status = "sent",
            RecipientEmail = "prospect@test.com",
            CreatedByUserId = "superadmin-001",
            BusinessId = 1000,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(-1), // Expired 1 hour ago
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10)
        };

        _repositoryMock
            .Setup(r => r.GetByTokenAsync("expired-token"))
            .ReturnsAsync(expiredInvitation);

        _repositoryMock
            .Setup(r => r.UpdateStatusAsync(20, "expired", null))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ValidateAndTrackAccessAsync("expired-token");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("expired", result.ErrorReason);
        Assert.NotNull(result.Invitation);
        _repositoryMock.Verify(r => r.UpdateStatusAsync(20, "expired", null), Times.Once);
    }

    #endregion

    #region RevokeAsync — Already Revoked No-Op (Req 11.3)

    [Fact]
    public async Task RevokeAsync_AlreadyRevokedInvitation_StillCallsUpdateStatus()
    {
        // Arrange — the repository's UpdateStatusAsync is called regardless of current state
        // (the service doesn't check existing status before revoking — it's idempotent)
        _repositoryMock
            .Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), "revoked", It.IsAny<DateTime?>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.RevokeAsync(42);

        // Assert — UpdateStatusAsync is still called (no-op from user perspective)
        _repositoryMock.Verify(
            r => r.UpdateStatusAsync(42, "revoked", It.IsAny<DateTime?>()),
            Times.Once);
    }

    #endregion

    #region CreateAsync — Email Failure Still Persists Invitation (Req 6.4)

    [Fact]
    public async Task CreateAsync_EmailSendFails_InvitationStillPersisted()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync((DemoInvitation?)null); // No collision

        _repositoryMock
            .Setup(r => r.GetDemoBusinessesAsync())
            .ReturnsAsync(new List<Business>
            {
                new Business { Id = 1000, Name = "Demo Business", IsDemoAccount = true }
            });

        _repositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<DemoInvitation>(), It.IsAny<List<DemoInvitationPermission>>()))
            .Returns(Task.CompletedTask);

        // Email service throws exception
        _emailServiceMock
            .Setup(e => e.SendDemoInvitationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        var request = new CreateDemoInvitationRequest
        {
            BusinessId = 1000,
            RecipientEmail = "prospect@test.com",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            Permissions = new List<ModulePermissionEntry>
            {
                new ModulePermissionEntry { Module = "invoice", AccessLevel = AccessLevels.ReadOnly }
            }
        };

        // Act — should NOT throw despite email failure
        var result = await _service.CreateAsync(request, "superadmin-001");

        // Assert — invitation was persisted (InsertAsync was called)
        Assert.NotNull(result);
        Assert.Equal("sent", result.Status);
        _repositoryMock.Verify(
            r => r.InsertAsync(It.IsAny<DemoInvitation>(), It.IsAny<List<DemoInvitationPermission>>()),
            Times.Once);
    }

    #endregion
}
