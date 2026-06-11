using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: quotation-acceptance, Property 2: Uniqueness — at most one acceptance per share

/// <summary>
/// Property-based tests for ProposalAcceptanceService uniqueness invariant.
/// Validates that attempting to accept a ProposalShare that already has an acceptance
/// record is rejected, and no new record is created.
/// **Validates: Requirements 3.1**
/// </summary>
public class ProposalAcceptanceUniquenessPropertyTests
{
    /// <summary>
    /// Creates a ProposalAcceptanceService with mocked dependencies configured
    /// to simulate a share that already has an existing acceptance record.
    /// </summary>
    private static (ProposalAcceptanceService Service, Mock<ProposalAcceptanceRepository> AcceptanceRepoMock) CreateServiceWithExistingAcceptance(
        int shareId,
        string token,
        DateTimeOffset existingAcceptedAtUtc)
    {
        // Mock the share repository to return a valid active non-expired share
        var mockShareRepo = new Mock<ProposalShareRepository>(MockBehavior.Strict, new object[] { null! });
        mockShareRepo
            .Setup(r => r.GetByTokenAsync(token))
            .ReturnsAsync(new ProposalShare
            {
                Id = shareId,
                QuotationId = 1,
                BusinessId = 1,
                ShareToken = token,
                SnapshotHtml = "<html></html>",
                CustomerEmail = "customer@example.com",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedByUserId = "user-1",
                IsActive = true
            });

        // Mock the acceptance repository to return an existing acceptance (simulating already accepted)
        var mockAcceptanceRepo = new Mock<ProposalAcceptanceRepository>(MockBehavior.Strict, new object[] { null! });
        mockAcceptanceRepo
            .Setup(r => r.GetByProposalShareIdAsync(shareId))
            .ReturnsAsync(new ProposalAcceptance
            {
                Id = 1,
                ProposalShareId = shareId,
                AcceptedTerms = "I accept this proposal and agree to proceed with the quoted work.",
                AcceptedAtUtc = existingAcceptedAtUtc,
                IpAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0 (original)",
                CreatedAtUtc = existingAcceptedAtUtc
            });

        var mockLogger = new Mock<ILogger<ProposalAcceptanceService>>();

        var service = new ProposalAcceptanceService(
            mockAcceptanceRepo.Object,
            mockShareRepo.Object,
            mockLogger.Object);

        return (service, mockAcceptanceRepo);
    }

    /// <summary>
    /// Property 2: For any ProposalShare that already has an Acceptance_Record,
    /// attempting to accept again SHALL be rejected (not persisted), the result
    /// SHALL have AlreadyAccepted = true, and InsertAsync SHALL never be called.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DuplicateAcceptance_IsRejected_AndNoNewRecordCreated(PositiveInt shareIdSeed, PositiveInt tokenSeed)
    {
        // Generate random share ID and token
        var shareId = shareIdSeed.Get;
        var token = $"share-token-{tokenSeed.Get}";
        var existingAcceptedAtUtc = DateTimeOffset.UtcNow.AddHours(-tokenSeed.Get % 100);

        var (service, acceptanceRepoMock) = CreateServiceWithExistingAcceptance(shareId, token, existingAcceptedAtUtc);

        // Attempt to accept again with different IP and user-agent
        var ip = $"10.0.{shareIdSeed.Get % 256}.{tokenSeed.Get % 256}";
        var userAgent = $"Mozilla/5.0 (attempt-{tokenSeed.Get})";

        var result = service.AcceptAsync(token, ip, userAgent).GetAwaiter().GetResult();

        // Assert: rejection — not successful
        var isNotSuccess = !result.Success;

        // Assert: AlreadyAccepted flag is true
        var isAlreadyAccepted = result.AlreadyAccepted;

        // Assert: AcceptedAtUtc matches the existing acceptance record's timestamp
        var hasCorrectTimestamp = result.AcceptedAtUtc == existingAcceptedAtUtc;

        // Verify: InsertAsync was never called (no new record created)
        acceptanceRepoMock.Verify(r => r.InsertAsync(It.IsAny<ProposalAcceptance>()), Times.Never());

        return (isNotSuccess && isAlreadyAccepted && hasCorrectTimestamp).ToProperty()
            .Label($"ShareId={shareId}, Token={token}: Success={result.Success}, " +
                   $"AlreadyAccepted={result.AlreadyAccepted}, AcceptedAtUtc={result.AcceptedAtUtc}");
    }
}
