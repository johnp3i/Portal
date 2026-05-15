using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for customer management.
/// </summary>
public interface ICustomerService
{
    Task<List<Customer>> GetCustomersAsync(string? searchTerm, bool? isActive);
    Task<Customer?> GetCustomerByIdAsync(int id);
    Task<Customer> CreateCustomerAsync(Customer customer);
    Task UpdateCustomerAsync(Customer customer);
    Task DeactivateCustomerAsync(int id);
}
