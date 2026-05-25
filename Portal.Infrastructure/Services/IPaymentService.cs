using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for payment recording, voiding, and payment history retrieval.
/// All operations enforce tenant isolation via BusinessId.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Records a payment against an issued invoice after validation.
    /// Validates: invoice exists, is Issued, amount > 0, amount ≤ outstanding balance.
    /// Triggers financial status recalculation on success.
    /// </summary>
    Task<ServiceResult> RecordPaymentAsync(RecordPaymentDto dto, int businessId, string userId);

    /// <summary>
    /// Voids a payment by setting IsVoided = 1.
    /// Returns informational message if already voided.
    /// Triggers financial status recalculation on the parent invoice.
    /// </summary>
    Task<ServiceResult> VoidPaymentAsync(int paymentId, int businessId);

    /// <summary>
    /// Gets all payments for an invoice (including voided) for display in payment history.
    /// Joins with PaymentMethodType to include the method name.
    /// </summary>
    Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(int invoiceId, int businessId);

    /// <summary>
    /// Gets active payment method types for dropdown population.
    /// </summary>
    Task<List<PaymentMethodType>> GetPaymentMethodTypesAsync();
}
