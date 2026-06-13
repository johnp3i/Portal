using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/sections")]
[ModuleAccess(PortalModules.Quotation)]
public class ProposalSectionController : ControllerBase
{
    private readonly IProposalSectionService _sectionService;
    private readonly ICurrentTenantService _tenantService;

    public ProposalSectionController(
        IProposalSectionService sectionService,
        ICurrentTenantService tenantService)
    {
        _sectionService = sectionService;
        _tenantService = tenantService;
    }

    [HttpPost("add")]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> AddSection([FromBody] AddSectionRequest request)
    {
        if (request.QuotationId <= 0)
        {
            return BadRequest(new { success = false, message = "Invalid quotation ID." });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { success = false, message = "Section name is required." });
        }

        try
        {
            await _sectionService.AddSectionAsync(request.QuotationId, request.Name, request.Description, request.ColumnConfiguration, request.SectionType, request.IsEmphasized, request.AccentColor, request.Label, request.IsTotalsTableShown, request.IsHalfWidth);
            var sections = await _sectionService.GetByQuotationIdAsync(request.QuotationId);
            return Ok(new { success = true, sections });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("remove")]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> RemoveSection([FromBody] RemoveSectionRequest request)
    {
        if (request.SectionId <= 0)
        {
            return BadRequest(new { success = false, message = "Invalid section ID." });
        }

        if (request.QuotationId <= 0)
        {
            return BadRequest(new { success = false, message = "Invalid quotation ID." });
        }

        try
        {
            await _sectionService.RemoveSectionAsync(request.SectionId, request.QuotationId);
            var sections = await _sectionService.GetByQuotationIdAsync(request.QuotationId);
            return Ok(new { success = true, sections });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("reorder")]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> ReorderSections([FromBody] ReorderSectionsRequest request)
    {
        if (request.QuotationId <= 0)
        {
            return BadRequest(new { success = false, message = "Invalid quotation ID." });
        }

        if (request.OrderedSectionIds == null || request.OrderedSectionIds.Count == 0)
        {
            return BadRequest(new { success = false, message = "Ordered section IDs are required." });
        }

        try
        {
            await _sectionService.ReorderSectionsAsync(request.QuotationId, request.OrderedSectionIds);
            var sections = await _sectionService.GetByQuotationIdAsync(request.QuotationId);
            return Ok(new { success = true, sections });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("move-line")]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> MoveLine([FromBody] MoveLineRequest request)
    {
        if (request.LineId <= 0)
        {
            return BadRequest(new { success = false, message = "Invalid line ID." });
        }

        try
        {
            await _sectionService.MoveLineToSectionAsync(request.LineId, request.TargetSectionId);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("reorder-lines")]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> ReorderLines([FromBody] ReorderLinesRequest request)
    {
        if (request.OrderedLineIds == null || request.OrderedLineIds.Count == 0)
        {
            return BadRequest(new { success = false, message = "Ordered line IDs are required." });
        }

        try
        {
            await _sectionService.ReorderLinesAsync(request.OrderedLineIds);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("update")]
    [ModuleAccess(PortalModules.Quotation, AccessLevels.Full)]
    public async Task<IActionResult> UpdateSection([FromBody] UpdateSectionRequest request)
    {
        if (request.SectionId <= 0)
        {
            return BadRequest(new { success = false, message = "Invalid section ID." });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { success = false, message = "Section name is required." });
        }

        try
        {
            await _sectionService.UpdateSectionAsync(request.SectionId, request.Name, request.Description, request.Notes, request.ColumnConfiguration, request.SectionType, request.IsEmphasized, request.AccentColor, request.Label, request.IsTotalsTableShown, request.IsHalfWidth);
            return Ok(new { success = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}

public class AddSectionRequest
{
    public int QuotationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ColumnConfiguration { get; set; } = "OneTime";
    public string SectionType { get; set; } = "LineItems";
    public bool IsEmphasized { get; set; }
    public string? AccentColor { get; set; }
    public string? Label { get; set; }
    public bool IsTotalsTableShown { get; set; }
    public bool IsHalfWidth { get; set; }
}

public class RemoveSectionRequest
{
    public int SectionId { get; set; }
    public int QuotationId { get; set; }
}

public class ReorderSectionsRequest
{
    public int QuotationId { get; set; }
    public List<int> OrderedSectionIds { get; set; } = new();
}

public class MoveLineRequest
{
    public int LineId { get; set; }
    public int? TargetSectionId { get; set; }
}

public class ReorderLinesRequest
{
    public List<int> OrderedLineIds { get; set; } = new();
}

public class UpdateSectionRequest
{
    public int SectionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? ColumnConfiguration { get; set; }
    public string? SectionType { get; set; }
    public bool? IsEmphasized { get; set; }
    public string? AccentColor { get; set; }
    public string? Label { get; set; }
    public bool? IsTotalsTableShown { get; set; }
    public bool? IsHalfWidth { get; set; }
}
