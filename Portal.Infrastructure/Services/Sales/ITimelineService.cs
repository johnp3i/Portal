using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Aggregates events from multiple sources into a unified timeline for a lead.
/// </summary>
public interface ITimelineService
{
    /// <summary>
    /// Returns a paginated, chronologically ordered timeline of all events for a lead.
    /// Sources: LeadResponse (entity), Meeting (entity), ActivityFeed, and synthetic creation event.
    /// </summary>
    Task<PagedResult<TimelineEventDto>> GetTimelineAsync(int leadRequestId, int page, int pageSize);
}
