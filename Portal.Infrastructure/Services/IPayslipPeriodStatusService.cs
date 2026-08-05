using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

public interface IPayslipPeriodStatusService
{
    /// <summary>
    /// Validates whether a transition from currentStatus to targetStatus is allowed.
    /// </summary>
    bool IsTransitionAllowed(byte currentStatusId, byte targetStatusId);

    /// <summary>
    /// Returns all valid target statuses from the given current status.
    /// </summary>
    IReadOnlyList<byte> GetAllowedTransitions(byte currentStatusId);

    /// <summary>
    /// Returns true if the given status allows payslip editing.
    /// Editable statuses: Draft (1), Preview (2), Unlocked (4).
    /// Non-editable statuses: Finalised (3), Re-finalised (5).
    /// </summary>
    bool IsEditableStatus(byte statusId);

    /// <summary>
    /// Executes the unlock transition: period → Unlocked, all payslips → Unlocked.
    /// Creates audit entries. Validates role permissions.
    /// </summary>
    Task<ServiceResult> UnlockPeriodAsync(int periodId, int businessId, string userId, string userRole);

    /// <summary>
    /// Executes re-finalisation: recalculates all payslips, transitions to Re-finalised,
    /// triggers P&L adjustment, creates audit entries.
    /// </summary>
    Task<ServiceResult> RefinalisePeriodAsync(int periodId, int businessId, string userId, string userRole);
}
