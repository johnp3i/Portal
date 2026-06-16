using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;
using Portal.Infrastructure.Services;

namespace Portal.Web.Services;

/// <summary>
/// Renders the invoice snapshot Razor view to an HTML string using ViewRenderService.
/// Fetches all required data internally given just an invoiceId.
/// </summary>
public class InvoiceRenderer : IInvoiceRenderer
{
    private readonly IViewRenderService _viewRenderService;
    private readonly IInvoiceService _invoiceService;
    private readonly IInvoiceSectionService _invoiceSectionService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ICustomerService _customerService;
    private readonly ILogoService _logoService;
    private readonly IBusinessService _businessService;
    private readonly BusinessPaymentDetailRepository _paymentDetailRepository;

    public InvoiceRenderer(
        IViewRenderService viewRenderService,
        IInvoiceService invoiceService,
        IInvoiceSectionService invoiceSectionService,
        ICurrentTenantService currentTenantService,
        ICustomerService customerService,
        ILogoService logoService,
        IBusinessService businessService,
        BusinessPaymentDetailRepository paymentDetailRepository)
    {
        _viewRenderService = viewRenderService;
        _invoiceService = invoiceService;
        _invoiceSectionService = invoiceSectionService;
        _currentTenantService = currentTenantService;
        _customerService = customerService;
        _logoService = logoService;
        _businessService = businessService;
        _paymentDetailRepository = paymentDetailRepository;
    }

    public async Task<string> RenderAsync(int invoiceId)
    {
        return await RenderAsync(invoiceId, _currentTenantService.CurrentBusinessId);
    }

    public async Task<string> RenderAsync(int invoiceId, int businessId)
    {
        var invoice = await _invoiceService.GetInvoiceByIdAsync(invoiceId)
            ?? throw new InvalidOperationException($"Invoice {invoiceId} not found.");

        var lines = await _invoiceService.GetInvoiceLinesAsync(invoiceId);
        var sections = await _invoiceSectionService.GetByInvoiceIdAsync(invoiceId);
        var customer = await _customerService.GetCustomerByIdAsync(invoice.CustomerId);
        var logos = await _logoService.GetByBusinessIdAsync(businessId);
        var primaryLogo = logos.FirstOrDefault(l => l.IsPrimary) ?? logos.FirstOrDefault();
        var business = await _businessService.GetBusinessByIdAsync(businessId);
        var profile = await _businessService.GetBusinessProfileAsync(businessId);
        var paymentDetails = await _paymentDetailRepository.GetByBusinessIdAsync(businessId);

        var model = new InvoiceSnapshotModel
        {
            Invoice = invoice,
            Lines = lines,
            Sections = sections,
            CustomerName = customer?.Name ?? "Unknown",
            LogoUrl = primaryLogo?.PublicUrl,
            BusinessName = business?.Name ?? "",
            Profile = profile,
            PaymentDetails = paymentDetails
        };

        return await _viewRenderService.RenderViewToStringAsync("~/Views/Invoice/Snapshot.cshtml", model);
    }
}
