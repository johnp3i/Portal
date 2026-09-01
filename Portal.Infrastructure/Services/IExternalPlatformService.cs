using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for External Platform management (external systems a business imports sales from,
/// identified by an invoice PlatformCode). All operations are scoped to the current tenant.
/// </summary>
public interface IExternalPlatformService
{
    Task<List<ExternalPlatform>> GetAllAsync(bool includeInactive);
    Task<List<ExternalPlatform>> GetActiveAsync();
    Task<ExternalPlatform?> GetByIdAsync(int id);
    Task<ServiceResult> CreateAsync(string name, string platformCode, string? description);
    Task<ServiceResult> UpdateAsync(int id, string name, string platformCode, string? description);
    Task<ServiceResult> SetActiveAsync(int id, bool isActive);
}
