using System.Text.RegularExpressions;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for customer management.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly CustomerRepository _customerRepository;
    private readonly ICurrentTenantService _currentTenantService;

    private static readonly Regex EmailRegex = new Regex(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public CustomerService(CustomerRepository customerRepository, ICurrentTenantService currentTenantService)
    {
        _customerRepository = customerRepository;
        _currentTenantService = currentTenantService;
    }

    public async Task<List<Customer>> GetCustomersAsync(string? searchTerm, bool? isActive)
    {
        var customers = await _customerRepository.GetAllByBusinessIdAsync(_currentTenantService.CurrentBusinessId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            customers = customers
                .Where(c => c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (isActive.HasValue)
        {
            customers = customers
                .Where(c => c.IsActive == isActive.Value)
                .ToList();
        }

        return customers;
    }

    public async Task<Customer?> GetCustomerByIdAsync(int id)
    {
        return await _customerRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);
    }

    public async Task<Customer> CreateCustomerAsync(Customer customer)
    {
        ValidateName(customer.Name);
        ValidateEmail(customer.Email);

        customer.BusinessId = _currentTenantService.CurrentBusinessId;
        customer.IsActive = true;
        customer.CreatedAtUtc = DateTime.UtcNow;
        customer.UpdatedAtUtc = DateTime.UtcNow;

        await _customerRepository.InsertAsync(customer);

        return customer;
    }

    public async Task UpdateCustomerAsync(Customer customer)
    {
        ValidateName(customer.Name);
        ValidateEmail(customer.Email);

        customer.UpdatedAtUtc = DateTime.UtcNow;

        await _customerRepository.UpdateAsync(customer);
    }

    public async Task DeactivateCustomerAsync(int id)
    {
        var customer = await _customerRepository.GetByIdAndBusinessIdAsync(id, _currentTenantService.CurrentBusinessId);

        if (customer == null)
        {
            throw new InvalidOperationException($"Customer with Id {id} not found.");
        }

        customer.IsActive = false;
        customer.UpdatedAtUtc = DateTime.UtcNow;

        await _customerRepository.UpdateAsync(customer);
    }

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Customer name is required");
        }
    }

    private static void ValidateEmail(string? email)
    {
        if (!string.IsNullOrEmpty(email) && !EmailRegex.IsMatch(email))
        {
            throw new ArgumentException("Email address is not in a valid format");
        }
    }
}
