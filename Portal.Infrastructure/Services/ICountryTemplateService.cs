using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Payroll;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Manages country-specific deduction templates and PAYE tax bands (SuperAdmin only).
/// Provides import functionality to create business-scoped deduction types from templates.
/// </summary>
public interface ICountryTemplateService
{
    // Country Deduction Templates
    Task<List<CountryDeductionTemplateDto>> GetTemplatesByCountryAsync(string countryCode);
    Task<ServiceResult> CreateTemplateAsync(CreateCountryTemplateRequest request);
    Task<ServiceResult> UpdateTemplateAsync(UpdateCountryTemplateRequest request);
    Task<ServiceResult> DeactivateTemplateAsync(int id);

    // PAYE Tax Bands
    Task<List<PayeTaxBandDto>> GetTaxBandsAsync(string countryCode, int? year = null);
    Task<ServiceResult> CreateTaxBandAsync(CreateTaxBandRequest request);
    Task<ServiceResult> UpdateTaxBandAsync(UpdateTaxBandRequest request);

    // Import templates to business
    Task<ServiceResult> ImportCountryTemplatesForBusinessAsync(int businessId, string countryCode);
}
