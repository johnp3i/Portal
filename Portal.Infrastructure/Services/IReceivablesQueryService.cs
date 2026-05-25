using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Provides filtered, paginated lists of issued invoices with their financial state.
/// Only returns non-deleted invoices with InvoiceStatusTypeId = 2 (Issued).
/// </summary>
public interface IReceivablesQueryService
{
    /// <summary>
    /// Returns paginated receivables list with multi-criteria filtering.
    /// Only returns non-deleted invoices with InvoiceStatusTypeId = 2 (Issued).
    /// </summary>
    Task<PagedResult<ReceivableDto>> GetReceivablesAsync(
        int businessId,
        string? searchTerm = null,
        int? financialStatusFilter = null,
        int? customerFilter = null,
        DateOnly? dueFrom = null,
        DateOnly? dueTo = null,
        int page = 1,
        int pageSize = 15);
}
