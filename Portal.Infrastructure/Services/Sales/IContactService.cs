using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for sales contact management.
/// </summary>
public interface IContactService
{
    Task<ServiceResult> CreateContactAsync(CreateContactRequest request);
    Task<ServiceResult> UpdateContactAsync(UpdateContactRequest request);
    Task<ServiceResult> DeactivateContactAsync(int id);
    Task<ServiceResult> ActivateContactAsync(int id);
    Task<SalesContact?> GetByIdAsync(int id);
    Task<PagedResult<SalesContact>> GetContactsPagedAsync(string? searchTerm, int page, int pageSize);
    Task<ContactDetailDto?> GetContactDetailAsync(int id);
    Task<ServiceResult> ConvertToCustomerAsync(int contactId);
}
