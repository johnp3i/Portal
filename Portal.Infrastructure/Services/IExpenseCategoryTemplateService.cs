using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Manages expense category templates (platform-wide) and business import.
/// </summary>
public interface IExpenseCategoryTemplateService
{
    Task<List<ExpenseCategoryTemplate>> GetActiveTemplatesAsync();
    Task<List<ExpenseCategoryTemplate>> GetAllTemplatesAsync();
    Task<ServiceResult> CreateAsync(string name, string? description);
    Task<ServiceResult> UpdateAsync(int id, string name, string? description);
    Task<ServiceResult> DeactivateAsync(int id);
    Task<ServiceResult> ReactivateAsync(int id);
    Task<ServiceResult<int>> ImportTemplatesAsync(int businessId, int[] templateIds);
}
