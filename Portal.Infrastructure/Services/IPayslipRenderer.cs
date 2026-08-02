using Portal.Infrastructure.Models.Payroll;

namespace Portal.Infrastructure.Services;

public interface IPayslipRenderer
{
    Task<string> RenderPayslipHtmlAsync(PayslipDetailDto payslip, string businessName, string businessAddress, bool includeSignature);
}
