using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Web.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for ProposalAcceptanceService covering fresh acceptance, duplicate detection,
/// inactive/expired share rejection, DbUpdateException race-condition handling,
/// and constant terms storage.
/// Validates Requirements 2.1, 3.1, 3.2, 6.2, 7.1.
/// </summary>
public class ProposalAcceptanceServiceTests
{
    private readonly Mock<ProposalAcceptanceRepository> _acceptanceRepositoryMock;
    private readonly Mock<ProposalShareRepository> _shareRepositoryMock;
    private readonly Mock<ILogger<ProposalAcceptanceService>> _loggerMock;
    private readonly ProposalAcceptanceService _service;

    public ProposalAcceptanceServiceTests()
    {
        _acceptanceRepositoryMock = new Mock<ProposalAcceptanceRepository>(MockBehavior.Loose, new object[] { null! });
        _shareRepositoryMock = new Mock<ProposalShareRepository>(MockBehavior.Loose, new object[] { null! });
        _loggerMock = new Mock<ILogger<ProposalAcceptanceService>>();

        _service = new ProposalAcceptanceService(
            _acceptanceRepositoryMock.Object,
            _shareRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region AcceptAsync — Fresh Active Share (Req 2.1, 7.1)

    [Fact]
    public async Task AcceptAsync_ReturnsSuccess_WithCorrectFields_ForFreshActiveShare()
    {
        // Arrange
        var share = new ProposalShare
        {
            Id = 1,
            QuotationId = 100,
            BusinessId = 1,
            ShareToken = "valid-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedByUserId = "user-1"
        };

        _shareRepositoryMock
            .Setup(r => r.GetByTokenAsync("valid-token"))
            .ReturnsAsync(share);

        _acceptanceRepositoryMock
            .Setup(r => r.GetByProposalShareIdAsync(1))
            .ReturnsAsync((ProposalAcceptance?)null);

        ProposalAcceptance? capturedEntity = null;
        _acceptanceRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ProposalAcceptance>()))
            .Callback<ProposalAcceptance>(entity => capturedEntity = entity)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.AcceptAsync("valid-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.AcceptedAtUtc);
        Assert.Equal("Proposal accepted successfully.", result.Message);
        Assert.NotNull(capturedEntity);
        Assert.Equal(1, capturedEntity!.ProposalShareId);
        Assert.Equal("192.168.1.1", capturedEntity.IpAddress);
        Assert.Equal("Mozilla/5.0", capturedEntity.UserAgent);
    }

    #endregion

    #region AcceptAsync — Already Accepted Duplicate (Req 3.1, 3.2)

    [Fact]
    public async Task AcceptAsync_ReturnsAlreadyAccepted_WithDate_ForDuplicateAttempt()
    {
        // Arrange
        var acceptedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var share = new ProposalShare
        {
            Id = 2,
            QuotationId = 200,
            BusinessId = 1,
            ShareToken = "accepted-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-3),
            CreatedByUserId = "user-1"
        };

        var existingAcceptance = new ProposalAcceptance
        {
            Id = 1,
            ProposalShareId = 2,
            AcceptedTerms = ProposalAcceptanceConstants.AcceptanceTermsText,
            AcceptedAtUtc = acceptedAt,
            IpAddress = "10.0.0.1",
            UserAgent = "Chrome/120",
            CreatedAtUtc = acceptedAt
        };

        _shareRepositoryMock
            .Setup(r => r.GetByTokenAsync("accepted-token"))
            .ReturnsAsync(share);

        _acceptanceRepositoryMock
            .Setup(r => r.GetByProposalShareIdAsync(2))
            .ReturnsAsync(existingAcceptance);

        // Act
        var result = await _service.AcceptAsync("accepted-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.AlreadyAccepted);
        Assert.Equal(acceptedAt, result.AcceptedAtUtc);
        Assert.Equal("This proposal has already been accepted.", result.Message);
    }

    #endregion

    #region AcceptAsync — Inactive Share (Req 6.2)

    [Fact]
    public async Task AcceptAsync_ReturnsError_ForInactiveShare()
    {
        // Arrange
        var share = new ProposalShare
        {
            Id = 3,
            QuotationId = 300,
            BusinessId = 1,
            ShareToken = "inactive-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = false,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5),
            CreatedByUserId = "user-1"
        };

        _shareRepositoryMock
            .Setup(r => r.GetByTokenAsync("inactive-token"))
            .ReturnsAsync(share);

        // Act
        var result = await _service.AcceptAsync("inactive-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.False(result.Success);
        Assert.False(result.AlreadyAccepted);
        Assert.Contains("no longer valid", result.Message);
    }

    #endregion

    #region AcceptAsync — Expired Share (Req 6.2)

    [Fact]
    public async Task AcceptAsync_ReturnsError_ForExpiredShare()
    {
        // Arrange
        var share = new ProposalShare
        {
            Id = 4,
            QuotationId = 400,
            BusinessId = 1,
            ShareToken = "expired-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(-1), // Expired 1 hour ago
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            CreatedByUserId = "user-1"
        };

        _shareRepositoryMock
            .Setup(r => r.GetByTokenAsync("expired-token"))
            .ReturnsAsync(share);

        // Act
        var result = await _service.AcceptAsync("expired-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.False(result.Success);
        Assert.False(result.AlreadyAccepted);
        Assert.Contains("expired", result.Message);
    }

    #endregion

    #region AcceptAsync — DbUpdateException Race Condition (Req 3.1, 3.2)

    [Fact]
    public async Task AcceptAsync_HandlesDbUpdateException_GracefullyAsDuplicate()
    {
        // Arrange
        var share = new ProposalShare
        {
            Id = 5,
            QuotationId = 500,
            BusinessId = 1,
            ShareToken = "race-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedByUserId = "user-1"
        };

        var raceAcceptance = new ProposalAcceptance
        {
            Id = 10,
            ProposalShareId = 5,
            AcceptedTerms = ProposalAcceptanceConstants.AcceptanceTermsText,
            AcceptedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            IpAddress = "10.0.0.2",
            UserAgent = "Safari/17",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1)
        };

        _shareRepositoryMock
            .Setup(r => r.GetByTokenAsync("race-token"))
            .ReturnsAsync(share);

        // First call returns null (no existing acceptance), second call (after exception) returns the race winner
        var getCallCount = 0;
        _acceptanceRepositoryMock
            .Setup(r => r.GetByProposalShareIdAsync(5))
            .ReturnsAsync(() =>
            {
                getCallCount++;
                return getCallCount == 1 ? null : raceAcceptance;
            });

        // InsertAsync throws DbUpdateException (UNIQUE constraint violation)
        _acceptanceRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ProposalAcceptance>()))
            .ThrowsAsync(new DbUpdateException("Unique constraint violation", new Exception()));

        // Act
        var result = await _service.AcceptAsync("race-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.AlreadyAccepted);
        Assert.NotNull(result.AcceptedAtUtc);
        Assert.Equal("This proposal has already been accepted.", result.Message);
    }

    #endregion

    #region AcceptAsync — Stores Constant AcceptanceTermsText (Req 7.1)

    [Fact]
    public async Task AcceptAsync_StoresConstantAcceptanceTermsText()
    {
        // Arrange
        var share = new ProposalShare
        {
            Id = 6,
            QuotationId = 600,
            BusinessId = 1,
            ShareToken = "terms-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedByUserId = "user-1"
        };

        _shareRepositoryMock
            .Setup(r => r.GetByTokenAsync("terms-token"))
            .ReturnsAsync(share);

        _acceptanceRepositoryMock
            .Setup(r => r.GetByProposalShareIdAsync(6))
            .ReturnsAsync((ProposalAcceptance?)null);

        ProposalAcceptance? capturedEntity = null;
        _acceptanceRepositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<ProposalAcceptance>()))
            .Callback<ProposalAcceptance>(entity => capturedEntity = entity)
            .Returns(Task.CompletedTask);

        // Act
        await _service.AcceptAsync("terms-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.NotNull(capturedEntity);
        Assert.Equal(ProposalAcceptanceConstants.AcceptanceTermsText, capturedEntity!.AcceptedTerms);
        Assert.Equal("I accept this proposal and agree to proceed with the quoted work.", capturedEntity.AcceptedTerms);
    }

    #endregion
}
