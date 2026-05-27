using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Entities.Identity;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Models;
using Serilog;
using System.Security.Claims;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
[Route("Admin/Users")]
public class AdminController : Controller
{
    private readonly IUserAdminService _userAdminService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(
        IUserAdminService userAdminService,
        ICurrentTenantService currentTenantService,
        UserManager<ApplicationUser> userManager)
    {
        _userAdminService = userAdminService;
        _currentTenantService = currentTenantService;
        _userManager = userManager;
    }

    // GET /Admin/Users
    [HttpGet("")]
    public async Task<IActionResult> Index(string? searchTerm, string? statusFilter, int page = 1)
    {
        var filter = new UserAdminFilter
        {
            SearchTerm = searchTerm,
            StatusFilter = statusFilter,
            PageNumber = page,
            PageSize = 20
        };

        var pagedResult = await _userAdminService.GetUsersAsync(filter);

        // Resolve the current user's UserBusinessId so the view can disable self-action buttons
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        ViewBag.CurrentUserId = currentUserId;
        ViewBag.SearchTerm = searchTerm;
        ViewBag.StatusFilter = statusFilter;

        return View(pagedResult);
    }

    // GET /Admin/Users/ModuleAccess/{userBusinessId}
    [HttpGet("ModuleAccess/{userBusinessId:int}")]
    public async Task<IActionResult> ModuleAccess(int userBusinessId)
    {
        var permissions = await _userAdminService.GetUserPermissionsAsync(userBusinessId);

        // Load user info for the heading
        var userBusiness = await _userAdminService.GetUsersAsync(new UserAdminFilter { PageSize = 1000 });
        var user = userBusiness.Items.FirstOrDefault(u => u.UserBusinessId == userBusinessId);

        ViewBag.UserBusinessId = userBusinessId;
        ViewBag.UserFullName = user?.FullName ?? "Unknown User";
        ViewBag.UserEmail = user?.Email ?? string.Empty;
        ViewBag.UserIsActive = user?.IsActive ?? false;
        ViewBag.Modules = PortalModules.All;
        ViewBag.CurrentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return View(permissions);
    }

    // POST /Admin/Users/UpdatePermission
    [HttpPost("UpdatePermission")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePermission([FromBody] UpdatePermissionRequest request)
    {
        try
        {
            if (!PortalModules.IsValid(request.Module))
                return Json(new { success = false, message = $"Invalid module: '{request.Module}'." });

            if (!AccessLevels.IsValid(request.AccessLevel))
                return Json(new { success = false, message = $"Invalid access level: '{request.AccessLevel}'." });

            var performedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _userAdminService.UpdatePermissionAsync(
                request.UserBusinessId, request.Module, request.AccessLevel, performedByUserId);

            return Json(new { success = result.Success, message = result.Success
                ? $"Access level for '{request.Module}' updated to '{request.AccessLevel}'."
                : result.Message });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating permission for UserBusinessId={UserBusinessId}, Module={Module}",
                request.UserBusinessId, request.Module);
            return Json(new { success = false, message = "The permission could not be updated. Please try again." });
        }
    }

    // POST /Admin/Users/ToggleStatus
    [HttpPost("ToggleStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus([FromBody] ToggleStatusRequest request)
    {
        try
        {
            var performedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            // Guard against self-deactivation
            if (!request.Activate)
            {
                // Resolve the current user's UserBusinessId
                var allUsers = await _userAdminService.GetUsersAsync(new UserAdminFilter { PageSize = 1000 });
                var currentUserRecord = allUsers.Items.FirstOrDefault(u => u.UserId == performedByUserId);
                if (currentUserRecord != null && currentUserRecord.UserBusinessId == request.UserBusinessId)
                {
                    return Json(new { success = false, message = "You cannot deactivate your own account." });
                }
            }

            ServiceResult result;
            if (request.Activate)
            {
                result = await _userAdminService.ReactivateUserAsync(request.UserBusinessId, performedByUserId);
            }
            else
            {
                result = await _userAdminService.DeactivateUserAsync(request.UserBusinessId, performedByUserId);
            }

            return Json(new { success = result.Success, message = result.Success
                ? (request.Activate ? "User reactivated successfully." : "User deactivated successfully.")
                : result.Message });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling status for UserBusinessId={UserBusinessId}, Activate={Activate}",
                request.UserBusinessId, request.Activate);
            return Json(new { success = false, message = "The operation could not be completed. Please try again." });
        }
    }
}
