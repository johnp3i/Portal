using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

public class PayslipPeriodStatusService : IPayslipPeriodStatusService
{
    private const byte Draft = 1;
    private const byte Preview = 2;
    private const byte Finalised = 3;
    private const byte Unlocked = 4;
    private const byte ReFinalised = 5;

    private static readonly Dictionary<byte, byte[]> AllowedTransitions = new()
    {
        { Draft, new[] { Preview } },
        { Preview, new[] { Finalised } },
        { Finalised, new[] { Unlocked } },
        { Unlocked, new[] { ReFinalised } },
        { ReFinalised, new[] { Unlocked } }
    };

    private static readonly HashSet<byte> EditableStatuses = new() { Draft, Preview, Unlocked };

    private readonly PayrollRepository _payrollRepository;
    private readonly IComplianceIntegrationService _complianceIntegrationService;

    public PayslipPeriodStatusService(
        PayrollRepository payrollRepository,
        IComplianceIntegrationService complianceIntegrationService)
    {
        _payrollRepository = payrollRepository;
        _complianceIntegrationService = complianceIntegrationService;
    }

    public bool IsTransitionAllowed(byte currentStatusId, byte targetStatusId)
    {
        return AllowedTransitions.TryGetValue(currentStatusId, out var allowed)
            && allowed.Contains(targetStatusId);
    }

    public IReadOnlyList<byte> GetAllowedTransitions(byte currentStatusId)
    {
        return AllowedTransitions.TryGetValue(currentStatusId, out var allowed)
            ? allowed.ToList().AsReadOnly()
            : Array.Empty<byte>().ToList().AsReadOnly();
    }

    public bool IsEditableStatus(byte statusId)
    {
        return EditableStatuses.Contains(statusId);
    }

    public async Task<ServiceResult> UnlockPeriodAsync(int periodId, int businessId, string userId, string userRole)
    {
        try
        {
            // Validate role
            if (userRole != "Owner" && userRole != "SuperAdmin")
                return ServiceResult.Fail("Only the business owner or a SuperAdmin can unlock a finalised period.");

            // Load period
            var period = await _payrollRepository.GetPeriodByIdAsync(periodId, businessId);
            if (period == null)
                return ServiceResult.Fail("Period not found.");

            // Validate status
            if (period.PayslipStatusTypeId != Finalised && period.PayslipStatusTypeId != ReFinalised)
                return ServiceResult.Fail("Only Finalised or Re-finalised periods can be unlocked.");

            // Optimistic concurrency update
            var updated = await _payrollRepository.UpdatePeriodStatusAsync(periodId, Unlocked, period.PayslipStatusTypeId, null);
            if (!updated)
                return ServiceResult.Fail("Period status has been changed by another user. Please refresh and try again.");

            // Cascade status to all payslips
            await _payrollRepository.UpdateAllPayslipStatusesInPeriodAsync(periodId, Unlocked);

            // Create audit entries for each payslip
            var payslips = await _payrollRepository.GetPayslipsByPeriodAsync(periodId);
            foreach (var payslip in payslips)
            {
                await _payrollRepository.InsertAuditLogAsync(new PayslipAuditLog
                {
                    PayslipId = payslip.Id,
                    UserId = userId,
                    PayslipAuditActionTypeId = 1, // Unlocked
                    FieldName = null,
                    OldValue = null,
                    NewValue = null
                });
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> RefinalisePeriodAsync(int periodId, int businessId, string userId, string userRole)
    {
        try
        {
            // Validate role
            if (userRole != "Owner" && userRole != "SuperAdmin")
                return ServiceResult.Fail("Only the business owner or a SuperAdmin can re-finalise a period.");

            // Load period
            var period = await _payrollRepository.GetPeriodByIdAsync(periodId, businessId);
            if (period == null)
                return ServiceResult.Fail("Period not found.");

            // Validate status
            if (period.PayslipStatusTypeId != Unlocked)
                return ServiceResult.Fail("Only Unlocked periods can be re-finalised.");

            // Optimistic concurrency update
            var updated = await _payrollRepository.UpdatePeriodStatusAsync(periodId, ReFinalised, Unlocked, DateTime.UtcNow);
            if (!updated)
                return ServiceResult.Fail("Period status has been changed by another user. Please refresh and try again.");

            // Cascade status to all payslips
            await _payrollRepository.UpdateAllPayslipStatusesInPeriodAsync(periodId, ReFinalised);

            // Create audit entries for each payslip
            var payslips = await _payrollRepository.GetPayslipsByPeriodAsync(periodId);
            foreach (var payslip in payslips)
            {
                await _payrollRepository.InsertAuditLogAsync(new PayslipAuditLog
                {
                    PayslipId = payslip.Id,
                    UserId = userId,
                    PayslipAuditActionTypeId = 3, // Re-finalised
                    FieldName = null,
                    OldValue = null,
                    NewValue = null
                });
            }

            // Phase D: Non-blocking compliance integration on re-finalisation
            try
            {
                await _complianceIntegrationService.UpdateComplianceFilingFromPayrollAsync(periodId, businessId, userId);
            }
            catch
            {
                // Non-blocking: compliance failure must not affect re-finalisation result
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
