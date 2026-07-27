using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;

namespace Portal.Web.ViewComponents;

/// <summary>
/// Renders the business identity card in the sidebar — shows business name and plan tier.
/// </summary>
public class BusinessIdentityCardViewComponent : ViewComponent
{
    private readonly IBusinessService _businessService;
    private readonly IPlanCheckService _planCheckService;

    public BusinessIdentityCardViewComponent(IBusinessService businessService, IPlanCheckService planCheckService)
    {
        _businessService = businessService;
        _planCheckService = planCheckService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var businessIdClaim = UserClaimsPrincipal.FindFirstValue("BusinessId");

        if (!int.TryParse(businessIdClaim, out var businessId) || businessId <= 0)
            return Content(string.Empty);

        var business = await _businessService.GetBusinessByIdAsync(businessId);
        if (business == null)
            return Content(string.Empty);

        var planName = await _planCheckService.GetCurrentPlanNameAsync() ?? "Free";

        ViewBag.BusinessName = business.Name;
        ViewBag.PlanName = planName;
        ViewBag.BusinessInitial = business.Name.Length > 0 ? business.Name[0].ToString().ToUpper() : "?";

        return View();
    }
}
