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

// Feature: subscription-permission-gating, Property 3: Demo sessions bypass permission filters

/// <summary>
/// Property-based tests verifying that demo sessions (identified by DemoInvitationId claim)
/// bypass both the PlanPermissionFilter and UserPermissionFilter checks.
/// Since both filters check for DemoInvitationId as their FIRST operation and return early,
/// we verify this bypass behavior is consistent regardless of the controller being accessed.
/// **Validates: Requirements 3.3, 4.5**
/// </summary>
public class DemoSessionBypassPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// All controllers from ModuleControllerMap that normally WOULD trigger a plan/user check.
    /// </summary>
    private static readonly string[] AllMappedControllers =
        ModuleControllerMap.Map.Values.SelectMany(v => v).ToArray();

    /// <summary>
    /// Controllers exempt from plan checks (non-module controllers).
    /// </summary>
    private static readonly string[] ExemptControllers =
        { "Home", "Account", "Demo", "Admin", "MyBusiness", "Billing", "SetupWizard", "Dashboard" };

    /// <summary>
    /// Combined pool of all controllers — both module and non-module.
    /// Demo sessions should bypass regardless of which type of controller is accessed.
    /// </summary>
    private static readonly string[] AllControllers =
        AllMappedControllers.Concat(ExemptControllers).ToArray();

    /// <summary>
    /// Creates an AuthorizationFilterContext with the specified DemoInvitationId claim,
    /// controller name in RouteData, and HTTP method.
    /// </summary>
    private static AuthorizationFilterContext CreateFilterContext(
        string demoInvitationIdValue,
        string controllerName,
        string httpMethod,
        bool isAuthenticated = true)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = httpMethod;

        var claims = new List<Claim>
        {
            new Claim("DemoInvitationId", demoInvitationIdValue),
            new Claim("IsDemoSession", "true")
        };

        if (isAuthenticated)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }
        else
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        }

        var routeData = new RouteData();
        routeData.Values["controller"] = controllerName;

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        var filters = new List<IFilterMetadata>();

        return new AuthorizationFilterContext(actionContext, filters);
    }

    /// <summary>
    /// Creates a PlanPermissionFilter with a mocked IPlanCheckService.
    /// The mock is configured to THROW if called — proving the filter bypasses entirely.
    /// </summary>
    private static PlanPermissionFilter CreatePlanFilter()
    {
        var mockService = new Mock<IPlanCheckService>();

        // Configure service to throw if any method is called — proves bypass
        mockService
            .Setup(s => s.HasActiveSubscriptionAsync())
            .ThrowsAsync(new InvalidOperationException("PlanCheckService should NOT be called for demo sessions"));
        mockService
            .Setup(s => s.IsModuleInPlanAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("PlanCheckService should NOT be called for demo sessions"));
        mockService
            .Setup(s => s.GetPlanModulesAsync())
            .ThrowsAsync(new InvalidOperationException("PlanCheckService should NOT be called for demo sessions"));

        return new PlanPermissionFilter(mockService.Object);
    }

    /// <summary>
    /// Creates a UserPermissionFilter with a mocked IPlanCheckService.
    /// The mock is configured to THROW if called — proving the filter bypasses entirely.
    /// </summary>
    private static UserPermissionFilter CreateUserFilter()
    {
        var mockService = new Mock<IPlanCheckService>();

        // Configure service to throw if any method is called — proves bypass
        mockService
            .Setup(s => s.IsOwnerAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("PlanCheckService should NOT be called for demo sessions"));
        mockService
            .Setup(s => s.GetEffectiveAccessLevelAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("PlanCheckService should NOT be called for demo sessions"));

        return new UserPermissionFilter(mockService.Object);
    }

    #endregion

    #region Property 3a: PlanPermissionFilter bypasses for any controller when DemoInvitationId is present

    /// <summary>
    /// Property 3a: For any module controller and any DemoInvitationId claim value,
    /// the PlanPermissionFilter SHALL skip its checks entirely (context.Result remains null).
    /// The service mock will throw if called, proving the bypass is complete.
    /// **Validates: Requirements 3.3, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlanFilter_BypassesForDemoSession_AnyModuleController(PositiveInt controllerSeed, PositiveInt claimSeed)
    {
        var controller = AllMappedControllers[controllerSeed.Get % AllMappedControllers.Length];
        var demoInvitationId = ((claimSeed.Get % 10000) + 1).ToString();

        var filter = CreatePlanFilter();
        var context = CreateFilterContext(demoInvitationId, controller, "GET");

        // Act — should NOT throw despite the mock being configured to throw on service calls
        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        // Assert: Result should be null (filter returned early without checking plan)
        var isAllowed = context.Result == null;

        return isAllowed.ToProperty()
            .Label($"PlanFilter should bypass for demo session — controller='{controller}', demoId='{demoInvitationId}', result={context.Result?.GetType().Name}");
    }

    #endregion

    #region Property 3b: UserPermissionFilter bypasses for any controller when DemoInvitationId is present

    /// <summary>
    /// Property 3b: For any module controller and any DemoInvitationId claim value,
    /// the UserPermissionFilter SHALL skip its checks entirely (context.Result remains null).
    /// The service mock will throw if called, proving the bypass is complete.
    /// **Validates: Requirements 3.3, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UserFilter_BypassesForDemoSession_AnyModuleController(PositiveInt controllerSeed, PositiveInt claimSeed)
    {
        var controller = AllMappedControllers[controllerSeed.Get % AllMappedControllers.Length];
        var demoInvitationId = ((claimSeed.Get % 10000) + 1).ToString();

        var filter = CreateUserFilter();
        var context = CreateFilterContext(demoInvitationId, controller, "POST");

        // Act — should NOT throw despite the mock being configured to throw on service calls
        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        // Assert: Result should be null (filter returned early without checking user permissions)
        var isAllowed = context.Result == null;

        return isAllowed.ToProperty()
            .Label($"UserFilter should bypass for demo session — controller='{controller}', demoId='{demoInvitationId}', result={context.Result?.GetType().Name}");
    }

    #endregion

    #region Property 3c: Both filters bypass regardless of HTTP method during demo sessions

    /// <summary>
    /// Property 3c: For any HTTP method (GET, POST, PUT, DELETE, PATCH) and any module controller,
    /// both filters SHALL bypass when a DemoInvitationId claim is present.
    /// This is critical because without the bypass, a POST to a readonly module would be blocked.
    /// **Validates: Requirements 3.3, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BothFilters_BypassForDemoSession_RegardlessOfHttpMethod(PositiveInt controllerSeed, PositiveInt methodSeed)
    {
        var httpMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH" };
        var controller = AllMappedControllers[controllerSeed.Get % AllMappedControllers.Length];
        var httpMethod = httpMethods[methodSeed.Get % httpMethods.Length];
        var demoInvitationId = "42"; // Any valid value

        var planFilter = CreatePlanFilter();
        var userFilter = CreateUserFilter();
        var planContext = CreateFilterContext(demoInvitationId, controller, httpMethod);
        var userContext = CreateFilterContext(demoInvitationId, controller, httpMethod);

        // Act
        planFilter.OnAuthorizationAsync(planContext).GetAwaiter().GetResult();
        userFilter.OnAuthorizationAsync(userContext).GetAwaiter().GetResult();

        // Assert: Both filters should allow through (null result)
        var planAllowed = planContext.Result == null;
        var userAllowed = userContext.Result == null;

        return (planAllowed && userAllowed).ToProperty()
            .Label($"Both filters should bypass — controller='{controller}', method='{httpMethod}', planResult={planContext.Result?.GetType().Name}, userResult={userContext.Result?.GetType().Name}");
    }

    #endregion

    #region Property 3d: DemoInvitationId claim value is irrelevant — any non-null value triggers bypass

    /// <summary>
    /// Property 3d: The filters check for the PRESENCE of the DemoInvitationId claim, not its value.
    /// For any non-empty string claim value, both filters SHALL bypass.
    /// **Validates: Requirements 3.3, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DemoBypass_TriggeredByClaimPresence_NotValue(NonEmptyString claimValue, PositiveInt controllerSeed)
    {
        var controller = AllMappedControllers[controllerSeed.Get % AllMappedControllers.Length];

        var planFilter = CreatePlanFilter();
        var userFilter = CreateUserFilter();
        var planContext = CreateFilterContext(claimValue.Get, controller, "GET");
        var userContext = CreateFilterContext(claimValue.Get, controller, "POST");

        // Act
        planFilter.OnAuthorizationAsync(planContext).GetAwaiter().GetResult();
        userFilter.OnAuthorizationAsync(userContext).GetAwaiter().GetResult();

        // Assert: Both should bypass regardless of claim value
        var planAllowed = planContext.Result == null;
        var userAllowed = userContext.Result == null;

        return (planAllowed && userAllowed).ToProperty()
            .Label($"Bypass should trigger for any claim value='{claimValue.Get}', controller='{controller}'");
    }

    #endregion
}
