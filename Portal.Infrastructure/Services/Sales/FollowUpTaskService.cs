using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for follow-up task management.
/// </summary>
public class FollowUpTaskService : IFollowUpTaskService
{
    private readonly FollowUpTaskRepository _taskRepository;
    private readonly ICurrentTenantService _tenantService;

    private static readonly string[] ValidTaskTypes = { "Call", "Email", "Follow-up", "Meeting Prep", "Other" };

    public FollowUpTaskService(
        FollowUpTaskRepository taskRepository,
        ICurrentTenantService tenantService)
    {
        _taskRepository = taskRepository;
        _tenantService = tenantService;
    }

    public async Task<ServiceResult> CreateTaskAsync(CreateFollowUpTaskRequest request, string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return ServiceResult.Fail("Title is required.");

            if (request.Title.Length > 200)
                return ServiceResult.Fail("Title must be 200 characters or fewer.");

            if (!ValidTaskTypes.Contains(request.TaskType))
                return ServiceResult.Fail("Invalid task type.");

            var entity = new FollowUpTask
            {
                BusinessId = _tenantService.CurrentBusinessId,
                LeadRequestId = request.LeadRequestId,
                ContactId = request.ContactId,
                TeamMemberId = request.TeamMemberId,
                Title = request.Title.Trim(),
                TaskType = request.TaskType,
                DueAtUtc = request.DueAtUtc,
                Notes = request.Notes?.Trim(),
                IsCompleted = false,
                SnoozedCount = 0,
                CreatedByUserId = userId
            };

            var id = await _taskRepository.InsertAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CompleteTaskAsync(int taskId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var task = await _taskRepository.GetByIdAsync(taskId, businessId);

            if (task == null)
                return ServiceResult.Fail("Task not found.");

            if (task.IsCompleted)
                return ServiceResult.Fail("Task is already completed.");

            await _taskRepository.CompleteAsync(taskId, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> SnoozeTaskAsync(int taskId, DateTime newDueDate)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var task = await _taskRepository.GetByIdAsync(taskId, businessId);

            if (task == null)
                return ServiceResult.Fail("Task not found.");

            if (task.IsCompleted)
                return ServiceResult.Fail("Cannot snooze a completed task.");

            await _taskRepository.SnoozeAsync(taskId, businessId, newDueDate);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ReopenTaskAsync(int taskId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var task = await _taskRepository.GetByIdAsync(taskId, businessId);

            if (task == null)
                return ServiceResult.Fail("Task not found.");

            if (!task.IsCompleted)
                return ServiceResult.Fail("Task is not completed.");

            await _taskRepository.ReopenAsync(taskId, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateTaskAsync(int taskId, string title, string taskType, DateTime dueAtUtc, string? notes)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(title))
                return ServiceResult.Fail("Title is required.");

            if (title.Length > 200)
                return ServiceResult.Fail("Title must be 200 characters or fewer.");

            if (!ValidTaskTypes.Contains(taskType))
                return ServiceResult.Fail("Invalid task type.");

            var businessId = _tenantService.CurrentBusinessId;
            var task = await _taskRepository.GetByIdAsync(taskId, businessId);

            if (task == null)
                return ServiceResult.Fail("Task not found.");

            await _taskRepository.UpdateAsync(taskId, businessId, title.Trim(), taskType, dueAtUtc, notes?.Trim());
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<FollowUpTaskDto>> GetTodaysActionsAsync(int? teamMemberId = null)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var tasks = await _taskRepository.GetTodaysActionsAsync(businessId, teamMemberId);
            return tasks.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<FollowUpTaskDto>> GetByLeadIdAsync(int leadRequestId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var tasks = await _taskRepository.GetByLeadRequestIdAsync(leadRequestId, businessId);
            return tasks.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<FollowUpTaskDto>> GetTasksPagedAsync(FollowUpTaskFilter filter, int page, int pageSize)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var (items, totalCount) = await _taskRepository.GetPagedAsync(
                businessId, filter.Status, filter.TaskType, filter.TeamMemberId,
                filter.DateFrom, filter.DateTo, page, pageSize);

            return new PagedResult<FollowUpTaskDto>
            {
                Items = items.Select(MapToDto).ToList(),
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

    public async Task<int> GetOverdueCountAsync(int? teamMemberId = null)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            return await _taskRepository.GetOverdueCountAsync(businessId, teamMemberId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static FollowUpTaskDto MapToDto(FollowUpTask entity)
    {
        var today = DateTime.UtcNow.Date;
        var dueDate = entity.DueAtUtc.Date;

        string urgency;
        if (entity.IsCompleted)
            urgency = "completed";
        else if (dueDate < today)
            urgency = "overdue";
        else if (dueDate == today)
            urgency = "today";
        else if (dueDate == today.AddDays(1))
            urgency = "tomorrow";
        else
            urgency = "upcoming";

        return new FollowUpTaskDto
        {
            Id = entity.Id,
            LeadRequestId = entity.LeadRequestId,
            ContactName = entity.Contact != null
                ? string.IsNullOrWhiteSpace(entity.Contact.LastName)
                    ? entity.Contact.FirstName
                    : $"{entity.Contact.FirstName} {entity.Contact.LastName}"
                : null,
            AssignedToName = entity.TeamMember != null
                ? string.IsNullOrWhiteSpace(entity.TeamMember.LastName)
                    ? entity.TeamMember.FirstName
                    : $"{entity.TeamMember.FirstName} {entity.TeamMember.LastName}"
                : null,
            Title = entity.Title,
            TaskType = entity.TaskType,
            DueAtUtc = entity.DueAtUtc,
            Notes = entity.Notes,
            IsCompleted = entity.IsCompleted,
            CompletedAtUtc = entity.CompletedAtUtc,
            SnoozedCount = entity.SnoozedCount,
            Urgency = urgency
        };
    }
}
