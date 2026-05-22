using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

/// <summary>
/// Unified management page for all shared document links (quotations and invoices).
/// </summary>
[Authorize]
[Route("shared-links")]
public class SharedLinksController : Controller
{
    private readonly ProposalShareRepository _proposalShareRepository;
    private readonly IInvoiceSharingService _invoiceSharingService;
    private readonly ICurrentTenantService _tenantService;

    public SharedLinksController(
        ProposalShareRepository proposalShareRepository,
        IInvoiceSharingService invoiceSharingService,
        ICurrentTenantService tenantService)
    {
        _proposalShareRepository = proposalShareRepository;
        _invoiceSharingService = invoiceSharingService;
        _tenantService = tenantService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var businessId = _tenantService.CurrentBusinessId;

        var proposalShares = await _proposalShareRepository.GetByBusinessIdAsync(businessId);
        var invoiceShares = await _invoiceSharingService.GetSharesByBusinessIdAsync(businessId);

        var links = new List<SharedLinkViewModel>();

        foreach (var ps in proposalShares)
        {
            links.Add(new SharedLinkViewModel
            {
                Id = ps.Id,
                DocumentType = "Quotation",
                DocumentReference = $"QUO-{ps.BusinessId}-{ps.QuotationId:D5}",
                CustomerName = "",
                CustomerEmail = ps.CustomerEmail,
                ShareToken = ps.ShareToken,
                CreatedAtUtc = ps.CreatedAtUtc,
                ExpiresAtUtc = ps.ExpiresAtUtc,
                IsActive = ps.IsActive,
                Status = DeriveStatus(ps.IsActive, ps.ExpiresAtUtc)
            });
        }

        foreach (var invShare in invoiceShares)
        {
            links.Add(new SharedLinkViewModel
            {
                Id = invShare.Id,
                DocumentType = "Invoice",
                DocumentReference = $"INV-{invShare.BusinessId}-{invShare.InvoiceId:D5}",
                CustomerName = "",
                CustomerEmail = invShare.CustomerEmail,
                ShareToken = invShare.ShareToken,
                CreatedAtUtc = invShare.CreatedAtUtc,
                ExpiresAtUtc = invShare.ExpiresAtUtc,
                IsActive = invShare.IsActive,
                Status = DeriveStatus(invShare.IsActive, invShare.ExpiresAtUtc)
            });
        }

        // Sort by most recent first
        links = links.OrderByDescending(l => l.CreatedAtUtc).ToList();

        return View(links);
    }

    [HttpPost("cancel-proposal/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelProposal(int id)
    {
        var businessId = _tenantService.CurrentBusinessId;
        await _proposalShareRepository.DeactivateByIdAsync(id, businessId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cancel-invoice/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelInvoice(int id)
    {
        await _invoiceSharingService.CancelShareAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("reactivate-proposal/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateProposal(int id)
    {
        var businessId = _tenantService.CurrentBusinessId;
        await _proposalShareRepository.ReactivateByIdAsync(id, businessId);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("reactivate-invoice/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateInvoice(int id)
    {
        var businessId = _tenantService.CurrentBusinessId;
        await _invoiceSharingService.ReactivateShareAsync(id);
        return RedirectToAction(nameof(Index));
    }

    private static string DeriveStatus(bool isActive, DateTimeOffset expiresAtUtc)
    {
        if (!isActive) return "Cancelled";
        if (expiresAtUtc <= DateTimeOffset.UtcNow) return "Expired";
        return "Active";
    }
}
