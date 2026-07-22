using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Entities.Sales;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Sales;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Repositories.Sales;

namespace Portal.Infrastructure.Services.Sales;

/// <summary>
/// Business logic for sales contact management.
/// </summary>
public class ContactService : IContactService
{
    private readonly SalesContactRepository _contactRepository;
    private readonly LeadRequestRepository _leadRequestRepository;
    private readonly LeadStatusTypeRepository _leadStatusTypeRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly ICurrentTenantService _tenantService;

    public ContactService(
        SalesContactRepository contactRepository,
        LeadRequestRepository leadRequestRepository,
        LeadStatusTypeRepository leadStatusTypeRepository,
        CustomerRepository customerRepository,
        ICurrentTenantService tenantService)
    {
        _contactRepository = contactRepository;
        _leadRequestRepository = leadRequestRepository;
        _leadStatusTypeRepository = leadStatusTypeRepository;
        _customerRepository = customerRepository;
        _tenantService = tenantService;
    }

    public async Task<ServiceResult> CreateContactAsync(CreateContactRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            // Validate: email or phone required
            if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.PhoneNumber))
                return ServiceResult.Fail("Either email or phone number is required.");

            // Validate email format
            if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email))
                return ServiceResult.Fail("Please enter a valid email address.");

            // Check email duplicate
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var existing = await _contactRepository.CheckDuplicateEmailAsync(request.Email, businessId);
                if (existing != null)
                    return ServiceResult.Fail($"A contact with this email already exists: {existing.FirstName} {existing.LastName}");
            }

            // Check phone duplicate
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var existing = await _contactRepository.CheckDuplicatePhoneAsync(request.PhoneNumber, businessId);
                if (existing != null)
                    return ServiceResult.Fail($"A contact with this phone number already exists: {existing.FirstName} {existing.LastName}");
            }

            var entity = new SalesContact
            {
                BusinessId = businessId,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                CompanyName = request.CompanyName,
                JobTitle = request.JobTitle,
                Country = request.Country,
                Notes = request.Notes,
                IsActive = true
            };

            var id = await _contactRepository.InsertAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateContactAsync(UpdateContactRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;

            var existing = await _contactRepository.GetByIdAsync(request.Id, businessId);
            if (existing == null)
                return ServiceResult.Fail("Contact not found.");

            // Validate: email or phone required
            if (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.PhoneNumber))
                return ServiceResult.Fail("Either email or phone number is required.");

            // Validate email format
            if (!string.IsNullOrWhiteSpace(request.Email) && !IsValidEmail(request.Email))
                return ServiceResult.Fail("Please enter a valid email address.");

            // Check email duplicate (excluding current)
            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var dup = await _contactRepository.CheckDuplicateEmailAsync(request.Email, businessId, request.Id);
                if (dup != null)
                    return ServiceResult.Fail($"A contact with this email already exists: {dup.FirstName} {dup.LastName}");
            }

            // Check phone duplicate (excluding current)
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var dup = await _contactRepository.CheckDuplicatePhoneAsync(request.PhoneNumber, businessId, request.Id);
                if (dup != null)
                    return ServiceResult.Fail($"A contact with this phone number already exists: {dup.FirstName} {dup.LastName}");
            }

            existing.FirstName = request.FirstName;
            existing.LastName = request.LastName;
            existing.Email = request.Email;
            existing.PhoneNumber = request.PhoneNumber;
            existing.CompanyName = request.CompanyName;
            existing.JobTitle = request.JobTitle;
            existing.Country = request.Country;
            existing.Notes = request.Notes;

            await _contactRepository.UpdateAsync(existing);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> DeactivateContactAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _contactRepository.DeactivateAsync(id, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ActivateContactAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            await _contactRepository.ActivateAsync(id, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<SalesContact?> GetByIdAsync(int id)
    {
        try
        {
            return await _contactRepository.GetByIdAsync(id, _tenantService.CurrentBusinessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PagedResult<SalesContact>> GetContactsPagedAsync(string? searchTerm, int page, int pageSize)
    {
        try
        {
            return await _contactRepository.GetPagedAsync(searchTerm, page, pageSize, _tenantService.CurrentBusinessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ContactDetailDto?> GetContactDetailAsync(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var contact = await _contactRepository.GetByIdAsync(id, businessId);
            if (contact == null) return null;

            var leads = await _leadRequestRepository.GetByContactIdAsync(id, businessId);
            var statuses = await _leadStatusTypeRepository.GetAllAsync();

            var dto = new ContactDetailDto
            {
                Id = contact.Id,
                FirstName = contact.FirstName,
                LastName = contact.LastName,
                Email = contact.Email,
                PhoneNumber = contact.PhoneNumber,
                CompanyName = contact.CompanyName,
                JobTitle = contact.JobTitle,
                Country = contact.Country,
                Notes = contact.Notes,
                IsActive = contact.IsActive,
                CreatedAtUtc = contact.CreatedAtUtc,
                InterestHistory = leads.Select(l =>
                {
                    var status = statuses.FirstOrDefault(s => s.Id == l.LeadStatusTypeId);
                    return new ContactInterestDto
                    {
                        LeadRequestId = l.Id,
                        StageName = status?.Name ?? "Unknown",
                        StageColour = status?.Colour,
                        RequestText = l.RequestText,
                        CreatedAtUtc = l.CreatedAtUtc
                    };
                }).ToList()
            };

            return dto;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ConvertToCustomerAsync(int contactId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var contact = await _contactRepository.GetByIdAsync(contactId, businessId);
            if (contact == null)
                return ServiceResult.Fail("Contact not found.");

            // Check if a customer already exists with same email or name
            var customers = await _customerRepository.GetAllByBusinessIdAsync(businessId);
            var existingByEmail = !string.IsNullOrWhiteSpace(contact.Email)
                ? customers.FirstOrDefault(c => c.Email != null && c.Email.Equals(contact.Email, StringComparison.OrdinalIgnoreCase))
                : null;

            if (existingByEmail != null)
                return ServiceResult<int>.Ok(existingByEmail.Id);

            var fullName = string.IsNullOrWhiteSpace(contact.LastName)
                ? contact.FirstName
                : $"{contact.FirstName} {contact.LastName}";

            var existingByName = customers.FirstOrDefault(c => c.Name.Equals(fullName, StringComparison.OrdinalIgnoreCase));
            if (existingByName != null)
                return ServiceResult<int>.Ok(existingByName.Id);

            // Create new customer from contact
            var customer = new Customer
            {
                BusinessId = businessId,
                Name = fullName,
                ContactPerson = fullName,
                Email = contact.Email,
                TelephoneNumber = contact.PhoneNumber,
                Country = contact.Country,
                ContactId = contactId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            var customerId = await _customerRepository.InsertAsync(customer);
            return ServiceResult.Ok(customerId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }
}
