using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Filters;
using Xunit;

namespace Portal.Tests.Unit.Filters;

/// <summary>
/// Unit tests for DemoPermissionFilter authorization enforcement.
/// Validates Requirements 14.1, 14.2, 14.3, 14.4.
/// </summary>
public class DemoPermissionFilterTests
{
    private const int TestInvitationId = 42;

    private readonly Mock<IDemoInvitationService> _demoServiceMock;
    private readonly DemoPermissionFilter _filter;

    public DemoPermissionFilterTests()
    {
        _demoServiceMock = new Mock<IDemoInvitationService>();
        _filter = new DemoPermissionFilter(_demoServiceMock.Object);
    }

    #region Helpers

    private static AuthorizationFilterContext CreateFilterContext(
        string controllerName,
        string httpMethod,
        ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = httpMethod;

        if (user != null)
            httpContext.User = user;

        var routeData = new RouteData();
        routeData.Values["controller"] = controllerName;

        var actionDescriptor = new ActionDescriptor();

        var actionContext = new ActionContext(httpContext, routeData, actionDescriptor);

        var filterContext = new AuthorizationFilterContext(
            actionContext,
            new List<IFilterMetadata>());

        return filterContext;
    }

    private static ClaimsPrincipal CreateDemoUser(int invitationId)
    {
        var claims = new List<Claim>
        {
            new Claim("DemoInvitationId", invitationId.ToString()),
            new Claim("IsDemoSession", "true"),
            new Claim("BusinessId", "1000")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateNonDemoUser()
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "regular-user-001"),
            new Claim(ClaimTypes.Email, "user@example.com")
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private void SetupPermissions(Dictionary<string, string> permissions)
    {
        _demoServiceMock
            .Setup(s => s.GetPermissionsForInvitationAsync(TestInvitationId))
            .ReturnsAsync(permissions);
    }

    #endregion

    #region Test 1: Non-demo user (no DemoInvitationId claim) → allows through

    [Fact]
    public async Task OnAuthorizationAsync_NonDemoUser_AllowsThrough()
    {
        // Arrange
        var user = CreateNonDemoUser();
        var context = CreateFilterContext("Invoice", "GET", user);

        // Act
        await _filter.OnAuthorizationAsync(context);

        // Assert — context.Result remains null (no short-circuit)
        Assert.Null(context.Result);
        _demoServiceMock.Verify(
            s => s.GetPermissionsForInvitationAsync(It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region Test 2: Demo user accessing non-module controller → allows through

    [Fact]
    public async Task OnAuthorizationAsync_DemoUserAccessingNonModuleController_AllowsThrough()
    {
        // Arrange — "Home" is not mapped to any module
        var user = CreateDemoUser(TestInvitationId);
        var context = CreateFilterContext("Home", "GET", user);

        // Act
        await _filter.OnAuthorizationAsync(context);

        // Assert — allows through without calling service
        Assert.Null(context.Result);
        _demoServiceMock.Verify(
            s => s.GetPermissionsForInvitationAsync(It.IsAny<int>()), Times.Never);
    }

    #endregion

    #region Test 3: Demo user accessing module with 'none' permission → DemoAccessRestricted view

    [Fact]
    public async Task OnAuthorizationAsync_DemoUserModuleWithNonePermission_ReturnsDemoAccessRestricted()
    {
        // Arrange
        var user = CreateDemoUser(TestInvitationId);
        var context = CreateFilterContext("Invoice", "GET", user);

        SetupPermissions(new Dictionary<string, string>
        {
            [PortalModules.Invoice] = AccessLevels.None
        });

        // Act
        await _filter.OnAuthorizationAsync(context);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(context.Result);
        Assert.Equal("DemoAccessRestricted", viewResult.ViewName);
    }

    #endregion

    #region Test 4: Demo user accessing module with no permission entry → DemoAccessRestricted view

    [Fact]
    public async Task OnAuthorizationAsync_DemoUserModuleWithNoPermissionEntry_ReturnsDemoAccessRestricted()
    {
        // Arrange — permissions dict has entries for other modules but NOT invoice
        var user = CreateDemoUser(TestInvitationId);
        var context = CreateFilterContext("Invoice", "GET", user);

        SetupPermissions(new Dictionary<string, string>
        {
            [PortalModules.Customer] = AccessLevels.Full
        });

        // Act
        await _filter.OnAuthorizationAsync(context);

        // Assert
        var viewResult = Assert.IsType<ViewResult>(context.Result);
        Assert.Equal("DemoAccessRestricted", viewResult.ViewName);
    }

    #endregion

    #region Test 5: Demo user with 'readonly' on a module + GET request → allows through

    [Fact]
    public async Task OnAuthorizationAsync_ReadonlyModuleGetRequest_AllowsThrough()
    {
        // Arrange
        var user = CreateDemoUser(TestInvitationId);
        var context = CreateFilterContext("Invoice", "GET", user);

        SetupPermissions(new Dictionary<string, string>
        {
            [PortalModules.Invoice] = AccessLevels.ReadOnly
        });

        // Act
        await _filter.OnAuthorizationAsync(context);

        // Assert
        Assert.Null(context.Result);
    }

    #endregion

    #region Test 6: Demo user with 'readonly' on a module + POST request → 403 JSON

    [Fact]
    public async Task OnAuthorizationAsync_ReadonlyModulePostRequest_Returns403Json()
    {
        // Arrange
        var user = CreateDemoUser(TestInvitationId);
        var context = CreateFilterContext("Invoice", "POST", user);

        SetupPermissions(new Dictionary<string, string>
        {
            [PortalModules.Invoice] = AccessLevels.ReadOnly
        });

        // Act
        await _filter.OnAuthorizationAsync(context);

        // Assert
        var jsonResult = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(403, jsonResult.StatusCode);
    }

    #endregion

    #region Test 7: Demo user with 'full' on a module + POST request → allows through

    [Fact]
    public async Task OnAuthorizationAsync_FullModulePostRequest_AllowsThrough()
    {
        // Arrange
        var user = CreateDemoUser(TestInvitationId);
        var context = CreateFilterContext("Invoice", "POST", user);

        SetupPermissions(new Dictionary<string, string>
        {
            [PortalModules.Invoice] = AccessLevels.Full
        });

        // Act
        await _filter.OnAuthorizationAsync(context);

        // Assert
        Assert.Null(context.Result);
    }

    #endregion

    #region Test 8: Demo user with 'full' on a module + GET request → allows through

    [Fact]
    public async Task OnAuthorizationAsync_FullModuleGetRequest_AllowsThrough()
    {
        // Arrange
        var user = CreateDemoUser(TestInvitationId);
        var context = CreateFilterContext("Invoice", "GET", user);

        SetupPermissions(new Dictionary<string, string>
        {
            [PortalModules.Invoice] = AccessLevels.Full
        });

        // Act
        await _filter.OnAuthorizationAsync(context);

        // Assert
        Assert.Null(context.Result);
    }

    #endregion
}
