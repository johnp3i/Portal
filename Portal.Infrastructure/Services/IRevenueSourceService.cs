using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for Revenue Source management (POS devices/registers).
/// </summary>
public interface IRevenueSourceService
{
    Task<List<RevenueSource>> GetAllAsync();
    Task<List<RevenueSource>> GetActiveAsync();
    Task<RevenueSource?> GetByIdAsync(int id);
    Task<ServiceResult> CreateAsync(RevenueSource source);
    Task<ServiceResult> UpdateAsync(RevenueSource source);
    Task<ServiceResult> ToggleActiveAsync(int id, bool isActive);
}
