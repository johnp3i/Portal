using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for invoice management including quotation conversion, standalone creation,
/// lifecycle transitions, pricing, and line management.
/// </summary>
public interface IInvoiceService
{
    // Conversion
    Task<Invoice> ConvertFromQuotationAsync(int quotationId, string userId);

    // Standalone creation
    Task<Invoice> CreateInvoiceAsync(int customerId, DateOnly invoiceDate, DateOnly dueDate,
        string? notes, bool isGrandTotalShown, List<CreateInvoiceLineDto> lines,
        List<CreateInvoiceSectionDto>? sections);

    // Queries
    Task<List<InvoiceListDto>> GetInvoicesAsync(int? statusFilter = null,
        int? financialStatusFilter = null, int? customerFilter = null);
    Task<Invoice?> GetInvoiceByIdAsync(int id);
    Task<List<InvoiceLine>> GetInvoiceLinesAsync(int invoiceId);

    // Lifecycle
    Task TransitionStatusAsync(int invoiceId, int newStatusId, string userId);

    // Invoice editing
    Task UpdateInvoiceAsync(int invoiceId, int customerId, DateOnly invoiceDate, DateOnly dueDate,
        string? notes, bool isGrandTotalShown);

    // Line management
    Task<InvoiceLine> AddLineAsync(int invoiceId, string description, decimal quantity,
        decimal unitPrice, decimal vatRate, decimal discount, string discountType,
        decimal? costPrice, string? referenceUrl, string? subtitle, int? invoiceSectionId);
    Task UpdateLineAsync(int lineId, string description, decimal quantity,
        decimal unitPrice, decimal vatRate, decimal discount, string discountType,
        decimal? costPrice, string? referenceUrl, string? subtitle, int? invoiceSectionId);
    Task RemoveLineAsync(int lineId);
}
