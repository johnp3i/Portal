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

[Authorize(Roles = "SuperAdmin")]
[ModuleAccess(PortalModules.Audit, AccessLevels.Full)]
[Route("Admin/Audit")]
public class AuditController : Controller
{
    private readonly IAuditLogQueryService _auditLogQueryService;
    private readonly MembershipDbContext _membershipDbContext;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly PortalDbContext _portalDbContext;

    public AuditController(
        IAuditLogQueryService auditLogQueryService,
        MembershipDbContext membershipDbContext,
        ICurrentTenantService currentTenantService,
        PortalDbContext portalDbContext)
    {
        _auditLogQueryService = auditLogQueryService;
        _membershipDbContext = membershipDbContext;
        _currentTenantService = currentTenantService;
        _portalDbContext = portalDbContext;
    }

    // TEMP DEBUG: Direct DB query to isolate the issue
    [HttpGet("Debug")]
    public async Task<IActionResult> Debug()
    {
        var businessId = _currentTenantService.CurrentBusinessId;

        // Query 1: With IgnoreQueryFilters
        var countNoFilter = await _portalDbContext.AuditLogs
            .IgnoreQueryFilters()
            .Where(a => a.BusinessId == businessId)
            .CountAsync();

        // Query 2: Without IgnoreQueryFilters (global filter applied)
        var countWithFilter = await _portalDbContext.AuditLogs
            .CountAsync();

        // Query 3: All records regardless of business
        var countAll = await _portalDbContext.AuditLogs
            .IgnoreQueryFilters()
            .CountAsync();

        // Query 4: Through the service layer
        var filter = new AuditLogFilter { PageNumber = 1, PageSize = 20 };
        var serviceResult = await _auditLogQueryService.GetAuditLogsAsync(filter);

        return Json(new
        {
            businessId,
            countNoFilter,
            countWithFilter,
            countAll,
            serviceResultCount = serviceResult.Items.Count,
            serviceTotalCount = serviceResult.TotalCount
        });
    }

    // GET /Admin/Audit
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var tableNames = await _auditLogQueryService.GetDistinctTableNamesAsync();

        var businessId = _currentTenantService.CurrentBusinessId;
        var users = await _membershipDbContext.UserBusinesses
            .Include(ub => ub.User)
            .Where(ub => ub.BusinessId == businessId)
            .Select(ub => new
            {
                value = ub.UserId,
                text = ub.User.FirstName + " " + ub.User.LastName
            })
            .ToListAsync();

        ViewBag.TableNames = tableNames;
        ViewBag.Users = users;

        return View();
    }

    // GET /Admin/Audit/Search?tableName=&action=&userId=&dateFrom=&dateTo=&page=1&pageSize=20
    [HttpGet("Search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? tableName,
        [FromQuery] string? action,
        [FromQuery] string? userId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value)
            {
                return Json(new { success = false, message = "Date From cannot be greater than Date To." });
            }

            var currentBusinessId = _currentTenantService.CurrentBusinessId;

            var filter = new AuditLogFilter
            {
                TableName = tableName,
                Action = action,
                UserId = userId,
                DateFrom = dateFrom,
                DateTo = dateTo,
                PageNumber = page,
                PageSize = pageSize
            };

            var pagedResult = await _auditLogQueryService.GetAuditLogsAsync(filter);

            // Build userId → displayName dictionary from MembershipDbContext
            var userIds = pagedResult.Items
                .Where(i => i.UserId != null)
                .Select(i => i.UserId!)
                .Distinct()
                .ToList();

            var userDisplayNames = await _membershipDbContext.UserBusinesses
                .Include(ub => ub.User)
                .Where(ub => ub.BusinessId == currentBusinessId && userIds.Contains(ub.UserId))
                .ToDictionaryAsync(
                    ub => ub.UserId,
                    ub => ub.User.FirstName + " " + ub.User.LastName);

            var data = pagedResult.Items.Select(item => new
            {
                id = item.Id,
                timestamp = item.Timestamp,
                userId = item.UserId,
                userDisplayName = item.UserId != null && userDisplayNames.TryGetValue(item.UserId, out var name)
                    ? name
                    : item.UserId,
                action = item.Action,
                tableName = item.TableName,
                recordId = item.RecordId,
                oldValues = item.OldValues,
                newValues = item.NewValues
            }).ToList();

            return Json(new
            {
                success = true,
                data,
                totalCount = pagedResult.TotalCount,
                currentPage = pagedResult.CurrentPage,
                totalPages = pagedResult.TotalPages
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error searching audit logs for business {BusinessId}", _currentTenantService.CurrentBusinessId);
            return Json(new { success = false, message = "The search could not be completed. Please try again." });
        }
    }
}
