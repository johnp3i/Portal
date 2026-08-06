using Microsoft.Extensions.Logging;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Manages country-specific deduction templates and PAYE tax bands.
/// SuperAdmin CRUD operations + business import functionality.
/// 
/// Key behaviours:
/// - Import creates business-scoped DeductionType copies from templates
/// - Rate conversion: CountryDeductionTemplate.DefaultRate (0.0880) → DeductionRateHistory.Rate (8.80)
/// - Creates PAYE DeductionType (IsPercentage=false) if not present
/// - Duplicate detection: warns if templates already imported
/// </summary>
public class CountryTemplateService : ICountryTemplateService
{
    private readonly PayrollRepository _payrollRepository;
    private readonly ILogger<CountryTemplateService> _logger;

    private static readonly Dictionary<byte, string> CategoryNames = new()
    {
        { 1, "Deduction" },
        { 2, "Contribution" }
    };

    public CountryTemplateService(
        PayrollRepository payrollRepository,
        ILogger<CountryTemplateService> logger)
    {
        _payrollRepository = payrollRepository;
        _logger = logger;
    }

    #region Country Deduction Templates

    public async Task<List<CountryDeductionTemplateDto>> GetTemplatesByCountryAsync(string countryCode)
    {
        try
        {
            var templates = await _payrollRepository.GetAllCountryTemplatesByCountryAsync(countryCode);

            return templates.Select(t => new CountryDeductionTemplateDto
            {
                Id = t.Id,
                CountryCode = t.CountryCode,
                DeductionName = t.DeductionName,
                Code = t.Code,
                IsPercentage = t.IsPercentage,
                DeductionCategoryTypeId = t.DeductionCategoryTypeId,
                CategoryName = CategoryNames.GetValueOrDefault(t.DeductionCategoryTypeId, "Unknown"),
                DefaultRate = t.DefaultRate,
                IsPayeDeductible = t.IsPayeDeductible,
                SortOrder = t.SortOrder,
                IsActive = t.IsActive
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateTemplateAsync(CreateCountryTemplateRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CountryCode))
                return ServiceResult.Fail("Country code is required.");

            if (string.IsNullOrWhiteSpace(request.DeductionName))
                return ServiceResult.Fail("Deduction name is required.");

            if (string.IsNullOrWhiteSpace(request.Code))
                return ServiceResult.Fail("Code is required.");

            if (request.DefaultRate <= 0 || request.DefaultRate >= 1)
                return ServiceResult.Fail("Default rate must be between 0 and 1 (exclusive).");

            if (request.DeductionCategoryTypeId < 1 || request.DeductionCategoryTypeId > 2)
                return ServiceResult.Fail("Invalid deduction category type.");

            var entity = new CountryDeductionTemplate
            {
                CountryCode = request.CountryCode.Trim().ToUpper(),
                DeductionName = request.DeductionName.Trim(),
                Code = request.Code.Trim(),
                IsPercentage = request.IsPercentage,
                DeductionCategoryTypeId = request.DeductionCategoryTypeId,
                DefaultRate = request.DefaultRate,
                IsPayeDeductible = request.IsPayeDeductible,
                SortOrder = request.SortOrder,
                IsActive = true
            };

            var id = await _payrollRepository.InsertCountryTemplateAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateTemplateAsync(UpdateCountryTemplateRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.DeductionName))
                return ServiceResult.Fail("Deduction name is required.");

            var existing = await _payrollRepository.GetCountryTemplateByIdAsync(request.Id);
            if (existing == null)
                return ServiceResult.Fail("Template not found.");

            existing.DeductionName = request.DeductionName.Trim();
            existing.DefaultRate = request.DefaultRate;
            existing.SortOrder = request.SortOrder;

            await _payrollRepository.UpdateCountryTemplateAsync(existing);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> DeactivateTemplateAsync(int id)
    {
        try
        {
            var existing = await _payrollRepository.GetCountryTemplateByIdAsync(id);
            if (existing == null)
                return ServiceResult.Fail("Template not found.");

            await _payrollRepository.DeactivateCountryTemplateAsync(id);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region PAYE Tax Bands

    public async Task<List<PayeTaxBandDto>> GetTaxBandsAsync(string countryCode, int? year = null)
    {
        try
        {
            var effectiveYear = year ?? DateTime.UtcNow.Year;
            var bands = await _payrollRepository.GetTaxBandsAsync(countryCode, effectiveYear);

            return bands.Select(b => new PayeTaxBandDto
            {
                Id = b.Id,
                CountryCode = b.CountryCode,
                LowerBound = b.LowerBound,
                UpperBound = b.UpperBound,
                Rate = b.Rate,
                EffectiveFromYear = b.EffectiveFromYear,
                EffectiveToYear = b.EffectiveToYear
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateTaxBandAsync(CreateTaxBandRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CountryCode))
                return ServiceResult.Fail("Country code is required.");

            if (request.Rate < 0 || request.Rate > 1)
                return ServiceResult.Fail("Rate must be between 0 and 1.");

            if (request.UpperBound.HasValue && request.LowerBound >= request.UpperBound.Value)
                return ServiceResult.Fail("Lower bound must be less than upper bound.");

            var band = new PayeTaxBand
            {
                CountryCode = request.CountryCode.Trim().ToUpper(),
                LowerBound = request.LowerBound,
                UpperBound = request.UpperBound,
                Rate = request.Rate,
                EffectiveFromYear = request.EffectiveFromYear,
                EffectiveToYear = request.EffectiveToYear
            };

            var id = await _payrollRepository.InsertTaxBandAsync(band);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateTaxBandAsync(UpdateTaxBandRequest request)
    {
        try
        {
            if (request.Rate < 0 || request.Rate > 1)
                return ServiceResult.Fail("Rate must be between 0 and 1.");

            if (request.UpperBound.HasValue && request.LowerBound >= request.UpperBound.Value)
                return ServiceResult.Fail("Lower bound must be less than upper bound.");

            var existing = await _payrollRepository.GetTaxBandByIdAsync(request.Id);
            if (existing == null)
                return ServiceResult.Fail("Tax band not found.");

            existing.LowerBound = request.LowerBound;
            existing.UpperBound = request.UpperBound;
            existing.Rate = request.Rate;
            existing.EffectiveFromYear = request.EffectiveFromYear;
            existing.EffectiveToYear = request.EffectiveToYear;

            await _payrollRepository.UpdateTaxBandAsync(existing);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Import Templates to Business

    public async Task<ServiceResult> ImportCountryTemplatesForBusinessAsync(int businessId, string countryCode)
    {
        try
        {
            // Load active templates for the country
            var templates = await _payrollRepository.GetCountryTemplatesByCountryAsync(countryCode);

            if (!templates.Any())
                return ServiceResult.Fail($"No active templates found for country code '{countryCode}'.");

            // Check for duplicates: does the business already have deduction types with matching codes?
            var existingTypes = await _payrollRepository.GetDeductionTypesByBusinessAsync(businessId);
            var existingCodes = existingTypes.Select(t => t.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var duplicates = templates.Where(t => existingCodes.Contains(t.Code)).ToList();
            if (duplicates.Any())
            {
                var dupeNames = string.Join(", ", duplicates.Select(d => d.DeductionName));
                return ServiceResult.Fail($"Templates already imported: {dupeNames}. Remove existing deduction types first or import manually.");
            }

            int importedCount = 0;

            foreach (var template in templates)
            {
                // Create business-scoped DeductionType with IsPayeDeductible propagated
                var deductionType = new DeductionType
                {
                    Name = template.DeductionName,
                    Code = template.Code,
                    IsPercentage = template.IsPercentage,
                    DeductionCategoryTypeId = template.DeductionCategoryTypeId,
                    BusinessId = businessId,
                    IsActive = true,
                    Country = countryCode,
                    IsTemplate = false,
                    IsPayeDeductible = template.IsPayeDeductible
                };

                // Rate conversion: DefaultRate (0.0880) → DeductionRateHistory.Rate (8.80)
                var rateHistories = new List<DeductionRateHistory>
                {
                    new DeductionRateHistory
                    {
                        Rate = template.DefaultRate * 100, // Convert decimal to percentage format
                        EffectiveFromUtc = new DateTime(DateTime.UtcNow.Year, 1, 1),
                        EffectiveToUtc = null
                    }
                };

                await _payrollRepository.InsertDeductionTypeWithRatesAsync(deductionType, rateHistories);
                importedCount++;
            }

            // Create PAYE DeductionType if not already present
            var payeTypeId = await _payrollRepository.GetPayeDeductionTypeIdForBusinessAsync(businessId);
            if (!payeTypeId.HasValue)
            {
                var payeType = new DeductionType
                {
                    Name = "PAYE Income Tax",
                    Code = "PAYE",
                    IsPercentage = false, // PAYE uses progressive bands, not a flat rate
                    DeductionCategoryTypeId = 1, // Employee deduction
                    BusinessId = businessId,
                    IsActive = true,
                    Country = countryCode,
                    IsTemplate = false,
                    IsPayeDeductible = false // PAYE itself is not deductible from PAYE base
                };

                await _payrollRepository.InsertDeductionTypeAsync(payeType);
            }

            _logger.LogInformation(
                "Imported {Count} country deduction templates for business {BusinessId} from country {CountryCode}.",
                importedCount, businessId, countryCode);

            return ServiceResult.Ok(importedCount);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion
}
