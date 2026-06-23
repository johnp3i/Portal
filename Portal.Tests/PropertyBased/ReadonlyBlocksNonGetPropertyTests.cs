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

// Feature: subscription-permission-gating, Property 5: Readonly blocks non-GET requests

/// <summary>
/// Property-based tests for readonly access enforcement logic.
/// For any user with 'readonly' access level on a module, non-GET HTTP requests
/// (excluding data-fetch actions starting with "Get" or "AxGet") SHALL be blocked,
/// while GET requests SHALL be allowed.
/// **Validates: Requirements 4.3**
/// </summary>
public class ReadonlyBlocksNonGetPropertyTests
{
    private static readonly string[] HttpMethods = { "GET", "POST", "PUT", "DELETE", "PATCH" };
    private static readonly string[] NonGetMethods = { "POST", "PUT", "DELETE", "PATCH" };
    private static readonly string[] RegularActions = { "Create", "Edit", "Delete", "Save", "Update", "Submit", "Approve" };
    private static readonly string[] DataFetchActions = { "GetDetails", "GetList", "AxGetItems", "GetCustomerData", "AxGetReport" };

    #region Property 5: Readonly access blocks non-GET requests

    /// <summary>
    /// Property 5a: With readonly access, GET requests are always allowed regardless of action name.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReadonlyAccess_GetRequest_AlwaysAllowed(PositiveInt actionSeed)
    {
        var allActions = RegularActions.Concat(DataFetchActions).ToArray();
        var actionName = allActions[actionSeed.Get % allActions.Length];

        var context = ExecuteFilter("Invoice", actionName, "GET", AccessLevels.ReadOnly);

        var isAllowed = context.Result == null;

        return isAllowed.ToProperty()
            .Label($"GET request with action='{actionName}' and readonly access → should be allowed");
    }

    /// <summary>
    /// Property 5b: With readonly access, non-GET requests with regular action names are blocked.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReadonlyAccess_NonGetRegularAction_IsBlocked(PositiveInt methodSeed, PositiveInt actionSeed)
    {
        var httpMethod = NonGetMethods[methodSeed.Get % NonGetMethods.Length];
        var actionName = RegularActions[actionSeed.Get % RegularActions.Length];

        var context = ExecuteFilter("Invoice", actionName, httpMethod, AccessLevels.ReadOnly);

        var isBlocked = context.Result != null;

        return isBlocked.ToProperty()
            .Label($"Method='{httpMethod}', Action='{actionName}', AccessLevel='readonly' → should be blocked");
    }

    /// <summary>
    /// Property 5c: With readonly access, non-GET requests with "Get"-prefixed actions are allowed
    /// (these are data-fetch endpoints that happen to use POST).
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReadonlyAccess_NonGetDataFetchAction_IsAllowed(PositiveInt methodSeed, PositiveInt actionSeed)
    {
        var httpMethod = NonGetMethods[methodSeed.Get % NonGetMethods.Length];
        var actionName = DataFetchActions[actionSeed.Get % DataFetchActions.Length];

        var context = ExecuteFilter("Invoice", actionName, httpMethod, AccessLevels.ReadOnly);

        var isAllowed = context.Result == null;

        return isAllowed.ToProperty()
            .Label($"Method='{httpMethod}', Action='{actionName}', AccessLevel='readonly' → data-fetch should be allowed");
    }

    /// <summary>
    /// Property 5d: For random HTTP methods and action names, the readonly enforcement rule holds:
    /// - GET always allowed
    /// - Non-GET + "Get"/"AxGet" prefixed action → allowed
    /// - Non-GET + other action → blocked
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReadonlyEnforcement_FollowsRule(PositiveInt methodSeed, PositiveInt actionSeed)
    {
        var httpMethod = HttpMethods[methodSeed.Get % HttpMethods.Length];
        var allActions = RegularActions.Concat(DataFetchActions).ToArray();
        var actionName = allActions[actionSeed.Get % allActions.Length];

        var context = ExecuteFilter("Invoice", actionName, httpMethod, AccessLevels.ReadOnly);

        var isBlocked = context.Result != null;

        // Expected behavior based on the filter logic:
        bool shouldBlock;
        if (httpMethod == "GET")
        {
            shouldBlock = false; // GET always allowed
        }
        else if (actionName.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                 actionName.StartsWith("AxGet", StringComparison.OrdinalIgnoreCase))
        {
            shouldBlock = false; // Data-fetch actions allowed even on non-GET
        }
        else
        {
            shouldBlock = true; // Non-GET with regular action → blocked
        }

        return (isBlocked == shouldBlock).ToProperty()
            .Label($"Method='{httpMethod}', Action='{actionName}', ExpectedBlock={shouldBlock}, ActualBlock={isBlocked}");
    }

    #endregion

    #region Exhaustive verification

    /// <summary>
    /// Exhaustive test: each HTTP method × action type combination with readonly access.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Fact]
    public void ReadonlyAccess_AllMethodActionCombinations_FollowRule()
    {
        // GET + any action → allowed
        foreach (var action in RegularActions.Concat(DataFetchActions))
        {
            var ctx = ExecuteFilter("Invoice", action, "GET", AccessLevels.ReadOnly);
            Assert.Null(ctx.Result);
        }

        // Non-GET + regular action → blocked
        foreach (var method in NonGetMethods)
        {
            foreach (var action in RegularActions)
            {
                var ctx = ExecuteFilter("Invoice", action, method, AccessLevels.ReadOnly);
                Assert.NotNull(ctx.Result);
            }
        }

        // Non-GET + data-fetch action → allowed
        foreach (var method in NonGetMethods)
        {
            foreach (var action in DataFetchActions)
            {
                var ctx = ExecuteFilter("Invoice", action, method, AccessLevels.ReadOnly);
                Assert.Null(ctx.Result);
            }
        }
    }

    /// <summary>
    /// Verifies that when a non-GET request is blocked for readonly users via AJAX,
    /// the response is a JSON 403.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Fact]
    public void ReadonlyAccess_NonGetBlocked_AjaxReturns403Json()
    {
        var context = ExecuteFilter("Invoice", "Create", "POST", AccessLevels.ReadOnly, isAjax: true);

        Assert.NotNull(context.Result);
        var jsonResult = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(403, jsonResult.StatusCode);
    }

    /// <summary>
    /// Verifies that full access does NOT block any HTTP method.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Fact]
    public void FullAccess_NeverBlocked_AnyMethod()
    {
        foreach (var method in HttpMethods)
        {
            var ctx = ExecuteFilter("Invoice", "Create", method, AccessLevels.Full);
            Assert.Null(ctx.Result);
        }
    }

    #endregion

    #region Helper Methods

    private static AuthorizationFilterContext ExecuteFilter(
        string controllerName,
        string actionName,
        string httpMethod,
        string accessLevel,
        bool isAjax = false)
    {
        var module = ModuleControllerMap.ResolveModule(controllerName)!;

        var mockPlanCheckService = new Mock<IPlanCheckService>();
        mockPlanCheckService.Setup(s => s.IsOwnerAsync(It.IsAny<string>())).ReturnsAsync(false);
        mockPlanCheckService.Setup(s => s.GetEffectiveAccessLevelAsync(It.IsAny<string>(), module))
            .ReturnsAsync(accessLevel);

        var filter = new UserPermissionFilter(mockPlanCheckService.Object);
        var context = CreateFilterContext(controllerName, actionName, httpMethod, isAjax: isAjax);

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        return context;
    }

    private static AuthorizationFilterContext CreateFilterContext(
        string controllerName,
        string actionName,
        string httpMethod,
        bool isAjax = false)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = httpMethod;

        if (isAjax)
        {
            httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-readonly-test")
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
