using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: invoice-acceptance, Property 3: Rejection on inactive or expired shares

/// <summary>
/// Property-based tests for InvoiceAcceptanceService.AcceptAsync rejection behavior
/// when a share is inactive or expired.
/// No acceptance record is persisted in either case — enforced by using MockBehavior.Strict
/// on the repository with no setup for InsertAsync or GetByInvoiceShareIdAsync.
/// If the service ever calls those methods, the strict mock will throw.
/// **Validates: Requirements 5.2**
/// </summary>
public class InvoiceAcceptanceRejectionPropertyTests
{
    /// <summary>
    /// Creates an InvoiceAcceptanceService with mocked dependencies configured for rejection testing.
    /// The repository mock uses Strict behavior with NO setup for InsertAsync or GetByInvoiceShareIdAsync —
    /// this guarantees the service short-circuits before reaching the repository.
    /// </summary>
    private static InvoiceAcceptanceService CreateServiceForRejection(InvoiceShare shareToReturn)
    {
        var mockSharingService = new Mock<IInvoiceSharingService>();
        mockSharingService
            .Setup(s => s.GetByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(shareToReturn);

        // Strict mock with NO setup for InsertAsync or GetByInvoiceShareIdAsync.
        // If these methods are called, an exception will be thrown — proving no persistence occurs.
        var mockAcceptanceRepo = new Mock<InvoiceAcceptanceRepository>(MockBehavior.Strict, new object[] { null! });

        var mockLogger = new Mock<ILogger<InvoiceAcceptanceService>>();

        return new InvoiceAcceptanceService(
            mockAcceptanceRepo.Object,
            mockSharingService.Object,
            mockLogger.Object);
    }

    #region Property 3a: Inactive share rejection

    /// <summary>
    /// Property 3a: For any InvoiceShare where IsActive = false (but non-expired),
    /// AcceptAsync SHALL reject the request with Success = false and message containing "no longer valid",
    /// and no acceptance record SHALL be persisted (enforced by strict mock with no repository setup).
    /// **Validates: Requirement 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InactiveShare_IsRejected(PositiveInt shareIdSeed, NonEmptyString tokenSeed)
    {
        var shareId = shareIdSeed.Get;
        var token = tokenSeed.Get;

        var inactiveShare = new InvoiceShare
        {
            Id = shareId,
            InvoiceId = 1,
            BusinessId = 1,
            ShareToken = token,
            SnapshotHtml = "<html></html>",
            CustomerEmail = "test@example.com",
            IsActive = false,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(30), // Not expired, but inactive
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedByUserId = "user-1"
        };

        var service = CreateServiceForRejection(inactiveShare);

        // If the service attempts to call InsertAsync or GetByInvoiceShareIdAsync,
        // the strict mock will throw — making this test fail.
        var result = service.AcceptAsync(token, "192.168.1.1", "TestAgent/1.0").GetAwaiter().GetResult();

        var isRejected = !result.Success;
        var hasCorrectMessage = result.Message != null && result.Message.Contains("no longer valid");

        return (isRejected && hasCorrectMessage).ToProperty()
            .Label($"ShareId={shareId}, Token='{token}': Success={result.Success}, Message='{result.Message}'");
    }

    #endregion

    #region Property 3b: Expired share rejection

    /// <summary>
    /// Property 3b: For any InvoiceShare where IsActive = true but ExpiresAtUtc is in the past,
    /// AcceptAsync SHALL reject the request with Success = false and message containing "expired",
    /// and no acceptance record SHALL be persisted (enforced by strict mock with no repository setup).
    /// **Validates: Requirement 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpiredShare_IsRejected(PositiveInt shareIdSeed, NonEmptyString tokenSeed, PositiveInt daysAgoSeed)
    {
        var shareId = shareIdSeed.Get;
        var token = tokenSeed.Get;
        var daysAgo = (daysAgoSeed.Get % 365) + 1; // 1 to 365 days in the past

        var expiredShare = new InvoiceShare
        {
            Id = shareId,
            InvoiceId = 1,
            BusinessId = 1,
            ShareToken = token,
            SnapshotHtml = "<html></html>",
            CustomerEmail = "test@example.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo), // Expired
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-daysAgo - 1),
            CreatedByUserId = "user-1"
        };

        var service = CreateServiceForRejection(expiredShare);

        // If the service attempts to call InsertAsync or GetByInvoiceShareIdAsync,
        // the strict mock will throw — making this test fail.
        var result = service.AcceptAsync(token, "10.0.0.1", "TestAgent/2.0").GetAwaiter().GetResult();

        var isRejected = !result.Success;
        var hasCorrectMessage = result.Message != null && result.Message.Contains("expired");

        return (isRejected && hasCorrectMessage).ToProperty()
            .Label($"ShareId={shareId}, DaysAgo={daysAgo}: Success={result.Success}, Message='{result.Message}'");
    }

    #endregion
}
