using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Filters;
using System.Security.Claims;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-permission-gating, Property 6: Owner bypass

/// <summary>
/// Property-based tests for business owner bypass in UserPermissionFilter.
/// For any module included in the business's subscription plan, a user who is the business owner
/// SHALL always be granted full access without requiring a UserBusinessPermission record.
/// **Validates: Requirements 4.4**
/// </summary>
public class OwnerBypassPropertyTests
{
    private static readonly string[] ModuleKeys = ModuleControllerMap.Map.Keys.ToArray();
    private static readonly string[] HttpMethods = { "GET", "POST", "PUT", "DELETE", "PATCH" };

    #region Property 6: Business owner always has full access to plan-permitted modules

    /// <summary>
    /// Property 6a: Owner is never blocked regardless of which module is requested.
    /// The filter skips all permission checks for owners.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Owner_NeverBlocked_AnyModule(PositiveInt moduleSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var controllers = ModuleControllerMap.Map[module];
        var controllerName = controllers[0];

        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync("owner-user-id")).ReturnsAsync(true);
        // We do NOT set up GetEffectiveAccessLevelAsync — it should never be called for owners

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext(controllerName, "Index", "GET", userId: "owner-user-id");

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        var isAllowed = context.Result == null;

        return isAllowed.ToProperty()
            .Label($"Owner requesting module='{module}' via controller='{controllerName}' → should always be allowed");
    }

    /// <summary>
    /// Property 6b: Owner is never blocked regardless of HTTP method (GET, POST, PUT, DELETE, PATCH).
    /// Even write operations on any module are allowed without permission checks.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Owner_NeverBlocked_AnyHttpMethod(PositiveInt moduleSeed, PositiveInt methodSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var controllers = ModuleControllerMap.Map[module];
        var controllerName = controllers[0];
        var httpMethod = HttpMethods[methodSeed.Get % HttpMethods.Length];

        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync("owner-user-id")).ReturnsAsync(true);

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext(controllerName, "Create", httpMethod, userId: "owner-user-id");

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        var isAllowed = context.Result == null;

        return isAllowed.ToProperty()
            .Label($"Owner with method='{httpMethod}', module='{module}' → should always be allowed");
    }

    /// <summary>
    /// Property 6c: Owner bypass does NOT call GetEffectiveAccessLevelAsync —
    /// it short-circuits before any permission lookup.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Owner_SkipsPermissionCheck(PositiveInt moduleSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var controllers = ModuleControllerMap.Map[module];
        var controllerName = controllers[0];

        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync("owner-user-id")).ReturnsAsync(true);

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext(controllerName, "Index", "GET", userId: "owner-user-id");

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        // Verify GetEffectiveAccessLevelAsync was never called
        mockPlanCheckService.Verify(
            s => s.GetEffectiveAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);

        return true.ToProperty()
            .Label($"Owner on module='{module}' → GetEffectiveAccessLevelAsync should not be called");
    }

    /// <summary>
    /// Property 6d: Non-owners DO get their permissions checked (contrast with owner behavior).
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonOwner_PermissionCheckIsCalled(PositiveInt moduleSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var controllers = ModuleControllerMap.Map[module];
        var controllerName = controllers[0];

        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync("regular-user-id")).ReturnsAsync(false);
        mockPlanCheckService.Setup(s => s.GetEffectiveAccessLevelAsync("regular-user-id", module))
            .ReturnsAsync(AccessLevels.Full);

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext(controllerName, "Index", "GET", userId: "regular-user-id");

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        // Verify GetEffectiveAccessLevelAsync WAS called for non-owners
        mockPlanCheckService.Verify(
            s => s.GetEffectiveAccessLevelAsync("regular-user-id", module),
            Times.Once);

        return true.ToProperty()
            .Label($"Non-owner on module='{module}' → GetEffectiveAccessLevelAsync should be called");
    }

    #endregion

    #region Exhaustive verification

    /// <summary>
    /// Exhaustive test: Owner is allowed for every mapped module with every HTTP method.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Fact]
    public void Owner_AllModulesAllMethods_AlwaysAllowed()
    {
        foreach (var module in ModuleKeys)
        {
            var controllerName = ModuleControllerMap.Map[module][0];

            foreach (var method in HttpMethods)
            {
                var mockPlanCheckService = new Mock<IPlanCheckService>();
                mockPlanCheckService.Setup(s => s.IsOwnerAsync("owner-id")).ReturnsAsync(true);

                var filter = new UserPermissionFilter(mockPlanCheckService.Object);
                var context = CreateFilterContext(controllerName, "AnyAction", method, userId: "owner-id");

                filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

                Assert.Null(context.Result);
            }
        }
    }

    /// <summary>
    /// Verifies that owner bypass still works even when no UserBusinessPermission would exist.
    /// The filter returns early so the permission lookup never happens.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Fact]
    public void Owner_NoPermissionRecord_StillAllowed()
    {
        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync("owner-id")).ReturnsAsync(true);
        // Deliberately set up GetEffectiveAccessLevelAsync to return 'none' — it should never be reached
        mockPlanCheckService.Setup(s => s.GetEffectiveAccessLevelAsync("owner-id", PortalModules.Invoice))
            .ReturnsAsync(AccessLevels.None);

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext("Invoice", "Index", "POST", userId: "owner-id");

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        // Owner should still be allowed despite GetEffectiveAccessLevel returning 'none'
        Assert.Null(context.Result);

        // And GetEffectiveAccessLevelAsync should NOT have been called
        mockPlanCheckService.Verify(
            s => s.GetEffectiveAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region Helper Methods

    private static AuthorizationFilterContext CreateFilterContext(
        string controllerName,
        string actionName,
        string httpMethod,
        string userId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = httpMethod;

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        var routeData = new RouteData();
        routeData.Values["controller"] = controllerName;
        routeData.Values["action"] = actionName;

        var actionDescriptor = new ActionDescriptor();
        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);
        var filters = new List<IFilterMetadata>();

        return new AuthorizationFilterContext(actionContext, filters);
    }

    #endregion
}
