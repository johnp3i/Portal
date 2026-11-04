using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for credit note management including creation, lifecycle transitions,
/// application, voiding, and query operations.
/// </summary>
public interface ICreditNoteService
{
    // Creation
    /// <summary>
    /// Creates a credit note. When <paramref name="dto"/>.InvoiceId is set, behaves as the classic
    /// invoice-linked credit note. When InvoiceId is null (account-level), <paramref name="dto"/>.CustomerId
    /// is required and <paramref name="accountCreditEnabled"/> must be true (the business capability
    /// gate) — otherwise the create is rejected.
    /// </summary>
    Task<ServiceResult<int>> CreateCreditNoteAsync(CreateCreditNoteDto dto, int businessId, string? userId, bool accountCreditEnabled);
    Task<ServiceResult> UpdateCreditNoteAsync(int creditNoteId, UpdateCreditNoteDto dto, int businessId);

    // Lifecycle
    Task<ServiceResult> IssueCreditNoteAsync(int creditNoteId, int businessId, string? userId);
    Task<ServiceResult> ApplyCreditNoteAsync(int creditNoteId, int businessId, string? userId);

    /// <summary>
    /// Manually applies an account-level credit note's available balance across one or more of the
    /// customer's outstanding invoices. Each allocation creates a CreditNoteApplication and
    /// recalculates the target invoice; the note becomes Applied only when fully drawn down.
    /// </summary>
    Task<ServiceResult> ApplyAccountCreditAsync(ApplyAccountCreditDto dto, int businessId, string? userId);

    Task<ServiceResult> VoidCreditNoteAsync(int creditNoteId, int businessId, string? userId);

    // Queries
    Task<(List<CreditNoteListDto> Items, int TotalCount)> GetCreditNotesPagedAsync(CreditNoteFilterDto filter, int businessId);
    Task<CreditNoteDetailDto?> GetCreditNoteDetailAsync(int creditNoteId, int businessId);
    Task<CreditNoteKpiDto> GetKpiAsync(int businessId);
    Task<List<EligibleInvoiceDto>> GetEligibleInvoicesAsync(int businessId);

    /// <summary>
    /// Outstanding invoices for a customer (Issued, not Paid/WrittenOff, balance &gt; 0), used by the
    /// account-credit apply UI.
    /// </summary>
    Task<List<EligibleInvoiceDto>> GetOutstandingInvoicesForCustomerAsync(int customerId, int businessId);

    Task<decimal> GetInvoiceOutstandingBalanceAsync(int invoiceId, int businessId);

    /// <summary>
    /// Returns the non-voided credit notes applied against the given invoice (number, amount,
    /// applied date) for display on the invoice detail view. Empty when none are applied.
    /// </summary>
    Task<List<AppliedCreditNoteDto>> GetAppliedCreditNotesForInvoiceAsync(int invoiceId, int businessId);
}
