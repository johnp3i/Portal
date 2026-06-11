using System.Text.RegularExpressions;
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

// Feature: demo-access-invitations, Property 1: Token format

/// <summary>
/// Property-based tests for DemoInvitationService.GenerateToken() format validity.
/// Verifies that every generated token is a valid Base64URL string (no padding)
/// that decodes to exactly 32 bytes.
/// **Validates: Requirements 4.1, 4.2**
/// </summary>
public class DemoInvitationTokenFormatPropertyTests
{
    private static readonly Regex Base64UrlCharPattern = new(@"^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    /// <summary>
    /// Creates a DemoInvitationService instance for token generation testing.
    /// GenerateToken() is a pure cryptographic function that doesn't use any dependencies.
    /// </summary>
    private static DemoInvitationService CreateService()
    {
        var tenantMock = new Mock<ICurrentTenantService>();
        var dbContextOptions = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: $"TokenFormat_{Guid.NewGuid()}")
            .Options;
        var portalDbContext = new PortalDbContext(dbContextOptions, tenantMock.Object);

        var mockRepository = new Mock<DemoInvitationRepository>(portalDbContext) { CallBase = false };
        var mockEmailService = new Mock<IEmailService>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockLogger = new Mock<ILogger<DemoInvitationService>>();

        var membershipDbContextOptions = new DbContextOptionsBuilder<MembershipDbContext>()
            .UseInMemoryDatabase(databaseName: $"TokenFormat_Membership_{Guid.NewGuid()}")
            .Options;
        var membershipDbContext = new MembershipDbContext(membershipDbContextOptions);

        return new DemoInvitationService(
            mockRepository.Object,
            mockEmailService.Object,
            mockHttpContextAccessor.Object,
            mockLogger.Object,
            membershipDbContext);
    }

    /// <summary>
    /// Property 1a: Every generated token contains only Base64URL characters [A-Za-z0-9_-]
    /// with no '=' padding characters.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedToken_ContainsOnlyBase64UrlCharacters()
    {
        var service = CreateService();
        var token = service.GenerateToken();

        var hasOnlyValidChars = Base64UrlCharPattern.IsMatch(token);
        var hasNoPadding = !token.Contains('=');

        return (hasOnlyValidChars && hasNoPadding).ToProperty()
            .Label($"Token '{token}' should contain only [A-Za-z0-9_-] and no '=' padding");
    }

    /// <summary>
    /// Property 1b: Every generated token, when decoded from Base64URL back to bytes,
    /// produces exactly 32 bytes (256 bits of entropy).
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedToken_DecodesTo32Bytes()
    {
        var service = CreateService();
        var token = service.GenerateToken();

        // Reverse Base64URL encoding: replace URL-safe chars and restore padding
        var base64 = token
            .Replace('-', '+')
            .Replace('_', '/');

        // Add padding to make length a multiple of 4
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        var bytes = Convert.FromBase64String(base64);

        return (bytes.Length == 32).ToProperty()
            .Label($"Token '{token}' should decode to exactly 32 bytes, got {bytes.Length}");
    }

    /// <summary>
    /// Property 1c: Every generated token is non-null and non-empty.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedToken_IsNonNullAndNonEmpty()
    {
        var service = CreateService();
        var token = service.GenerateToken();

        return (!string.IsNullOrEmpty(token)).ToProperty()
            .Label("Token should never be null or empty");
    }
}
