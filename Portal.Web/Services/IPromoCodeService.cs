using Portal.Infrastructure.Models;
using Portal.Web.Models.PromoCode;

namespace Portal.Web.Services;

/// <summary>
/// Service for promo code administration: creation, revocation, and listing.
/// </summary>
public interface IPromoCodeService
{
    /// <summary>
    /// Creates a new promo code with the specified parameters.
    /// Generates a unique 8-character alphanumeric code using cryptographic randomness.
    /// If BoundEmail is provided, MaxRedemptions is forced to 1.
    /// </summary>
    Task<PromoCodeCreateResult> CreateAsync(CreatePromoCodeRequest request, string createdByUserId);

    /// <summary>
    /// Revokes a promo code by setting IsRevoked to true.
    /// Returns failure if the code is already revoked, expired, or fully redeemed.
    /// </summary>
    Task<ServiceResult> RevokeAsync(int promoCodeId, string revokedByUserId);

    /// <summary>
    /// Retrieves a paginated, filtered list of promo codes mapped to list item DTOs
    /// with derived status and type.
    /// </summary>
    Task<PagedResult<PromoCodeListItem>> GetAllAsync(Models.PromoCode.PromoCodeFilter filter);

    /// <summary>
    /// Retrieves a promo code by its database Id.
    /// Returns null if the promo code does not exist.
    /// </summary>
    Task<PromoCodeListItem?> GetByIdAsync(int id);

    /// <summary>
    /// Increments the sent count for a promo code after it is emailed.
    /// </summary>
    Task IncrementSentCountAsync(int promoCodeId);

    /// <summary>
    /// Resets the sent count to zero for a promo code.
    /// </summary>
    Task ResetSentCountAsync(int promoCodeId);
}
