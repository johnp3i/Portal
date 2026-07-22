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
            await _leadRequestRepository.UpdateStageAsync(id, businessId, leadStatusTypeId);
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
            await SuggestStageTransitionAsync(leadRequestId, "proposal_linked");

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
                LeadStatusTypeId = lead.LeadStatusTypeId,
                StageName = status?.Name ?? "Unknown",
                StageColour = status?.Colour,
                IsTerminal = status?.IsTerminal ?? false,
                AssignedToUserId = lead.AssignedToUserId,
                TeamMemberId = lead.TeamMemberId,
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
                        ContactName = string.Empty, // Will be filled below
                        AssignedToUserId = l.AssignedToUserId,
                        CreatedAtUtc = l.CreatedAtUtc,
                        LeadStatusTypeId = l.LeadStatusTypeId
                    }).ToList()
            }).OrderBy(g => g.DisplayOrder).ToList();

            // Enrich contact names
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

                    if (lead.ProductId.HasValue)
                    {
                        var product = await _productRepository.GetByIdAsync(lead.ProductId.Value, businessId);
                        card.ProductName = product?.Name;
                    }

                    card.SourceName = sourceTypes.FirstOrDefault(s => s.Id == lead.LeadSourceTypeId)?.Name;
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
    public async Task SuggestStageTransitionAsync(int leadRequestId, string eventType)
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
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
