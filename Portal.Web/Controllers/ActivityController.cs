using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Security;
using Serilog;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.AuditLog, AccessLevels.ReadOnly)]
[Route("Activity")]
public class ActivityController : Controller
{
    private readonly IActivitySummaryService _activitySummaryService;
    private readonly IAuditLogQueryService _auditLogQueryService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly MembershipDbContext _membershipDbContext;
    private readonly ILogger<ActivityController> _logger;

    public ActivityController(
        IActivitySummaryService activitySummaryService,
        IAuditLogQueryService auditLogQueryService,
        ICurrentTenantService currentTenantService,
        MembershipDbContext membershipDbContext,
        ILogger<ActivityController> logger)
    {
        _activitySummaryService = activitySummaryService;
        _auditLogQueryService = auditLogQueryService;
        _currentTenantService = currentTenantService;
        _membershipDbContext = membershipDbContext;
        _logger = logger;
    }

    /// <summary>
    /// Activity Log main page — loads team members for filter dropdown and serves the view.
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;

            var teamMembers = await _membershipDbContext.UserBusinesses
                .Include(ub => ub.User)
                .Where(ub => ub.BusinessId == businessId && ub.IsActive)
                .Select(ub => new { UserId = ub.UserId, DisplayName = ub.User.FirstName + " " + ub.User.LastName })
                .ToListAsync();

            ViewBag.TeamMembers = teamMembers.Select(t => new Dictionary<string, string>
            {
                ["userId"] = t.UserId,
                ["displayName"] = t.DisplayName ?? "User"
            }).ToList();
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Activity Log index for business {BusinessId}", _currentTenantService.CurrentBusinessId);
            return View();
        }
    }

    /// <summary>
    /// Returns paginated, filtered activity data for the timeline feed.
    /// </summary>
    [HttpGet("AxGetActivity")]
    public async Task<IActionResult> AxGetActivity(
        [FromQuery] string? whatChanged = null,
        [FromQuery] string? whoChanged = null,
        [FromQuery] string? changeType = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 8)
    {
        try
        {
            // Validate dates
            if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value)
                return Json(new { success = false, message = "Date From cannot be greater than Date To." });

            // Build AuditLogFilter from business-friendly parameters
            var filter = new AuditLogFilter
            {
                PageNumber = page,
                PageSize = pageSize,
                DateFrom = dateFrom,
                DateTo = dateTo
            };

            // Map "What changed" → TableName
            if (!string.IsNullOrEmpty(whatChanged) && whatChanged != "Everything")
            {
                var tableNameMap = new Dictionary<string, string>
                {
                    ["Invoices"] = "Invoice",
                    ["Quotations"] = "Quotation",
                    ["Customers"] = "Customer",
                    ["Purchases"] = "Purchase",
                    ["Payments"] = "Payment",
                    ["Credit Notes"] = "CreditNote",
                    ["Settings"] = "Business"
                };
                if (tableNameMap.TryGetValue(whatChanged, out var tableName))
                    filter.TableName = tableName;
            }

            // Map "Who changed" → UserId
            if (!string.IsNullOrEmpty(whoChanged) && whoChanged != "Everyone")
            {
                if (whoChanged != "system")
                    filter.UserId = whoChanged;
            }

            // Map "Change type" → Action
            if (!string.IsNullOrEmpty(changeType) && changeType != "All changes")
            {
                var actionMap = new Dictionary<string, string>
                {
                    ["Created"] = "Insert",
                    ["Edited"] = "Update",
                    ["Deleted"] = "Delete",
                    ["Status changed"] = "Update"
                };
                if (actionMap.TryGetValue(changeType, out var action))
                    filter.Action = action;
            }

            // Query via existing service
            var pagedResult = await _auditLogQueryService.GetAuditLogsAsync(filter);

            // Transform to activity items
            var activityItems = await _activitySummaryService.TransformAsync(pagedResult.Items);

            // Post-filter for "Status changed" (only items where ActionType == "StatusChanged")
            if (changeType == "Status changed")
            {
                activityItems = activityItems.Where(a => a.ActionType == "StatusChanged").ToList();
            }

            return Json(new
            {
                success = true,
                data = activityItems,
                totalCount = pagedResult.TotalCount,
                currentPage = page,
                totalPages = (int)Math.Ceiling((double)pagedResult.TotalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading activity for business {BusinessId}", _currentTenantService.CurrentBusinessId);
            return Json(new { success = false, message = "Could not load activity data. Please try again." });
        }
    }

    /// <summary>
    /// Returns weekly quick stats for the Activity Log header.
    /// </summary>
    [HttpGet("AxGetStats")]
    public async Task<IActionResult> AxGetStats()
    {
        try
        {
            var stats = await _activitySummaryService.GetQuickStatsAsync();
            return Json(new { success = true, data = stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading activity stats for business {BusinessId}", _currentTenantService.CurrentBusinessId);
            return Json(new { success = false, message = "Could not load statistics." });
        }
    }
}
