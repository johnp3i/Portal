using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

public interface IPayslipEmailService
{
    Task<ServiceResult> SendPayslipAsync(int payslipId, int businessId, string userId, bool includeSignature);
    Task<ServiceResult> SendAllPayslipsAsync(int periodId, int businessId, string userId, bool includeSignature);
}
