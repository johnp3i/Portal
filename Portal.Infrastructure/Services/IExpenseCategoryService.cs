using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for expense category management.
/// </summary>
public interface IExpenseCategoryService
{
    Task<List<ExpenseCategory>> GetExpenseCategoriesAsync();
    Task<List<ExpenseCategory>> GetActiveExpenseCategoriesAsync();
    Task<ExpenseCategory?> GetExpenseCategoryByIdAsync(int id);
    Task<ServiceResult> CreateExpenseCategoryAsync(ExpenseCategory category);
    Task<ServiceResult> UpdateExpenseCategoryAsync(ExpenseCategory category);
    Task<ServiceResult> DeactivateExpenseCategoryAsync(int id);
}
