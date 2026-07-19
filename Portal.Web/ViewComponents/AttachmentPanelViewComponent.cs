using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Services;
using Portal.Web.Models;

namespace Portal.Web.ViewComponents;

/// <summary>
/// ViewComponent that renders the full attachment panel or soft-gate teaser
/// depending on the user's plan. Self-contained: handles plan check, data loading,
/// and ownership resolution.
/// </summary>
public class AttachmentPanelViewComponent : ViewComponent
{
    private readonly IDocumentAttachmentService _attachmentService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IPlanCheckService _planCheckService;

    public AttachmentPanelViewComponent(
        IDocumentAttachmentService attachmentService,
        ICurrentTenantService currentTenantService,
        IPlanCheckService planCheckService)
    {
        _attachmentService = attachmentService;
        _currentTenantService = currentTenantService;
        _planCheckService = planCheckService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string entityType, int entityId)
    {
        // Z-Report attachments are included with the Foundation tier (no separate plan check)
        var isZReportAttachment = entityType.Equals("RevenueSummary", StringComparison.OrdinalIgnoreCase);

        if (!isZReportAttachment)
        {
            // Check if user's plan includes attachments module
            var hasAccess = await _planCheckService.IsModuleInPlanAsync(PortalModules.Attachments);

            if (!hasAccess)
            {
                return View("SoftGate");
            }
        }

        var businessId = _currentTenantService.CurrentBusinessId;
        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

        var attachments = await _attachmentService.GetByEntityAsync(businessId, entityType, entityId, userId);

        var model = new AttachmentPanelViewModel
        {
            EntityType = entityType,
            EntityId = entityId,
            Attachments = attachments,
            MaxAttachments = isZReportAttachment ? 1 : 5,
            IsReadOnly = false
        };

        return View("Default", model);
    }
}
