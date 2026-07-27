using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Services;

namespace Portal.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
[Route("Admin/BusinessInsights")]
public class AdminBusinessInsightsController : Controller
{
    private readonly IBusinessInsightsService _insightsService;

    public AdminBusinessInsightsController(IBusinessInsightsService insightsService)
    {
        _insightsService = insightsService;
    }

    // GET /Admin/BusinessInsights
    [HttpGet("")]
    public async Task<IActionResult> Index(string? searchTerm, string? planFilter, string? statusFilter, string? activityFilter, int page = 1)
    {
        try
        {
            var filter = new BusinessInsightFilter
            {
                SearchTerm = searchTerm,
                PlanFilter = planFilter,
                StatusFilter = statusFilter,
                ActivityFilter = activityFilter,
                PageNumber = page,
                PageSize = 20
            };

            var (items, summary, totalCount) = await _insightsService.GetBusinessInsightsAsync(filter);

            var pageSize = 20;
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            ViewBag.Summary = summary;
            ViewBag.SearchTerm = searchTerm ?? string.Empty;
            ViewBag.PlanFilter = planFilter ?? string.Empty;
            ViewBag.StatusFilter = statusFilter ?? string.Empty;
            ViewBag.ActivityFilter = activityFilter ?? string.Empty;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.PageSize = pageSize;

            return View(items);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
