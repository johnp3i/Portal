namespace Portal.Web.Services;

/// <summary>
/// Sends branded promotional emails containing a promo code and registration link.
/// Does not modify the promo code record (sending is not a redemption).
/// </summary>
public interface IPromoEmailService
{
    /// <summary>
    /// Sends a branded promo code email to the specified recipient.
    /// </summary>
    /// <param name="recipientEmail">The email address to send to.</param>
    /// <param name="code">The promo code value.</param>
    /// <param name="durationMonths">Trial duration in months.</param>
    /// <param name="expiresAtUtc">When the promo code expires.</param>
    /// <param name="promoCodeId">The promo code Id for structured logging.</param>
    /// <returns>True if email was sent successfully; false on failure.</returns>
    Task<bool> SendPromoCodeEmailAsync(string recipientEmail, string code, int durationMonths, DateTime expiresAtUtc, int promoCodeId);
}
