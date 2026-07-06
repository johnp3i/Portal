using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service for payment instructions — bank transfer details on shared invoices,
/// payment declarations, and the business-level toggle.
/// </summary>
public interface IPaymentInstructionsService
{
    /// <summary>
    /// Gets payment instruction data for the shared invoice modal.
    /// Returns null if the toggle is disabled or no active payment details exist.
    /// </summary>
    Task<PaymentInstructionsData?> GetPaymentInstructionsAsync(int invoiceId, int businessId);

    /// <summary>
    /// Processes a customer's payment declaration. Validates share token, rate limit,
    /// updates invoice status to PaymentOnboard, and creates an audit log entry.
    /// </summary>
    Task<PaymentDeclarationResult> DeclarePaymentAsync(string shareToken, string ipAddress);

    /// <summary>
    /// Enables or disables the payment instructions toggle for a business.
    /// Returns false if the business has no active payment details (cannot enable).
    /// </summary>
    Task<ToggleResult> SetPaymentInstructionsEnabledAsync(int businessId, bool enabled);

    /// <summary>
    /// Checks whether the payment instructions toggle is enabled for a business.
    /// </summary>
    Task<bool> IsEnabledForBusinessAsync(int businessId);
}
