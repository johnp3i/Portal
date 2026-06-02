using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Logging;
using Moq;
using Portal.Web.Models.Stripe;
using Portal.Web.Models.ViewComponents;
using Portal.Web.Services;
using Portal.Web.Services.Stripe;
using Portal.Web.ViewComponents;
using Xunit;

namespace Portal.Tests.Unit.ViewComponents;

/// <summary>
/// Unit tests for SubscriptionStatusIndicatorViewComponent.
/// Validates Requirements 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 5.1, 5.2, 6.2, 6.5.
/// </summary>
public class SubscriptionStatusIndicatorViewComponentTests
{
    private readonly Mock<ISubscriptionPlanService> _subscriptionPlanServiceMock;
    private readonly Mock<IPlatformConfigService> _platformConfigServiceMock;
    private readonly Mock<ILogger<SubscriptionStatusIndicatorViewComponent>> _loggerMock;

    public SubscriptionStatusIndicatorViewComponentTests()
    {
        _subscriptionPlanServiceMock = new Mock<ISubscriptionPlanService>();
        _platformConfigServiceMock = new Mock<IPlatformConfigService>();
        _loggerMock = new Mock<ILogger<SubscriptionStatusIndicatorViewComponent>>();

        // Default: return "Trial" for TrialBadgeText
        _platformConfigServiceMock
            .Setup(s => s.GetValueAsync("TrialBadgeText"))
            .ReturnsAsync("Trial");
    }

    #region Helpers

    private SubscriptionStatusIndicatorViewComponent CreateComponent(ClaimsPrincipal user)
    {
        var component = new SubscriptionStatusIndicatorViewComponent(
            _subscriptionPlanServiceMock.Object,
            _platformConfigServiceMock.Object,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext { User = user };
        var viewContext = new ViewContext { HttpContext = httpContext };
        var viewComponentContext = new ViewComponentContext
        {
            ViewContext = viewContext
        };

        component.ViewComponentContext = viewComponentContext;
        return component;
    }

    private static ClaimsPrincipal CreateUser(
        string? businessId = null,
        bool isSuperAdmin = false,
        bool isOwner = false,
        bool isAuthenticated = true)
    {
        var claims = new List<Claim>();

        if (businessId != null)
        {
            claims.Add(new Claim("BusinessId", businessId));
        }

        if (isOwner)
        {
            claims.Add(new Claim("IsOwner", "true"));
        }

        if (isSuperAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "SuperAdmin"));
        }

        var identity = new ClaimsIdentity(
            claims,
            isAuthenticated ? "TestAuthentication" : null);

        return new ClaimsPrincipal(identity);
    }

    private static SubscriptionAccessResult CreateAccessResult(
        string? subscriptionStatus = "active",
        string? planName = "Business Plan",
        bool hasActiveSubscription = true,
        bool isGraceAccess = false)
    {
        return new SubscriptionAccessResult
        {
            SubscriptionStatus = subscriptionStatus!,
            PlanName = planName!,
            HasActiveSubscription = hasActiveSubscription,
            IsGraceAccess = isGraceAccess
        };
    }

    private static void AssertEmptyContent(IViewComponentResult result)
    {
        var contentResult = Assert.IsType<ContentViewComponentResult>(result);
        Assert.Equal(string.Empty, contentResult.Content);
    }

    private static SubscriptionStatusIndicatorViewModel AssertViewWithModel(IViewComponentResult result)
    {
        var viewResult = Assert.IsType<ViewViewComponentResult>(result);
        var model = Assert.IsType<SubscriptionStatusIndicatorViewModel>(viewResult.ViewData!.Model);
        return model;
    }

    #endregion

    #region Requirement 4.1 — SuperAdmin with null BusinessId returns empty content

    [Fact]
    public async Task InvokeAsync_SuperAdmin_NullBusinessId_ReturnsEmptyContent()
    {
        // Arrange
        var user = CreateUser(businessId: null, isSuperAdmin: true);
        var component = CreateComponent(user);

        // Act
        var result = await component.InvokeAsync();

        // Assert
        AssertEmptyContent(result);
    }

    #endregion

    #region Requirement 4.2 — SuperAdmin with valid BusinessId renders indicator view

    [Fact]
    public async Task InvokeAsync_SuperAdmin_ValidBusinessId_RendersView()
    {
        // Arrange
        var user = CreateUser(businessId: "5", isSuperAdmin: true);
        var component = CreateComponent(user);

        _subscriptionPlanServiceMock
            .Setup(s => s.GetAccessAsync(5))
            .ReturnsAsync(CreateAccessResult(subscriptionStatus: "active", planName: "Pro Plan"));

        // Act
        var result = await component.InvokeAsync();

        // Assert
        var model = AssertViewWithModel(result);
        Assert.Equal("Pro Plan", model.PlanName);
        Assert.Equal("Active", model.BadgeText);
    }

    #endregion

    #region Requirement 4.3 — SuperAdmin with BusinessId but no subscription record returns empty

    [Fact]
    public async Task InvokeAsync_SuperAdmin_NoSubscriptionRecord_ReturnsEmptyContent()
    {
        // Arrange
        var user = CreateUser(businessId: "5", isSuperAdmin: true);
        var component = CreateComponent(user);

        _subscriptionPlanServiceMock
            .Setup(s => s.GetAccessAsync(5))
            .ReturnsAsync(CreateAccessResult(subscriptionStatus: null, planName: null));

        // Act
        var result = await component.InvokeAsync();

        // Assert
        AssertEmptyContent(result);
    }

    #endregion

    #region Requirements 3.1, 3.2 — Owner user produces ViewModel with IsOwner = true

    [Fact]
    public async Task InvokeAsync_OwnerUser_ViewModelHasIsOwnerTrue()
    {
        // Arrange
        var user = CreateUser(businessId: "1", isOwner: true);
        var component = CreateComponent(user);

        _subscriptionPlanServiceMock
            .Setup(s => s.GetAccessAsync(1))
            .ReturnsAsync(CreateAccessResult());

        // Act
        var result = await component.InvokeAsync();

        // Assert
        var model = AssertViewWithModel(result);
        Assert.True(model.IsOwner);
    }

    #endregion

    #region Requirement 3.3 — Non-owner user produces ViewModel with IsOwner = false

    [Fact]
    public async Task InvokeAsync_NonOwnerUser_ViewModelHasIsOwnerFalse()
    {
        // Arrange
        var user = CreateUser(businessId: "1", isOwner: false);
        var component = CreateComponent(user);

        _subscriptionPlanServiceMock
            .Setup(s => s.GetAccessAsync(1))
            .ReturnsAsync(CreateAccessResult());

        // Act
        var result = await component.InvokeAsync();

        // Assert
        var model = AssertViewWithModel(result);
        Assert.False(model.IsOwner);
    }

    #endregion

    #region Requirement 6.5 — Missing BusinessId claim returns empty content

    [Fact]
    public async Task InvokeAsync_MissingBusinessIdClaim_ReturnsEmptyContent()
    {
        // Arrange
        var user = CreateUser(businessId: null);
        var component = CreateComponent(user);

        // Act
        var result = await component.InvokeAsync();

        // Assert
        AssertEmptyContent(result);
    }

    #endregion

    #region Requirement 6.5 — Non-numeric BusinessId claim returns empty content

    [Fact]
    public async Task InvokeAsync_NonNumericBusinessId_ReturnsEmptyContent()
    {
        // Arrange
        var user = CreateUser(businessId: "abc");
        var component = CreateComponent(user);

        // Act
        var result = await component.InvokeAsync();

        // Assert
        AssertEmptyContent(result);
    }

    #endregion

    #region Requirement 6.5 — Zero BusinessId claim returns empty content

    [Fact]
    public async Task InvokeAsync_ZeroBusinessId_ReturnsEmptyContent()
    {
        // Arrange
        var user = CreateUser(businessId: "0");
        var component = CreateComponent(user);

        // Act
        var result = await component.InvokeAsync();

        // Assert
        AssertEmptyContent(result);
    }

    #endregion

    #region Requirement 6.2 — Service called with correct parsed BusinessId integer

    [Fact]
    public async Task InvokeAsync_ValidBusinessId_ServiceCalledWithCorrectParsedId()
    {
        // Arrange
        var user = CreateUser(businessId: "42");
        var component = CreateComponent(user);

        _subscriptionPlanServiceMock
            .Setup(s => s.GetAccessAsync(42))
            .ReturnsAsync(CreateAccessResult())
            .Verifiable();

        // Act
        await component.InvokeAsync();

        // Assert
        _subscriptionPlanServiceMock.Verify(s => s.GetAccessAsync(42), Times.Once);
    }

    #endregion

    #region Requirement 5.1 — Null PlanName in result maps to "No Plan" in ViewModel

    [Fact]
    public async Task InvokeAsync_NullPlanName_ViewModelShowsNoPlan()
    {
        // Arrange
        var user = CreateUser(businessId: "1");
        var component = CreateComponent(user);

        _subscriptionPlanServiceMock
            .Setup(s => s.GetAccessAsync(1))
            .ReturnsAsync(CreateAccessResult(planName: null, subscriptionStatus: "active"));

        // Act
        var result = await component.InvokeAsync();

        // Assert
        var model = AssertViewWithModel(result);
        Assert.Equal("No Plan", model.PlanName);
    }

    #endregion

    #region Requirement 5.2 — Null SubscriptionStatus in result maps to "No Subscription" badge

    [Fact]
    public async Task InvokeAsync_NullSubscriptionStatus_ViewModelShowsNoSubscription()
    {
        // Arrange
        var user = CreateUser(businessId: "1");
        var component = CreateComponent(user);

        _subscriptionPlanServiceMock
            .Setup(s => s.GetAccessAsync(1))
            .ReturnsAsync(CreateAccessResult(subscriptionStatus: null, planName: "Some Plan"));

        // Act
        var result = await component.InvokeAsync();

        // Assert
        var model = AssertViewWithModel(result);
        Assert.Equal("No Subscription", model.BadgeText);
        Assert.Equal("#C24A4A", model.BadgeBackgroundColor);
    }

    #endregion

    #region Error Handling — Service exception caught and empty content returned

    [Fact]
    public async Task InvokeAsync_ServiceThrowsException_ReturnsEmptyContent()
    {
        // Arrange
        var user = CreateUser(businessId: "1");
        var component = CreateComponent(user);

        _subscriptionPlanServiceMock
            .Setup(s => s.GetAccessAsync(1))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        // Act
        var result = await component.InvokeAsync();

        // Assert
        AssertEmptyContent(result);
    }

    #endregion
}
