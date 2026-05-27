using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for credit note management including creation, lifecycle transitions,
/// application, voiding, and query operations.
/// </summary>
public interface ICreditNoteService
{
    // Creation
    Task<ServiceResult<int>> CreateCreditNoteAsync(CreateCreditNoteDto dto, int businessId, string? userId);
    Task<ServiceResult> UpdateCreditNoteAsync(int creditNoteId, UpdateCreditNoteDto dto, int businessId);

    // Lifecycle
    Task<ServiceResult> IssueCreditNoteAsync(int creditNoteId, int businessId, string? userId);
    Task<ServiceResult> ApplyCreditNoteAsync(int creditNoteId, int businessId, string? userId);
    Task<ServiceResult> VoidCreditNoteAsync(int creditNoteId, int businessId, string? userId);

    // Queries
    Task<(List<CreditNoteListDto> Items, int TotalCount)> GetCreditNotesPagedAsync(CreditNoteFilterDto filter, int businessId);
    Task<CreditNoteDetailDto?> GetCreditNoteDetailAsync(int creditNoteId, int businessId);
    Task<CreditNoteKpiDto> GetKpiAsync(int businessId);
    Task<List<EligibleInvoiceDto>> GetEligibleInvoicesAsync(int businessId);
    Task<decimal> GetInvoiceOutstandingBalanceAsync(int invoiceId, int businessId);
}
