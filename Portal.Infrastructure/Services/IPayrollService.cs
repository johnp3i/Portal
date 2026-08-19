using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Payroll;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Service interface for all Payroll Phase A business operations.
/// Covers department management, employee management, earning/deduction types,
/// payslip period management, batch generation, payslip detail, and PDF/email.
/// </summary>
public interface IPayrollService
{
    // Department Management
    Task<List<DepartmentDto>> GetDepartmentsAsync(int businessId);
    Task<DepartmentDto?> GetDepartmentByIdAsync(int id, int businessId);
    Task<ServiceResult> CreateDepartmentAsync(int businessId, CreateDepartmentRequest request);
    Task<ServiceResult> UpdateDepartmentAsync(int businessId, UpdateDepartmentRequest request);
    Task<ServiceResult> ToggleDepartmentAsync(int id, int businessId);

    // Employee Management
    Task<PagedResult<EmployeeDto>> GetEmployeesAsync(int businessId, string? search, int? departmentId, bool? isActive, int page, int pageSize);
    Task<EmployeeDetailDto?> GetEmployeeByIdAsync(int id, int businessId);
    Task<ServiceResult> CreateEmployeeAsync(int businessId, CreateEmployeeRequest request);
    Task<ServiceResult> UpdateEmployeeAsync(int businessId, UpdateEmployeeRequest request);
    Task<ServiceResult> ToggleEmployeeAsync(int id, int businessId);

    // Earning Types (Admin)
    Task<List<EarningTypeDto>> GetEarningTypesAsync();
    Task<ServiceResult> CreateEarningTypeAsync(CreateEarningTypeRequest request);
    Task<ServiceResult> ToggleEarningTypeAsync(int id);

    // Deduction Types (Business + Admin)
    Task<List<DeductionTypeDto>> GetDeductionTypesForBusinessAsync(int businessId);
    Task<ServiceResult> CreateDeductionTypeAsync(int businessId, CreateDeductionTypeRequest request);
    Task<ServiceResult> ToggleDeductionTypeAsync(int id);
    Task<List<DeductionRateHistoryDto>> GetRateHistoryAsync(int deductionTypeId);
    Task<ServiceResult> AddRateHistoryAsync(AddRateHistoryRequest request);

    // Deduction Template Import
    Task<List<DeductionTypeDto>> GetDeductionTemplatesAsync(string country);
    Task<ServiceResult> ImportDeductionTemplatesAsync(int businessId, int[] templateIds);

    // Employee Default Earnings
    Task<List<EmployeeDefaultEarningsDto>> GetDefaultEarningsAsync(int employeeId, int businessId);
    Task<ServiceResult> SaveDefaultEarningsAsync(int businessId, int employeeId, List<EmployeeDefaultEarningInput> lines);

    // Period Management
    Task<List<PayslipPeriodDto>> GetPeriodsAsync(int businessId);
    Task<PayslipPeriodDetailDto?> GetPeriodDetailAsync(int id, int businessId);
    Task<ServiceResult> CreatePeriodAsync(int businessId, CreatePeriodRequest request);
    Task<ServiceResult> FinalisePeriodAsync(int id, int businessId);

    // Payslip Generation
    Task<BatchGenerationPreview> GeneratePayslipsPreviewAsync(int periodId, int businessId);
    Task<ServiceResult> ConfirmBatchGenerationAsync(int periodId, int businessId);
    Task<PayslipDetailDto?> GetPayslipDetailAsync(int id, int businessId);
    Task<ServiceResult> SaveEarningLinesAsync(int businessId, SaveEarningLinesRequest request);
    Task<ServiceResult> SaveManagerNotesAsync(int businessId, SaveManagerNotesRequest request);

    // Payslip PDF & Email
    Task<byte[]> GeneratePayslipPdfAsync(int payslipId, int businessId, bool includeSignature);
    Task<ServiceResult> SendPayslipEmailAsync(int payslipId, int businessId, string userId, bool includeSignature);
    Task<ServiceResult> SendAllPayslipEmailsAsync(int periodId, int businessId, string userId, bool includeSignature);

    // Phase B: Unlock & Re-finalise
    Task<ServiceResult> UnlockPeriodAsync(int periodId, int businessId, string userId, string userRole);
    Task<ServiceResult> RefinalisePeriodAsync(int periodId, int businessId, string userId, string userRole);

    // Phase B: Audit History
    Task<List<PayslipAuditLogDto>> GetPayslipAuditHistoryAsync(int payslipId, int businessId);
    Task<List<PeriodAuditGroupDto>> GetPeriodAuditSummaryAsync(int periodId, int businessId);

    // Phase D: PAYE Toggle
    Task<ServiceResult> UpdateEmployeePayeStatusAsync(int businessId, int employeeId, bool isPayeApplicable);

    // Phase D: Contribution Report
    Task<ContributionReportDto?> GetContributionReportAsync(int periodId, int businessId);
    Task<List<PayslipPeriodComplianceFilingDto>> GetComplianceFilingHistoryAsync(int periodId, int businessId);

    // Earnings Override & Salary Register
    Task<RecalculationResult> RecalculateEmployeeAsync(int employeeId, int periodId, int businessId, List<EarningLineOverride> overriddenLines);
    Task<ServiceResult> ConfirmBatchGenerationWithOverridesAsync(int periodId, int businessId, List<EmployeeEarningsOverride> overrides);
    Task<SalaryRegisterViewModel> GetSalaryRegisterAsync(int businessId, int? departmentId, bool? isActive);
    Task<ServiceResult> UpdateBaseSalaryAsync(int employeeId, int businessId, decimal newSalary);
}
