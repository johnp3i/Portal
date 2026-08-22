using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

public interface IActivityFeedService
{
    /// <summary>
    /// Records an activity entry. Non-blocking — failures are logged but do not propagate.
    /// </summary>
    Task RecordAsync(ActivityEntry entry);

    /// <summary>
    /// Gets paginated activity feed for a lead (newest first).
    /// </summary>
    Task<List<ActivityFeedDto>> GetByLeadAsync(int leadRequestId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Gets paginated global activity feed for the business (newest first, across all leads).
    /// </summary>
    Task<List<ActivityFeedDto>> GetAllAsync(int page = 1, int pageSize = 15);

    /// <summary>
    /// Gets filtered, paginated activity feed for the Activity page.
    /// </summary>
    Task<PagedResult<ActivityFeedPageDto>> GetPagedAsync(ActivityFeedFilter filter, int page = 1, int pageSize = 15);

    /// <summary>
    /// Gets the most recent N activity entries for the pipeline summary widget.
    /// </summary>
    Task<List<ActivityFeedDto>> GetRecentAsync(int count = 10);
}
