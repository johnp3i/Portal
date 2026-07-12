using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;

namespace Portal.Web.ViewComponents;

/// <summary>
/// ViewComponent that renders a paperclip icon with attachment count badge.
/// Used on list views to indicate which records have attachments.
/// Returns empty content when the count is zero.
/// </summary>
public class AttachmentCountViewComponent : ViewComponent
{
    private readonly IDocumentAttachmentService _attachmentService;
    private readonly ICurrentTenantService _currentTenantService;

    public AttachmentCountViewComponent(
        IDocumentAttachmentService attachmentService,
        ICurrentTenantService currentTenantService)
    {
        _attachmentService = attachmentService;
        _currentTenantService = currentTenantService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string entityType, int entityId)
    {
        var businessId = _currentTenantService.CurrentBusinessId;
        var count = await _attachmentService.GetCountAsync(businessId, entityType, entityId);

        if (count == 0)
            return Content(string.Empty);

        return View("Default", count);
    }
}
