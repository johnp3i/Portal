using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;
using Portal.Infrastructure.Services;
using Portal.Infrastructure.Services.Sales;
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
        return View(members);
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

        ViewBag.Statuses = statuses;
        ViewBag.ResponseTypes = responseTypes;
        ViewBag.MeetingTypes = meetingTypes;
        ViewBag.Products = products;

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
            var result = await _leadRequestService.UpdateRequestDetailsAsync(request.Id, request.RequestText);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating request details");
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
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking lead as won");
            return Json(new { success = false, message = "An error occurred while marking the lead as won." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetPipelineData(string? assignedToUserId, int? productId)
    {
        try
        {
            var data = await _leadRequestService.GetPipelineDataAsync(assignedToUserId, productId);
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
            var result = await _meetingService.CancelMeetingAsync(id, description);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling meeting");
            return Json(new { success = false, message = "An error occurred while cancelling the meeting." });
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
                    products = products.Select(p => new { p.Id, p.Name })
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading lookups");
            return Json(new { success = false, message = "Failed to load lookup data." });
        }
    }
}
