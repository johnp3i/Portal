using Portal.Infrastructure.Models.Payroll;

namespace Portal.Infrastructure.Services;

public interface IPayrollReportService
{
    // Employee History
    Task<EmployeePayslipHistoryDto> GetEmployeeHistoryAsync(int employeeId, int businessId, int? year);

    // Annual Summary
    Task<AnnualSummaryDto> GetAnnualSummaryAsync(int employeeId, int businessId, int year);
    Task<byte[]> GenerateAnnualSummaryPdfAsync(int employeeId, int businessId, int year);

    // Earnings Breakdown
    Task<EarningsBreakdownDto> GetEarningsBreakdownAsync(int businessId, EarningsBreakdownFilter filter);
    Task<byte[]> ExportEarningsBreakdownToExcelAsync(int businessId, EarningsBreakdownFilter filter);

    // Period Summary
    Task<int?> GetLatestFinalisedPeriodIdAsync(int businessId);
    Task<PeriodSummaryDto> GetPeriodSummaryAsync(int periodId, int businessId, int? departmentId);
    Task<byte[]> GeneratePeriodSummaryPdfAsync(int periodId, int businessId, int? departmentId);
    Task<byte[]> ExportPeriodSummaryToExcelAsync(int periodId, int businessId, int? departmentId);

    // Employee Statement
    Task<byte[]> GenerateEmployeeStatementPdfAsync(int employeeId, int businessId, int startYear, int startMonth, int endYear, int endMonth);

    // Download All (ZIP)
    Task<byte[]> GenerateAllPayslipsPdfZipAsync(int periodId, int businessId);

    // Email log
    Task<List<PayslipEmailLogDto>> GetEmailLogForPayslipAsync(int payslipId);
    Task<PayslipEmailSummaryDto> GetEmailSummaryForPeriodAsync(int periodId, int businessId);
    Task<PayslipEmailLogDto?> GetLastEmailForPayslipAsync(int payslipId);

    // Payroll Register
    /// <summary>
    /// Returns the employees eligible for the Payroll Register over the given range: only employees
    /// that have a Finalised_Payslip (PayslipStatusTypeId IN (3,5)) in ANY Range_Period for the
    /// business, projected to a lightweight option (EmployeeId + EmployeeName) and sorted
    /// alphabetically by name. Enumerates Range_Periods via GetPeriodsByBusinessAsync then reads
    /// GetFinalisedPayslipsForPeriodAsync once per Range_Period. Names are resolved via a single
    /// batched GetEmployeesByIdsAsync (NOT GetActiveEmployeesForPeriodAsync) so terminated/inactive
    /// employees with an in-range finalised payslip still appear. Additive: does not alter the
    /// protected operations. (R3.1, R3.4, R3.6, R8.5)
    /// </summary>
    Task<List<RegisterEmployeeOption>> GetRegisterEligibleEmployeesAsync(
        int fromYear, int fromMonth, int toYear, int toMonth, int businessId);

    /// <summary>
    /// Builds the single Payroll Register .xlsx workbook for the selected range and employees, scoped
    /// to the business. Enumerates Range_Periods once, reads finalised payslips once per Range_Period,
    /// loads reference lookups once for the whole range (N+1 avoidance, R14.3), includes only selected
    /// employees that have a Finalised_Payslip in at least one Range_Period (R9.3), builds the Summary
    /// sheet (index 1, dynamic Contribution_Type columns) and one detail sheet per eligible selected
    /// employee. Returns null (or an empty byte[]) when no selected employee has an in-range finalised
    /// payslip, so the controller can decline with a JSON message (R4.7, R9.5). Rethrows on
    /// data-access errors (R10.3).
    /// </summary>
    Task<byte[]?> GenerateRegisterWorkbookAsync(
        int fromYear, int fromMonth, int toYear, int toMonth, int businessId, int[] employeeIds);

    /// <summary>
    /// The shared data computation behind both the Download (<see cref="GenerateRegisterWorkbookAsync"/>)
    /// and the Preview (<c>AxGetPreviewRegister</c>) actions. Resolves the ordered Range_Periods and
    /// builds one <see cref="RegisterEmployeeAggregate"/> per eligible selected employee (dedup,
    /// preserving selection order), then returns both in a <see cref="RegisterData"/>. Because the
    /// workbook is built from exactly these aggregates and the preview JSON is projected from them, a
    /// preview and a download of the same selection agree cell-for-cell and can never drift
    /// (R15.12, R12.6).
    ///
    /// Returns <c>null</c> on any decline path (non-positive business id, null/empty selection, a range
    /// that resolves to no Range_Period, or a selection where no employee is eligible); a non-null
    /// result always carries at least one aggregate. Feature-owned with no external callers, so adding
    /// it is purely additive (R12.6). Rethrows on data-access errors.
    /// </summary>
    Task<RegisterData?> GetRegisterDataAsync(
        int fromYear, int fromMonth, int toYear, int toMonth, int businessId, int[] employeeIds);
}
