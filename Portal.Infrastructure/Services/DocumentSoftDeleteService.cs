using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Handles soft-deletion of invoices and quotations.
/// Only Draft status documents (StatusTypeId = 1) are eligible for soft-delete.
/// Follows the dedicated-service pattern established by DocumentDuplicationService.
/// </summary>
public class DocumentSoftDeleteService : IDocumentSoftDeleteService
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly InvoiceRepository _invoiceRepository;
    private readonly QuotationRepository _quotationRepository;

    private const int DraftInvoiceStatusTypeId = 1;
    private const int DraftQuotationStatusTypeId = 1;

    public DocumentSoftDeleteService(
        ICurrentTenantService currentTenantService,
        InvoiceRepository invoiceRepository,
        QuotationRepository quotationRepository)
    {
        _currentTenantService = currentTenantService;
        _invoiceRepository = invoiceRepository;
        _quotationRepository = quotationRepository;
    }

    public async Task<ServiceResult> SoftDeleteInvoiceAsync(int invoiceId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var invoice = await _invoiceRepository.GetByIdAndBusinessIdAsync(invoiceId, businessId);

            if (invoice == null)
                return ServiceResult.Fail("Invoice not found.");

            if (invoice.BusinessId != businessId)
                return ServiceResult.Fail("Invoice does not belong to this business.");

            if (invoice.IsDeleted)
                return ServiceResult.Fail("Invoice has already been deleted.");

            if (invoice.InvoiceStatusTypeId != DraftInvoiceStatusTypeId)
                return ServiceResult.Fail("Only draft invoices can be deleted.");

            await _invoiceRepository.SoftDeleteAsync(invoiceId, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<ServiceResult> SoftDeleteQuotationAsync(int quotationId)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var quotation = await _quotationRepository.GetByIdAndBusinessIdAsync(quotationId, businessId);

            if (quotation == null)
                return ServiceResult.Fail("Quotation not found.");

            if (quotation.BusinessId != businessId)
                return ServiceResult.Fail("Quotation does not belong to this business.");

            if (quotation.IsDeleted)
                return ServiceResult.Fail("Quotation has already been deleted.");

            if (quotation.QuotationStatusTypeId != DraftQuotationStatusTypeId)
                return ServiceResult.Fail("Only draft quotations can be deleted.");

            await _quotationRepository.SoftDeleteAsync(quotationId, businessId);
            return ServiceResult.Ok();
        }
        catch (Exception)
        {
            throw;
        }
    }
}
