using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Services.Sales;
using Portal.Web.Models;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Sales)]
public class SalesController : Controller
{
    private readonly ILeadRequestService _leadRequestService;
    private readonly IContactService _contactService;
    private readonly ISalesProductService _productService;
    private readonly IResponseService _responseService;
    private readonly IMeetingService _meetingService;
    private readonly LeadStatusTypeRepository _statusTypeRepository;
    private readonly LeadSourceTypeRepository _sourceTypeRepository;
    private readonly LeadSourceReferenceTypeRepository _sourceRefTypeRepository;
    private readonly LeadResponseTypeRepository _responseTypeRepository;
    private readonly MeetingTypeRepository _meetingTypeRepository;
    private readonly ICurrentTenantService _tenantService;
    private readonly IBusinessService _businessService;
    private readonly ITeamMemberService _teamMemberService;
    private readonly IActivityFeedService _activityFeedService;
    private readonly IFollowUpTaskService _followUpTaskService;
    private readonly IInsightsService _insightsService;
    private readonly ITimelineService _timelineService;
    private readonly IPlanCheckService _planCheckService;
    private readonly ILogger<SalesController> _logger;

    public SalesController(
        ILeadRequestService leadRequestService,
        IContactService contactService,
        ISalesProductService productService,
        IResponseService responseService,
        IMeetingService meetingService,
        LeadStatusTypeRepository statusTypeRepository,
        LeadSourceTypeRepository sourceTypeRepository,
        LeadSourceReferenceTypeRepository sourceRefTypeRepository,
        LeadResponseTypeRepository responseTypeRepository,
        MeetingTypeRepository meetingTypeRepository,
        ICurrentTenantService tenantService,
        IBusinessService businessService,
        ITeamMemberService teamMemberService,
        IActivityFeedService activityFeedService,
        IFollowUpTaskService followUpTaskService,
        IInsightsService insightsService,
        ITimelineService timelineService,
        IPlanCheckService planCheckService,
        ILogger<SalesController> logger)
    {
        _leadRequestService = leadRequestService;
        _contactService = contactService;
        _productService = productService;
        _responseService = responseService;
        _meetingService = meetingService;
        _statusTypeRepository = statusTypeRepository;
        _sourceTypeRepository = sourceTypeRepository;
        _sourceRefTypeRepository = sourceRefTypeRepository;
        _responseTypeRepository = responseTypeRepository;
        _meetingTypeRepository = meetingTypeRepository;
        _tenantService = tenantService;
        _businessService = businessService;
        _teamMemberService = teamMemberService;
        _activityFeedService = activityFeedService;
        _followUpTaskService = followUpTaskService;
        _insightsService = insightsService;
        _timelineService = timelineService;
        _planCheckService = planCheckService;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    // PAGE ACTIONS
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Pipeline()
    {
        var statuses = await _statusTypeRepository.GetAllAsync();
        var products = await _productService.GetActiveProductsAsync();
        ViewBag.Statuses = statuses;
        ViewBag.Products = products;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Contacts(string? search, int page = 1)
    {
        var paged = await _contactService.GetContactsPagedAsync(search, page, 15);
        ViewBag.SearchTerm = search;
        return View(paged);
    }

    [HttpGet]
    public async Task<IActionResult> ContactDetail(int id)
    {
        var detail = await _contactService.GetContactDetailAsync(id);
        if (detail == null) return NotFound();
        return View(detail);
    }

    [HttpGet]
    public async Task<IActionResult> Products(string? search, int page = 1)
    {
        var paged = await _productService.GetProductsPagedAsync(search, page, 15);
        ViewBag.SearchTerm = search;

        // Build lookup for linked catalogue product names
        var linkedIds = paged.Items.Where(p => p.ProductId.HasValue).Select(p => p.ProductId!.Value).Distinct().ToList();
        if (linkedIds.Any())
        {
            var dbContext = HttpContext.RequestServices.GetRequiredService<Portal.Infrastructure.Data.PortalDbContext>();
            var catalogLookup = await dbContext.Products
                .IgnoreQueryFilters()
                .Where(p => linkedIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.ProductCode + " — " + p.Description);
            ViewBag.CatalogLookup = catalogLookup;
        }
        else
        {
            ViewBag.CatalogLookup = new Dictionary<int, string>();
        }

        return View(paged);
    }

    [HttpGet]
    public async Task<IActionResult> Meetings()
    {
        var meetings = await _meetingService.GetAllMeetingsAsync();
        var meetingTypes = await _meetingTypeRepository.GetAllAsync();
        ViewBag.MeetingTypes = meetingTypes;
        return View(meetings);
    }

    [HttpGet]
    public async Task<IActionResult> Templates(int page = 1)
    {
        var paged = await _responseService.GetTemplatesPagedAsync(page, 15);
        var responseTypes = await _responseTypeRepository.GetAllAsync();
        var products = await _productService.GetActiveProductsAsync();
        ViewBag.ResponseTypes = responseTypes;
        ViewBag.Products = products;
        return View(paged);
    }

    [HttpGet]
    public async Task<IActionResult> Team()
    {
        var members = await _teamMemberService.GetAllAsync();

        // KPI data
        var activeMembers = members.Count(m => m.IsActive);
        var totalActiveLeads = members.Sum(m => m.ActiveLeadCount);
        var unassignedLeads = await _teamMemberService.GetUnassignedLeadCountAsync();

        ViewBag.ActiveMembers = activeMembers;
        ViewBag.TotalActiveLeads = totalActiveLeads;
        ViewBag.AvgLeadsPerMember = activeMembers > 0 ? Math.Round((double)totalActiveLeads / activeMembers, 1) : 0;
        ViewBag.UnassignedLeads = unassignedLeads;

        return View(members);
    }

    [HttpGet]
    public IActionResult Tasks()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Insights()
    {
        var isInPlan = await _planCheckService.IsModuleInPlanAsync(PortalModules.Sales);
        if (!isInPlan)
        {
            var requiredPlan = await _planCheckService.GetRequiredPlanForModuleAsync(PortalModules.Sales) ?? "Professional";
            return View("PlanSoftGate", new SoftGateViewModel
            {
                ModuleName = PortalModules.Sales,
                ModuleDisplayName = "Sales Insights",
                ModuleDescription = "View operational metrics, conversion rates, revenue breakdowns, and pipeline performance analytics.",
                RequiredPlanName = requiredPlan,
                CurrentPlanName = "your current plan"
            });
        }

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> LeadDetail(int id)
    {
        var detail = await _leadRequestService.GetLeadDetailAsync(id);
        if (detail == null) return NotFound();

        var statuses = await _statusTypeRepository.GetAllAsync();
        var responseTypes = await _responseTypeRepository.GetAllAsync();
        var meetingTypes = await _meetingTypeRepository.GetAllAsync();
        var products = await _productService.GetActiveProductsAsync();
        var sourceTypes = await _sourceTypeRepository.GetAllAsync();
        var sourceRefTypes = await _sourceRefTypeRepository.GetAllAsync();

        ViewBag.Statuses = statuses;
        ViewBag.ResponseTypes = responseTypes;
        ViewBag.MeetingTypes = meetingTypes;
        ViewBag.Products = products;
        ViewBag.SourceTypes = sourceTypes;
        ViewBag.SourceRefTypes = sourceRefTypes;

        var teamMembers = await _teamMemberService.GetActiveAsync();
        ViewBag.TeamMembers = teamMembers;

        var businessId = _tenantService.CurrentBusinessId;
        var profile = await _businessService.GetBusinessProfileAsync(businessId);
        ViewBag.CurrencySymbol = profile?.CurrencySymbol ?? "€";

        return View(detail);
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — CONTACTS
    // ═══════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateContact([FromBody] CreateContactRequest request)
    {
        try
        {
            var result = await _contactService.CreateContactAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contact");
            return Json(new { success = false, message = "An error occurred while creating the contact." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateContact([FromBody] UpdateContactRequest request)
    {
        try
        {
            var result = await _contactService.UpdateContactAsync(request);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contact");
            return Json(new { success = false, message = "An error occurred while updating the contact." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeactivateContact(int id)
    {
        try
        {
            var result = await _contactService.DeactivateContactAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating contact");
            return Json(new { success = false, message = "An error occurred while deactivating the contact." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostActivateContact(int id)
    {
        try
        {
            var result = await _contactService.ActivateContactAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating contact");
            return Json(new { success = false, message = "An error occurred while activating the contact." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetContactsSearch(string? search, int page = 1)
    {
        try
        {
            var paged = await _contactService.GetContactsPagedAsync(search, page, 15);
            var items = paged.Items.Select(c => new
            {
                c.Id,
                c.FirstName,
                c.LastName,
                c.Email,
                c.PhoneNumber,
                c.CompanyName,
                c.IsActive,
                c.CreatedAtUtc,
                FullName = string.IsNullOrWhiteSpace(c.LastName) ? c.FirstName : $"{c.FirstName} {c.LastName}"
            });
            return Json(new { success = true, data = items, totalCount = paged.TotalCount, currentPage = paged.CurrentPage, totalPages = paged.TotalPages });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching contacts");
            return Json(new { success = false, message = "Failed to search contacts." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — PRODUCTS
    // ═══════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateProduct([FromBody] CreateSalesProductRequest request)
    {
        try
        {
            var result = await _productService.CreateProductAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            return Json(new { success = false, message = "An error occurred while creating the product." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateProduct([FromBody] UpdateSalesProductRequest request)
    {
        try
        {
            var result = await _productService.UpdateProductAsync(request);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product");
            return Json(new { success = false, message = "An error occurred while updating the product." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeactivateProduct(int id)
    {
        try
        {
            var result = await _productService.DeactivateProductAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating product");
            return Json(new { success = false, message = "An error occurred while deactivating the product." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostActivateProduct(int id)
    {
        try
        {
            var result = await _productService.ActivateProductAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating product");
            return Json(new { success = false, message = "An error occurred while activating the product." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetCatalogProducts()
    {
        try
        {
            var dbContext = HttpContext.RequestServices.GetRequiredService<Portal.Infrastructure.Data.PortalDbContext>();
            var products = await dbContext.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Description)
                .Select(p => new { p.Id, Name = p.Description, p.ProductCode, SellingPrice = p.DefaultSellingPrice })
                .ToListAsync();

            return Json(new { success = true, data = products });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load catalog products." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — LEAD MANAGEMENT
    // ═══════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateLeadRequest([FromBody] CreateLeadRequestDto request)
    {
        try
        {
            var result = await _leadRequestService.CreateLeadRequestAsync(request);
            if (result.Success && result.Id > 0)
                await RecordActivityAsync(result.Id.Value, "lead_created", "New lead created.");
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating lead request");
            return Json(new { success = false, message = "An error occurred while creating the lead." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostChangeLeadStage(int id, int leadStatusTypeId)
    {
        try
        {
            var result = await _leadRequestService.ChangeStageAsync(id, leadStatusTypeId);
            if (result.Success)
            {
                var statuses = await _statusTypeRepository.GetAllAsync();
                var stageName = statuses.FirstOrDefault(s => s.Id == leadStatusTypeId)?.Name ?? "Unknown";
                await RecordActivityAsync(id, "stage_changed", $"Stage changed to {stageName}.");
            }
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing lead stage");
            return Json(new { success = false, message = "An error occurred while changing the stage." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostAssignLead(int id, string userId)
    {
        try
        {
            var result = await _leadRequestService.AssignLeadAsync(id, userId);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning lead");
            return Json(new { success = false, message = "An error occurred while assigning the lead." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUnassignLead(int id)
    {
        try
        {
            var result = await _leadRequestService.UnassignLeadAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning lead");
            return Json(new { success = false, message = "An error occurred while unassigning the lead." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCancelLead(int id, string? description)
    {
        try
        {
            var result = await _leadRequestService.CancelLeadAsync(id, description);
            if (result.Success)
                await RecordActivityAsync(id, "lead_cancelled", $"Lead cancelled.{(string.IsNullOrWhiteSpace(description) ? "" : $" Reason: {description}")}");
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling lead");
            return Json(new { success = false, message = "An error occurred while cancelling the lead." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostReactivateLead(int id)
    {
        try
        {
            var result = await _leadRequestService.ReactivateLeadAsync(id);
            if (result.Success)
                await RecordActivityAsync(id, "lead_reactivated", "Lead reactivated and returned to New stage.");
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating lead");
            return Json(new { success = false, message = "An error occurred while reactivating the lead." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeactivateLead(int id)
    {
        try
        {
            var result = await _leadRequestService.DeactivateLeadAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating lead");
            return Json(new { success = false, message = "An error occurred while deactivating the lead." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateRequestDetails([FromBody] UpdateRequestDetailsRequest request)
    {
        try
        {
            var result = await _leadRequestService.UpdateLeadDetailsAsync(
                request.Id, request.ProductId, request.LeadSourceTypeId,
                request.LeadSourceReferenceTypeId, request.SourceUrl, request.RequestText);
            if (result.Success)
                await RecordActivityAsync(request.Id, "request_details_updated", "Lead information updated.");
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lead details");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostMarkAsWon(int id)
    {
        try
        {
            var result = await _leadRequestService.MarkAsWonAsync(id);
            if (result.Success)
                await RecordActivityAsync(id, "marked_as_won", "Lead marked as Won. Contact converted to customer.");
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking lead as won");
            return Json(new { success = false, message = "An error occurred while marking the lead as won." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetPipelineData(string? assignedToUserId, int? productId, int? teamMemberId)
    {
        try
        {
            var data = await _leadRequestService.GetPipelineDataAsync(assignedToUserId, productId, teamMemberId);
            var cancelledLeads = await _leadRequestService.GetCancelledLeadsAsync();
            return Json(new { success = true, data, cancelledLeads });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pipeline data");
            return Json(new { success = false, message = "Failed to load pipeline data." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetLeadDetail(int id)
    {
        try
        {
            var detail = await _leadRequestService.GetLeadDetailAsync(id);
            if (detail == null)
                return Json(new { success = false, message = "Lead not found." });
            return Json(new { success = true, data = detail });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading lead detail");
            return Json(new { success = false, message = "Failed to load lead details." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — MEETINGS
    // ═══════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateMeeting([FromBody] CreateMeetingRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _meetingService.CreateMeetingAsync(request, userId);
            if (result.Success && request.LeadRequestId.HasValue)
                await RecordActivityAsync(request.LeadRequestId.Value, "meeting_scheduled", $"Meeting scheduled: {request.Subject}.");
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating meeting");
            return Json(new { success = false, message = "An error occurred while creating the meeting." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateMeeting([FromBody] UpdateMeetingRequest request)
    {
        try
        {
            var result = await _meetingService.UpdateMeetingAsync(request);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating meeting");
            return Json(new { success = false, message = "An error occurred while updating the meeting." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCancelMeeting(int id, string? description)
    {
        try
        {
            var meeting = await _meetingService.GetByIdAsync(id);
            var result = await _meetingService.CancelMeetingAsync(id, description);
            if (result.Success && meeting?.LeadRequestId.HasValue == true)
                await RecordActivityAsync(meeting.LeadRequestId.Value, "meeting_cancelled", $"Meeting cancelled: {meeting.Subject}.");
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling meeting");
            return Json(new { success = false, message = "An error occurred while cancelling the meeting." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostReactivateMeeting(int id)
    {
        try
        {
            var result = await _meetingService.ReactivateMeetingAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating meeting");
            return Json(new { success = false, message = "An error occurred while reactivating the meeting." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetDownloadIcs(int id)
    {
        try
        {
            var bytes = await _meetingService.GenerateIcsFileAsync(id);
            if (bytes.Length == 0) return NotFound();
            return File(bytes, "text/calendar", "meeting.ics");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ICS file");
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateMeetingProductRequest([FromBody] CreateMeetingProductRequestDto request)
    {
        try
        {
            var result = await _meetingService.CreateProductRequestAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating meeting product request");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateMeetingOpportunity([FromBody] CreateMeetingOpportunityDto request)
    {
        try
        {
            var result = await _meetingService.CreateOpportunityAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating meeting opportunity");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — RESPONSES & TEMPLATES
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> AxGetPrepareResponse(int leadRequestId)
    {
        try
        {
            var prepared = await _responseService.PrepareResponseAsync(leadRequestId);
            if (prepared == null)
                return Json(new { success = false, message = "Lead not found." });
            return Json(new { success = true, data = prepared });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing response");
            return Json(new { success = false, message = "Failed to prepare response." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSendResponse([FromBody] SendResponseRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _responseService.SendResponseAsync(request, userId);
            if (result.Success)
                await RecordActivityAsync(request.LeadRequestId, "response_logged", "Response logged.");
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending response");
            return Json(new { success = false, message = "An error occurred while sending the response." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateTemplate([FromBody] CreateTemplateRequest request)
    {
        try
        {
            var result = await _responseService.CreateTemplateAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template");
            return Json(new { success = false, message = "An error occurred while creating the template." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateTemplate([FromBody] UpdateTemplateRequest request)
    {
        try
        {
            var result = await _responseService.UpdateTemplateAsync(request);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template");
            return Json(new { success = false, message = "An error occurred while updating the template." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeactivateTemplate(int id)
    {
        try
        {
            var result = await _responseService.DeactivateTemplateAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating template");
            return Json(new { success = false, message = "An error occurred while deactivating the template." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostActivateTemplate(int id)
    {
        try
        {
            var result = await _responseService.ActivateTemplateAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating template");
            return Json(new { success = false, message = "An error occurred while activating the template." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetTemplateById(int id)
    {
        try
        {
            var template = await _responseService.GetTemplateByIdAsync(id);
            if (template == null)
                return Json(new { success = false, message = "Template not found." });

            return Json(new { success = true, data = template });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading template");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetTemplatesForLead()
    {
        try
        {
            var templates = await _responseService.GetActiveTemplatesAsync();
            return Json(new { success = true, data = templates });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading active templates");
            return Json(new { success = false, message = "Failed to load templates." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetRenderTemplate(int templateId, int leadRequestId)
    {
        try
        {
            var rendered = await _responseService.RenderTemplateForLeadAsync(templateId, leadRequestId);
            if (rendered == null)
                return Json(new { success = false, message = "Template or lead not found." });

            return Json(new { success = true, data = rendered });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering template");
            return Json(new { success = false, message = "Failed to render template." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — TEAM MEMBERS
    // ═══════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateTeamMember([FromBody] CreateTeamMemberRequest request)
    {
        try
        {
            var result = await _teamMemberService.CreateAsync(request);
            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating team member");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateTeamMember([FromBody] UpdateTeamMemberRequest request)
    {
        try
        {
            var result = await _teamMemberService.UpdateAsync(request);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating team member");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostDeactivateTeamMember(int id)
    {
        try
        {
            var result = await _teamMemberService.DeactivateAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating team member");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostActivateTeamMember(int id)
    {
        try
        {
            var result = await _teamMemberService.ActivateAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating team member");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetTeamMembers()
    {
        try
        {
            var members = await _teamMemberService.GetActiveAsync();
            return Json(new { success = true, data = members });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading team members");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostAssignTeamMember(int leadId, int teamMemberId)
    {
        try
        {
            var result = await _leadRequestService.AssignToTeamMemberAsync(leadId, teamMemberId);
            if (result.Success)
            {
                var member = await _teamMemberService.GetByIdAsync(teamMemberId);
                var memberName = member?.DisplayName ?? "Unknown";
                await RecordActivityAsync(leadId, "assigned", $"Assigned to {memberName}.");
            }
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning team member");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUnassignTeamMember(int leadId)
    {
        try
        {
            var result = await _leadRequestService.UnassignTeamMemberAsync(leadId);
            if (result.Success)
                await RecordActivityAsync(leadId, "unassigned", "Lead unassigned.");
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning team member");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — ACTIVITY FEED
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> AxGetActivityFeed(int leadRequestId, int page = 1)
    {
        try
        {
            var feed = await _activityFeedService.GetByLeadAsync(leadRequestId, page);
            return Json(new { success = true, data = feed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading activity feed");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetGlobalActivityFeed(int page = 1)
    {
        try
        {
            var feed = await _activityFeedService.GetAllAsync(page);
            return Json(new { success = true, data = feed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading global activity feed");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — PROPOSAL & INVOICE LINKING
    // ═══════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostLinkProposal(int leadRequestId, int quotationId)
    {
        try
        {
            var result = await _leadRequestService.LinkProposalAsync(leadRequestId, quotationId);
            if (result.Success)
                await RecordActivityAsync(leadRequestId, "proposal_linked", $"Proposal #{quotationId} linked to lead.");
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking proposal");
            return Json(new { success = false, message = "An error occurred while linking the proposal." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostLinkInvoice(int leadRequestId, int invoiceId)
    {
        try
        {
            var result = await _leadRequestService.LinkInvoiceAsync(leadRequestId, invoiceId);
            if (result.Success)
                await RecordActivityAsync(leadRequestId, "invoice_linked", $"Invoice #{invoiceId} linked to lead.");
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking invoice");
            return Json(new { success = false, message = "An error occurred while linking the invoice." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — LOOKUPS (for forms)
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> AxGetLookups()
    {
        try
        {
            var statuses = await _statusTypeRepository.GetAllAsync();
            var sources = await _sourceTypeRepository.GetAllAsync();
            var sourceRefs = await _sourceRefTypeRepository.GetAllAsync();
            var responseTypes = await _responseTypeRepository.GetAllAsync();
            var meetingTypes = await _meetingTypeRepository.GetAllAsync();
            var products = await _productService.GetActiveProductsAsync();
            var teamMembers = await _teamMemberService.GetActiveAsync();

            return Json(new
            {
                success = true,
                data = new
                {
                    statuses = statuses.Select(s => new { s.Id, s.Name, s.DisplayOrder, s.Colour, s.IsTerminal }),
                    sources = sources.Select(s => new { s.Id, s.Name }),
                    sourceReferences = sourceRefs.Select(s => new { s.Id, s.Name }),
                    responseTypes = responseTypes.Select(r => new { r.Id, r.Name }),
                    meetingTypes = meetingTypes.Select(m => new { m.Id, m.Name }),
                    products = products.Select(p => new { p.Id, p.Name }),
                    teamMembers = teamMembers.Select(t => new { t.Id, t.DisplayName })
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading lookups");
            return Json(new { success = false, message = "Failed to load lookup data." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — FOLLOW-UP TASKS
    // ═══════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateTask([FromBody] CreateFollowUpTaskRequest request)
    {
        try
        {
            if (request == null)
                return Json(new { success = false, message = "Invalid request data." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var result = await _followUpTaskService.CreateTaskAsync(request, userId);

            if (result.Success && request.LeadRequestId.HasValue)
                await RecordActivityAsync(request.LeadRequestId.Value, "task_created", $"Follow-up task created: {request.Title}");

            return Json(new { success = result.Success, message = result.Message, id = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating follow-up task");
            return Json(new { success = false, message = "An error occurred while creating the task." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCompleteTask(int id)
    {
        try
        {
            var result = await _followUpTaskService.CompleteTaskAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing follow-up task");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostReopenTask(int id)
    {
        try
        {
            var result = await _followUpTaskService.ReopenTaskAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reopening follow-up task");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostMarkTaskUnprocessed(int id)
    {
        try
        {
            var result = await _followUpTaskService.MarkTaskUnprocessedAsync(id);

            if (!result.Success)
            {
                return Json(new { success = false, message = result.Message });
            }

            return Json(new { success = true, message = "Task marked as unprocessed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking follow-up task as unprocessed");
            return Json(new { success = false, message = "Something went wrong. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateTask([FromBody] UpdateFollowUpTaskRequest request)
    {
        try
        {
            if (request == null)
                return Json(new { success = false, message = "Invalid request data." });

            var result = await _followUpTaskService.UpdateTaskAsync(request.Id, request.Title, request.TaskType, request.DueAtUtc, request.Notes, request.ScheduledTimeUtc);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating follow-up task");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSnoozeTask(int id, DateTime newDueDate)
    {
        try
        {
            var result = await _followUpTaskService.SnoozeTaskAsync(id, newDueDate);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error snoozing follow-up task");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetTodaysActions(int? teamMemberId)
    {
        try
        {
            var tasks = await _followUpTaskService.GetTodaysActionsAsync(teamMemberId);
            return Json(new { success = true, data = tasks });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading today's actions");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetUpcomingMeetingsBrief()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var meetings = await _meetingService.GetUpcomingMeetingsBriefAsync(businessId);
            return Json(new { success = true, data = meetings });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load upcoming meetings brief");
            return Json(new { success = true, data = new List<MeetingBriefDto>() });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetTasksByLead(int leadRequestId)
    {
        try
        {
            var tasks = await _followUpTaskService.GetByLeadIdAsync(leadRequestId);
            return Json(new { success = true, data = tasks });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tasks for lead");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetTasksPaged(string? status, string? taskType, int? teamMemberId, DateTime? dateFrom, DateTime? dateTo, int page = 1)
    {
        try
        {
            var filter = new FollowUpTaskFilter
            {
                Status = status,
                TaskType = taskType,
                TeamMemberId = teamMemberId,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            var result = await _followUpTaskService.GetTasksPagedAsync(filter, page, 15);
            return Json(new
            {
                success = true,
                data = result.Items,
                totalCount = result.TotalCount,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading paged tasks");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetOverdueTaskCount(int? teamMemberId)
    {
        try
        {
            var count = await _followUpTaskService.GetOverdueCountAsync(teamMemberId);
            return Json(new { success = true, count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading overdue task count");
            return Json(new { success = false, message = "An error occurred." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — LEAD PRIORITY
    // ═══════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSetLeadPriority(int leadRequestId, int leadPriorityTypeId)
    {
        try
        {
            var result = await _leadRequestService.SetPriorityAsync(leadRequestId, leadPriorityTypeId);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting lead priority");
            return Json(new { success = false, message = "Something went wrong. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostClearLeadPriority(int leadRequestId)
    {
        try
        {
            var result = await _leadRequestService.ClearPriorityAsync(leadRequestId);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing lead priority");
            return Json(new { success = false, message = "Something went wrong. Please try again." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetLeadPriorityTypes()
    {
        try
        {
            var types = await _leadRequestService.GetPriorityTypesAsync();
            return Json(new { success = true, data = types });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading lead priority types");
            return Json(new { success = false, message = "Failed to load priority types." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — INSIGHTS
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> AxGetInsightsMetrics(DateTime startDate, DateTime endDate)
    {
        try
        {
            if (startDate >= endDate)
                return Json(new { success = false, message = "Start date must be before end date." });

            var metrics = await _insightsService.GetMetricsAsync(startDate, endDate);
            return Json(new { success = true, data = metrics });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading insights metrics");
            return Json(new { success = false, message = "Failed to load insights metrics." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // AJAX — TIMELINE
    // ═══════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> AxGetLeadTimeline(int leadRequestId, int page = 1)
    {
        try
        {
            var pageSize = 20;
            var result = await _timelineService.GetTimelineAsync(leadRequestId, page, pageSize);
            return Json(new { success = true, data = result.Items, hasMore = result.TotalCount > page * pageSize });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading lead timeline");
            return Json(new { success = false, message = "Failed to load timeline." });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // PRIVATE — ACTIVITY FEED RECORDING
    // ═══════════════════════════════════════════════════════════

    private async Task RecordActivityAsync(int leadRequestId, string action, string description)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var businessId = _tenantService.CurrentBusinessId;

            await _activityFeedService.RecordAsync(new ActivityEntry
            {
                BusinessId = businessId,
                LeadRequestId = leadRequestId,
                Action = action,
                Description = description,
                PerformedByUserId = userId
            });
        }
        catch (Exception ex)
        {
            // Non-blocking: log but don't fail the primary action
            _logger.LogWarning(ex, "Failed to record activity for lead {LeadRequestId}", leadRequestId);
        }
    }
}