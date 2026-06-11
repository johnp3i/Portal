using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: quotation-acceptance, Property 3: Rejection on inactive or expired shares

/// <summary>
/// Property-based tests for ProposalAcceptanceService.AcceptAsync rejection behavior
/// when a share is inactive or expired.
/// No acceptance record is persisted in either case — enforced by using MockBehavior.Strict
/// on the repository with no setup for InsertAsync or GetByProposalShareIdAsync.
/// If the service ever calls those methods, the strict mock will throw.
/// **Validates: Requirements 6.2**
/// </summary>
public class ProposalAcceptanceRejectionPropertyTests
{
    /// <summary>
    /// Creates a ProposalAcceptanceService with mocked dependencies configured for rejection testing.
    /// The acceptance repository mock uses Strict behavior with NO setup for InsertAsync or GetByProposalShareIdAsync —
    /// this guarantees the service short-circuits before reaching the repository.
    /// </summary>
    private static ProposalAcceptanceService CreateServiceForRejection(ProposalShare shareToReturn)
    {
        var mockShareRepo = new Mock<ProposalShareRepository>(MockBehavior.Loose, new object[] { null! });
        mockShareRepo
            .Setup(r => r.GetByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(shareToReturn);

        // Strict mock with NO setup for InsertAsync or GetByProposalShareIdAsync.
        // If these methods are called, an exception will be thrown — proving no persistence occurs.
        var mockAcceptanceRepo = new Mock<ProposalAcceptanceRepository>(MockBehavior.Strict, new object[] { null! });

        var mockLogger = new Mock<ILogger<ProposalAcceptanceService>>();

        return new ProposalAcceptanceService(
            mockAcceptanceRepo.Object,
            mockShareRepo.Object,
            mockLogger.Object);
    }

    #region Property 3a: Inactive share rejection

    /// <summary>
    /// Property 3a: For any ProposalShare where IsActive = false (regardless of expiry),
    /// AcceptAsync SHALL reject the request with Success = false and message containing "no longer valid",
    /// and no acceptance record SHALL be persisted (enforced by strict mock with no repository setup).
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InactiveShare_IsRejected(PositiveInt shareIdSeed, NonEmptyString tokenSeed, NonEmptyString ipSeed, NonEmptyString userAgentSeed)
    {
        var shareId = shareIdSeed.Get;
        var token = tokenSeed.Get;
        var ip = ipSeed.Get;
        var userAgent = userAgentSeed.Get;

        var inactiveShare = new ProposalShare
        {
            Id = shareId,
            QuotationId = 1,
            BusinessId = 1,
            ShareToken = token,
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@example.com",
            IsActive = false,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30), // Not expired, but inactive
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedByUserId = "user-1"
        };

        var service = CreateServiceForRejection(inactiveShare);

        // If the service attempts to call InsertAsync or GetByProposalShareIdAsync,
        // the strict mock will throw — making this test fail.
        var result = service.AcceptAsync(token, ip, userAgent).GetAwaiter().GetResult();

        var isRejected = !result.Success;
        var hasCorrectMessage = result.Message != null && result.Message.Contains("no longer valid");

        return (isRejected && hasCorrectMessage).ToProperty()
            .Label($"ShareId={shareId}, Token='{token}': Success={result.Success}, Message='{result.Message}'");
    }

    #endregion

    #region Property 3b: Expired share rejection

    /// <summary>
    /// Property 3b: For any ProposalShare where IsActive = true but ExpiresAtUtc is in the past,
    /// AcceptAsync SHALL reject the request with Success = false and message containing "expired",
    /// and no acceptance record SHALL be persisted (enforced by strict mock with no repository setup).
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpiredShare_IsRejected(PositiveInt shareIdSeed, NonEmptyString tokenSeed, PositiveInt daysAgoSeed, NonEmptyString ipSeed, NonEmptyString userAgentSeed)
    {
        var shareId = shareIdSeed.Get;
        var token = tokenSeed.Get;
        var daysAgo = (daysAgoSeed.Get % 365) + 1; // 1 to 365 days in the past
        var ip = ipSeed.Get;
        var userAgent = userAgentSeed.Get;

        var expiredShare = new ProposalShare
        {
            Id = shareId,
            QuotationId = 1,
            BusinessId = 1,
            ShareToken = token,
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@example.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo), // Expired
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo - 1),
            CreatedByUserId = "user-1"
        };

        var service = CreateServiceForRejection(expiredShare);

        // If the service attempts to call InsertAsync or GetByProposalShareIdAsync,
        // the strict mock will throw — making this test fail.
        var result = service.AcceptAsync(token, ip, userAgent).GetAwaiter().GetResult();

        var isRejected = !result.Success;
        var hasCorrectMessage = result.Message != null && result.Message.Contains("expired");

        return (isRejected && hasCorrectMessage).ToProperty()
            .Label($"ShareId={shareId}, DaysAgo={daysAgo}: Success={result.Success}, Message='{result.Message}'");
    }

    #endregion
}
