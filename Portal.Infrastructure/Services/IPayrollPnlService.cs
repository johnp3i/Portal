using Portal.Infrastructure.Models;

namespace Portal.Infrastructure.Services;

public interface IPayrollPnlService
{
    /// <summary>
    /// Creates two Purchase entries (Salary Cost + Employer Contributions) for a finalised period.
    /// Must be called within an existing transaction.
    /// </summary>
    Task<ServiceResult> CreatePnlEntriesAsync(int periodId, int businessId);

    /// <summary>
    /// Reverses existing P&L entries (marks as cancelled) and creates new entries
    /// with recalculated totals. Used during re-finalisation.
    /// Must be called within an existing transaction.
    /// userId is required to populate CancelledByUserId on cancelled entries.
    /// </summary>
    Task<ServiceResult> AdjustPnlEntriesAsync(int periodId, int businessId, string userId);

    /// <summary>
    /// Ensures the business has the required payroll expense categories and internal supplier.
    /// Called once during first finalisation — idempotent.
    /// </summary>
    Task EnsurePayrollPnlSetupAsync(int businessId);
}
