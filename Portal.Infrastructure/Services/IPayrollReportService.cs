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
}
