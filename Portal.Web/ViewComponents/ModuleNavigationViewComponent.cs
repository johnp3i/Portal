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
    private readonly IPlanCheckService _planCheckService;

    public ModuleNavigationViewComponent(
        IPermissionService permissionService,
        ICurrentTenantService currentTenantService,
        IBusinessService businessService,
        IPlanCheckService planCheckService)
    {
        _permissionService = permissionService;
        _currentTenantService = currentTenantService;
        _businessService = businessService;
        _planCheckService = planCheckService;
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
            // Check if user is the business owner
            var isOwner = await _planCheckService.IsOwnerAsync(userId);

            if (isOwner)
            {
                // Owner sees all modules included in their plan with full access
                var planModules = await _planCheckService.GetPlanModulesAsync();
                permissions = planModules.ToDictionary(m => m, _ => AccessLevels.Full);
            }
            else
            {
                // Team members see only explicitly granted modules
                permissions = await _permissionService.GetAllAccessLevelsAsync(userId);
            }
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
