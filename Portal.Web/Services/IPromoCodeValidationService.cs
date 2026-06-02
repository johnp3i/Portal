using Portal.Web.Models.PromoCode;

namespace Portal.Web.Services;

/// <summary>
/// Validates promo codes during the registration flow.
/// Performs server-side validation including existence, revocation, expiry,
/// redemption limits, and email binding checks.
/// </summary>
public interface IPromoCodeValidationService
{
    /// <summary>
    /// Validates a promo code for use during registration.
    /// Input is sanitized (trimmed and uppercased) before validation.
    /// Returns a result indicating validity with an error message on failure,
    /// or PromoCodeId and DurationMonths on success.
    /// </summary>
    Task<PromoCodeValidationResult> ValidateForRegistrationAsync(string code, string registrationEmail);
}
