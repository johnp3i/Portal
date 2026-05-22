using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

/// <summary>
/// Public, unauthenticated controller for viewing shared proposals via token-based URLs.
/// </summary>
[AllowAnonymous]
public class ProposalController : Controller
{
    private readonly IProposalService _proposalService;

    public ProposalController(IProposalService proposalService)
    {
        _proposalService = proposalService;
    }

    [HttpGet("/proposal/{token}")]
    public async Task<IActionResult> ViewProposal(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return NotFound();

        var share = await _proposalService.GetByTokenAsync(token);
        if (share == null)
            return NotFound();

        // Set cache-control to prevent intermediary caching
        Response.Headers["Cache-Control"] = "no-store";

        // Check if cancelled or expired
        if (!share.IsActive || share.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return View("~/Views/Shared/Unavailable.cshtml");
        }

        // Return the stored HTML snapshot directly
        return Content(share.SnapshotHtml, "text/html");
    }
}
