using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Helpers;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

public class ActivityFeedService : IActivityFeedService
{
    private readonly ActivityFeedRepository _repository;
    private readonly ICurrentTenantService _tenantService;
    private readonly UserNameResolver _userNameResolver;
    private readonly ILogger<ActivityFeedService> _logger;

    public ActivityFeedService(
        ActivityFeedRepository repository,
        ICurrentTenantService tenantService,
        UserNameResolver userNameResolver,
        ILogger<ActivityFeedService> logger)
    {
        _repository = repository;
        _tenantService = tenantService;
        _userNameResolver = userNameResolver;
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
}
