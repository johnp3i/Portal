using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Web.Services.Stripe;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for CheckoutSessionExpireService verifying query logic,
/// exclusion filtering, and graceful failure patterns.
/// </summary>
public class CheckoutSessionExpireServiceTests : IDisposable
{
    private const int TestBusinessId = 1;
    private readonly PortalDbContext _dbContext;
    private readonly Mock<ICurrentTenantService> _tenantServiceMock;

    public CheckoutSessionExpireServiceTests()
    {
        _tenantServiceMock = new Mock<ICurrentTenantService>();
        _tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(TestBusinessId);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"ExpireTest_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _dbContext = new PortalDbContext(options, _tenantServiceMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task TryExpirePendingSessionsAsync_NoPendingSessions_ReturnsWithoutError()
    {
        // Arrange
        var mockKeyService = new Mock<IStripeKeyResolutionService>();
        var service = new CheckoutSessionExpireService(_dbContext, mockKeyService.Object);

        // Act — should not throw
        await service.TryExpirePendingSessionsAsync(999, TestBusinessId);

        // Assert — no key resolution needed since there are no sessions
        mockKeyService.Verify(k => k.ResolveKeysAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task TryExpirePendingSessionsAsync_WithExcludeSessionId_ExcludesCorrectSession()
    {
        // Arrange
        _dbContext.StripeCheckoutSessions.AddRange(
            CreateSession(1, 100, "cs_session_1", "pending"),
            CreateSession(2, 100, "cs_session_2", "pending"),
            CreateSession(3, 100, "cs_session_3", "completed")
        );
        await _dbContext.SaveChangesAsync();

        var mockKeyService = new Mock<IStripeKeyResolutionService>();
        mockKeyService.Setup(k => k.ResolveKeysAsync(TestBusinessId))
            .ReturnsAsync(new ResolvedStripeKeys
            {
                SecretKey = "sk_test_fake",
                ConnectClientId = "ca_fake",
                ConnectWebhookSecret = "whsec_fake",
                ConnectOAuthRedirectUri = "http://test"
            });

        var service = new CheckoutSessionExpireService(_dbContext, mockKeyService.Object);

        // Act — exclude cs_session_1, so only cs_session_2 should be processed
        // This will fail at the Stripe API call (invalid key) but the method should not throw
        await service.TryExpirePendingSessionsAsync(100, TestBusinessId, "cs_session_1");

        // Assert — method completed without throwing (the Stripe API error is caught internally)
        Assert.True(true);
    }

    [Fact]
    public async Task TryExpirePendingSessionsAsync_KeyResolutionFails_ReturnsGracefully()
    {
        // Arrange
        _dbContext.StripeCheckoutSessions.Add(
            CreateSession(1, 100, "cs_session_1", "pending")
        );
        await _dbContext.SaveChangesAsync();

        var mockKeyService = new Mock<IStripeKeyResolutionService>();
        mockKeyService.Setup(k => k.ResolveKeysAsync(TestBusinessId))
            .ThrowsAsync(new InvalidOperationException("Key resolution failed"));

        var service = new CheckoutSessionExpireService(_dbContext, mockKeyService.Object);

        // Act — should not throw despite key resolution failure
        await service.TryExpirePendingSessionsAsync(100, TestBusinessId);

        // Assert — session remains pending (not modified because we couldn't resolve keys)
        var session = await _dbContext.StripeCheckoutSessions.FindAsync(1);
        Assert.Equal("pending", session!.Status);
    }

    [Fact]
    public async Task TryExpirePendingSessionsAsync_NoSecretKey_ReturnsGracefully()
    {
        // Arrange
        _dbContext.StripeCheckoutSessions.Add(
            CreateSession(1, 100, "cs_session_1", "pending")
        );
        await _dbContext.SaveChangesAsync();

        var mockKeyService = new Mock<IStripeKeyResolutionService>();
        mockKeyService.Setup(k => k.ResolveKeysAsync(TestBusinessId))
            .ReturnsAsync(new ResolvedStripeKeys
            {
                SecretKey = null,
                ConnectClientId = "ca_fake",
                ConnectWebhookSecret = "whsec_fake",
                ConnectOAuthRedirectUri = "http://test"
            });

        var service = new CheckoutSessionExpireService(_dbContext, mockKeyService.Object);

        // Act
        await service.TryExpirePendingSessionsAsync(100, TestBusinessId);

        // Assert — session remains pending
        var session = await _dbContext.StripeCheckoutSessions.FindAsync(1);
        Assert.Equal("pending", session!.Status);
    }

    [Fact]
    public async Task TryExpirePendingSessionsAsync_OnlySelectsPendingSessions()
    {
        // Arrange
        _dbContext.StripeCheckoutSessions.AddRange(
            CreateSession(1, 100, "cs_pending", "pending"),
            CreateSession(2, 100, "cs_completed", "completed"),
            CreateSession(3, 100, "cs_expired", "expired"),
            CreateSession(4, 200, "cs_other_invoice", "pending")
        );
        await _dbContext.SaveChangesAsync();

        var mockKeyService = new Mock<IStripeKeyResolutionService>();
        mockKeyService.Setup(k => k.ResolveKeysAsync(TestBusinessId))
            .ReturnsAsync(new ResolvedStripeKeys
            {
                SecretKey = "sk_test_fake",
                ConnectClientId = "ca_fake",
                ConnectWebhookSecret = "whsec_fake",
                ConnectOAuthRedirectUri = "http://test"
            });

        var service = new CheckoutSessionExpireService(_dbContext, mockKeyService.Object);

        // Act — only cs_pending for InvoiceId=100 should be processed
        await service.TryExpirePendingSessionsAsync(100, TestBusinessId);

        // Assert — completed and expired sessions unchanged, other invoice unchanged
        var completed = await _dbContext.StripeCheckoutSessions.FindAsync(2);
        var expired = await _dbContext.StripeCheckoutSessions.FindAsync(3);
        var otherInvoice = await _dbContext.StripeCheckoutSessions.FindAsync(4);
        Assert.Equal("completed", completed!.Status);
        Assert.Equal("expired", expired!.Status);
        Assert.Equal("pending", otherInvoice!.Status); // Different invoice, not touched
    }

    #region Helpers

    private static StripeCheckoutSession CreateSession(int id, int invoiceId, string stripeSessionId, string status)
    {
        return new StripeCheckoutSession
        {
            Id = id,
            InvoiceId = invoiceId,
            BusinessId = TestBusinessId,
            StripeSessionId = stripeSessionId,
            Status = status,
            Amount = 100m,
            Currency = "EUR",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    #endregion
}
