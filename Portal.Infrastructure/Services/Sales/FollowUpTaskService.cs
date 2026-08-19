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
    private readonly SalesContactRepository _contactRepository;
    private readonly ICurrentTenantService _tenantService;

    private static readonly string[] ValidTaskTypes = { "Call", "Email", "Follow-up", "Meeting Prep", "Other" };

    public FollowUpTaskService(
        FollowUpTaskRepository taskRepository,
        SalesContactRepository contactRepository,
        ICurrentTenantService tenantService)
    {
        _taskRepository = taskRepository;
        _contactRepository = contactRepository;
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
                ScheduledTimeUtc = request.ScheduledTimeUtc,
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

    public async Task<ServiceResult> MarkTaskUnprocessedAsync(int taskId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var task = await _taskRepository.GetByIdAsync(taskId, businessId);

            if (task == null)
                return ServiceResult.Fail("Task not found.");

            if (task.IsCompleted)
                return ServiceResult.Fail("Task is already closed.");

            await _taskRepository.MarkUnprocessedAsync(taskId, businessId);
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

    public async Task<ServiceResult> UpdateTaskAsync(int taskId, string title, string taskType, DateTime dueAtUtc, string? notes, TimeOnly? scheduledTimeUtc)
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

            await _taskRepository.UpdateAsync(taskId, businessId, title.Trim(), taskType, dueAtUtc, notes?.Trim(), scheduledTimeUtc);
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

            // Batch-fetch contacts for all tasks that have a ContactId
            var contactIds = tasks.Where(t => t.ContactId.HasValue).Select(t => t.ContactId!.Value).Distinct();
            var contactsLookup = await _contactRepository.GetByIdsAsync(contactIds, businessId);

            return tasks.Select(t => MapToDto(t, contactsLookup)).ToList();
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

            var contactIds = tasks.Where(t => t.ContactId.HasValue).Select(t => t.ContactId!.Value).Distinct();
            var contactsLookup = await _contactRepository.GetByIdsAsync(contactIds, businessId);

            return tasks.Select(t => MapToDto(t, contactsLookup)).ToList();
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

            var contactIds = items.Where(t => t.ContactId.HasValue).Select(t => t.ContactId!.Value).Distinct();
            var contactsLookup = await _contactRepository.GetByIdsAsync(contactIds, businessId);

            return new PagedResult<FollowUpTaskDto>
            {
                Items = items.Select(t => MapToDto(t, contactsLookup)).ToList(),
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

    public async Task<List<DashboardTaskBriefDto>> GetDashboardBriefAsync(int businessId)
    {
        try
        {
            var tasks = await _taskRepository.GetDashboardBriefAsync(businessId);
            var today = DateTime.UtcNow.Date;

            var contactIds = tasks.Where(t => t.ContactId.HasValue).Select(t => t.ContactId!.Value).Distinct();
            var contactsLookup = await _contactRepository.GetByIdsAsync(contactIds, businessId);

            return tasks.Select(t =>
            {
                string? contactName = null;
                if (t.ContactId.HasValue && contactsLookup.TryGetValue(t.ContactId.Value, out var contact))
                {
                    contactName = string.IsNullOrWhiteSpace(contact.LastName)
                        ? contact.FirstName
                        : $"{contact.FirstName} {contact.LastName}";
                }

                return new DashboardTaskBriefDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    TaskType = t.TaskType,
                    DueAtUtc = t.DueAtUtc,
                    ScheduledTimeUtc = t.ScheduledTimeUtc,
                    ContactName = contactName,
                    Urgency = t.DueAtUtc.Date == today ? "today" : "tomorrow"
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static FollowUpTaskDto MapToDto(FollowUpTask entity, Dictionary<int, SalesContact> contactsLookup)
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

        string? contactName = null;
        if (entity.ContactId.HasValue && contactsLookup.TryGetValue(entity.ContactId.Value, out var contact))
        {
            contactName = string.IsNullOrWhiteSpace(contact.LastName)
                ? contact.FirstName
                : $"{contact.FirstName} {contact.LastName}";
        }

        return new FollowUpTaskDto
        {
            Id = entity.Id,
            LeadRequestId = entity.LeadRequestId,
            ContactName = contactName,
            AssignedToName = null, // TeamMember names resolved separately if needed
            Title = entity.Title,
            TaskType = entity.TaskType,
            DueAtUtc = entity.DueAtUtc,
            Notes = entity.Notes,
            IsCompleted = entity.IsCompleted,
            CompletedAtUtc = entity.CompletedAtUtc,
            SnoozedCount = entity.SnoozedCount,
            TaskOutcome = entity.TaskOutcome,
            ScheduledTimeUtc = entity.ScheduledTimeUtc,
            Urgency = urgency
        };
    }
}
