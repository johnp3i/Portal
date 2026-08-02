using Portal.Infrastructure.Models.Payroll;

namespace Portal.Infrastructure.Services;

public class PayslipRenderer : IPayslipRenderer
{
    public Task<string> RenderPayslipHtmlAsync(PayslipDetailDto payslip, string businessName, string businessAddress, bool includeSignature)
    {
        // Stub - returns basic HTML structure. Full branded template to be implemented.
        var html = $"<h1>Payslip - {payslip.EmployeeName}</h1><p>{payslip.Month}/{payslip.Year}</p><p>Net: &euro;{payslip.NetSalary:N2}</p>";
        return Task.FromResult(html);
    }
}
