using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/catalog")]
[ModuleAccess(PortalModules.Quotation)]
public class LineItemCatalogController : ControllerBase
{
    private readonly ILineItemCatalogService _lineItemCatalogService;
    private readonly ICurrentTenantService _tenantService;

    public LineItemCatalogController(
        ILineItemCatalogService lineItemCatalogService,
        ICurrentTenantService tenantService)
    {
        _lineItemCatalogService = lineItemCatalogService;
        _tenantService = tenantService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q)
    {
        var businessId = _tenantService.CurrentBusinessId;
        var results = await _lineItemCatalogService.SearchAsync(businessId, q ?? string.Empty);
        return Ok(results);
    }
}
