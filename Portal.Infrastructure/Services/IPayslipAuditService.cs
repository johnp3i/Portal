using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.Payroll;

namespace Portal.Infrastructure.Services;

public interface IPayslipAuditService
{
    /// <summary>
    /// Records a status change audit entry (Unlocked, Re-finalised) with no field details.
    /// </summary>
    Task RecordStatusChangeAsync(int payslipId, string userId, byte actionTypeId);

    /// <summary>
    /// Compares old and new earning lines, detecting additions, removals, and amount modifications.
    /// Uses positional indexing for duplicate earning types.
    /// </summary>
    Task RecordEarningLineChangesAsync(int payslipId, string userId, List<PayslipEarningLine> oldLines, List<PayslipEarningLine> newLines, List<EarningType> earningTypes);

    /// <summary>
    /// Records a manager notes change if the value actually changed.
    /// </summary>
    Task RecordManagerNotesChangeAsync(int payslipId, string userId, string? oldNotes, string? newNotes);

    /// <summary>
    /// Records a payslip being added to or removed from a period.
    /// </summary>
    Task RecordPayslipAddedOrRemovedAsync(int payslipId, string userId, bool isAdded, string employeeName);

    /// <summary>
    /// Returns the full audit history for a specific payslip in reverse chronological order.
    /// </summary>
    Task<List<PayslipAuditLogDto>> GetAuditHistoryAsync(int payslipId, int businessId);

    /// <summary>
    /// Returns audit entries for all payslips in a period, grouped by employee.
    /// </summary>
    Task<List<PeriodAuditGroupDto>> GetPeriodAuditSummaryAsync(int periodId, int businessId);
}
