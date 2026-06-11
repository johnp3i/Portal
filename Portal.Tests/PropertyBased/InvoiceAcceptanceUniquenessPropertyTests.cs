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

// Feature: invoice-acceptance, Property 2: Uniqueness — at most one acceptance per InvoiceShare

/// <summary>
/// Property-based tests for InvoiceAcceptanceService uniqueness invariant.
/// Validates that attempting to accept an InvoiceShare that already has an acceptance
/// record is rejected, and no new record is created.
/// **Validates: Requirements 3.1**
/// </summary>
public class InvoiceAcceptanceUniquenessPropertyTests
{
    /// <summary>
    /// Creates an InvoiceAcceptanceService with mocked dependencies configured
    /// to simulate a share that already has an existing acceptance record.
    /// </summary>
    private static (InvoiceAcceptanceService Service, Mock<InvoiceAcceptanceRepository> AcceptanceRepoMock) CreateServiceWithExistingAcceptance(
        int shareId,
        string token,
        DateTimeOffset existingAcceptedAtUtc)
    {
        // Mock the sharing service to return a valid active non-expired share
        var mockSharingService = new Mock<IInvoiceSharingService>();
        mockSharingService
            .Setup(s => s.GetByTokenAsync(token))
            .ReturnsAsync(new InvoiceShare
            {
                Id = shareId,
                InvoiceId = 1,
                BusinessId = 1,
                ShareToken = token,
                SnapshotHtml = "<html></html>",
                CustomerEmail = "customer@example.com",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedByUserId = "user-1",
                IsActive = true
            });

        // Mock the repository to return an existing acceptance (simulating already accepted)
        var mockAcceptanceRepo = new Mock<InvoiceAcceptanceRepository>(MockBehavior.Strict, new object[] { null! });
        mockAcceptanceRepo
            .Setup(r => r.GetByInvoiceShareIdAsync(shareId))
            .ReturnsAsync(new InvoiceAcceptance
            {
                Id = 1,
                InvoiceShareId = shareId,
                AcceptedTerms = "I accept this invoice as correct and agree to pay by the due date.",
                AcceptedAtUtc = existingAcceptedAtUtc,
                IpAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0 (original)",
                CreatedAtUtc = existingAcceptedAtUtc
            });

        var mockLogger = new Mock<ILogger<InvoiceAcceptanceService>>();

        var service = new InvoiceAcceptanceService(
            mockAcceptanceRepo.Object,
            mockSharingService.Object,
            mockLogger.Object);

        return (service, mockAcceptanceRepo);
    }

    /// <summary>
    /// Property 2: For any InvoiceShare that already has an Acceptance_Record,
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
        acceptanceRepoMock.Verify(r => r.InsertAsync(It.IsAny<InvoiceAcceptance>()), Times.Never());

        return (isNotSuccess && isAlreadyAccepted && hasCorrectTimestamp).ToProperty()
            .Label($"ShareId={shareId}, Token={token}: Success={result.Success}, " +
                   $"AlreadyAccepted={result.AlreadyAccepted}, AcceptedAtUtc={result.AcceptedAtUtc}");
    }
}
