using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for expense category management.
/// </summary>
public class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly ExpenseCategoryRepository _expenseCategoryRepository;
    private readonly AuditLogRepository _auditLogRepository;
    private readonly ICurrentTenantService _currentTenantService;

    public ExpenseCategoryService(
        ExpenseCategoryRepository expenseCategoryRepository,
        AuditLogRepository auditLogRepository,
        ICurrentTenantService currentTenantService)
    {
        _expenseCategoryRepository = expenseCategoryRepository;
        _auditLogRepository = auditLogRepository;
        _currentTenantService = currentTenantService;
    }

    public async Task<List<ExpenseCategory>> GetExpenseCategoriesAsync()
    {
        return await _expenseCategoryRepository.GetAllByBusinessIdAsync(_currentTenantService.CurrentBusinessId);
    }

    public async Task<List<ExpenseCategory>> GetActiveExpenseCategoriesAsync()
    {
        var categories = await _expenseCategoryRepository.GetAllByBusinessIdAsync(_currentTenantService.CurrentBusinessId);
        return categories.Where(c => c.IsActive).ToList();
    }

    public async Task<ExpenseCategory?> GetExpenseCategoryByIdAsync(int id)
    {
        return await _expenseCategoryRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
    }

    public async Task<ServiceResult> CreateExpenseCategoryAsync(ExpenseCategory category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            return ServiceResult.Fail("Expense category name is required.");
        }

        if (category.ExpenseTypeId is null || category.ExpenseTypeId is not (1 or 2))
        {
            return ServiceResult.Fail("Expense Type is required. Select Services or Goods.");
        }

        category.BusinessId = _currentTenantService.CurrentBusinessId;
        category.IsActive = true;

        var newId = await _expenseCategoryRepository.InsertAsync(category);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Create",
            TableName = "[purchase].[ExpenseCategory]",
            RecordId = newId.ToString(),
            NewValues = $"Name: {category.Name}",
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok(newId);
    }

    public async Task<ServiceResult> UpdateExpenseCategoryAsync(ExpenseCategory category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            return ServiceResult.Fail("Expense category name is required.");
        }

        if (category.ExpenseTypeId is null || category.ExpenseTypeId is not (1 or 2))
        {
            return ServiceResult.Fail("Expense Type is required. Select Services or Goods.");
        }

        var existing = await _expenseCategoryRepository.GetByIdAndBusinessIdAsync(category.Id, _currentTenantService.CurrentBusinessId);

        if (existing == null)
        {
            return ServiceResult.Fail("Expense category not found.");
        }

        category.BusinessId = _currentTenantService.CurrentBusinessId;

        await _expenseCategoryRepository.UpdateAsync(category);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeactivateExpenseCategoryAsync(int id)
    {
        var existing = await _expenseCategoryRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);

        if (existing == null)
        {
            return ServiceResult.Fail("Expense category not found.");
        }

        await _expenseCategoryRepository.DeactivateAsync(id, _currentTenantService.CurrentBusinessId);

        await _auditLogRepository.InsertAsync(new AuditLog
        {
            BusinessId = _currentTenantService.CurrentBusinessId,
            Action = "Deactivate",
            TableName = "[purchase].[ExpenseCategory]",
            RecordId = id.ToString(),
            OldValues = $"IsActive: true",
            NewValues = $"IsActive: false",
            Timestamp = DateTime.UtcNow
        });

        return ServiceResult.Ok();
    }
}
