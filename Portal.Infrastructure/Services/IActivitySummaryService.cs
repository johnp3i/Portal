using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Transforms raw AuditLog records into business-friendly activity summaries
/// and computes weekly quick stats.
/// </summary>
public interface IActivitySummaryService
{
    /// <summary>
    /// Transforms a list of AuditLog records into ActivityItemDtos with plain-English summaries.
    /// </summary>
    Task<List<ActivityItemDto>> TransformAsync(List<AuditLog> records);

    /// <summary>
    /// Computes weekly quick stats for the current business.
    /// </summary>
    Task<ActivityStatsDto> GetQuickStatsAsync();
}
