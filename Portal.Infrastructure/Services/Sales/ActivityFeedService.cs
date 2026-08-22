using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Helpers;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

public class ActivityFeedService : IActivityFeedService
{
    private readonly ActivityFeedRepository _repository;
    private readonly ICurrentTenantService _tenantService;
    private readonly UserNameResolver _userNameResolver;
    private readonly LeadRequestRepository _leadRequestRepository;
    private readonly SalesContactRepository _contactRepository;
    private readonly ILogger<ActivityFeedService> _logger;

    public ActivityFeedService(
        ActivityFeedRepository repository,
        ICurrentTenantService tenantService,
        UserNameResolver userNameResolver,
        LeadRequestRepository leadRequestRepository,
        SalesContactRepository contactRepository,
        ILogger<ActivityFeedService> logger)
    {
        _repository = repository;
        _tenantService = tenantService;
        _userNameResolver = userNameResolver;
        _leadRequestRepository = leadRequestRepository;
        _contactRepository = contactRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RecordAsync(ActivityEntry entry)
    {
        try
        {
            var entity = new ActivityFeedEntry
            {
                BusinessId = entry.BusinessId,
                LeadRequestId = entry.LeadRequestId,
                Action = entry.Action,
                Description = entry.Description,
                PerformedByUserId = entry.PerformedByUserId,
                PerformedByTeamMemberId = entry.PerformedByTeamMemberId,
                Metadata = entry.Metadata
            };

            await _repository.InsertAsync(entity);
        }
        catch (Exception ex)
        {
            // Non-blocking: log but don't propagate
            _logger.LogWarning(ex, "Failed to record activity feed entry for lead {LeadRequestId}, action {Action}",
                entry.LeadRequestId, entry.Action);
        }
    }

    /// <inheritdoc />
    public async Task<List<ActivityFeedDto>> GetByLeadAsync(int leadRequestId, int page = 1, int pageSize = 20)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var entries = await _repository.GetByLeadRequestIdAsync(leadRequestId, businessId, page, pageSize);

            // Resolve user names
            var userIds = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.PerformedByUserId))
                .Select(e => e.PerformedByUserId!)
                .Distinct();

            var names = await _userNameResolver.ResolveNamesAsync(userIds);

            return entries.Select(e => new ActivityFeedDto
            {
                Id = e.Id,
                Action = e.Action,
                Description = e.Description,
                PerformedByName = !string.IsNullOrWhiteSpace(e.PerformedByUserId)
                    ? _userNameResolver.GetDisplayName(names, e.PerformedByUserId)
                    : null,
                Metadata = e.Metadata,
                CreatedAtUtc = e.CreatedAtUtc
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ActivityFeedDto>> GetAllAsync(int page = 1, int pageSize = 15)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var entries = await _repository.GetAllByBusinessIdAsync(businessId, page, pageSize);

            var userIds = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.PerformedByUserId))
                .Select(e => e.PerformedByUserId!)
                .Distinct();

            var names = await _userNameResolver.ResolveNamesAsync(userIds);

            return entries.Select(e => new ActivityFeedDto
            {
                Id = e.Id,
                Action = e.Action,
                Description = e.Description,
                PerformedByName = !string.IsNullOrWhiteSpace(e.PerformedByUserId)
                    ? _userNameResolver.GetDisplayName(names, e.PerformedByUserId)
                    : null,
                Metadata = e.Metadata,
                CreatedAtUtc = e.CreatedAtUtc
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PagedResult<ActivityFeedPageDto>> GetPagedAsync(ActivityFeedFilter filter, int page = 1, int pageSize = 15)
    {
        try
        {
            if (page < 1) page = 1;
            var businessId = _tenantService.CurrentBusinessId;

            var totalCount = await _repository.GetCountByBusinessIdAsync(businessId, filter.ActionType, filter.DateFrom, filter.DateTo);
            var entries = await _repository.GetPagedByBusinessIdAsync(businessId, filter.ActionType, filter.DateFrom, filter.DateTo, page, pageSize);

            // Resolve user names
            var userIds = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.PerformedByUserId))
                .Select(e => e.PerformedByUserId!)
                .Distinct();
            var names = await _userNameResolver.ResolveNamesAsync(userIds);

            // Batch-resolve lead names
            var leadIds = entries
                .Where(e => e.LeadRequestId > 0)
                .Select(e => e.LeadRequestId)
                .Distinct()
                .ToList();
            var leadNames = await ResolveLeadNamesAsync(leadIds, businessId);

            var items = entries.Select(e => new ActivityFeedPageDto
            {
                Id = e.Id,
                Action = e.Action,
                Description = e.Description,
                PerformedByName = !string.IsNullOrWhiteSpace(e.PerformedByUserId)
                    ? _userNameResolver.GetDisplayName(names, e.PerformedByUserId)
                    : null,
                LeadName = leadNames.TryGetValue(e.LeadRequestId, out var leadName) ? leadName : null,
                Metadata = e.Metadata,
                CreatedAtUtc = e.CreatedAtUtc
            }).ToList();

            return new PagedResult<ActivityFeedPageDto>
            {
                Items = items,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<ActivityFeedDto>> GetRecentAsync(int count = 10)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var entries = await _repository.GetRecentByBusinessIdAsync(businessId, count);

            var userIds = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.PerformedByUserId))
                .Select(e => e.PerformedByUserId!)
                .Distinct();
            var names = await _userNameResolver.ResolveNamesAsync(userIds);

            // Batch-resolve lead names for the pipeline widget
            var leadIds = entries
                .Where(e => e.LeadRequestId > 0)
                .Select(e => e.LeadRequestId)
                .Distinct()
                .ToList();
            var leadNames = await ResolveLeadNamesAsync(leadIds, businessId);

            return entries.Select(e => new ActivityFeedDto
            {
                Id = e.Id,
                Action = e.Action,
                Description = e.Description,
                PerformedByName = !string.IsNullOrWhiteSpace(e.PerformedByUserId)
                    ? _userNameResolver.GetDisplayName(names, e.PerformedByUserId)
                    : null,
                LeadName = leadNames.TryGetValue(e.LeadRequestId, out var leadName) ? leadName : null,
                Metadata = e.Metadata,
                CreatedAtUtc = e.CreatedAtUtc
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private async Task<Dictionary<int, string>> ResolveLeadNamesAsync(List<int> leadRequestIds, int businessId)
    {
        var result = new Dictionary<int, string>();
        if (!leadRequestIds.Any()) return result;

        // Load all leads in one pass
        var leads = new Dictionary<int, LeadRequest>();
        foreach (var leadId in leadRequestIds)
        {
            try
            {
                var lead = await _leadRequestRepository.GetByIdAsync(leadId, businessId);
                if (lead != null) leads[leadId] = lead;
            }
            catch { /* Non-blocking */ }
        }

        // Batch-resolve contact names
        var contactIds = leads.Values.Select(l => l.ContactId).Distinct();
        var contacts = await _contactRepository.GetByIdsAsync(contactIds, businessId);

        foreach (var leadId in leadRequestIds)
        {
            if (leads.TryGetValue(leadId, out var lead))
            {
                contacts.TryGetValue(lead.ContactId, out var contact);
                var name = contact != null
                    ? (string.IsNullOrWhiteSpace(contact.LastName) ? contact.FirstName : $"{contact.FirstName} {contact.LastName}")
                    : $"Lead #{leadId}";
                result[leadId] = name;
            }
            else
            {
                result[leadId] = $"Lead #{leadId}";
            }
        }

        return result;
    }
}
