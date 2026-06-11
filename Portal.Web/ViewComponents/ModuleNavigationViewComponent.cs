using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;

namespace Portal.Web.ViewComponents;

public class ModuleNavigationViewComponent : ViewComponent
{
    private readonly IPermissionService _permissionService;

    public ModuleNavigationViewComponent(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var isSuperAdmin = UserClaimsPrincipal.IsInRole("SuperAdmin");
        var demoInvitationIdClaim = UserClaimsPrincipal.FindFirstValue("DemoInvitationId");

        Dictionary<string, string> permissions;

        if (isSuperAdmin)
        {
            // SuperAdmin sees everything
            permissions = PortalModules.All.ToDictionary(m => m, _ => AccessLevels.Full);
        }
        else if (!string.IsNullOrEmpty(demoInvitationIdClaim) && int.TryParse(demoInvitationIdClaim, out var invitationId))
        {
            // Demo session — load permissions from DemoInvitationPermission
            permissions = await _permissionService.GetDemoPermissionsAsync(invitationId);
        }
        else if (!string.IsNullOrEmpty(userId))
        {
            permissions = await _permissionService.GetAllAccessLevelsAsync(userId);
        }
        else
        {
            permissions = new Dictionary<string, string>();
        }

        return View(permissions);
    }
}
