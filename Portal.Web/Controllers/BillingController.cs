using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;
using Portal.Web.Services.Stripe;

namespace Portal.Web.Controllers;

/// <summary>
/// Controller for the billing history page where business owners can view their
/// subscription status, payment history, and download PDF invoices.
/// Restricted to users with the owner role.
/// </summary>
[Authorize]
[Route("Account/Billing")]
public class BillingController : Controller
{
    private readonly IBillingService _billingService;
    private readonly ICurrentTenantService _tenantService;
    private readonly IBusinessPlanRepository _businessPlanRepository;
    private readonly IPlanRepository _planRepository;
    private readonly MembershipDbContext _membershipDbContext;
    private readonly ILogger<BillingController> _logger;

    private const int DefaultPageSize = 10;

    public BillingController(
        IBillingService billingService,
        ICurrentTenantService tenantService,
        IBusinessPlanRepository businessPlanRepository,
        IPlanRepository planRepository,
        MembershipDbContext membershipDbContext,
        ILogger<BillingController> logger)
    {
        _billingService = billingService;
        _tenantService = tenantService;
        _businessPlanRepository = businessPlanRepository;
        _planRepository = planRepository;
        _membershipDbContext = membershipDbContext;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1)
    {
        if (!IsOwner())
        {
            return Forbid();
        }

        var businessId = _tenantService.CurrentBusinessId;

        if (businessId == 0)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            var overview = await _billingService.GetBillingOverviewAsync(businessId);
            var invoices = await _billingService.GetInvoicesAsync(businessId, page, DefaultPageSize);

            ViewData["Title"] = "Billing";
            ViewData["Overview"] = overview;
            ViewData["Invoices"] = invoices;

            // Seat usage
            var activePlan = await _businessPlanRepository.GetActiveByBusinessIdAsync(businessId);
            if (activePlan != null)
            {
                var plan = await _planRepository.GetByIdAsync(activePlan.PlanId);
                var maxUsers = plan?.MaxUsers ?? -1;
                var activeUsers = await _membershipDbContext.UserBusinesses
                    .CountAsync(ub => ub.BusinessId == businessId && ub.IsActive);
                ViewData["MaxUsers"] = maxUsers;
                ViewData["ActiveUsers"] = activeUsers;
            }

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading billing page for BusinessId {BusinessId}", businessId);
            return View("Error");
        }
    }

    [HttpGet("DownloadInvoice/{id:int}")]
    public async Task<IActionResult> DownloadInvoice(int id)
    {
        if (!IsOwner())
        {
            return Forbid();
        }

        var businessId = _tenantService.CurrentBusinessId;

        if (businessId == 0)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            var pdfBytes = await _billingService.GenerateInvoicePdfAsync(id, businessId);
            return File(pdfBytes, "application/pdf", $"Invoice-{id}.pdf");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invoice {InvoiceId} not found for BusinessId {BusinessId}", id, businessId);
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF for InvoiceId {InvoiceId}, BusinessId {BusinessId}", id, businessId);
            return StatusCode(500, "Failed to generate invoice PDF.");
        }
    }

    private bool IsOwner()
    {
        return User.IsInRole("SuperAdmin") || User.HasClaim("IsOwner", "true");
    }
}
