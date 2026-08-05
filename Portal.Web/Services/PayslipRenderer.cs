using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Services;

namespace Portal.Web.Services;

/// <summary>
/// Renders the payslip Razor view to an HTML string using ViewRenderService.
/// Lives in the Web project because it depends on IViewRenderService (same pattern as InvoiceRenderer).
/// </summary>
public class PayslipRenderer : IPayslipRenderer
{
    private readonly IViewRenderService _viewRenderService;
    private readonly ILogoService _logoService;

    public PayslipRenderer(IViewRenderService viewRenderService, ILogoService logoService)
    {
        _viewRenderService = viewRenderService;
        _logoService = logoService;
    }

    public async Task<string> RenderPayslipHtmlAsync(
        PayslipDetailDto payslip, string businessName, string businessAddress, bool includeSignature)
    {
        try
        {
            var model = new PayslipPdfViewModel
            {
                Payslip = payslip,
                BusinessName = businessName,
                BusinessAddress = businessAddress,
                IncludeSignature = includeSignature
            };

            return await _viewRenderService.RenderViewToStringAsync(
                "~/Views/Payroll/PdfTemplates/Payslip.cshtml", model);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
