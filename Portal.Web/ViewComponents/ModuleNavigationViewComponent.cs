using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;

namespace Portal.Web.ViewComponents;

public class ModuleNavigationViewComponent : ViewComponent
{
    private readonly IPermissionService _permissionService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IBusinessService _businessService;

    public ModuleNavigationViewComponent(
        IPermissionService permissionService,
        ICurrentTenantService currentTenantService,
        IBusinessService businessService)
    {
        _permissionService = permissionService;
        _currentTenantService = currentTenantService;
        _businessService = businessService;
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

        // Z-Report feature flag
        var businessId = _currentTenantService.CurrentBusinessId;
        if (businessId > 0)
        {
            var profile = await _businessService.GetBusinessProfileAsync(businessId);
            ViewData["IsZReportEnabled"] = profile?.IsZReportEnabled ?? false;
        }
        else
        {
            ViewData["IsZReportEnabled"] = false;
        }

        return View(permissions);
    }
}
