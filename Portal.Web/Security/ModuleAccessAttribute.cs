using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;

namespace Portal.Web.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ModuleAccessAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Module { get; }
    public string RequiredLevel { get; }

    public ModuleAccessAttribute(string module, string requiredLevel = AccessLevels.ReadOnly)
    {
        Module = module;
        RequiredLevel = requiredLevel;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // SuperAdmin bypasses all checks
        if (user.IsInRole("SuperAdmin"))
            return;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var permissionService = context.HttpContext.RequestServices
            .GetRequiredService<IPermissionService>();

        var accessLevel = await permissionService.GetAccessLevelAsync(userId, Module);

        if (!AccessLevels.MeetsRequirement(accessLevel, RequiredLevel))
        {
            context.Result = new ForbidResult();
        }
    }
}
