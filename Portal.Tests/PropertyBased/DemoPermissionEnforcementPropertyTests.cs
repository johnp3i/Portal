using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Filters;
using System.Security.Claims;
using Xunit;

namespace Portal.Tests.PropertyBased;

// Feature: demo-access-invitations, Property 5: Permission enforcement

/// <summary>
/// Property-based tests for DemoPermissionFilter.OnAuthorizationAsync permission enforcement.
/// Validates that for any DemoInvitationId claim and controller mapped to a module:
/// 'none' → denied, 'readonly' → GET allowed/non-GET blocked, 'full' → all allowed.
/// **Validates: Requirements 8.5, 14.1, 14.2, 14.3, 14.4**
/// </summary>
public class DemoPermissionEnforcementPropertyTests
{
    #region Test Infrastructure

    /// <summary>
    /// All module names from PortalModules that the filter recognizes.
    /// Only modules that have controller mappings in ModuleControllerMap are testable here.
    /// </summary>
    private static readonly string[] AllModules = new[]
    {
        PortalModules.Customer, PortalModules.Quotation, PortalModules.Invoice,
        PortalModules.Revenue, PortalModules.Purchase, PortalModules.Vat,
        PortalModules.Credit, PortalModules.Audit, PortalModules.Products,
        PortalModules.Cashflow, PortalModules.Pnl, PortalModules.ExpenseInsights,
        PortalModules.Attachments, PortalModules.ClientPortal, PortalModules.ActivityTimeline,
        PortalModules.Api, PortalModules.Webhooks, PortalModules.MultiCurrency
    };

    /// <summary>
    /// Maps each module to one representative controller name that belongs to it.
    /// Used to create RouteData that the filter will resolve to the module.
    /// </summary>
    private static readonly Dictionary<string, string> ModuleToController = new()
    {
        [PortalModules.Customer] = "Customer",
        [PortalModules.Quotation] = "Quotation",
        [PortalModules.Invoice] = "Invoice",
        [PortalModules.Revenue] = "Payment",
        [PortalModules.Purchase] = "Purchase",
        [PortalModules.Vat] = "Vat",
        [PortalModules.Credit] = "CreditNote",
        [PortalModules.Audit] = "Audit",
        [PortalModules.Products] = "Product",
        [PortalModules.Cashflow] = "Cashflow",
        [PortalModules.Pnl] = "ProfitLoss",
        [PortalModules.ExpenseInsights] = "ExpenseInsight",
        [PortalModules.Attachments] = "Attachment",
        [PortalModules.ClientPortal] = "ClientPortal",
        [PortalModules.ActivityTimeline] = "ActivityTimeline",
        [PortalModules.Api] = "Api",
        [PortalModules.Webhooks] = "Webhook",
        [PortalModules.MultiCurrency] = "MultiCurrency"
    };

    /// <summary>
    /// HTTP methods considered non-GET (write operations).
    /// </summary>
    private static readonly string[] NonGetMethods = { "POST", "PUT", "DELETE", "PATCH" };

    /// <summary>
    /// Creates an AuthorizationFilterContext with the specified DemoInvitationId claim,
    /// controller name in RouteData, and HTTP method.
    /// </summary>
    private static AuthorizationFilterContext CreateFilterContext(
        int demoInvitationId,
        string controllerName,
        string httpMethod)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = httpMethod;

        var claims = new List<Claim>
        {
            new Claim("DemoInvitationId", demoInvitationId.ToString()),
            new Claim("IsDemoSession", "true")
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        var routeData = new RouteData();
        routeData.Values["controller"] = controllerName;

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        var filters = new List<IFilterMetadata>();

        return new AuthorizationFilterContext(actionContext, filters);
    }

    /// <summary>
    /// Creates the DemoPermissionFilter with a mocked IDemoInvitationService that returns
    /// the specified permissions dictionary for the given invitation ID.
    /// </summary>
    private static DemoPermissionFilter CreateFilter(
        int invitationId,
        Dictionary<string, string> permissions)
    {
        var mockService = new Mock<IDemoInvitationService>();
        mockService
            .Setup(s => s.GetPermissionsForInvitationAsync(invitationId))
            .ReturnsAsync(permissions);

        return new DemoPermissionFilter(mockService.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    /// <summary>
    /// FsCheck generator for a random module name from PortalModules.All.
    /// </summary>
    private static Gen<string> GenModule => Gen.Elements(AllModules);

    /// <summary>
    /// FsCheck generator for a random non-GET HTTP method.
    /// </summary>
    private static Gen<string> GenNonGetMethod => Gen.Elements(NonGetMethods);

    #endregion

    #region Property 5a: 'none' access level → denied (DemoAccessRestricted view)

    /// <summary>
    /// Property 5a: When the module's access level is 'none', the filter SHALL deny access
    /// by setting context.Result to a ViewResult with ViewName "DemoAccessRestricted",
    /// regardless of the HTTP method used.
    /// **Validates: Requirements 8.5, 14.1, 14.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoneAccessLevel_DeniesAccess_ReturnsRestrictedView()
    {
        var property = Prop.ForAll(
            GenModule.ToArbitrary(),
            Arb.From<PositiveInt>(),
            Gen.Elements("GET", "POST", "PUT", "DELETE").ToArbitrary(),
            (module, invitationIdSeed, httpMethod) =>
            {
                var invitationId = (invitationIdSeed.Get % 10000) + 1;
                var controllerName = ModuleToController[module];
                var permissions = new Dictionary<string, string>
                {
                    [module] = AccessLevels.None
                };

                var filter = CreateFilter(invitationId, permissions);
                var context = CreateFilterContext(invitationId, controllerName, httpMethod);

                // Act
                filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

                // Assert: Result should be a ViewResult with "DemoAccessRestricted"
                var isViewResult = context.Result is ViewResult;
                var viewName = (context.Result as ViewResult)?.ViewName;
                var isDenied = isViewResult && viewName == "DemoAccessRestricted";

                return isDenied.ToProperty()
                    .Label($"module={module}, controller={controllerName}, method={httpMethod}, " +
                           $"invitationId={invitationId}, resultType={context.Result?.GetType().Name}, " +
                           $"viewName={viewName}");
            });

        return property;
    }

    #endregion

    #region Property 5b: Missing permission entry → denied (DemoAccessRestricted view)

    /// <summary>
    /// Property 5b: When the module has NO permission entry in the dictionary, the filter SHALL deny access
    /// by setting context.Result to a ViewResult with ViewName "DemoAccessRestricted".
    /// **Validates: Requirements 8.5, 14.1, 14.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MissingPermissionEntry_DeniesAccess_ReturnsRestrictedView()
    {
        var property = Prop.ForAll(
            GenModule.ToArbitrary(),
            Arb.From<PositiveInt>(),
            Gen.Elements("GET", "POST", "PUT", "DELETE").ToArbitrary(),
            (module, invitationIdSeed, httpMethod) =>
            {
                var invitationId = (invitationIdSeed.Get % 10000) + 1;
                var controllerName = ModuleToController[module];

                // Empty permissions dictionary — no entry for this module
                var permissions = new Dictionary<string, string>();

                var filter = CreateFilter(invitationId, permissions);
                var context = CreateFilterContext(invitationId, controllerName, httpMethod);

                // Act
                filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

                // Assert: Result should be a ViewResult with "DemoAccessRestricted"
                var isViewResult = context.Result is ViewResult;
                var viewName = (context.Result as ViewResult)?.ViewName;
                var isDenied = isViewResult && viewName == "DemoAccessRestricted";

                return isDenied.ToProperty()
                    .Label($"module={module}, controller={controllerName}, method={httpMethod}, " +
                           $"invitationId={invitationId}, resultType={context.Result?.GetType().Name}, " +
                           $"viewName={viewName}");
            });

        return property;
    }

    #endregion

    #region Property 5c: 'readonly' + GET → allowed (context.Result is null)

    /// <summary>
    /// Property 5c: When the module's access level is 'readonly' and the HTTP method is GET,
    /// the filter SHALL allow the request through (context.Result remains null).
    /// **Validates: Requirements 14.1, 14.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReadOnlyAccessLevel_GetRequest_AllowsThrough()
    {
        var property = Prop.ForAll(
            GenModule.ToArbitrary(),
            Arb.From<PositiveInt>(),
            (module, invitationIdSeed) =>
            {
                var invitationId = (invitationIdSeed.Get % 10000) + 1;
                var controllerName = ModuleToController[module];
                var permissions = new Dictionary<string, string>
                {
                    [module] = AccessLevels.ReadOnly
                };

                var filter = CreateFilter(invitationId, permissions);
                var context = CreateFilterContext(invitationId, controllerName, "GET");

                // Act
                filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

                // Assert: Result should be null (allowed through)
                var isAllowed = context.Result == null;

                return isAllowed.ToProperty()
                    .Label($"module={module}, controller={controllerName}, method=GET, " +
                           $"invitationId={invitationId}, resultType={context.Result?.GetType().Name}");
            });

        return property;
    }

    #endregion

    #region Property 5d: 'readonly' + non-GET → blocked (403 JsonResult)

    /// <summary>
    /// Property 5d: When the module's access level is 'readonly' and the HTTP method is NOT GET
    /// (POST, PUT, DELETE, PATCH), the filter SHALL block the request with a JsonResult
    /// having StatusCode 403.
    /// **Validates: Requirements 14.1, 14.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReadOnlyAccessLevel_NonGetRequest_Returns403Json()
    {
        var property = Prop.ForAll(
            GenModule.ToArbitrary(),
            Arb.From<PositiveInt>(),
            GenNonGetMethod.ToArbitrary(),
            (module, invitationIdSeed, httpMethod) =>
            {
                var invitationId = (invitationIdSeed.Get % 10000) + 1;
                var controllerName = ModuleToController[module];
                var permissions = new Dictionary<string, string>
                {
                    [module] = AccessLevels.ReadOnly
                };

                var filter = CreateFilter(invitationId, permissions);
                var context = CreateFilterContext(invitationId, controllerName, httpMethod);
                // Set AJAX header so the filter returns JsonResult instead of ViewResult
                context.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

                // Act
                filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

                // Assert: Result should be a JsonResult with StatusCode 403
                var isJsonResult = context.Result is JsonResult;
                var statusCode = (context.Result as JsonResult)?.StatusCode;
                var isBlocked = isJsonResult && statusCode == 403;

                return isBlocked.ToProperty()
                    .Label($"module={module}, controller={controllerName}, method={httpMethod}, " +
                           $"invitationId={invitationId}, resultType={context.Result?.GetType().Name}, " +
                           $"statusCode={statusCode}");
            });

        return property;
    }

    #endregion

    #region Property 5e: 'full' access level → all methods allowed (context.Result is null)

    /// <summary>
    /// Property 5e: When the module's access level is 'full', the filter SHALL allow all requests
    /// through regardless of HTTP method (context.Result remains null).
    /// **Validates: Requirements 14.1, 14.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FullAccessLevel_AnyMethod_AllowsThrough()
    {
        var property = Prop.ForAll(
            GenModule.ToArbitrary(),
            Arb.From<PositiveInt>(),
            Gen.Elements("GET", "POST", "PUT", "DELETE", "PATCH").ToArbitrary(),
            (module, invitationIdSeed, httpMethod) =>
            {
                var invitationId = (invitationIdSeed.Get % 10000) + 1;
                var controllerName = ModuleToController[module];
                var permissions = new Dictionary<string, string>
                {
                    [module] = AccessLevels.Full
                };

                var filter = CreateFilter(invitationId, permissions);
                var context = CreateFilterContext(invitationId, controllerName, httpMethod);

                // Act
                filter.OnAuthorizationAsync(context).GetAwaiter().GetResult();

                // Assert: Result should be null (allowed through)
                var isAllowed = context.Result == null;

                return isAllowed.ToProperty()
                    .Label($"module={module}, controller={controllerName}, method={httpMethod}, " +
                           $"invitationId={invitationId}, resultType={context.Result?.GetType().Name}");
            });

        return property;
    }

    #endregion
}
