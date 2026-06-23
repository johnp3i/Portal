using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Web.Models;
using Serilog;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
[Route("Admin/Subscriptions")]
public class AdminSubscriptionController : Controller
{
    private readonly PortalDbContext _portalDbContext;

    public AdminSubscriptionController(PortalDbContext portalDbContext)
    {
        _portalDbContext = portalDbContext;
    }

    // GET: /Admin/Subscriptions
    [HttpGet("")]
    public async Task<IActionResult> SubscriptionManagement()
    {
        try
        {
            var businesses = await _portalDbContext.Businesses
                .Where(b => !b.IsDemoAccount)
                .Select(b => new SubscriptionManagementItem
                {
                    BusinessId = b.Id,
                    BusinessName = b.Name,
                    IsActive = b.IsActive
                })
                .ToListAsync();

            var businessPlans = await _portalDbContext.BusinessPlans
                .Include(bp => bp.Plan)
                .Where(bp => bp.IsActive)
                .ToListAsync();

            foreach (var business in businesses)
            {
                var bp = businessPlans.FirstOrDefault(x => x.BusinessId == business.BusinessId);
                if (bp != null)
                {
                    business.BusinessPlanId = bp.Id;
                    business.PlanName = bp.Plan.Name;
                    business.Status = bp.Status;
                    business.StartedAtUtc = bp.StartDateUtc;
                    business.ExpiresAtUtc = bp.EndDateUtc;
                    business.TrialEndsAtUtc = bp.TrialEndsAtUtc;
                }
            }

            var availablePlans = await _portalDbContext.Plans
                .Where(p => p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new AvailablePlanItem
                {
                    PlanId = p.Id,
                    PlanName = p.Name
                })
                .ToListAsync();

            var viewModel = new SubscriptionManagementViewModel
            {
                Businesses = businesses,
                AvailablePlans = availablePlans
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error loading subscription management page");
            return View("Error");
        }
    }

    // POST: /Admin/Subscriptions/ChangePlan
    [HttpPost("ChangePlan")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostChangeBusinessPlan([FromBody] ChangeBusinessPlanRequest request)
    {
        try
        {
            var businessPlan = await _portalDbContext.BusinessPlans
                .FirstOrDefaultAsync(bp => bp.BusinessId == request.BusinessId && bp.IsActive);

            if (businessPlan == null)
                return Json(new { success = false, message = "No active subscription found for this business." });

            var newPlan = await _portalDbContext.Plans
                .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive);

            if (newPlan == null)
                return Json(new { success = false, message = "The selected plan does not exist or is inactive." });

            var oldPlanId = businessPlan.PlanId;
            businessPlan.PlanId = request.PlanId;

            await _portalDbContext.SaveChangesAsync();

            Log.Information("SuperAdmin changed business {BusinessId} plan from PlanId={OldPlanId} to PlanId={NewPlanId}",
                request.BusinessId, oldPlanId, request.PlanId);

            return Json(new { success = true, message = $"Plan changed to '{newPlan.Name}' successfully." });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error changing plan for BusinessId={BusinessId}, PlanId={PlanId}",
                request.BusinessId, request.PlanId);
            return Json(new { success = false, message = "The plan could not be changed. Please try again." });
        }
    }

    // POST: /Admin/Subscriptions/ChangeStatus
    [HttpPost("ChangeStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostChangeSubscriptionStatus([FromBody] ChangeSubscriptionStatusRequest request)
    {
        try
        {
            var validStatuses = new[] { "active", "trial", "cancelled", "expired" };
            if (!validStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
                return Json(new { success = false, message = $"Invalid status '{request.Status}'. Valid values: active, trial, cancelled, expired." });

            var businessPlan = await _portalDbContext.BusinessPlans
                .FirstOrDefaultAsync(bp => bp.Id == request.BusinessPlanId);

            if (businessPlan == null)
                return Json(new { success = false, message = "Business subscription record not found." });

            var oldStatus = businessPlan.Status;
            businessPlan.Status = request.Status.ToLowerInvariant();

            await _portalDbContext.SaveChangesAsync();

            Log.Information("SuperAdmin changed BusinessPlan {BusinessPlanId} status from '{OldStatus}' to '{NewStatus}'",
                request.BusinessPlanId, oldStatus, request.Status);

            return Json(new { success = true, message = $"Subscription status changed to '{request.Status}' successfully." });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error changing subscription status for BusinessPlanId={BusinessPlanId}, Status={Status}",
                request.BusinessPlanId, request.Status);
            return Json(new { success = false, message = "The status could not be changed. Please try again." });
        }
    }
}

/// <summary>
/// View model for the subscription management page.
/// </summary>
public class SubscriptionManagementViewModel
{
    public List<SubscriptionManagementItem> Businesses { get; set; } = new();

    public List<AvailablePlanItem> AvailablePlans { get; set; } = new();
}

/// <summary>
/// A single business row in the subscription management table.
/// </summary>
public class SubscriptionManagementItem
{
    public int BusinessId { get; set; }

    public string BusinessName { get; set; } = null!;

    public bool IsActive { get; set; }

    public int? BusinessPlanId { get; set; }

    public string? PlanName { get; set; }

    public string? Status { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime? TrialEndsAtUtc { get; set; }
}

/// <summary>
/// A plan option available for assignment.
/// </summary>
public class AvailablePlanItem
{
    public int PlanId { get; set; }

    public string PlanName { get; set; } = null!;
}
