using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for tenant administration.
/// </summary>
public class BusinessService : IBusinessService
{
    private readonly BusinessRepository _businessRepository;
    private readonly PortalDbContext _portalDbContext;

    private static readonly int[] AllowedVatPeriods = { 1, 2, 3, 4, 6, 12 };

    public BusinessService(BusinessRepository businessRepository, PortalDbContext portalDbContext)
    {
        _businessRepository = businessRepository;
        _portalDbContext = portalDbContext;
    }

    public async Task<List<Business>> GetAllBusinessesAsync()
    {
        return await _businessRepository.GetAllAsync();
    }

    public async Task<Business?> GetBusinessByIdAsync(int id)
    {
        return await _businessRepository.GetByIdAsync(id);
    }

    public async Task<Business> CreateBusinessAsync(string name)
    {
        var business = new Business
        {
            Name = name,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _businessRepository.InsertAsync(business);

        return business;
    }

    public async Task UpdateBusinessAsync(Business business)
    {
        business.UpdatedAtUtc = DateTime.UtcNow;
        await _businessRepository.UpdateAsync(business);
    }

    public async Task DeactivateBusinessAsync(int id)
    {
        var business = await _businessRepository.GetByIdAsync(id);

        if (business == null)
        {
            throw new InvalidOperationException($"Business with Id {id} not found.");
        }

        business.IsActive = false;
        business.UpdatedAtUtc = DateTime.UtcNow;

        await _businessRepository.UpdateAsync(business);
    }

    public async Task<bool> IsBusinessNameUniqueAsync(string name, int? excludeId = null)
    {
        return await _businessRepository.IsNameUniqueAsync(name, excludeId);
    }

    public async Task<BusinessProfile?> GetBusinessProfileAsync(int businessId)
    {
        return await _portalDbContext.BusinessProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(bp => bp.BusinessId == businessId);
    }

    public async Task SaveBusinessProfileAsync(BusinessProfile profile)
    {
        if (!AllowedVatPeriods.Contains(profile.VatPeriodLengthInMonths))
        {
            throw new ArgumentException(
                $"VatPeriodLengthInMonths must be one of: {string.Join(", ", AllowedVatPeriods)}");
        }

        var existing = await _portalDbContext.BusinessProfiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(bp => bp.BusinessId == profile.BusinessId);

        if (existing != null)
        {
            existing.CompanyRegistrationNumber = profile.CompanyRegistrationNumber;
            existing.VatRegistrationNumber = profile.VatRegistrationNumber;
            existing.VatRegistrationDate = profile.VatRegistrationDate;
            existing.VatPeriodLengthInMonths = profile.VatPeriodLengthInMonths;
            existing.AddressLine1 = profile.AddressLine1;
            existing.AddressLine2 = profile.AddressLine2;
            existing.City = profile.City;
            existing.PostalCode = profile.PostalCode;
            existing.Country = profile.Country;
            existing.TelephoneNumber = profile.TelephoneNumber;
            existing.MobileNumber = profile.MobileNumber;
            existing.Email = profile.Email;
            existing.Website = profile.Website;
            existing.CurrencySymbol = profile.CurrencySymbol;
        }
        else
        {
            _portalDbContext.BusinessProfiles.Add(profile);
        }

        await _portalDbContext.SaveChangesAsync();
    }
}
