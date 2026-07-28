using Microsoft.EntityFrameworkCore;
using Moq;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for PromoCodeValidationService covering the 5-step validation flow.
/// Validates Requirements 5.5, 5.7, 9.4, 9.5.
/// </summary>
public class PromoCodeValidationServiceTests
{
    private readonly Mock<PromoCodeRepository> _promoCodeRepoMock;
    private readonly PromoCodeValidationService _service;

    public PromoCodeValidationServiceTests()
    {
        var tenantServiceMock = new Mock<ICurrentTenantService>();
        tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(1);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var portalDbContext = new PortalDbContext(options, tenantServiceMock.Object);

        _promoCodeRepoMock = new Mock<PromoCodeRepository>(MockBehavior.Loose, portalDbContext);
        _service = new PromoCodeValidationService(_promoCodeRepoMock.Object, Mock.Of<IPlanRepository>());
    }

    #region Helpers

    private static PromoCode CreateValidPromoCode(
        int id = 1,
        string code = "ABCD1234",
        int durationMonths = 3,
        int maxRedemptions = 10,
        int currentRedemptions = 0,
        DateTime? expiresAtUtc = null,
        string? boundEmail = null,
        bool isRevoked = false)
    {
        return new PromoCode
        {
            Id = id,
            Code = code,
            DurationMonths = durationMonths,
            MaxRedemptions = maxRedemptions,
            CurrentRedemptions = currentRedemptions,
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddDays(30),
            BoundEmail = boundEmail,
            IsRevoked = isRevoked,
            CreatedByUserId = "admin-001",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-7)
        };
    }

    #endregion

    #region Valid Code — Happy Path (Req 5.5)

    [Fact]
    public async Task ValidateForRegistrationAsync_ValidCode_ReturnsIsValidWithDetails()
    {
        // Arrange
        var promoCode = CreateValidPromoCode(id: 42, durationMonths: 6);
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync("ABCD1234")).ReturnsAsync(promoCode);

        // Act
        var result = await _service.ValidateForRegistrationAsync("ABCD1234", "user@example.com");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(42, result.PromoCodeId);
        Assert.Equal(6, result.DurationMonths);
        Assert.Null(result.ErrorMessage);
    }

    #endregion

    #region Non-existent Code (Req 5.5)

    [Fact]
    public async Task ValidateForRegistrationAsync_NonExistentCode_ReturnsInvalidPromoCode()
    {
        // Arrange
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync(It.IsAny<string>())).ReturnsAsync((PromoCode?)null);

        // Act
        var result = await _service.ValidateForRegistrationAsync("NOEXIST1", "user@example.com");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Invalid promo code", result.ErrorMessage);
        Assert.Null(result.PromoCodeId);
        Assert.Null(result.DurationMonths);
    }

    #endregion

    #region Revoked Code (Req 5.5)

    [Fact]
    public async Task ValidateForRegistrationAsync_RevokedCode_ReturnsRevokedMessage()
    {
        // Arrange
        var promoCode = CreateValidPromoCode(isRevoked: true);
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync("ABCD1234")).ReturnsAsync(promoCode);

        // Act
        var result = await _service.ValidateForRegistrationAsync("ABCD1234", "user@example.com");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("This code has been revoked", result.ErrorMessage);
    }

    #endregion

    #region Expired Code (Req 5.5)

    [Fact]
    public async Task ValidateForRegistrationAsync_ExpiredCode_ReturnsExpiredMessage()
    {
        // Arrange
        var promoCode = CreateValidPromoCode(expiresAtUtc: DateTime.UtcNow.AddDays(-1));
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync("ABCD1234")).ReturnsAsync(promoCode);

        // Act
        var result = await _service.ValidateForRegistrationAsync("ABCD1234", "user@example.com");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("This code has expired", result.ErrorMessage);
    }

    #endregion

    #region Fully Redeemed Code (Req 5.5)

    [Fact]
    public async Task ValidateForRegistrationAsync_FullyRedeemedCode_ReturnsMaxRedemptionsMessage()
    {
        // Arrange
        var promoCode = CreateValidPromoCode(maxRedemptions: 5, currentRedemptions: 5);
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync("ABCD1234")).ReturnsAsync(promoCode);

        // Act
        var result = await _service.ValidateForRegistrationAsync("ABCD1234", "user@example.com");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("This code has reached its maximum redemptions", result.ErrorMessage);
    }

    #endregion

    #region Email Mismatch — Generic Error (Req 9.5)

    [Fact]
    public async Task ValidateForRegistrationAsync_EmailMismatch_ReturnsGenericInvalidMessage()
    {
        // Arrange — code is bound to a different email
        var promoCode = CreateValidPromoCode(boundEmail: "bound@example.com");
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync("ABCD1234")).ReturnsAsync(promoCode);

        // Act
        var result = await _service.ValidateForRegistrationAsync("ABCD1234", "other@example.com");

        // Assert — returns generic message, does NOT reveal code is email-bound
        Assert.False(result.IsValid);
        Assert.Equal("Invalid promo code", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateForRegistrationAsync_EmailMatch_ReturnsValid()
    {
        // Arrange — code is bound to matching email
        var promoCode = CreateValidPromoCode(id: 10, durationMonths: 12, boundEmail: "user@example.com");
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync("ABCD1234")).ReturnsAsync(promoCode);

        // Act
        var result = await _service.ValidateForRegistrationAsync("ABCD1234", "user@example.com");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(10, result.PromoCodeId);
        Assert.Equal(12, result.DurationMonths);
    }

    [Fact]
    public async Task ValidateForRegistrationAsync_EmailMatchCaseInsensitive_ReturnsValid()
    {
        // Arrange — code is bound to email with different casing
        var promoCode = CreateValidPromoCode(id: 10, durationMonths: 12, boundEmail: "User@Example.COM");
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync("ABCD1234")).ReturnsAsync(promoCode);

        // Act
        var result = await _service.ValidateForRegistrationAsync("ABCD1234", "user@example.com");

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region Input Sanitization (Req 9.4)

    [Fact]
    public async Task ValidateForRegistrationAsync_WhitespaceInput_TrimsBeforeValidation()
    {
        // Arrange
        var promoCode = CreateValidPromoCode();
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync("ABCD1234")).ReturnsAsync(promoCode);

        // Act — input has leading/trailing spaces
        var result = await _service.ValidateForRegistrationAsync("  ABCD1234  ", "user@example.com");

        // Assert
        Assert.True(result.IsValid);
        _promoCodeRepoMock.Verify(r => r.GetByCodeAsync("ABCD1234"), Times.Once);
    }

    [Fact]
    public async Task ValidateForRegistrationAsync_LowercaseInput_ConvertsToUppercase()
    {
        // Arrange
        var promoCode = CreateValidPromoCode();
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync("ABCD1234")).ReturnsAsync(promoCode);

        // Act — input is lowercase
        var result = await _service.ValidateForRegistrationAsync("abcd1234", "user@example.com");

        // Assert
        Assert.True(result.IsValid);
        _promoCodeRepoMock.Verify(r => r.GetByCodeAsync("ABCD1234"), Times.Once);
    }

    [Fact]
    public async Task ValidateForRegistrationAsync_MixedCaseWithSpaces_SanitizesCorrectly()
    {
        // Arrange
        var promoCode = CreateValidPromoCode();
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync("ABCD1234")).ReturnsAsync(promoCode);

        // Act — mixed case with spaces
        var result = await _service.ValidateForRegistrationAsync(" AbCd1234 ", "user@example.com");

        // Assert
        Assert.True(result.IsValid);
        _promoCodeRepoMock.Verify(r => r.GetByCodeAsync("ABCD1234"), Times.Once);
    }

    #endregion

    #region Validation Order — Revoked Before Expired

    [Fact]
    public async Task ValidateForRegistrationAsync_RevokedAndExpired_ReturnsRevokedMessage()
    {
        // Arrange — code is both revoked AND expired; revoked check comes first
        var promoCode = CreateValidPromoCode(
            isRevoked: true,
            expiresAtUtc: DateTime.UtcNow.AddDays(-1));
        _promoCodeRepoMock.Setup(r => r.GetByCodeAsync("ABCD1234")).ReturnsAsync(promoCode);

        // Act
        var result = await _service.ValidateForRegistrationAsync("ABCD1234", "user@example.com");

        // Assert — revoked takes precedence
        Assert.False(result.IsValid);
        Assert.Equal("This code has been revoked", result.ErrorMessage);
    }

    #endregion
}
