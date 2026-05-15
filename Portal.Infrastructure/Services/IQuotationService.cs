using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Business logic for quotation management including lifecycle transitions, pricing, and audit logging.
/// </summary>
public interface IQuotationService
{
    Task<List<QuotationListDto>> GetQuotationsAsync(int? statusFilter = null, int? customerFilter = null, DateTime? dateFrom = null, DateTime? dateTo = null);
    Task<Quotation?> GetQuotationByIdAsync(int id);
    Task<List<QuotationLine>> GetQuotationLinesAsync(int quotationId);
    Task<Quotation> CreateQuotationAsync(int customerId, DateOnly? validUntil, string? notes);
    Task UpdateQuotationAsync(int quotationId, int customerId, DateOnly? validUntil, string? notes, int? quotationContactId = null, bool? isGrandTotalShown = null);
    Task TransitionStatusAsync(int quotationId, int newStatusId, string userId);
    Task<QuotationLine> AddLineAsync(int quotationId, string description, decimal quantity, decimal unitPrice, decimal vatRate, string? referenceUrl = null, decimal discount = 0, string discountType = "Percentage", string? subtitle = null, decimal? costPrice = null);
    Task UpdateLineAsync(int lineId, string description, decimal quantity, decimal unitPrice, decimal vatRate, string? referenceUrl = null, decimal discount = 0, string discountType = "Percentage", string? subtitle = null, decimal? costPrice = null);
    Task RemoveLineAsync(int lineId);
    bool IsExpired(Quotation quotation);
    Dictionary<int, List<int>> GetValidTransitions();
}
