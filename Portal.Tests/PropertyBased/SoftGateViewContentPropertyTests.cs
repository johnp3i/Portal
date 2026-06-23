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
using Portal.Web.Models;
using System.Security.Claims;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: subscription-permission-gating, Property 10: Soft-gate view content

/// <summary>
/// Property-based tests verifying that the PlanPermissionFilter's soft-gate view model
/// always contains the module's display name and the name of the lowest plan tier that includes it.
/// **Validates: Requirements 7.1**
/// </summary>
public class SoftGateViewContentPropertyTests
{
    private static readonly string[] ModuleKeys = ModuleControllerMap.Map.Keys.ToArray();
    private static readonly string[] PlanNames = { "Starter", "Professional", "Enterprise" };

    #region Property 10: Soft-gate view contains module identification and required plan

    /// <summary>
    /// Property 10a: When a module is not in the business's plan, the PlanPermissionFilter returns
    /// a ViewResult containing a SoftGateViewModel with non-empty ModuleDisplayName and RequiredPlanName.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BlockedModule_SoftGateContainsModuleDisplayNameAndPlan(PositiveInt moduleSeed, PositiveInt planSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var requiredPlan = PlanNames[planSeed.Get % PlanNames.Length];
        var controllerName = ModuleControllerMap.Map[module][0];

        // Mock: has active subscription BUT module is NOT in plan
        var mockService = new Mock<IPlanCheckService>();
        mockService.Setup(s => s.HasActiveSubscriptionAsync()).ReturnsAsync(true);
        mockService.Setup(s => s.IsModuleInPlanAsync(module)).ReturnsAsync(false);
        mockService.Setup(s => s.GetRequiredPlanForModuleAsync(module)).ReturnsAsync(requiredPlan);

        var filter = new PlanPermissionFilter(mockService.Object);
        var context = CreateFilterContext(controllerName);

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        // The filter should block and set a ViewResult
        var viewResult = context.Result as ViewResult;
        if (viewResult == null)
            return false.ToProperty().Label($"Expected ViewResult but got {context.Result?.GetType().Name ?? "null"}");

        if (viewResult.ViewName != "PlanSoftGate")
            return false.ToProperty().Label($"Expected view 'PlanSoftGate' but got '{viewResult.ViewName}'");

        // Extract the model
        var model = viewResult.ViewData?.Model as SoftGateViewModel;
        if (model == null)
            return false.ToProperty().Label("Expected SoftGateViewModel but model was null");

        var hasModuleName = !string.IsNullOrEmpty(model.ModuleName);
        var hasDisplayName = !string.IsNullOrEmpty(model.ModuleDisplayName);
        var hasRequiredPlan = !string.IsNullOrEmpty(model.RequiredPlanName);

        return (hasModuleName && hasDisplayName && hasRequiredPlan).ToProperty()
            .Label($"Module='{module}', ModuleName='{model.ModuleName}', DisplayName='{model.ModuleDisplayName}', RequiredPlan='{model.RequiredPlanName}'");
    }

    /// <summary>
    /// Property 10b: The ModuleName in the SoftGateViewModel matches the resolved module key.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BlockedModule_SoftGateModuleNameMatchesResolvedKey(PositiveInt moduleSeed, PositiveInt planSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var requiredPlan = PlanNames[planSeed.Get % PlanNames.Length];
        var controllerName = ModuleControllerMap.Map[module][0];

        var mockService = new Mock<IPlanCheckService>();
        mockService.Setup(s => s.HasActiveSubscriptionAsync()).ReturnsAsync(true);
        mockService.Setup(s => s.IsModuleInPlanAsync(module)).ReturnsAsync(false);
        mockService.Setup(s => s.GetRequiredPlanForModuleAsync(module)).ReturnsAsync(requiredPlan);

        var filter = new PlanPermissionFilter(mockService.Object);
        var context = CreateFilterContext(controllerName);

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        var viewResult = context.Result as ViewResult;
        if (viewResult == null)
            return false.ToProperty().Label($"Expected ViewResult but got {context.Result?.GetType().Name ?? "null"}");

        var model = viewResult.ViewData?.Model as SoftGateViewModel;
        if (model == null)
            return false.ToProperty().Label("Expected SoftGateViewModel but model was null");

        return (model.ModuleName == module).ToProperty()
            .Label($"Expected ModuleName='{module}' but got '{model.ModuleName}'");
    }

    /// <summary>
    /// Property 10c: The RequiredPlanName in the SoftGateViewModel matches the value returned
    /// by GetRequiredPlanForModuleAsync — proving the filter passes through the plan service's response.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BlockedModule_RequiredPlanMatchesServiceResponse(PositiveInt moduleSeed, PositiveInt planSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var requiredPlan = PlanNames[planSeed.Get % PlanNames.Length];
        var controllerName = ModuleControllerMap.Map[module][0];

        var mockService = new Mock<IPlanCheckService>();
        mockService.Setup(s => s.HasActiveSubscriptionAsync()).ReturnsAsync(true);
        mockService.Setup(s => s.IsModuleInPlanAsync(module)).ReturnsAsync(false);
        mockService.Setup(s => s.GetRequiredPlanForModuleAsync(module)).ReturnsAsync(requiredPlan);

        var filter = new PlanPermissionFilter(mockService.Object);
        var context = CreateFilterContext(controllerName);

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        var viewResult = context.Result as ViewResult;
        if (viewResult == null)
            return false.ToProperty().Label($"Expected ViewResult but got {context.Result?.GetType().Name ?? "null"}");

        var model = viewResult.ViewData?.Model as SoftGateViewModel;
        if (model == null)
            return false.ToProperty().Label("Expected SoftGateViewModel but model was null");

        return (model.RequiredPlanName == requiredPlan).ToProperty()
            .Label($"Expected RequiredPlanName='{requiredPlan}' but got '{model.RequiredPlanName}'");
    }

    /// <summary>
    /// Property 10d: When GetRequiredPlanForModuleAsync returns null, the filter uses a fallback
    /// string ("a higher plan") rather than leaving RequiredPlanName empty.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BlockedModule_NullPlanFallsBackToDefaultString(PositiveInt moduleSeed)
    {
        var module = ModuleKeys[moduleSeed.Get % ModuleKeys.Length];
        var controllerName = ModuleControllerMap.Map[module][0];

        var mockService = new Mock<IPlanCheckService>();
        mockService.Setup(s => s.HasActiveSubscriptionAsync()).ReturnsAsync(true);
        mockService.Setup(s => s.IsModuleInPlanAsync(module)).ReturnsAsync(false);
        mockService.Setup(s => s.GetRequiredPlanForModuleAsync(module)).ReturnsAsync((string?)null);

        var filter = new PlanPermissionFilter(mockService.Object);
        var context = CreateFilterContext(controllerName);

        filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

        var viewResult = context.Result as ViewResult;
        if (viewResult == null)
            return false.ToProperty().Label($"Expected ViewResult but got {context.Result?.GetType().Name ?? "null"}");

        var model = viewResult.ViewData?.Model as SoftGateViewModel;
        if (model == null)
            return false.ToProperty().Label("Expected SoftGateViewModel but model was null");

        var hasNonEmptyPlan = !string.IsNullOrEmpty(model.RequiredPlanName);

        return hasNonEmptyPlan.ToProperty()
            .Label($"RequiredPlanName should be non-empty when service returns null, got '{model.RequiredPlanName}'");
    }

    #endregion

    #region Helpers

    private static AuthorizationFilterContext CreateFilterContext(string controllerName)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-test-id"),
            new Claim("BusinessId", "1")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        var routeData = new RouteData();
        routeData.Values["controller"] = controllerName;
        routeData.Values["action"] = "Index";

        var actionDescriptor = new ActionDescriptor();
        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);
        var filters = new List<IFilterMetadata>();

        return new AuthorizationFilterContext(actionContext, filters);
    }

    #endregion
}
