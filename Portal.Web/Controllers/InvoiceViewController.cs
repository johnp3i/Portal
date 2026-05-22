using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

/// <summary>
/// Public, unauthenticated controller for viewing shared invoices via token-based URLs.
/// </summary>
[AllowAnonymous]
public class InvoiceViewController : Controller
{
    private readonly IInvoiceSharingService _sharingService;

    public InvoiceViewController(IInvoiceSharingService sharingService)
    {
        _sharingService = sharingService;
    }

    [HttpGet("/invoice-view/{token}")]
    public async Task<IActionResult> ViewInvoice(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        var share = await _sharingService.GetByTokenAsync(token);
        if (share == null)
            return NotFound();

        Response.Headers["Cache-Control"] = "no-store";

        // Check if cancelled or expired
        if (!share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return View("~/Views/Shared/Unavailable.cshtml");
        }

        return Content(share.SnapshotHtml, "text/html");
    }
}
