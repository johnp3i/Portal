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

// Feature: subscription-permission-gating, Property 4: User filter access control

/// <summary>
/// Property-based tests for UserPermissionFilter access control logic.
/// For any non-owner, non-demo user requesting a module controller that passes the plan check,
/// the UserPermissionFilter SHALL allow the request if and only if a UserBusinessPermission record
/// exists for that user/module with AccessLevel not equal to 'none'.
/// **Validates: Requirements 4.1, 4.2**
/// </summary>
public class UserFilterAccessPropertyTests
{
    private static readonly string[] ModuleKeys = ModuleControllerMap.Map.Keys.ToArray();
    private static readonly string[] AccessLevelOptions = { AccessLevels.Full, AccessLevels.ReadOnly, AccessLevels.None };

    #region Property 4: User filter grants non-owner access iff permission record exists with level != 'none'

    /// <summary>
    /// Property 4a: When GetEffectiveAccessLevelAsync returns 'none', the filter blocks access.
    /// This simulates either no permission record or an explicit 'none' permission.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonOwnerWithNoneAccess_IsBlocked(PositiveInt moduleSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var controllers = ModuleControllerMap.Map[module];
        var controllerName = controllers[0];

        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync(It.IsAny<string>())).ReturnsAsync(false);
        mockPlanCheckService.Setup(s => s.GetEffectiveAccessLevelAsync(It.IsAny<string>(), module))
            .ReturnsAsync(AccessLevels.None);

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext(controllerName, "Index", "GET", authenticated: true, userId: "user-123");

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        // Filter should have set a result (blocking the request)
        var isBlocked = context.Result != null;

        return isBlocked.ToProperty()
            .Label($"Module='{module}', Controller='{controllerName}', AccessLevel='none' → should be blocked");
    }

    /// <summary>
    /// Property 4b: When GetEffectiveAccessLevelAsync returns 'full', the filter allows access.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonOwnerWithFullAccess_IsAllowed(PositiveInt moduleSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var controllers = ModuleControllerMap.Map[module];
        var controllerName = controllers[0];

        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync(It.IsAny<string>())).ReturnsAsync(false);
        mockPlanCheckService.Setup(s => s.GetEffectiveAccessLevelAsync(It.IsAny<string>(), module))
            .ReturnsAsync(AccessLevels.Full);

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext(controllerName, "Index", "GET", authenticated: true, userId: "user-123");

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        // Filter should NOT have set a result (allowing the request)
        var isAllowed = context.Result == null;

        return isAllowed.ToProperty()
            .Label($"Module='{module}', Controller='{controllerName}', AccessLevel='full' → should be allowed");
    }

    /// <summary>
    /// Property 4c: When GetEffectiveAccessLevelAsync returns 'readonly' and request is GET,
    /// the filter allows access (with readonly flag set).
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonOwnerWithReadonlyAccess_GetRequest_IsAllowed(PositiveInt moduleSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var controllers = ModuleControllerMap.Map[module];
        var controllerName = controllers[0];

        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync(It.IsAny<string>())).ReturnsAsync(false);
        mockPlanCheckService.Setup(s => s.GetEffectiveAccessLevelAsync(It.IsAny<string>(), module))
            .ReturnsAsync(AccessLevels.ReadOnly);

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext(controllerName, "Index", "GET", authenticated: true, userId: "user-123");

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        // Filter should NOT have set a result (allowing the request)
        var isAllowed = context.Result == null;
        // Additionally, UserReadOnly should be set
        var isReadonlyFlagSet = context.HttpContext.Items.ContainsKey("UserReadOnly")
                             && (bool)context.HttpContext.Items["UserReadOnly"]!;

        return (isAllowed && isReadonlyFlagSet).ToProperty()
            .Label($"Module='{module}', Controller='{controllerName}', AccessLevel='readonly', GET → should be allowed with readonly flag");
    }

    /// <summary>
    /// Property 4d: For random access levels, access is granted iff level is not 'none' (for GET requests).
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonOwnerAccess_GrantedIffLevelNotNone_ForGetRequests(PositiveInt moduleSeed, PositiveInt levelSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var controllers = ModuleControllerMap.Map[module];
        var controllerName = controllers[0];
        var accessLevel = AccessLevelOptions[levelSeed.Get % AccessLevelOptions.Length];

        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync(It.IsAny<string>())).ReturnsAsync(false);
        mockPlanCheckService.Setup(s => s.GetEffectiveAccessLevelAsync(It.IsAny<string>(), module))
            .ReturnsAsync(accessLevel);

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext(controllerName, "Index", "GET", authenticated: true, userId: "user-123");

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        var isAllowed = context.Result == null;
        var shouldAllow = accessLevel != AccessLevels.None;

        return (isAllowed == shouldAllow).ToProperty()
            .Label($"Module='{module}', AccessLevel='{accessLevel}', Expected allow={shouldAllow}, Actual allow={isAllowed}");
    }

    #endregion

    #region Exhaustive verification

    /// <summary>
    /// Exhaustive test: each access level for a non-owner produces the expected filter decision on GET.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact]
    public void NonOwner_AccessDecision_MatchesAccessLevel_Exhaustive()
    {
        var module = PortalModules.Invoice;
        var controllerName = "Invoice";

        // AccessLevel 'full' → allowed
        AssertFilterDecision(controllerName, module, AccessLevels.Full, "GET", "Index", expectBlocked: false);

        // AccessLevel 'readonly' on GET → allowed (with flag)
        AssertFilterDecision(controllerName, module, AccessLevels.ReadOnly, "GET", "Index", expectBlocked: false);

        // AccessLevel 'none' → blocked
        AssertFilterDecision(controllerName, module, AccessLevels.None, "GET", "Index", expectBlocked: true);
    }

    /// <summary>
    /// Verifies that for AJAX requests with 'none' access, the filter returns a JsonResult with 403 status.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Fact]
    public void NonOwner_NoneAccess_AjaxRequest_Returns403Json()
    {
        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync(It.IsAny<string>())).ReturnsAsync(false);
        mockPlanCheckService.Setup(s => s.GetEffectiveAccessLevelAsync(It.IsAny<string>(), PortalModules.Invoice))
            .ReturnsAsync(AccessLevels.None);

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext("Invoice", "Index", "GET", authenticated: true, userId: "user-123", isAjax: true);

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        Assert.NotNull(context.Result);
        var jsonResult = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(403, jsonResult.StatusCode);
    }

    #endregion

    #region Helper Methods

    private static void AssertFilterDecision(string controllerName, string module, string accessLevel, string httpMethod, string actionName, bool expectBlocked)
    {
        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync(It.IsAny<string>())).ReturnsAsync(false);
        mockPlanCheckService.Setup(s => s.GetEffectiveAccessLevelAsync(It.IsAny<string>(), module))
            .ReturnsAsync(accessLevel);

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext(controllerName, actionName, httpMethod, authenticated: true, userId: "user-123");

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        if (expectBlocked)
            Assert.NotNull(context.Result);
        else
            Assert.Null(context.Result);
    }

    private static AuthorizationFilterContext CreateFilterContext(
        string controllerName,
        string actionName,
        string httpMethod,
        bool authenticated,
        string? userId = null,
        bool isAjax = false,
        string? demoInvitationId = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = httpMethod;

        if (isAjax)
        {
            httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        }

        var claims = new List<Claim>();
        if (authenticated && userId != null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }
        if (demoInvitationId != null)
        {
            claims.Add(new Claim("DemoInvitationId", demoInvitationId));
        }

        var identity = new ClaimsIdentity(authenticated ? claims : null, authenticated ? "TestAuth" : null);
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
