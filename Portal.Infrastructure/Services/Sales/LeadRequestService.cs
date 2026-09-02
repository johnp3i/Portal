using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for lead request pipeline management.
/// </summary>
public class LeadRequestService : ILeadRequestService
{
    private readonly LeadRequestRepository _leadRequestRepository;
    private readonly SalesContactRepository _contactRepository;
    private readonly SalesProductRepository _productRepository;
    private readonly LeadStatusTypeRepository _statusTypeRepository;
    private readonly LeadSourceTypeRepository _sourceTypeRepository;
    private readonly LeadSourceReferenceTypeRepository _sourceRefTypeRepository;
    private readonly LeadResponseRepository _responseRepository;
    private readonly LeadResponseTypeRepository _responseTypeRepository;
    private readonly MeetingRepository _meetingRepository;
    private readonly MeetingTypeRepository _meetingTypeRepository;
    private readonly LeadPriorityTypeRepository _priorityTypeRepository;
    private readonly LeadTrackingHistoryRepository _leadTrackingHistoryRepository;
    private readonly IContactService _contactService;
    private readonly ICurrentTenantService _tenantService;
    private readonly PortalDbContext _context;

    public LeadRequestService(
        LeadRequestRepository leadRequestRepository,
        SalesContactRepository contactRepository,
        SalesProductRepository productRepository,
        LeadStatusTypeRepository statusTypeRepository,
        LeadSourceTypeRepository sourceTypeRepository,
        LeadSourceReferenceTypeRepository sourceRefTypeRepository,
        LeadResponseRepository responseRepository,
        LeadResponseTypeRepository responseTypeRepository,
        MeetingRepository meetingRepository,
        MeetingTypeRepository meetingTypeRepository,
        LeadPriorityTypeRepository priorityTypeRepository,
        LeadTrackingHistoryRepository leadTrackingHistoryRepository,
        IContactService contactService,
        ICurrentTenantService tenantService,
        PortalDbContext context)
    {
        _leadRequestRepository = leadRequestRepository;
        _contactRepository = contactRepository;
        _productRepository = productRepository;
        _statusTypeRepository = statusTypeRepository;
        _sourceTypeRepository = sourceTypeRepository;
        _sourceRefTypeRepository = sourceRefTypeRepository;
        _responseRepository = responseRepository;
        _responseTypeRepository = responseTypeRepository;
        _meetingRepository = meetingRepository;
        _meetingTypeRepository = meetingTypeRepository;
        _priorityTypeRepository = priorityTypeRepository;
        _leadTrackingHistoryRepository = leadTrackingHistoryRepository;
        _contactService = contactService;
        _tenantService = tenantService;
        _context = context;
    }

    public async Task<ServiceResult> CreateLeadRequestAsync(CreateLeadRequestDto request)
    {
        try
        {
            var entity = new LeadRequest
            {
                BusinessId = _tenantService.CurrentBusinessId,
                ContactId = request.ContactId,
                ProductId = request.ProductId,
                LeadSourceTypeId = request.LeadSourceTypeId,
                LeadSourceReferenceTypeId = request.LeadSourceReferenceTypeId,
                LeadStatusTypeId = 1, // New
                SourceUrl = request.SourceUrl,
                RequestText = request.RequestText,
                IsCancelled = false,
                IsActive = true
            };

            var id = await _leadRequestRepository.InsertAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ChangeStageAsync(int id, int leadStatusTypeId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            // Terminal stages: Won (6), Lost (7). Inactive (8) is excluded from ClosedAtUtc logic.
            const int WonStageId = 6;
            const int LostStageId = 7;

            var lead = await _leadRequestRepository.GetByIdAsync(id, businessId);
            if (lead == null)
                return ServiceResult.Fail("Lead not found.");

            var currentStageIsTerminal = lead.LeadStatusTypeId == WonStageId || lead.LeadStatusTypeId == LostStageId;
            var newStageIsTerminal = leadStatusTypeId == WonStageId || leadStatusTypeId == LostStageId;

            await _leadRequestRepository.UpdateStageAsync(id, businessId, leadStatusTypeId);

            // ClosedAtUtc lifecycle: set when transitioning TO terminal, clear when reopening FROM terminal
            if (newStageIsTerminal && !currentStageIsTerminal && lead.ClosedAtUtc == null)
            {
                await _leadRequestRepository.SetClosedAtUtcAsync(id, DateTime.UtcNow, businessId);
            }
            else if (currentStageIsTerminal && !newStageIsTerminal)
            {
                await _leadRequestRepository.SetClosedAtUtcAsync(id, null, businessId);
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> AssignLeadAsync(int id, string userId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _leadRequestRepository.UpdateAssignmentAsync(id, businessId, userId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> AssignToTeamMemberAsync(int id, int teamMemberId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _leadRequestRepository.UpdateTeamMemberAsync(id, businessId, teamMemberId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UnassignTeamMemberAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _leadRequestRepository.UpdateTeamMemberAsync(id, businessId, null);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UnassignLeadAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _leadRequestRepository.UpdateAssignmentAsync(id, businessId, null);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CancelLeadAsync(int id, string? description)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _leadRequestRepository.CancelAsync(id, businessId, description);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> DeactivateLeadAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _leadRequestRepository.DeactivateAsync(id, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ReactivateLeadAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var lead = await _leadRequestRepository.GetByIdAsync(id, businessId);
            if (lead == null)
                return ServiceResult.Fail("Lead not found.");

            if (!lead.IsCancelled)
                return ServiceResult.Fail("This lead is not cancelled.");

            await _leadRequestRepository.ReactivateAsync(id, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<LeadCardDto>> GetCancelledLeadsAsync()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var leads = await _leadRequestRepository.GetByBusinessIdAsync(businessId);
            var cancelledLeads = leads.Where(l => l.IsCancelled).ToList();

            var result = new List<LeadCardDto>();
            foreach (var lead in cancelledLeads)
            {
                var contact = await _contactRepository.GetByIdAsync(lead.ContactId, businessId);
                var contactName = contact != null
                    ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                    : "Unknown";

                result.Add(new LeadCardDto
                {
                    Id = lead.Id,
                    LeadNumber = lead.LeadNumber,
                    ContactName = contactName,
                    CompanyName = contact?.CompanyName,
                    CreatedAtUtc = lead.CreatedAtUtc,
                    LeadStatusTypeId = lead.LeadStatusTypeId
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateRequestDetailsAsync(int id, string? requestText)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var lead = await _leadRequestRepository.GetByIdAsync(id, businessId);
            if (lead == null)
                return ServiceResult.Fail("Lead not found.");

            await _leadRequestRepository.UpdateRequestTextAsync(id, businessId, requestText);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateLeadDetailsAsync(int id, int? productId, int leadSourceTypeId, int? leadSourceReferenceTypeId, string? sourceUrl, string? requestText)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var lead = await _leadRequestRepository.GetByIdAsync(id, businessId);
            if (lead == null)
                return ServiceResult.Fail("Lead not found.");

            await _leadRequestRepository.UpdateLeadDetailsAsync(id, businessId, productId, leadSourceTypeId, leadSourceReferenceTypeId, sourceUrl, requestText);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> MarkAsWonAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var lead = await _leadRequestRepository.GetByIdAsync(id, businessId);
            if (lead == null)
                return ServiceResult.Fail("Lead not found.");

            // Set stage to Won (6)
            await _leadRequestRepository.UpdateStageAsync(id, businessId, 6);

            // Trigger Contact→Customer conversion
            var conversionResult = await _contactService.ConvertToCustomerAsync(lead.ContactId);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> LinkProposalAsync(int leadRequestId, int quotationId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            const string query = @"
                UPDATE [quotation].[Quotation]
                SET [LeadRequestId] = @LeadRequestId
                WHERE [Id] = @QuotationId AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@LeadRequestId", leadRequestId),
                new SqlParameter("@QuotationId", quotationId),
                new SqlParameter("@BusinessId", businessId));

            // Suggest stage transition to Proposal Sent (5)
            await SuggestStageTransitionAsync(leadRequestId, "proposal_linked", quotationId);

            // Record activity
            try
            {
                var reference = await _context.Quotations
                    .Where(q => q.Id == quotationId)
                    .Select(q => q.Reference)
                    .FirstOrDefaultAsync() ?? $"#{quotationId}";

                const string activityQuery = @"
                    INSERT INTO [sales].[ActivityFeed]
                        ([BusinessId], [LeadRequestId], [Action], [Description], [PerformedByUserId], [PerformedByTeamMemberId], [Metadata])
                    VALUES
                        (@BusinessId, @LeadRequestId, @Action, @Description, NULL, NULL, NULL)";

                await _context.Database.ExecuteSqlRawAsync(activityQuery,
                    new SqlParameter("@BusinessId", businessId),
                    new SqlParameter("@LeadRequestId", leadRequestId),
                    new SqlParameter("@Action", "proposal_linked"),
                    new SqlParameter("@Description", $"Proposal {reference} linked to lead."));
            }
            catch { /* Non-blocking */ }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> LinkInvoiceAsync(int leadRequestId, int invoiceId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            const string query = @"
                UPDATE [invoice].[Invoice]
                SET [LeadRequestId] = @LeadRequestId
                WHERE [Id] = @InvoiceId AND [BusinessId] = @BusinessId";

            await _context.Database.ExecuteSqlRawAsync(query,
                new SqlParameter("@LeadRequestId", leadRequestId),
                new SqlParameter("@InvoiceId", invoiceId),
                new SqlParameter("@BusinessId", businessId));

            // Record activity
            try
            {
                var invoiceNumber = await _context.Invoices
                    .Where(i => i.Id == invoiceId)
                    .Select(i => i.InvoiceNumber)
                    .FirstOrDefaultAsync() ?? $"#{invoiceId}";

                const string activityQuery = @"
                    INSERT INTO [sales].[ActivityFeed]
                        ([BusinessId], [LeadRequestId], [Action], [Description], [PerformedByUserId], [PerformedByTeamMemberId], [Metadata])
                    VALUES
                        (@BusinessId, @LeadRequestId, @Action, @Description, NULL, NULL, NULL)";

                await _context.Database.ExecuteSqlRawAsync(activityQuery,
                    new SqlParameter("@BusinessId", businessId),
                    new SqlParameter("@LeadRequestId", leadRequestId),
                    new SqlParameter("@Action", "invoice_linked"),
                    new SqlParameter("@Description", $"Invoice {invoiceNumber} linked to lead."));
            }
            catch { /* Non-blocking */ }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<LeadRequestDetailDto?> GetLeadDetailAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var lead = await _leadRequestRepository.GetByIdAsync(id, businessId);
            if (lead == null) return null;

            var contact = await _contactRepository.GetByIdAsync(lead.ContactId, businessId);
            var statuses = await _statusTypeRepository.GetAllAsync();
            var sources = await _sourceTypeRepository.GetAllAsync();
            var sourceRefs = await _sourceRefTypeRepository.GetAllAsync();
            var responseTypes = await _responseTypeRepository.GetAllAsync();
            var meetingTypes = await _meetingTypeRepository.GetAllAsync();

            var status = statuses.FirstOrDefault(s => s.Id == lead.LeadStatusTypeId);
            var source = sources.FirstOrDefault(s => s.Id == lead.LeadSourceTypeId);
            var sourceRef = lead.LeadSourceReferenceTypeId.HasValue
                ? sourceRefs.FirstOrDefault(s => s.Id == lead.LeadSourceReferenceTypeId.Value)
                : null;

            // Get responses
            var responses = await _responseRepository.GetByLeadRequestIdAsync(lead.Id);

            // Get meetings
            var meetings = await _meetingRepository.GetByLeadRequestIdAsync(lead.Id, businessId);

            // Get linked quotations
            var linkedQuotations = await _context.Quotations
                .Where(q => q.LeadRequestId == lead.Id && !q.IsDeleted)
                .Select(q => new LinkedDocumentDto
                {
                    Id = q.Id,
                    Reference = q.Reference,
                    Status = q.QuotationStatusType.Name,
                    TotalAmount = q.TotalAmount,
                    CreatedAtUtc = q.CreatedAtUtc
                }).ToListAsync();

            // Get linked invoices
            var linkedInvoices = await _context.Invoices
                .Where(i => i.LeadRequestId == lead.Id)
                .Select(i => new LinkedDocumentDto
                {
                    Id = i.Id,
                    Reference = i.InvoiceNumber,
                    Status = i.InvoiceStatusType.Name,
                    TotalAmount = i.TotalAmount,
                    CreatedAtUtc = i.CreatedAtUtc
                }).ToListAsync();

            SalesProduct? product = null;
            if (lead.ProductId.HasValue)
                product = await _productRepository.GetByIdAsync(lead.ProductId.Value, businessId);

            var dto = new LeadRequestDetailDto
            {
                Id = lead.Id,
                LeadNumber = lead.LeadNumber,
                ContactId = lead.ContactId,
                ContactName = contact != null
                    ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                    : "Unknown",
                ContactEmail = contact?.Email,
                ContactPhone = contact?.PhoneNumber,
                CompanyName = contact?.CompanyName,
                ProductId = lead.ProductId,
                ProductName = product?.Name,
                SourceName = source?.Name ?? "Unknown",
                SourceReferenceName = sourceRef?.Name,
                SourceUrl = lead.SourceUrl,
                RequestText = lead.RequestText,
                LeadSourceTypeId = lead.LeadSourceTypeId,
                LeadSourceReferenceTypeId = lead.LeadSourceReferenceTypeId,
                LeadStatusTypeId = lead.LeadStatusTypeId,
                StageName = status?.Name ?? "Unknown",
                StageColour = status?.Colour,
                IsTerminal = status?.IsTerminal ?? false,
                AssignedToUserId = lead.AssignedToUserId,
                TeamMemberId = lead.TeamMemberId,
                LeadPriorityTypeId = lead.LeadPriorityTypeId,
                IsCancelled = lead.IsCancelled,
                CancellationDescription = lead.CancellationDescription,
                CreatedAtUtc = lead.CreatedAtUtc,
                Responses = responses.Select(r => new LeadResponseHistoryDto
                {
                    Id = r.Id,
                    ResponseTypeName = responseTypes.FirstOrDefault(rt => rt.Id == r.LeadResponseTypeId)?.Name ?? "Unknown",
                    ResponseText = r.ResponseText,
                    IsAutomated = r.IsAutomated,
                    SentAtUtc = r.SentAtUtc
                }).ToList(),
                Meetings = meetings.Select(m => new LeadMeetingDto
                {
                    Id = m.Id,
                    Subject = m.Subject,
                    MeetingTypeName = meetingTypes.FirstOrDefault(mt => mt.Id == m.MeetingTypeId)?.Name ?? "Unknown",
                    ScheduledAtUtc = m.ScheduledAtUtc,
                    DurationMinutes = m.DurationMinutes,
                    IsCancelled = m.IsCancelled
                }).ToList(),
                LinkedQuotations = linkedQuotations,
                LinkedInvoices = linkedInvoices
            };

            // Resolve assigned team member name
            if (lead.TeamMemberId.HasValue)
            {
                var teamMember = await _context.TeamMembers.FirstOrDefaultAsync(tm => tm.Id == lead.TeamMemberId.Value && tm.BusinessId == businessId);
                if (teamMember != null)
                {
                    dto.AssignedToUserName = string.IsNullOrWhiteSpace(teamMember.LastName)
                        ? teamMember.FirstName
                        : $"{teamMember.FirstName} {teamMember.LastName}";
                }
            }

            // Resolve priority name and colour
            if (lead.LeadPriorityTypeId.HasValue)
            {
                var priorityTypes = await _priorityTypeRepository.GetAllAsync();
                var priority = priorityTypes.FirstOrDefault(p => p.Id == lead.LeadPriorityTypeId.Value);
                if (priority != null)
                {
                    dto.PriorityName = priority.Name;
                    dto.PriorityColour = priority.Colour;
                }
            }

            return dto;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<PipelineStageGroupDto>> GetPipelineDataAsync(string? assignedToUserId, int? productId, int? teamMemberId = null)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var statuses = await _statusTypeRepository.GetAllAsync();
            var sourceTypes = await _sourceTypeRepository.GetAllAsync();
            var leads = await _leadRequestRepository.GetByBusinessIdAsync(businessId);
            var contacts = new Dictionary<int, SalesContact>();

            // Filter out cancelled leads from the board
            leads = leads.Where(l => !l.IsCancelled).ToList();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(assignedToUserId))
                leads = leads.Where(l => l.AssignedToUserId == assignedToUserId).ToList();

            if (teamMemberId.HasValue)
                leads = leads.Where(l => l.TeamMemberId == teamMemberId.Value).ToList();

            if (productId.HasValue)
                leads = leads.Where(l => l.ProductId == productId.Value).ToList();

            // Batch-load priority types for enrichment
            var priorityTypes = await _priorityTypeRepository.GetAllAsync();

            // Batch-load products for all leads that have a ProductId
            var productIds = leads.Where(l => l.ProductId.HasValue).Select(l => l.ProductId!.Value).Distinct().ToList();
            var productLookup = new Dictionary<int, string>();
            foreach (var pid in productIds)
            {
                var product = await _productRepository.GetByIdAsync(pid, businessId);
                if (product != null) productLookup[pid] = product.Name;
            }

            // Batch-load last activity dates for DaysSinceLastActivity computation
            var leadIds = leads.Select(l => l.Id).ToList();
            var activityDates = await _leadRequestRepository.GetLastActivityDatesAsync(leadIds, businessId);
            var activityDateLookup = activityDates.ToDictionary(a => a.LeadRequestId, a => a.LastActivityDateUtc);

            // Build pipeline groups
            var groups = statuses.Select(s => new PipelineStageGroupDto
            {
                LeadStatusTypeId = s.Id,
                StageName = s.Name,
                Colour = s.Colour,
                DisplayOrder = s.DisplayOrder,
                IsTerminal = s.IsTerminal,
                Count = leads.Count(l => l.LeadStatusTypeId == s.Id),
                Leads = leads
                    .Where(l => l.LeadStatusTypeId == s.Id)
                    .Select(l => new LeadCardDto
                    {
                        Id = l.Id,
                        LeadNumber = l.LeadNumber,
                        ContactName = string.Empty, // Will be filled below
                        AssignedToUserId = l.AssignedToUserId,
                        CreatedAtUtc = l.CreatedAtUtc,
                        LeadStatusTypeId = l.LeadStatusTypeId,
                        LeadPriorityTypeId = l.LeadPriorityTypeId
                    }).ToList()
            }).OrderBy(g => g.DisplayOrder).ToList();

            // Enrich contact names, priority, and days-since-last-activity
            foreach (var group in groups)
            {
                foreach (var card in group.Leads)
                {
                    var lead = leads.First(l => l.Id == card.Id);
                    if (!contacts.ContainsKey(lead.ContactId))
                    {
                        var contact = await _contactRepository.GetByIdAsync(lead.ContactId, businessId);
                        if (contact != null)
                            contacts[lead.ContactId] = contact;
                    }

                    if (contacts.TryGetValue(lead.ContactId, out var c))
                        card.ContactName = string.IsNullOrWhiteSpace(c.LastName) ? c.FirstName : $"{c.FirstName} {c.LastName}";

                    if (lead.ProductId.HasValue && productLookup.TryGetValue(lead.ProductId.Value, out var productName))
                        card.ProductName = productName;

                    card.SourceName = sourceTypes.FirstOrDefault(s => s.Id == lead.LeadSourceTypeId)?.Name;

                    // Enrich priority name and colour
                    if (lead.LeadPriorityTypeId.HasValue)
                    {
                        var priority = priorityTypes.FirstOrDefault(p => p.Id == lead.LeadPriorityTypeId.Value);
                        if (priority != null)
                        {
                            card.PriorityName = priority.Name;
                            card.PriorityColour = priority.Colour;
                        }
                    }

                    // Compute DaysSinceLastActivity from batch-loaded activity dates
                    if (activityDateLookup.TryGetValue(card.Id, out var lastActivityDate))
                    {
                        card.DaysSinceLastActivity = Math.Max(0, (int)(DateTime.UtcNow.Date - lastActivityDate.Date).TotalDays);
                    }
                    else
                    {
                        // Fallback to CreatedAtUtc if no activity date returned
                        card.DaysSinceLastActivity = Math.Max(0, (int)(DateTime.UtcNow.Date - lead.CreatedAtUtc.Date).TotalDays);
                    }
                }
            }

            return groups;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<LeadTableRowDto>> GetLeadsPagedAsync(LeadFilterDto filter)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var paged = await _leadRequestRepository.GetPagedAsync(
                filter.AssignedToUserId, filter.ProductId, filter.LeadStatusTypeId,
                filter.SearchTerm, filter.Page, filter.PageSize, businessId);

            var statuses = await _statusTypeRepository.GetAllAsync();
            var sources = await _sourceTypeRepository.GetAllAsync();

            var rows = new List<LeadTableRowDto>();
            foreach (var lead in paged.Items)
            {
                var contact = await _contactRepository.GetByIdAsync(lead.ContactId, businessId);
                var status = statuses.FirstOrDefault(s => s.Id == lead.LeadStatusTypeId);
                var source = sources.FirstOrDefault(s => s.Id == lead.LeadSourceTypeId);

                string? productName = null;
                if (lead.ProductId.HasValue)
                {
                    var product = await _productRepository.GetByIdAsync(lead.ProductId.Value, businessId);
                    productName = product?.Name;
                }

                rows.Add(new LeadTableRowDto
                {
                    Id = lead.Id,
                    LeadNumber = lead.LeadNumber,
                    ContactName = contact != null
                        ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                        : "Unknown",
                    CompanyName = contact?.CompanyName,
                    ProductName = productName,
                    StageName = status?.Name ?? "Unknown",
                    StageColour = status?.Colour,
                    SourceName = source?.Name ?? "Unknown",
                    IsCancelled = lead.IsCancelled,
                    CreatedAtUtc = lead.CreatedAtUtc
                });
            }

            return new PagedResult<LeadTableRowDto>
            {
                Items = rows,
                CurrentPage = paged.CurrentPage,
                PageSize = paged.PageSize,
                TotalCount = paged.TotalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Suggests a stage transition based on a pipeline event.
    /// Only updates if the current stage is earlier than the suggested stage.
    /// </summary>
    public async Task SuggestStageTransitionAsync(int leadRequestId, string eventType, int? relatedEntityId = null)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var lead = await _leadRequestRepository.GetByIdAsync(leadRequestId, businessId);
            if (lead == null) return;

            // Don't suggest transitions for terminal stages
            var statuses = await _statusTypeRepository.GetAllAsync();
            var currentStatus = statuses.FirstOrDefault(s => s.Id == lead.LeadStatusTypeId);
            if (currentStatus?.IsTerminal == true) return;

            int? suggestedStatusId = eventType switch
            {
                "response_sent" when lead.LeadStatusTypeId == 1 => 2, // New → Contacted
                "meeting_scheduled" when lead.LeadStatusTypeId < 4 => 4, // → Meeting Scheduled
                "proposal_linked" when lead.LeadStatusTypeId < 5 => 5, // → Proposal Sent
                _ => null
            };

            if (suggestedStatusId.HasValue)
            {
                await _leadRequestRepository.UpdateStageAsync(leadRequestId, businessId, suggestedStatusId.Value);

                // Record tracking history
                int actionTypeId = eventType switch
                {
                    "response_sent" => 4,
                    "meeting_scheduled" => 1,
                    "proposal_linked" => 5,
                    _ => 6 // ManualStageChange fallback
                };

                await _leadTrackingHistoryRepository.InsertAsync(new LeadTrackingHistory
                {
                    LeadRequestId = leadRequestId,
                    BusinessId = businessId,
                    LeadTrackingActionTypeId = actionTypeId,
                    FromLeadStatusTypeId = lead.LeadStatusTypeId,
                    ToLeadStatusTypeId = suggestedStatusId.Value,
                    RelatedEntityId = relatedEntityId,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> SetPriorityAsync(int leadRequestId, int leadPriorityTypeId)
    {
        try
        {
            if (leadPriorityTypeId < 1 || leadPriorityTypeId > 3)
                return ServiceResult.Fail("Invalid priority type.");

            var businessId = _tenantService.CurrentBusinessId;
            await _leadRequestRepository.UpdatePriorityAsync(leadRequestId, leadPriorityTypeId, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ClearPriorityAsync(int leadRequestId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _leadRequestRepository.UpdatePriorityAsync(leadRequestId, null, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<LeadPriorityTypeDto>> GetPriorityTypesAsync()
    {
        try
        {
            var priorityTypes = await _priorityTypeRepository.GetAllAsync();
            return priorityTypes.Select(p => new LeadPriorityTypeDto
            {
                Id = p.Id,
                Name = p.Name,
                Colour = p.Colour
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task ReevaluateStageOnMeetingChangeAsync(int leadRequestId, string changeType, int? meetingId = null)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var lead = await _leadRequestRepository.GetByIdAsync(leadRequestId, businessId);
            if (lead == null) return;

            var statuses = await _statusTypeRepository.GetAllAsync();
            var currentStatus = statuses.FirstOrDefault(s => s.Id == lead.LeadStatusTypeId);
            if (currentStatus?.IsTerminal == true) return;

            if (changeType == "meeting_cancelled")
            {
                // Only regress if currently at Meetings stage (4)
                if (lead.LeadStatusTypeId != 4) return;

                // Query all tracking history for this lead
                var history = await _leadTrackingHistoryRepository.GetByLeadRequestIdAsync(leadRequestId, businessId);

                int targetStage;

                // Cold-start fallback: if no history exists (pre-migration lead), use live-state check
                if (!history.Any())
                {
                    var remainingMeetings = await _meetingRepository.GetByLeadRequestIdAsync(leadRequestId, businessId);
                    if (remainingMeetings.Any())
                    {
                        targetStage = 4; // Other active meetings exist, stay at Meetings
                    }
                    else
                    {
                        var responses = await _responseRepository.GetByLeadRequestIdAsync(leadRequestId);
                        targetStage = responses.Any() ? 2 : 1; // Contacted if responses exist, else New
                    }
                }
                else
                {
                    // Filter to forward action types
                    var forwardActionTypes = new[] { 1, 4, 5, 6 };
                    var forwardRecords = history.Where(h => forwardActionTypes.Contains(h.LeadTrackingActionTypeId)).ToList();

                    // Batch-load meeting statuses for MeetingScheduled records
                    var meetingRecordIds = forwardRecords
                        .Where(h => h.LeadTrackingActionTypeId == 1 && h.RelatedEntityId.HasValue)
                        .Select(h => h.RelatedEntityId!.Value)
                        .Distinct()
                        .ToList();

                    var activeMeetingIds = new HashSet<int>();
                    foreach (var mId in meetingRecordIds)
                    {
                        var meeting = await _meetingRepository.GetByIdAsync(mId, businessId);
                        if (meeting != null && !meeting.IsCancelled && meeting.IsActive)
                            activeMeetingIds.Add(mId);
                    }

                    // Validate each forward record
                    var validRecords = forwardRecords.Where(h =>
                    {
                        return h.LeadTrackingActionTypeId switch
                        {
                            1 => h.RelatedEntityId.HasValue && activeMeetingIds.Contains(h.RelatedEntityId.Value), // MeetingScheduled
                            4 => true,  // ResponseSent — always valid
                            5 => true,  // ProposalLinked — always valid
                            6 => true,  // ManualStageChange — always valid
                            _ => false
                        };
                    }).ToList();

                    // Find highest ToLeadStatusTypeId among all valid records
                    targetStage = validRecords.Any()
                        ? validRecords.Max(h => h.ToLeadStatusTypeId)
                        : 1; // Default to New if no valid forward records
                }

                // Write "MeetingCancelled" history record
                await _leadTrackingHistoryRepository.InsertAsync(new LeadTrackingHistory
                {
                    LeadRequestId = leadRequestId,
                    BusinessId = businessId,
                    LeadTrackingActionTypeId = 2, // MeetingCancelled
                    FromLeadStatusTypeId = lead.LeadStatusTypeId,
                    ToLeadStatusTypeId = targetStage,
                    RelatedEntityId = meetingId,
                    CreatedAtUtc = DateTime.UtcNow
                });

                // Update stage only if different
                if (targetStage != lead.LeadStatusTypeId)
                {
                    await _leadRequestRepository.UpdateStageAsync(leadRequestId, businessId, targetStage);
                }
            }
            else if (changeType == "meeting_reactivated")
            {
                // Only advance if below Meetings stage (4)
                if (lead.LeadStatusTypeId >= 4) return;

                // Write "MeetingReactivated" history record
                await _leadTrackingHistoryRepository.InsertAsync(new LeadTrackingHistory
                {
                    LeadRequestId = leadRequestId,
                    BusinessId = businessId,
                    LeadTrackingActionTypeId = 3, // MeetingReactivated
                    FromLeadStatusTypeId = lead.LeadStatusTypeId,
                    ToLeadStatusTypeId = 4,
                    RelatedEntityId = meetingId,
                    CreatedAtUtc = DateTime.UtcNow
                });

                // Advance to stage 4 (Meetings)
                await _leadRequestRepository.UpdateStageAsync(leadRequestId, businessId, 4);
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
