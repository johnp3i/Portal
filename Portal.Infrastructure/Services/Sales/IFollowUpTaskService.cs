using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for follow-up task management.
/// </summary>
public interface IFollowUpTaskService
{
    Task<ServiceResult> CreateTaskAsync(CreateFollowUpTaskRequest request, string userId);
    Task<ServiceResult> CompleteTaskAsync(int taskId);
    Task<ServiceResult> MarkTaskUnprocessedAsync(int taskId);
    Task<ServiceResult> ReopenTaskAsync(int taskId);
    Task<ServiceResult> UpdateTaskAsync(int taskId, string title, string taskType, DateTime dueAtUtc, string? notes, TimeOnly? scheduledTimeUtc);
    Task<ServiceResult> SnoozeTaskAsync(int taskId, DateTime newDueDate);
    Task<List<FollowUpTaskDto>> GetTodaysActionsAsync(int? teamMemberId = null);
    Task<List<FollowUpTaskDto>> GetByLeadIdAsync(int leadRequestId);
    Task<PagedResult<FollowUpTaskDto>> GetTasksPagedAsync(FollowUpTaskFilter filter, int page, int pageSize);
    Task<int> GetOverdueCountAsync(int? teamMemberId = null);
    Task<List<DashboardTaskBriefDto>> GetDashboardBriefAsync(int businessId);
}
