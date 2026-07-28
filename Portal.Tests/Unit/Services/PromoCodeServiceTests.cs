using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Models.PromoCode;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for PromoCodeService covering code creation, validation,
/// collision retry, email-bound override, and revocation.
/// Validates Requirements 2.1, 2.4, 2.6, 2.7, 5.5.
/// </summary>
public class PromoCodeServiceTests
{
    private const string TestUserId = "superadmin-001";

    private readonly Mock<PromoCodeRepository> _promoCodeRepoMock;
    private readonly Mock<ILogger<PromoCodeService>> _loggerMock;
    private readonly PromoCodeService _service;

    public PromoCodeServiceTests()
    {
        var tenantServiceMock = new Mock<ICurrentTenantService>();
        tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(1);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var portalDbContext = new PortalDbContext(options, tenantServiceMock.Object);

        _promoCodeRepoMock = new Mock<PromoCodeRepository>(MockBehavior.Loose, portalDbContext);
        _loggerMock = new Mock<ILogger<PromoCodeService>>();

        _service = new PromoCodeService(
            _promoCodeRepoMock.Object,
            Mock.Of<IPlanRepository>(),
            _loggerMock.Object);
    }

    #region Helpers

    private static CreatePromoCodeRequest CreateValidRequest(
        int durationMonths = 3,
        int maxRedemptions = 10,
        DateTime? expiresAtUtc = null,
        string? boundEmail = null)
    {
        return new CreatePromoCodeRequest
        {
            DurationMonths = durationMonths,
            MaxRedemptions = maxRedemptions,
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddDays(30),
            BoundEmail = boundEmail
        };
    }

    #endregion

    #region CreateAsync — Happy Path (Req 2.1)

    [Fact]
    public async Task CreateAsync_ValidInput_ReturnsSuccessWithCode()
    {
        // Arrange
        var request = CreateValidRequest();
        _promoCodeRepoMock.Setup(r => r.CodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _promoCodeRepoMock.Setup(r => r.InsertAsync(It.IsAny<PromoCode>())).ReturnsAsync(1);

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Code);
        Assert.Equal(8, result.Code!.Length);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CreateAsync_ValidInput_CodeContainsOnlyAllowedCharacters()
    {
        // Arrange
        var request = CreateValidRequest();
        _promoCodeRepoMock.Setup(r => r.CodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _promoCodeRepoMock.Setup(r => r.InsertAsync(It.IsAny<PromoCode>())).ReturnsAsync(1);

        const string allowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.True(result.Success);
        foreach (char c in result.Code!)
        {
            Assert.Contains(c, allowedChars);
        }
    }

    [Fact]
    public async Task CreateAsync_ValidInput_CodeDoesNotContainAmbiguousCharacters()
    {
        // Arrange
        var request = CreateValidRequest();
        _promoCodeRepoMock.Setup(r => r.CodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _promoCodeRepoMock.Setup(r => r.InsertAsync(It.IsAny<PromoCode>())).ReturnsAsync(1);

        const string ambiguousChars = "O0Il1";

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.True(result.Success);
        foreach (char c in result.Code!)
        {
            Assert.DoesNotContain(c.ToString(), ambiguousChars);
        }
    }

    #endregion

    #region CreateAsync — Email-Bound Override (Req 2.2)

    [Fact]
    public async Task CreateAsync_EmailBound_ForcesMaxRedemptionsToOne()
    {
        // Arrange
        var request = CreateValidRequest(maxRedemptions: 100, boundEmail: "user@example.com");
        _promoCodeRepoMock.Setup(r => r.CodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _promoCodeRepoMock.Setup(r => r.InsertAsync(It.IsAny<PromoCode>())).ReturnsAsync(1);

        PromoCode? capturedEntity = null;
        _promoCodeRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<PromoCode>()))
            .Callback<PromoCode>(entity => capturedEntity = entity)
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedEntity);
        Assert.Equal(1, capturedEntity!.MaxRedemptions);
        Assert.Equal("user@example.com", capturedEntity.BoundEmail);
    }

    #endregion

    #region CreateAsync — Duration Validation (Req 2.6)

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(25)]
    [InlineData(100)]
    public async Task CreateAsync_InvalidDuration_ReturnsFailure(int durationMonths)
    {
        // Arrange
        var request = CreateValidRequest(durationMonths: durationMonths);

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Duration", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    [InlineData(24)]
    public async Task CreateAsync_ValidDuration_ReturnsSuccess(int durationMonths)
    {
        // Arrange
        var request = CreateValidRequest(durationMonths: durationMonths);
        _promoCodeRepoMock.Setup(r => r.CodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _promoCodeRepoMock.Setup(r => r.InsertAsync(It.IsAny<PromoCode>())).ReturnsAsync(1);

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.True(result.Success);
    }

    #endregion

    #region CreateAsync — MaxRedemptions Validation (Req 2.7)

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    [InlineData(1000)]
    public async Task CreateAsync_InvalidMaxRedemptions_GenericCode_ReturnsFailure(int maxRedemptions)
    {
        // Arrange
        var request = CreateValidRequest(maxRedemptions: maxRedemptions);

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("redemptions", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    [InlineData(500)]
    public async Task CreateAsync_ValidMaxRedemptions_GenericCode_ReturnsSuccess(int maxRedemptions)
    {
        // Arrange
        var request = CreateValidRequest(maxRedemptions: maxRedemptions);
        _promoCodeRepoMock.Setup(r => r.CodeExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _promoCodeRepoMock.Setup(r => r.InsertAsync(It.IsAny<PromoCode>())).ReturnsAsync(1);

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.True(result.Success);
    }

    #endregion

    #region CreateAsync — Expiry Date Validation (Req 2.5)

    [Fact]
    public async Task CreateAsync_ExpiryInPast_ReturnsFailure()
    {
        // Arrange
        var request = CreateValidRequest(expiresAtUtc: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Expiry", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_ExpiryLessThanOneDayInFuture_ReturnsFailure()
    {
        // Arrange — set expiry to 12 hours from now (less than 1 day)
        var request = CreateValidRequest(expiresAtUtc: DateTime.UtcNow.AddHours(12));

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Expiry", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region CreateAsync — Collision Retry (Req 2.4)

    [Fact]
    public async Task CreateAsync_CollisionRetrySuccess_ReturnsCodeAfterRetries()
    {
        // Arrange — first 2 codes collide, third succeeds
        var request = CreateValidRequest();
        var callCount = 0;
        _promoCodeRepoMock
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount <= 2; // First 2 collide, third succeeds
            });
        _promoCodeRepoMock.Setup(r => r.InsertAsync(It.IsAny<PromoCode>())).ReturnsAsync(1);

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Code);
        Assert.Equal(3, callCount); // 2 collisions + 1 success
    }

    [Fact]
    public async Task CreateAsync_CollisionExhausted_ReturnsFailure()
    {
        // Arrange — all 5 attempts collide
        var request = CreateValidRequest();
        _promoCodeRepoMock.Setup(r => r.CodeExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var result = await _service.CreateAsync(request, TestUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Code generation failed", result.ErrorMessage!);
        _promoCodeRepoMock.Verify(r => r.CodeExistsAsync(It.IsAny<string>()), Times.Exactly(5));
    }

    #endregion

    #region RevokeAsync (Req 3.5, 3.6)

    [Fact]
    public async Task RevokeAsync_RepositoryReturnsTrue_ReturnsOk()
    {
        // Arrange
        _promoCodeRepoMock.Setup(r => r.RevokeAsync(1)).ReturnsAsync(true);

        // Act
        var result = await _service.RevokeAsync(1, TestUserId);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task RevokeAsync_RepositoryReturnsFalse_ReturnsFail()
    {
        // Arrange
        _promoCodeRepoMock.Setup(r => r.RevokeAsync(99)).ReturnsAsync(false);

        // Act
        var result = await _service.RevokeAsync(99, TestUserId);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }

    #endregion
}
