using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for customer management.
/// </summary>
public interface ICustomerService
{
    Task<List<Customer>> GetCustomersAsync(string? searchTerm, bool? isActive);
    Task<PagedResult<Customer>> GetCustomersPagedAsync(string? searchTerm, bool? isActive, int page, int pageSize, int businessId);
    Task<Customer?> GetCustomerByIdAsync(int id);
    Task<Customer?> GetCustomerByIdAsync(int id, int businessId);
    Task<Customer> CreateCustomerAsync(Customer customer);
    Task UpdateCustomerAsync(Customer customer);
    Task DeactivateCustomerAsync(int id);
}
