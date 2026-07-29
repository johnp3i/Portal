using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;

namespace Portal.Web.ViewComponents;

public class UpcomingFilingsViewComponent : ViewComponent
{
    private readonly IComplianceService _complianceService;
    private readonly ICurrentTenantService _tenantService;
    private readonly IPlanCheckService _planCheckService;

    public UpcomingFilingsViewComponent(
        IComplianceService complianceService,
        ICurrentTenantService tenantService,
        IPlanCheckService planCheckService)
    {
        _complianceService = complianceService;
        _tenantService = tenantService;
        _planCheckService = planCheckService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            var hasAccess = await _planCheckService.IsModuleInPlanAsync(PortalModules.Compliance);
            if (!hasAccess)
                return Content(string.Empty);

            var businessId = _tenantService.CurrentBusinessId;
            var filings = await _complianceService.GetUpcomingFilingsAsync(businessId, days: 30, maxItems: 5);
            return View(filings);
        }
        catch (Exception ex)
        {
            return Content(string.Empty);
        }
    }
}
