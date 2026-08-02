using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

public class PayslipEmailService : IPayslipEmailService
{
    public Task<ServiceResult> SendPayslipAsync(int payslipId, int businessId, string userId, bool includeSignature)
    {
        // Stub - full implementation requires IEmailService integration
        return Task.FromResult(ServiceResult.Fail("Email service not yet configured for payslips."));
    }

    public Task<ServiceResult> SendAllPayslipsAsync(int periodId, int businessId, string userId, bool includeSignature)
    {
        return Task.FromResult(ServiceResult.Fail("Email service not yet configured for payslips."));
    }
}
