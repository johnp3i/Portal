using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Receipt;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for payment receipt generation, retrieval, sharing, and voiding.
/// </summary>
public interface IPaymentReceiptService
{
    /// <summary>
    /// Generates a receipt for a payment. Handles per-invoice, global, and credit-applied payments.
    /// </summary>
    Task<ServiceResult<ReceiptViewModel>> GenerateReceiptAsync(int paymentId, int businessId, string userId, int? signatureId = null, string? notes = null);

    /// <summary>
    /// Gets a fully-populated receipt view model by Id.
    /// </summary>
    Task<ReceiptViewModel?> GetReceiptAsync(int receiptId, int businessId);

    /// <summary>
    /// Gets a paginated list of receipts for the receipt index page.
    /// </summary>
    Task<(List<ReceiptListDto> Items, int TotalCount)> GetReceiptsPagedAsync(
        int businessId, int? customerId, DateTime? fromDate, DateTime? toDate, bool? isVoided,
        int page, int pageSize);

    /// <summary>
    /// Voids a receipt and deactivates its share links.
    /// </summary>
    Task<ServiceResult> VoidReceiptAsync(int receiptId, int businessId);

    /// <summary>
    /// Voids the receipt associated with a payment (called during payment void cascade).
    /// </summary>
    Task VoidByPaymentIdAsync(int paymentId, int businessId);

    /// <summary>
    /// Checks if a receipt already exists for a payment.
    /// </summary>
    Task<bool> HasReceiptAsync(int paymentId, int businessId);
}
