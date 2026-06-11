using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: quotation-acceptance, Property 1: Acceptance persistence round-trip

/// <summary>
/// Property-based tests for ProposalAcceptanceService persistence round-trip.
/// For any valid ProposalShare (active, non-expired) and any HTTP context (IP + user-agent),
/// submitting an acceptance SHALL persist a record where ProposalShareId matches the share,
/// AcceptedTerms equals the constant text, IpAddress matches input, and UserAgent matches input.
/// **Validates: Requirements 2.1, 7.1, 7.2, 7.3, 7.4**
/// </summary>
public class ProposalAcceptancePersistencePropertyTests
{
    /// <summary>
    /// Generates a valid IPv4 address string from random octets.
    /// </summary>
    private static string GenerateIPv4(int seed)
    {
        var a = Math.Abs(seed % 256);
        var b = Math.Abs((seed / 256) % 256);
        var c = Math.Abs((seed / 65536) % 256);
        var d = Math.Abs((seed / 16777216) % 256);
        return $"{a}.{b}.{c}.{d}";
    }

    /// <summary>
    /// Generates an IPv6-style address string from a seed.
    /// </summary>
    private static string GenerateIPv6(int seed)
    {
        var absSeed = Math.Abs(seed);
        return $"fe80::{absSeed % 0xFFFF:x4}:{(absSeed / 0xFFFF) % 0xFFFF:x4}";
    }

    /// <summary>
    /// Property 1: For any active, non-expired ProposalShare with random IP and user-agent,
    /// calling AcceptAsync SHALL return Success = true and persist a record where
    /// ProposalShareId matches, AcceptedTerms equals the constant, IpAddress matches,
    /// and UserAgent matches the input.
    /// **Validates: Requirements 2.1, 7.1, 7.2, 7.3, 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AcceptAsync_PersistsCorrectFields_ForAnyValidInput(PositiveInt shareIdSeed, int ipSeed, NonNull<string> userAgentSeed)
    {
        var shareId = shareIdSeed.Get;
        var shareToken = $"token-{shareId}";
        var userAgent = userAgentSeed.Get;

        // Alternate between IPv4 and IPv6 based on seed parity
        var ipAddress = ipSeed % 2 == 0
            ? GenerateIPv4(ipSeed)
            : GenerateIPv6(ipSeed);

        // We need to capture the entity after InsertAsync is called
        ProposalAcceptance? capturedEntity = null;

        var mockShareRepo = new Mock<ProposalShareRepository>(MockBehavior.Loose, new object[] { null! });
        mockShareRepo
            .Setup(r => r.GetByTokenAsync(shareToken))
            .ReturnsAsync(new ProposalShare
            {
                Id = shareId,
                QuotationId = 1,
                BusinessId = 1,
                ShareToken = shareToken,
                SnapshotHtml = "<html></html>",
                CustomerEmail = "customer@test.com",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedByUserId = "user-1",
                IsActive = true
            });

        var mockAcceptanceRepo = new Mock<ProposalAcceptanceRepository>(MockBehavior.Strict, new object[] { null! });

        // First call to GetByProposalShareIdAsync returns null (no existing acceptance)
        mockAcceptanceRepo
            .Setup(r => r.GetByProposalShareIdAsync(shareId))
            .ReturnsAsync((ProposalAcceptance?)null);

        // Capture the entity on InsertAsync
        mockAcceptanceRepo
            .Setup(r => r.InsertAsync(It.IsAny<ProposalAcceptance>()))
            .Callback<ProposalAcceptance>(entity => capturedEntity = entity)
            .Returns(Task.CompletedTask);

        var mockLogger = new Mock<ILogger<ProposalAcceptanceService>>();

        var service = new ProposalAcceptanceService(
            mockAcceptanceRepo.Object,
            mockShareRepo.Object,
            mockLogger.Object);

        var result = service.AcceptAsync(shareToken, ipAddress, userAgent).GetAwaiter().GetResult();

        var success = result.Success;
        var shareIdMatches = capturedEntity != null && capturedEntity.ProposalShareId == shareId;
        var termsMatch = capturedEntity != null && capturedEntity.AcceptedTerms == ProposalAcceptanceConstants.AcceptanceTermsText;
        var ipMatches = capturedEntity != null && capturedEntity.IpAddress == ipAddress;
        var uaMatches = capturedEntity != null && capturedEntity.UserAgent == userAgent;

        return (success && shareIdMatches && termsMatch && ipMatches && uaMatches).ToProperty()
            .Label($"ShareId={shareId}, IP={ipAddress}, UA='{userAgent}': " +
                   $"Success={success}, ShareIdMatch={shareIdMatches}, TermsMatch={termsMatch}, " +
                   $"IpMatch={ipMatches}, UaMatch={uaMatches}");
    }
}
