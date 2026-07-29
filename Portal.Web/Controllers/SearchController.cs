using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

[Authorize]
public class SearchController : Controller
{
    private readonly IGlobalSearchService _globalSearchService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IPlanCheckService _planCheckService;

    public SearchController(
        IGlobalSearchService globalSearchService,
        ICurrentTenantService currentTenantService,
        IPlanCheckService planCheckService)
    {
        _globalSearchService = globalSearchService;
        _currentTenantService = currentTenantService;
        _planCheckService = planCheckService;
    }

    [HttpGet]
    public async Task<IActionResult> AxGetGlobalSearch(string? query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return Json(new { success = true, data = new { groups = Array.Empty<object>() } });
            }

            var businessId = _currentTenantService.CurrentBusinessId;

            // Get plan modules directly from the service (not HttpContext.Items — filter doesn't populate it for exempt controllers)
            var planModules = await _planCheckService.GetPlanModulesAsync();
            var permittedModules = planModules?.ToHashSet() ?? new HashSet<string>();

            var result = await _globalSearchService.SearchAsync(query.Trim(), businessId, permittedModules);

            return Json(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Search is temporarily unavailable." });
        }
    }
}
