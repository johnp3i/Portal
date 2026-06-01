using Microsoft.EntityFrameworkCore;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Services;
using Portal.Web.Models.Stripe;

namespace Portal.Web.Services.Stripe;

/// <summary>
/// Manages the post-signup setup wizard flow where new business owners configure
/// their business details (name, VAT, address, logo, currency) before accessing the dashboard.
/// </summary>
public class SetupWizardService : ISetupWizardService
{
    private readonly PortalDbContext _portalDbContext;
    private readonly ILogoService _logoService;
    private readonly ILogger<SetupWizardService> _logger;

    private static readonly HashSet<string> AllowedLogoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/svg+xml"
    };

    private const long MaxLogoSizeBytes = 2 * 1024 * 1024; // 2MB

    public SetupWizardService(
        PortalDbContext portalDbContext,
        ILogoService logoService,
        ILogger<SetupWizardService> logger)
    {
        _portalDbContext = portalDbContext;
        _logoService = logoService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsSetupCompleteAsync(int businessId)
    {
        try
        {
            return await _portalDbContext.BusinessProfiles
                .AnyAsync(bp => bp.BusinessId == businessId);
        }
        catch (Exception)
        {
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SetupWizardResult> CompleteSetupAsync(int businessId, SetupWizardModel model)
    {
        try
        {
            var result = new SetupWizardResult();

            // Validate business name
            if (string.IsNullOrWhiteSpace(model.BusinessName))
            {
                result.ValidationErrors.Add("BusinessName", "Business name is required.");
                result.ErrorMessage = "Validation failed.";
                return result;
            }

            if (model.BusinessName.Length > 200)
            {
                result.ValidationErrors.Add("BusinessName", "Business name cannot exceed 200 characters.");
                result.ErrorMessage = "Validation failed.";
                return result;
            }

            // Validate VAT number length
            if (!string.IsNullOrEmpty(model.VatNumber) && model.VatNumber.Length > 50)
            {
                result.ValidationErrors.Add("VatNumber", "VAT number cannot exceed 50 characters.");
                result.ErrorMessage = "Validation failed.";
                return result;
            }

            // Validate currency is provided
            if (string.IsNullOrWhiteSpace(model.CurrencySymbol))
            {
                result.ValidationErrors.Add("CurrencySymbol", "Currency is required.");
                result.ErrorMessage = "Validation failed.";
                return result;
            }

            // Validate logo file if provided
            if (model.Logo != null)
            {
                var logoValidation = ValidateLogoFile(model.Logo);
                if (logoValidation != null)
                {
                    result.ValidationErrors.Add("Logo", logoValidation);
                    result.ErrorMessage = "Validation failed.";
                    return result;
                }
            }

            // Check business name uniqueness
            var nameTaken = await IsBusinessNameTakenAsync(model.BusinessName, businessId);
            if (nameTaken)
            {
                result.ValidationErrors.Add("BusinessName", "This business name is already in use.");
                result.ErrorMessage = "Validation failed.";
                return result;
            }

            // Create BusinessProfile record
            var businessProfile = new BusinessProfile
            {
                BusinessId = businessId,
                CompanyRegistrationNumber = string.Empty,
                VatRegistrationNumber = model.VatNumber ?? string.Empty,
                VatRegistrationDate = DateOnly.FromDateTime(DateTime.UtcNow),
                VatPeriodLengthInMonths = 2,
                AddressLine1 = model.AddressLine1 ?? string.Empty,
                AddressLine2 = model.AddressLine2,
                City = model.City ?? string.Empty,
                PostalCode = model.PostalCode ?? string.Empty,
                Country = model.Country ?? string.Empty,
                Email = string.Empty,
                CurrencySymbol = model.CurrencySymbol
            };

            _portalDbContext.BusinessProfiles.Add(businessProfile);

            // Update Business.Name with the provided business name
            var business = await _portalDbContext.Businesses
                .FirstOrDefaultAsync(b => b.Id == businessId);

            if (business == null)
            {
                result.ErrorMessage = "Business not found.";
                return result;
            }

            business.Name = model.BusinessName;

            // Handle logo upload if provided
            if (model.Logo != null)
            {
                await _logoService.UploadAsync(businessId, model.Logo, model.BusinessName);
            }

            await _portalDbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Setup wizard completed for business {BusinessId}. BusinessName: {BusinessName}",
                businessId, model.BusinessName);

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error completing setup wizard for business {BusinessId}", businessId);

            return new SetupWizardResult
            {
                ErrorMessage = "An unexpected error occurred. Please try again."
            };
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsBusinessNameTakenAsync(string name, int excludeBusinessId)
    {
        try
        {
            return await _portalDbContext.Businesses
                .AnyAsync(b => b.Name == name && b.Id != excludeBusinessId);
        }
        catch (Exception)
        {
            throw;
        }
    }

    private static string? ValidateLogoFile(IFormFile file)
    {
        if (file.Length == 0)
        {
            return "Logo file is empty.";
        }

        if (!AllowedLogoContentTypes.Contains(file.ContentType))
        {
            return "Invalid file format. Accepted formats: PNG, JPG, or SVG.";
        }

        if (file.Length > MaxLogoSizeBytes)
        {
            return "File size exceeds the maximum allowed size of 2MB.";
        }

        return null;
    }
}
