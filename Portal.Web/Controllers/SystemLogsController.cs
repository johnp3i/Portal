using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;
using Portal.Web.Security;
using Serilog;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
[ModuleAccess(PortalModules.Audit, AccessLevels.Full)]
[Route("Admin/SystemLogs")]
public class SystemLogsController : Controller
{
    private readonly ISystemLogQueryService _systemLogQueryService;

    public SystemLogsController(ISystemLogQueryService systemLogQueryService)
    {
        _systemLogQueryService = systemLogQueryService;
    }

    // GET /Admin/SystemLogs
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var levels = await _systemLogQueryService.GetDistinctLevelsAsync();
        var sourceContexts = await _systemLogQueryService.GetDistinctSourceContextsAsync();

        ViewBag.Levels = levels;
        ViewBag.SourceContexts = sourceContexts;

        try
        {
            var kpiCounts = await _systemLogQueryService.GetKpiCountsAsync();
            ViewBag.KpiCounts = kpiCounts;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load KPI counts for System Logs page");
            ViewBag.KpiCounts = null;
        }

        return View();
    }

    // GET /Admin/SystemLogs/KpiCounts
    [HttpGet("KpiCounts")]
    public async Task<IActionResult> GetKpiCounts()
    {
        try
        {
            var counts = await _systemLogQueryService.GetKpiCountsAsync();
            return Json(new
            {
                success = true,
                errorCount24h = counts.ErrorCount24h,
                warningCount24h = counts.WarningCount24h,
                totalToday = counts.TotalToday
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error retrieving KPI counts");
            return Json(new { success = false, message = "Failed to retrieve KPI counts." });
        }
    }

    // GET /Admin/SystemLogs/Search?level=&dateFrom=&dateTo=&userId=&correlationId=&sourceContext=&requestPath=&page=1&pageSize=50
    [HttpGet("Search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? level,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? userId,
        [FromQuery] string? correlationId,
        [FromQuery] string? sourceContext,
        [FromQuery] string? requestPath,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value)
            {
                return Json(new { success = false, message = "Date From cannot be greater than Date To." });
            }

            var filter = new SystemLogFilter
            {
                Level = level,
                DateFrom = dateFrom,
                DateTo = dateTo,
                UserId = userId,
                CorrelationId = correlationId,
                SourceContext = sourceContext,
                RequestPath = requestPath,
                PageNumber = page,
                PageSize = pageSize
            };

            var pagedResult = await _systemLogQueryService.GetLogsAsync(filter);

            var data = pagedResult.Items.Select(item => new
            {
                id = item.Id,
                timeStamp = item.TimeStamp,
                level = item.Level,
                message = item.Message,
                exception = item.Exception,
                userId = item.UserId,
                correlationId = item.CorrelationId,
                sourceContext = item.SourceContext,
                requestPath = item.RequestPath,
                machineName = item.MachineName
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
            Log.Error(ex, "Error searching system logs");
            return Json(new { success = false, message = "The search could not be completed. Please try again." });
        }
    }

    // GET /Admin/SystemLogs/ExportCsv?level=&dateFrom=&dateTo=&userId=&correlationId=&sourceContext=&requestPath=
    [HttpGet("ExportCsv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? level,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? userId,
        [FromQuery] string? correlationId,
        [FromQuery] string? sourceContext,
        [FromQuery] string? requestPath)
    {
        try
        {
            if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value)
            {
                return Json(new { success = false, message = "Date From cannot be greater than Date To." });
            }

            var filter = new SystemLogFilter
            {
                Level = level,
                DateFrom = dateFrom,
                DateTo = dateTo,
                UserId = userId,
                CorrelationId = correlationId,
                SourceContext = sourceContext,
                RequestPath = requestPath
            };

            var exportResult = await _systemLogQueryService.GetExportLogsAsync(filter, 10000);

            var data = exportResult.Items.Select(item => new
            {
                id = item.Id,
                timeStamp = item.TimeStamp,
                level = item.Level,
                message = item.Message,
                exception = item.Exception,
                userId = item.UserId,
                correlationId = item.CorrelationId,
                sourceContext = item.SourceContext,
                requestPath = item.RequestPath,
                machineName = item.MachineName
            }).ToList();

            return Json(new
            {
                success = true,
                data,
                totalCount = exportResult.TotalCount,
                isTruncated = exportResult.IsTruncated
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error exporting system logs to CSV");
            return Json(new { success = false, message = "Export failed. Please try again." });
        }
    }
}
