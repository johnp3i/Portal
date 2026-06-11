using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Xunit;

namespace Portal.Tests.Unit.Services;

/// <summary>
/// Unit tests for InvoiceAcceptanceService covering fresh acceptance, duplicate detection,
/// inactive/expired share rejection, DbUpdateException race-condition handling,
/// and constant terms storage.
/// Validates Requirements 2.1, 3.1, 3.2, 5.2, 6.1.
/// </summary>
public class InvoiceAcceptanceServiceTests
{
    private readonly Mock<InvoiceAcceptanceRepository> _repositoryMock;
    private readonly Mock<IInvoiceSharingService> _sharingServiceMock;
    private readonly Mock<ILogger<InvoiceAcceptanceService>> _loggerMock;
    private readonly InvoiceAcceptanceService _service;

    public InvoiceAcceptanceServiceTests()
    {
        _repositoryMock = new Mock<InvoiceAcceptanceRepository>(MockBehavior.Loose, new object[] { null! });
        _sharingServiceMock = new Mock<IInvoiceSharingService>();
        _loggerMock = new Mock<ILogger<InvoiceAcceptanceService>>();

        _service = new InvoiceAcceptanceService(
            _repositoryMock.Object,
            _sharingServiceMock.Object,
            _loggerMock.Object);
    }

    #region AcceptAsync — Fresh Active Share (Req 2.1, 6.1)

    [Fact]
    public async Task AcceptAsync_ReturnsSuccess_ForFreshActiveShare()
    {
        // Arrange
        var share = new InvoiceShare
        {
            Id = 1,
            InvoiceId = 100,
            BusinessId = 1,
            ShareToken = "valid-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedByUserId = "user-1"
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync("valid-token"))
            .ReturnsAsync(share);

        _repositoryMock
            .Setup(r => r.GetByInvoiceShareIdAsync(1))
            .ReturnsAsync((InvoiceAcceptance?)null);

        _repositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<InvoiceAcceptance>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.AcceptAsync("valid-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.AcceptedAtUtc);
        Assert.Equal("Invoice accepted successfully.", result.Message);
    }

    #endregion

    #region AcceptAsync — Already Accepted Duplicate (Req 3.1)

    [Fact]
    public async Task AcceptAsync_ReturnsAlreadyAccepted_ForDuplicateAttempt()
    {
        // Arrange
        var acceptedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var share = new InvoiceShare
        {
            Id = 2,
            InvoiceId = 200,
            BusinessId = 1,
            ShareToken = "accepted-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-3),
            CreatedByUserId = "user-1"
        };

        var existingAcceptance = new InvoiceAcceptance
        {
            Id = 1,
            InvoiceShareId = 2,
            AcceptedTerms = InvoiceAcceptanceConstants.AcceptanceTermsText,
            AcceptedAtUtc = acceptedAt,
            IpAddress = "10.0.0.1",
            UserAgent = "Chrome/120",
            CreatedAtUtc = acceptedAt
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync("accepted-token"))
            .ReturnsAsync(share);

        _repositoryMock
            .Setup(r => r.GetByInvoiceShareIdAsync(2))
            .ReturnsAsync(existingAcceptance);

        // Act
        var result = await _service.AcceptAsync("accepted-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.AlreadyAccepted);
        Assert.Equal(acceptedAt, result.AcceptedAtUtc);
        Assert.Equal("This invoice has already been accepted.", result.Message);
    }

    #endregion

    #region AcceptAsync — Inactive Share (Req 5.2)

    [Fact]
    public async Task AcceptAsync_ReturnsError_ForInactiveShare()
    {
        // Arrange
        var share = new InvoiceShare
        {
            Id = 3,
            InvoiceId = 300,
            BusinessId = 1,
            ShareToken = "inactive-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = false,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-5),
            CreatedByUserId = "user-1"
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync("inactive-token"))
            .ReturnsAsync(share);

        // Act
        var result = await _service.AcceptAsync("inactive-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.False(result.Success);
        Assert.False(result.AlreadyAccepted);
        Assert.Contains("no longer valid", result.Message);
    }

    #endregion

    #region AcceptAsync — Expired Share (Req 5.2)

    [Fact]
    public async Task AcceptAsync_ReturnsError_ForExpiredShare()
    {
        // Arrange
        var share = new InvoiceShare
        {
            Id = 4,
            InvoiceId = 400,
            BusinessId = 1,
            ShareToken = "expired-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(-1), // Expired 1 hour ago
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
            CreatedByUserId = "user-1"
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync("expired-token"))
            .ReturnsAsync(share);

        // Act
        var result = await _service.AcceptAsync("expired-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.False(result.Success);
        Assert.False(result.AlreadyAccepted);
        Assert.Contains("expired", result.Message);
    }

    #endregion

    #region AcceptAsync — DbUpdateException Race Condition (Req 3.2)

    [Fact]
    public async Task AcceptAsync_HandlesDbUpdateException_AsDuplicate()
    {
        // Arrange
        var share = new InvoiceShare
        {
            Id = 5,
            InvoiceId = 500,
            BusinessId = 1,
            ShareToken = "race-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedByUserId = "user-1"
        };

        var raceAcceptance = new InvoiceAcceptance
        {
            Id = 10,
            InvoiceShareId = 5,
            AcceptedTerms = InvoiceAcceptanceConstants.AcceptanceTermsText,
            AcceptedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            IpAddress = "10.0.0.2",
            UserAgent = "Safari/17",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1)
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync("race-token"))
            .ReturnsAsync(share);

        // First call returns null (no existing acceptance), second call (after exception) returns the race winner
        var getCallCount = 0;
        _repositoryMock
            .Setup(r => r.GetByInvoiceShareIdAsync(5))
            .ReturnsAsync(() =>
            {
                getCallCount++;
                return getCallCount == 1 ? null : raceAcceptance;
            });

        // InsertAsync throws DbUpdateException (UNIQUE constraint violation)
        _repositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<InvoiceAcceptance>()))
            .ThrowsAsync(new DbUpdateException("Unique constraint violation", new Exception()));

        // Act
        var result = await _service.AcceptAsync("race-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.AlreadyAccepted);
        Assert.NotNull(result.AcceptedAtUtc);
        Assert.Equal("This invoice has already been accepted.", result.Message);
    }

    #endregion

    #region AcceptAsync — Stores Constant Accepted Terms (Req 6.1)

    [Fact]
    public async Task AcceptAsync_StoresConstantAcceptedTerms()
    {
        // Arrange
        var share = new InvoiceShare
        {
            Id = 6,
            InvoiceId = 600,
            BusinessId = 1,
            ShareToken = "terms-token",
            SnapshotHtml = "<html></html>",
            CustomerEmail = "customer@test.com",
            IsActive = true,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            CreatedByUserId = "user-1"
        };

        _sharingServiceMock
            .Setup(s => s.GetByTokenAsync("terms-token"))
            .ReturnsAsync(share);

        _repositoryMock
            .Setup(r => r.GetByInvoiceShareIdAsync(6))
            .ReturnsAsync((InvoiceAcceptance?)null);

        InvoiceAcceptance? capturedEntity = null;
        _repositoryMock
            .Setup(r => r.InsertAsync(It.IsAny<InvoiceAcceptance>()))
            .Callback<InvoiceAcceptance>(entity => capturedEntity = entity)
            .Returns(Task.CompletedTask);

        // Act
        await _service.AcceptAsync("terms-token", "192.168.1.1", "Mozilla/5.0");

        // Assert
        Assert.NotNull(capturedEntity);
        Assert.Equal(InvoiceAcceptanceConstants.AcceptanceTermsText, capturedEntity!.AcceptedTerms);
    }

    #endregion
}
