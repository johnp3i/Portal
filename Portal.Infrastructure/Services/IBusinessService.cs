using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for tenant administration.
/// </summary>
public interface IBusinessService
{
    Task<List<Business>> GetAllBusinessesAsync();
    Task<Business?> GetBusinessByIdAsync(int id);
    Task<Business> CreateBusinessAsync(string name);
    Task UpdateBusinessAsync(Business business);
    Task DeactivateBusinessAsync(int id);
    Task<bool> IsBusinessNameUniqueAsync(string name, int? excludeId = null);
    Task<BusinessProfile?> GetBusinessProfileAsync(int businessId);
    Task SaveBusinessProfileAsync(BusinessProfile profile);
}
