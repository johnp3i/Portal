using Portal.Infrastructure.Repositories;
using Portal.Web.Models.PromoCode;

namespace Portal.Web.Services;

/// <summary>
/// Validates promo codes for the registration flow.
/// Performs a 5-step server-side validation:
/// 1. Sanitize input (trim + uppercase)
/// 2. Check code exists
/// 3. Check not revoked
/// 4. Check not expired
/// 5. Check not fully redeemed
/// 6. If email-bound: check registration email matches (case-insensitive)
/// 
/// For email mismatch, returns a generic "Invalid promo code" message
/// to avoid revealing that the code exists or is email-bound (Req 9.5).
/// </summary>
public class PromoCodeValidationService : IPromoCodeValidationService
{
    private readonly PromoCodeRepository _promoCodeRepository;

    public PromoCodeValidationService(PromoCodeRepository promoCodeRepository)
    {
        _promoCodeRepository = promoCodeRepository;
    }

    /// <inheritdoc />
    public async Task<PromoCodeValidationResult> ValidateForRegistrationAsync(string code, string registrationEmail)
    {
        try
        {
            // Step 1: Sanitize input — trim whitespace and convert to uppercase
            var sanitizedCode = code?.Trim().ToUpperInvariant() ?? string.Empty;
            var sanitizedEmail = registrationEmail?.Trim() ?? string.Empty;

            // Step 2: Check code exists
            var promoCode = await _promoCodeRepository.GetByCodeAsync(sanitizedCode);

            if (promoCode == null)
            {
                return new PromoCodeValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid promo code"
                };
            }

            // Step 3: Check not revoked
            if (promoCode.IsRevoked)
            {
                return new PromoCodeValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "This code has been revoked"
                };
            }

            // Step 4: Check not expired
            if (promoCode.ExpiresAtUtc <= DateTime.UtcNow)
            {
                return new PromoCodeValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "This code has expired"
                };
            }

            // Step 5: Check not fully redeemed
            if (promoCode.CurrentRedemptions >= promoCode.MaxRedemptions)
            {
                return new PromoCodeValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "This code has reached its maximum redemptions"
                };
            }

            // Step 6: If email-bound, check registration email matches (case-insensitive)
            if (promoCode.BoundEmail != null)
            {
                var boundEmailTrimmed = promoCode.BoundEmail.Trim();

                if (!string.Equals(sanitizedEmail, boundEmailTrimmed, StringComparison.OrdinalIgnoreCase))
                {
                    // Return generic message — do NOT reveal the code is email-bound (Req 9.5)
                    return new PromoCodeValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Invalid promo code"
                    };
                }
            }

            // All checks passed — return valid result
            return new PromoCodeValidationResult
            {
                IsValid = true,
                PromoCodeId = promoCode.Id,
                DurationMonths = promoCode.DurationMonths
            };
        }
        catch (Exception)
        {
            throw;
        }
    }
}
