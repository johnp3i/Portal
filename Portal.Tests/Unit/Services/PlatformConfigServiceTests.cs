using Microsoft.AspNetCore.Http;
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
/// Unit tests for PlatformConfigService covering GetValueAsync, SetValueAsync,
/// and request-scoped caching via HttpContext.Items.
/// Validates Requirements 8.2, 8.3.
/// </summary>
public class PlatformConfigServiceTests
{
    private readonly Mock<PlatformConfigRepository> _repoMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly DefaultHttpContext _httpContext;
    private readonly PlatformConfigService _service;

    public PlatformConfigServiceTests()
    {
        var tenantServiceMock = new Mock<ICurrentTenantService>();
        tenantServiceMock.Setup(t => t.CurrentBusinessId).Returns(1);

        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var portalDbContext = new PortalDbContext(options, tenantServiceMock.Object);

        _repoMock = new Mock<PlatformConfigRepository>(MockBehavior.Loose, portalDbContext);

        _httpContext = new DefaultHttpContext();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(_httpContext);

        _service = new PlatformConfigService(
            _repoMock.Object,
            _httpContextAccessorMock.Object);
    }

    #region GetValueAsync — Repository Returns Value (Req 8.2)

    [Fact]
    public async Task GetValueAsync_ExistingKey_ReturnsValueFromRepository()
    {
        // Arrange
        var config = new PlatformConfig
        {
            Key = "ShowPromoCodeField",
            Value = "true",
            LastModifiedAtUtc = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.GetByKeyAsync("ShowPromoCodeField")).ReturnsAsync(config);

        // Act
        var result = await _service.GetValueAsync("ShowPromoCodeField");

        // Assert
        Assert.Equal("true", result);
    }

    #endregion

    #region GetValueAsync — Non-Existent Key Returns Null (Req 8.3)

    [Fact]
    public async Task GetValueAsync_NonExistentKey_ReturnsNull()
    {
        // Arrange
        _repoMock.Setup(r => r.GetByKeyAsync("NonExistent")).ReturnsAsync((PlatformConfig?)null);

        // Act
        var result = await _service.GetValueAsync("NonExistent");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetValueAsync — Caching on First Call (Req 8.4)

    [Fact]
    public async Task GetValueAsync_FirstCall_CachesInHttpContextItems()
    {
        // Arrange
        var config = new PlatformConfig
        {
            Key = "TrialBadgeText",
            Value = "Trial",
            LastModifiedAtUtc = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.GetByKeyAsync("TrialBadgeText")).ReturnsAsync(config);

        // Act
        await _service.GetValueAsync("TrialBadgeText");

        // Assert — value is stored in HttpContext.Items
        Assert.True(_httpContext.Items.ContainsKey("PlatformConfig_TrialBadgeText"));
        Assert.Equal("Trial", _httpContext.Items["PlatformConfig_TrialBadgeText"]);
    }

    #endregion

    #region GetValueAsync — Returns Cached Value on Second Call (Req 8.4)

    [Fact]
    public async Task GetValueAsync_SecondCall_ReturnsCachedValueWithoutRepoCall()
    {
        // Arrange
        var config = new PlatformConfig
        {
            Key = "ShowPromoCodeField",
            Value = "false",
            LastModifiedAtUtc = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.GetByKeyAsync("ShowPromoCodeField")).ReturnsAsync(config);

        // Act — first call hits repo
        var result1 = await _service.GetValueAsync("ShowPromoCodeField");
        // Second call should use cache
        var result2 = await _service.GetValueAsync("ShowPromoCodeField");

        // Assert
        Assert.Equal("false", result1);
        Assert.Equal("false", result2);
        // Repository should have been called only once
        _repoMock.Verify(r => r.GetByKeyAsync("ShowPromoCodeField"), Times.Once);
    }

    #endregion

    #region GetValueAsync — Caches Null Values

    [Fact]
    public async Task GetValueAsync_NullValue_CachesNullToAvoidRepeatedDbCalls()
    {
        // Arrange
        _repoMock.Setup(r => r.GetByKeyAsync("MissingKey")).ReturnsAsync((PlatformConfig?)null);

        // Act
        var result1 = await _service.GetValueAsync("MissingKey");
        var result2 = await _service.GetValueAsync("MissingKey");

        // Assert
        Assert.Null(result1);
        Assert.Null(result2);
        _repoMock.Verify(r => r.GetByKeyAsync("MissingKey"), Times.Once);
    }

    #endregion

    #region SetValueAsync — Calls Repository and Invalidates Cache

    [Fact]
    public async Task SetValueAsync_CallsRepositoryUpsert()
    {
        // Arrange
        _repoMock.Setup(r => r.UpsertAsync("ShowPromoCodeField", "true")).Returns(Task.CompletedTask);

        // Act
        await _service.SetValueAsync("ShowPromoCodeField", "true");

        // Assert
        _repoMock.Verify(r => r.UpsertAsync("ShowPromoCodeField", "true"), Times.Once);
    }

    [Fact]
    public async Task SetValueAsync_InvalidatesCacheEntry()
    {
        // Arrange — first, cache a value
        var config = new PlatformConfig
        {
            Key = "ShowPromoCodeField",
            Value = "false",
            LastModifiedAtUtc = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.GetByKeyAsync("ShowPromoCodeField")).ReturnsAsync(config);
        _repoMock.Setup(r => r.UpsertAsync("ShowPromoCodeField", "true")).Returns(Task.CompletedTask);

        // Populate cache
        await _service.GetValueAsync("ShowPromoCodeField");
        Assert.True(_httpContext.Items.ContainsKey("PlatformConfig_ShowPromoCodeField"));

        // Act — update the value
        await _service.SetValueAsync("ShowPromoCodeField", "true");

        // Assert — cache entry should be removed
        Assert.False(_httpContext.Items.ContainsKey("PlatformConfig_ShowPromoCodeField"));
    }

    [Fact]
    public async Task SetValueAsync_AfterInvalidation_NextGetCallsRepoAgain()
    {
        // Arrange
        var originalConfig = new PlatformConfig
        {
            Key = "ShowPromoCodeField",
            Value = "false",
            LastModifiedAtUtc = DateTime.UtcNow
        };
        var updatedConfig = new PlatformConfig
        {
            Key = "ShowPromoCodeField",
            Value = "true",
            LastModifiedAtUtc = DateTime.UtcNow
        };

        var callCount = 0;
        _repoMock.Setup(r => r.GetByKeyAsync("ShowPromoCodeField"))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? originalConfig : updatedConfig;
            });
        _repoMock.Setup(r => r.UpsertAsync("ShowPromoCodeField", "true")).Returns(Task.CompletedTask);

        // Act
        var first = await _service.GetValueAsync("ShowPromoCodeField");
        await _service.SetValueAsync("ShowPromoCodeField", "true");
        var second = await _service.GetValueAsync("ShowPromoCodeField");

        // Assert
        Assert.Equal("false", first);
        Assert.Equal("true", second);
        _repoMock.Verify(r => r.GetByKeyAsync("ShowPromoCodeField"), Times.Exactly(2));
    }

    #endregion

    #region GetValueAsync — No HttpContext Available

    [Fact]
    public async Task GetValueAsync_NoHttpContext_StillReturnsValueFromRepo()
    {
        // Arrange — no HttpContext
        var noContextAccessorMock = new Mock<IHttpContextAccessor>();
        noContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var service = new PlatformConfigService(_repoMock.Object, noContextAccessorMock.Object);

        var config = new PlatformConfig
        {
            Key = "SomeKey",
            Value = "SomeValue",
            LastModifiedAtUtc = DateTime.UtcNow
        };
        _repoMock.Setup(r => r.GetByKeyAsync("SomeKey")).ReturnsAsync(config);

        // Act
        var result = await service.GetValueAsync("SomeKey");

        // Assert
        Assert.Equal("SomeValue", result);
    }

    #endregion
}
