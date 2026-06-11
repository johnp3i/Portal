using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: invoice-acceptance, Property 1: Acceptance persistence round-trip

/// <summary>
/// Property-based tests for InvoiceAcceptanceService persistence round-trip.
/// For any valid InvoiceShare (active, non-expired) and any HTTP context (IP + user-agent),
/// submitting an acceptance SHALL persist a record where InvoiceShareId matches the share,
/// AcceptedTerms equals the constant text, IpAddress matches input, and UserAgent matches input.
/// **Validates: Requirements 2.1, 6.1, 6.2, 6.3, 6.4**
/// </summary>
public class InvoiceAcceptancePersistencePropertyTests
{
    /// <summary>
    /// Creates an InvoiceAcceptanceService with mocked dependencies.
    /// The mock captures the entity passed to InsertAsync so we can verify field values.
    /// </summary>
    private static (InvoiceAcceptanceService Service, InvoiceAcceptance? CapturedEntity) CreateServiceWithMocks(
        int shareId, string shareToken, string ipAddress, string userAgent)
    {
        InvoiceAcceptance? capturedEntity = null;

        var mockSharingService = new Mock<IInvoiceSharingService>();
        mockSharingService
            .Setup(s => s.GetByTokenAsync(shareToken))
            .ReturnsAsync(new InvoiceShare
            {
                Id = shareId,
                InvoiceId = 1,
                BusinessId = 1,
                ShareToken = shareToken,
                SnapshotHtml = "<html></html>",
                CustomerEmail = "customer@test.com",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedByUserId = "user-1",
                IsActive = true
            });

        var mockAcceptanceRepo = new Mock<InvoiceAcceptanceRepository>(MockBehavior.Strict, new object[] { null! });

        // First call to GetByInvoiceShareIdAsync returns null (no existing acceptance)
        mockAcceptanceRepo
            .Setup(r => r.GetByInvoiceShareIdAsync(shareId))
            .ReturnsAsync((InvoiceAcceptance?)null);

        // Capture the entity on InsertAsync
        mockAcceptanceRepo
            .Setup(r => r.InsertAsync(It.IsAny<InvoiceAcceptance>()))
            .Callback<InvoiceAcceptance>(entity => capturedEntity = entity)
            .Returns(Task.CompletedTask);

        var mockLogger = new Mock<ILogger<InvoiceAcceptanceService>>();

        var service = new InvoiceAcceptanceService(
            mockAcceptanceRepo.Object,
            mockSharingService.Object,
            mockLogger.Object);

        return (service, capturedEntity);
    }

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
    /// Property 1: For any active, non-expired InvoiceShare with random IP and user-agent,
    /// calling AcceptAsync SHALL return Success = true and persist a record where
    /// InvoiceShareId matches, AcceptedTerms equals the constant, IpAddress matches,
    /// and UserAgent matches the input.
    /// **Validates: Requirements 2.1, 6.1, 6.2, 6.3, 6.4**
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
        InvoiceAcceptance? capturedEntity = null;

        var mockSharingService = new Mock<IInvoiceSharingService>();
        mockSharingService
            .Setup(s => s.GetByTokenAsync(shareToken))
            .ReturnsAsync(new InvoiceShare
            {
                Id = shareId,
                InvoiceId = 1,
                BusinessId = 1,
                ShareToken = shareToken,
                SnapshotHtml = "<html></html>",
                CustomerEmail = "customer@test.com",
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                CreatedByUserId = "user-1",
                IsActive = true
            });

        var mockAcceptanceRepo = new Mock<InvoiceAcceptanceRepository>(MockBehavior.Strict, new object[] { null! });

        mockAcceptanceRepo
            .Setup(r => r.GetByInvoiceShareIdAsync(shareId))
            .ReturnsAsync((InvoiceAcceptance?)null);

        mockAcceptanceRepo
            .Setup(r => r.InsertAsync(It.IsAny<InvoiceAcceptance>()))
            .Callback<InvoiceAcceptance>(entity => capturedEntity = entity)
            .Returns(Task.CompletedTask);

        var mockLogger = new Mock<ILogger<InvoiceAcceptanceService>>();

        var service = new InvoiceAcceptanceService(
            mockAcceptanceRepo.Object,
            mockSharingService.Object,
            mockLogger.Object);

        var result = service.AcceptAsync(shareToken, ipAddress, userAgent).GetAwaiter().GetResult();

        var success = result.Success;
        var shareIdMatches = capturedEntity != null && capturedEntity.InvoiceShareId == shareId;
        var termsMatch = capturedEntity != null && capturedEntity.AcceptedTerms == InvoiceAcceptanceConstants.AcceptanceTermsText;
        var ipMatches = capturedEntity != null && capturedEntity.IpAddress == ipAddress;
        var uaMatches = capturedEntity != null && capturedEntity.UserAgent == userAgent;

        return (success && shareIdMatches && termsMatch && ipMatches && uaMatches).ToProperty()
            .Label($"ShareId={shareId}, IP={ipAddress}, UA='{userAgent}': " +
                   $"Success={success}, ShareIdMatch={shareIdMatches}, TermsMatch={termsMatch}, " +
                   $"IpMatch={ipMatches}, UaMatch={uaMatches}");
    }
}
