using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for payment recording, voiding, and payment history retrieval.
/// All operations enforce tenant isolation via BusinessId.
/// </summary>
public interface IPaymentService
{
    Task<ServiceResult> RecordPaymentAsync(RecordPaymentDto dto, int businessId, string userId);
    Task<ServiceResult> VoidPaymentAsync(int paymentId, int businessId);
    Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(int invoiceId, int businessId);
    Task<List<PaymentMethodType>> GetPaymentMethodTypesAsync();

    /// <summary>
    /// Records a global (customer-level) payment and allocates across outstanding invoices.
    /// </summary>
    Task<ServiceResult<AllocationResult>> RecordGlobalPaymentAsync(RecordGlobalPaymentDto dto, int businessId, string userId);

    /// <summary>
    /// Voids a global parent payment and cascades to all child allocations.
    /// </summary>
    Task<ServiceResult> VoidGlobalPaymentAsync(int paymentId, int businessId);

    /// <summary>
    /// Gets outstanding invoices for a customer (for manual allocation UI).
    /// </summary>
    Task<List<OutstandingInvoiceDto>> GetOutstandingForCustomerAsync(int customerId, int businessId);

    /// <summary>
    /// Gets the total available credit balance for a customer (sum of CreditAmount on non-voided parent payments).
    /// </summary>
    Task<decimal> GetCreditBalanceAsync(int customerId, int businessId);

    /// <summary>
    /// Applies existing credit from a customer's overpayment to their outstanding invoices using FIFO.
    /// </summary>
    Task<ServiceResult<AllocationResult>> ApplyCreditAsync(int customerId, int businessId, string userId);

    /// <summary>
    /// Voids a single child allocation, returning its amount to the parent's CreditAmount.
    /// </summary>
    Task<ServiceResult> VoidChildAllocationAsync(int paymentId, int businessId);

    /// <summary>
    /// Smart void that detects payment type and calls the appropriate void method.
    /// Returns a message describing what was done.
    /// </summary>
    Task<ServiceResult> VoidPaymentSmartAsync(int paymentId, int businessId);
}
