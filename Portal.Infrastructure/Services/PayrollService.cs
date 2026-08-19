using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Portal.Infrastructure.Data;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

/// <summary>
/// Full PayrollService implementing IPayrollService.
/// Orchestrates all payroll business logic: departments, employees, earning/deduction types,
/// periods, batch generation, payslip detail, and PDF/email stubs.
/// </summary>
public class PayrollService : IPayrollService
{
    private readonly PayrollRepository _repository;
    private readonly IPayslipCalculationEngine _calculationEngine;
    private readonly IPayslipCalculationOrchestrator _orchestrator;
    private readonly IPayslipPeriodStatusService _periodStatusService;
    private readonly IPayslipAuditService _auditService;
    private readonly IPayrollPnlService _pnlService;
    private readonly IComplianceIntegrationService _complianceIntegrationService;
    private readonly IPayslipPdfService _pdfService;
    private readonly IPayslipRenderer _renderer;
    private readonly IBusinessService _businessService;
    private readonly PortalDbContext _portalDbContext;

    public PayrollService(
        PayrollRepository repository,
        IPayslipCalculationEngine calculationEngine,
        IPayslipCalculationOrchestrator orchestrator,
        IPayslipPeriodStatusService periodStatusService,
        IPayslipAuditService auditService,
        IPayrollPnlService pnlService,
        IComplianceIntegrationService complianceIntegrationService,
        IPayslipPdfService pdfService,
        IPayslipRenderer renderer,
        IBusinessService businessService,
        PortalDbContext portalDbContext)
    {
        _repository = repository;
        _calculationEngine = calculationEngine;
        _orchestrator = orchestrator;
        _periodStatusService = periodStatusService;
        _auditService = auditService;
        _pnlService = pnlService;
        _complianceIntegrationService = complianceIntegrationService;
        _pdfService = pdfService;
        _renderer = renderer;
        _businessService = businessService;
        _portalDbContext = portalDbContext;
    }

    #region Department Management

    public async Task<List<DepartmentDto>> GetDepartmentsAsync(int businessId)
    {
        try
        {
            var departments = await _repository.GetDepartmentsByBusinessAsync(businessId);
            var employees = await _repository.GetEmployeesAsync(businessId, null, null, null, 1, int.MaxValue);

            return departments.Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                IsActive = d.IsActive,
                CreatedAtUtc = d.CreatedAtUtc,
                EmployeeCount = employees.Items.Count(e => e.DepartmentId == d.Id)
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<DepartmentDto?> GetDepartmentByIdAsync(int id, int businessId)
    {
        try
        {
            var department = await _repository.GetDepartmentByIdAsync(id, businessId);
            if (department == null) return null;

            return new DepartmentDto
            {
                Id = department.Id,
                Name = department.Name,
                IsActive = department.IsActive,
                CreatedAtUtc = department.CreatedAtUtc,
                EmployeeCount = 0
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateDepartmentAsync(int businessId, CreateDepartmentRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Department name is required.");

            var exists = await _repository.DepartmentNameExistsAsync(businessId, request.Name.Trim(), null);
            if (exists)
                return ServiceResult.Fail("A department with this name already exists.");

            var entity = new Department
            {
                BusinessId = businessId,
                Name = request.Name.Trim()
            };

            var id = await _repository.InsertDepartmentAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateDepartmentAsync(int businessId, UpdateDepartmentRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Department name is required.");

            var department = await _repository.GetDepartmentByIdAsync(request.Id, businessId);
            if (department == null)
                return ServiceResult.Fail("Department not found.");

            var exists = await _repository.DepartmentNameExistsAsync(businessId, request.Name.Trim(), request.Id);
            if (exists)
                return ServiceResult.Fail("A department with this name already exists.");

            department.Name = request.Name.Trim();
            await _repository.UpdateDepartmentAsync(department);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ToggleDepartmentAsync(int id, int businessId)
    {
        try
        {
            var department = await _repository.GetDepartmentByIdAsync(id, businessId);
            if (department == null)
                return ServiceResult.Fail("Department not found.");

            // If currently active, check for active employees before deactivating
            if (department.IsActive)
            {
                var hasEmployees = await _repository.DepartmentHasActiveEmployeesAsync(id);
                if (hasEmployees)
                    return ServiceResult.Fail("Cannot deactivate a department that has active employees.");
            }

            // Toggle by updating the name (repository toggles via direct SQL if needed)
            // We reuse update to flip IsActive
            department.IsActive = !department.IsActive;
            await _repository.UpdateDepartmentAsync(department);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Employee Management

    private static readonly Dictionary<byte, string> SalaryTypeNames = new()
    {
        { 1, "Full-time" },
        { 2, "Part-time" },
        { 3, "Hourly" }
    };

    public async Task<PagedResult<EmployeeDto>> GetEmployeesAsync(
        int businessId, string? search, int? departmentId, bool? isActive, int page, int pageSize)
    {
        try
        {
            var (items, totalCount) = await _repository.GetEmployeesAsync(
                businessId, search, departmentId, isActive, page, pageSize);

            var departments = await _repository.GetDepartmentsByBusinessAsync(businessId);
            var deptLookup = departments.ToDictionary(d => d.Id, d => d.Name);

            var dtos = items.Select(e => new EmployeeDto
            {
                Id = e.Id,
                Name = e.Name,
                Position = e.Position,
                DepartmentName = e.DepartmentId.HasValue && deptLookup.ContainsKey(e.DepartmentId.Value)
                    ? deptLookup[e.DepartmentId.Value] : null,
                SalaryTypeName = SalaryTypeNames.GetValueOrDefault(e.SalaryTypeId, "Unknown"),
                BaseSalary = e.BaseSalary,
                IsActive = e.IsActive,
                StartDate = e.StartDate
            }).ToList();

            return new PagedResult<EmployeeDto>
            {
                Items = dtos,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<EmployeeDetailDto?> GetEmployeeByIdAsync(int id, int businessId)
    {
        try
        {
            var employee = await _repository.GetEmployeeByIdAsync(id, businessId);
            if (employee == null) return null;

            return new EmployeeDetailDto
            {
                Id = employee.Id,
                DepartmentId = employee.DepartmentId,
                Name = employee.Name,
                Position = employee.Position,
                SocialInsuranceNumber = employee.SocialInsuranceNumber,
                IdNumber = employee.IdNumber,
                Phone = employee.Phone,
                Email = employee.Email,
                StartDate = employee.StartDate,
                EndDate = employee.EndDate,
                SalaryTypeId = employee.SalaryTypeId,
                BaseSalary = employee.BaseSalary,
                HourlyRate = employee.HourlyRate,
                BankAccount = employee.BankAccount,
                IsActive = employee.IsActive,
                IsPayeApplicable = employee.IsPayeApplicable
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateEmployeeAsync(int businessId, CreateEmployeeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Employee name is required.");

            if (string.IsNullOrWhiteSpace(request.SocialInsuranceNumber))
                return ServiceResult.Fail("Social insurance number is required.");

            if (string.IsNullOrWhiteSpace(request.IdNumber))
                return ServiceResult.Fail("ID number is required.");

            if (request.SalaryTypeId < 1 || request.SalaryTypeId > 3)
                return ServiceResult.Fail("Invalid salary type.");

            var sinExists = await _repository.SocialInsuranceNumberExistsAsync(
                businessId, request.SocialInsuranceNumber.Trim(), null);
            if (sinExists)
                return ServiceResult.Fail("An employee with this social insurance number already exists.");

            var idExists = await _repository.IdNumberExistsAsync(
                businessId, request.IdNumber.Trim(), null);
            if (idExists)
                return ServiceResult.Fail("An employee with this ID number already exists.");

            var entity = new Employee
            {
                BusinessId = businessId,
                DepartmentId = request.DepartmentId,
                Name = request.Name.Trim(),
                Position = request.Position?.Trim(),
                SocialInsuranceNumber = request.SocialInsuranceNumber.Trim(),
                IdNumber = request.IdNumber.Trim(),
                Phone = request.Phone?.Trim(),
                Email = request.Email?.Trim(),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                SalaryTypeId = request.SalaryTypeId,
                BaseSalary = request.BaseSalary,
                HourlyRate = request.HourlyRate,
                BankAccount = request.BankAccount?.Trim(),
                IsActive = true
            };

            var id = await _repository.InsertEmployeeAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateEmployeeAsync(int businessId, UpdateEmployeeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Employee name is required.");

            if (string.IsNullOrWhiteSpace(request.SocialInsuranceNumber))
                return ServiceResult.Fail("Social insurance number is required.");

            if (string.IsNullOrWhiteSpace(request.IdNumber))
                return ServiceResult.Fail("ID number is required.");

            if (request.SalaryTypeId < 1 || request.SalaryTypeId > 3)
                return ServiceResult.Fail("Invalid salary type.");

            var employee = await _repository.GetEmployeeByIdAsync(request.Id, businessId);
            if (employee == null)
                return ServiceResult.Fail("Employee not found.");

            var sinExists = await _repository.SocialInsuranceNumberExistsAsync(
                businessId, request.SocialInsuranceNumber.Trim(), request.Id);
            if (sinExists)
                return ServiceResult.Fail("An employee with this social insurance number already exists.");

            var idExists = await _repository.IdNumberExistsAsync(
                businessId, request.IdNumber.Trim(), request.Id);
            if (idExists)
                return ServiceResult.Fail("An employee with this ID number already exists.");

            employee.DepartmentId = request.DepartmentId;
            employee.Name = request.Name.Trim();
            employee.Position = request.Position?.Trim();
            employee.SocialInsuranceNumber = request.SocialInsuranceNumber.Trim();
            employee.IdNumber = request.IdNumber.Trim();
            employee.Phone = request.Phone?.Trim();
            employee.Email = request.Email?.Trim();
            employee.StartDate = request.StartDate;
            employee.EndDate = request.EndDate;
            employee.SalaryTypeId = request.SalaryTypeId;
            employee.BaseSalary = request.BaseSalary;
            employee.HourlyRate = request.HourlyRate;
            employee.BankAccount = request.BankAccount?.Trim();

            await _repository.UpdateEmployeeAsync(employee);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ToggleEmployeeAsync(int id, int businessId)
    {
        try
        {
            var employee = await _repository.GetEmployeeByIdAsync(id, businessId);
            if (employee == null)
                return ServiceResult.Fail("Employee not found.");

            employee.IsActive = !employee.IsActive;
            await _repository.UpdateEmployeeAsync(employee);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Earning Types

    public async Task<List<EarningTypeDto>> GetEarningTypesAsync()
    {
        try
        {
            var earningTypes = await _repository.GetAllEarningTypesAsync();

            return earningTypes.Select(e => new EarningTypeDto
            {
                Id = e.Id,
                Name = e.Name,
                Code = e.Code,
                IsActive = e.IsActive,
                SortOrder = e.SortOrder
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateEarningTypeAsync(CreateEarningTypeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Earning type name is required.");

            if (string.IsNullOrWhiteSpace(request.Code))
                return ServiceResult.Fail("Earning type code is required.");

            var entity = new EarningType
            {
                Name = request.Name.Trim(),
                Code = request.Code.Trim(),
                IsActive = true,
                SortOrder = request.SortOrder
            };

            var id = await _repository.InsertEarningTypeAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ToggleEarningTypeAsync(int id)
    {
        try
        {
            await _repository.ToggleEarningTypeAsync(id);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Deduction Types

    private static readonly Dictionary<byte, string> DeductionCategoryNames = new()
    {
        { 1, "Deduction" },
        { 2, "Contribution" }
    };

    public async Task<List<DeductionTypeDto>> GetDeductionTypesForBusinessAsync(int businessId)
    {
        try
        {
            var types = await _repository.GetDeductionTypesByBusinessAsync(businessId);
            var dtos = new List<DeductionTypeDto>();

            foreach (var t in types)
            {
                var rateHistory = await _repository.GetRateHistoryAsync(t.Id);
                var currentRate = rateHistory.FirstOrDefault(r => r.EffectiveToUtc == null);

                dtos.Add(new DeductionTypeDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Code = t.Code,
                    IsPercentage = t.IsPercentage,
                    DeductionCategoryTypeId = t.DeductionCategoryTypeId,
                    CategoryName = DeductionCategoryNames.GetValueOrDefault(t.DeductionCategoryTypeId, "Unknown"),
                    IsActive = t.IsActive,
                    Country = t.Country,
                    CurrentRate = currentRate?.Rate
                });
            }

            return dtos;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreateDeductionTypeAsync(int businessId, CreateDeductionTypeRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return ServiceResult.Fail("Deduction type name is required.");

            if (string.IsNullOrWhiteSpace(request.Code))
                return ServiceResult.Fail("Deduction type code is required.");

            if (request.DeductionCategoryTypeId < 1 || request.DeductionCategoryTypeId > 2)
                return ServiceResult.Fail("Invalid deduction category type.");

            if (request.InitialRate <= 0)
                return ServiceResult.Fail("Initial rate must be greater than zero.");

            var entity = new DeductionType
            {
                Name = request.Name.Trim(),
                Code = request.Code.Trim(),
                IsPercentage = request.IsPercentage,
                DeductionCategoryTypeId = request.DeductionCategoryTypeId,
                BusinessId = businessId,
                IsActive = true,
                Country = request.Country,
                IsTemplate = false
            };

            var rates = new List<DeductionRateHistory>
            {
                new DeductionRateHistory
                {
                    Rate = request.InitialRate,
                    EffectiveFromUtc = request.EffectiveFromUtc,
                    EffectiveToUtc = null
                }
            };

            await _repository.InsertDeductionTypeWithRatesAsync(entity, rates);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ToggleDeductionTypeAsync(int id)
    {
        try
        {
            await _repository.ToggleDeductionTypeAsync(id);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<DeductionRateHistoryDto>> GetRateHistoryAsync(int deductionTypeId)
    {
        try
        {
            var history = await _repository.GetRateHistoryAsync(deductionTypeId);

            return history.Select(h => new DeductionRateHistoryDto
            {
                Id = h.Id,
                Rate = h.Rate,
                EffectiveFromUtc = h.EffectiveFromUtc,
                EffectiveToUtc = h.EffectiveToUtc
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> AddRateHistoryAsync(AddRateHistoryRequest request)
    {
        try
        {
            if (request.Rate <= 0)
                return ServiceResult.Fail("Rate must be greater than zero.");

            // Validate no overlap: check that the new EffectiveFromUtc doesn't fall within existing active periods
            var existingHistory = await _repository.GetRateHistoryAsync(request.DeductionTypeId);
            var currentOpen = existingHistory.FirstOrDefault(h => h.EffectiveToUtc == null);

            if (currentOpen != null && request.EffectiveFromUtc <= currentOpen.EffectiveFromUtc)
                return ServiceResult.Fail("New rate effective date must be after the current rate's effective date.");

            // Close the current open rate
            if (currentOpen != null)
            {
                await _repository.CloseCurrentRateAsync(request.DeductionTypeId, request.EffectiveFromUtc);
            }

            // Insert new rate with open end
            var entity = new DeductionRateHistory
            {
                DeductionTypeId = request.DeductionTypeId,
                Rate = request.Rate,
                EffectiveFromUtc = request.EffectiveFromUtc,
                EffectiveToUtc = null
            };

            await _repository.InsertRateHistoryAsync(entity);
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Deduction Template Import

    public async Task<List<DeductionTypeDto>> GetDeductionTemplatesAsync(string country)
    {
        try
        {
            var templates = await _repository.GetTemplatesByCountryAsync(country);
            var dtos = new List<DeductionTypeDto>();

            foreach (var t in templates)
            {
                var rateHistory = await _repository.GetRateHistoryAsync(t.Id);
                var currentRate = rateHistory.FirstOrDefault(r => r.EffectiveToUtc == null);

                dtos.Add(new DeductionTypeDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Code = t.Code,
                    IsPercentage = t.IsPercentage,
                    DeductionCategoryTypeId = t.DeductionCategoryTypeId,
                    CategoryName = DeductionCategoryNames.GetValueOrDefault(t.DeductionCategoryTypeId, "Unknown"),
                    IsActive = t.IsActive,
                    Country = t.Country,
                    CurrentRate = currentRate?.Rate
                });
            }

            return dtos;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ImportDeductionTemplatesAsync(int businessId, int[] templateIds)
    {
        try
        {
            if (templateIds == null || templateIds.Length == 0)
                return ServiceResult.Fail("No templates selected for import.");

            foreach (var templateId in templateIds)
            {
                // Load template
                var templates = await _repository.GetTemplatesByCountryAsync("CY");
                var template = templates.FirstOrDefault(t => t.Id == templateId);
                if (template == null) continue;

                // Load template rates
                var rates = await _repository.GetRateHistoryAsync(templateId);

                // Create business-specific copy
                var newType = new DeductionType
                {
                    Name = template.Name,
                    Code = template.Code,
                    IsPercentage = template.IsPercentage,
                    DeductionCategoryTypeId = template.DeductionCategoryTypeId,
                    BusinessId = businessId,
                    IsActive = true,
                    Country = template.Country,
                    IsTemplate = false
                };

                var newRates = rates.Select(r => new DeductionRateHistory
                {
                    Rate = r.Rate,
                    EffectiveFromUtc = r.EffectiveFromUtc,
                    EffectiveToUtc = r.EffectiveToUtc
                }).ToList();

                await _repository.InsertDeductionTypeWithRatesAsync(newType, newRates);
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Employee Default Earnings

    public async Task<List<EmployeeDefaultEarningsDto>> GetDefaultEarningsAsync(int employeeId, int businessId)
    {
        try
        {
            // Validate employee belongs to business
            var employee = await _repository.GetEmployeeByIdAsync(employeeId, businessId);
            if (employee == null) return new List<EmployeeDefaultEarningsDto>();

            var defaults = await _repository.GetDefaultEarningsByEmployeeAsync(employeeId);
            var earningTypes = await _repository.GetAllEarningTypesAsync();
            var typeLookup = earningTypes.ToDictionary(e => e.Id, e => e.Name);

            return defaults.Select(d => new EmployeeDefaultEarningsDto
            {
                Id = d.Id,
                EarningTypeId = d.EarningTypeId,
                EarningTypeName = typeLookup.GetValueOrDefault(d.EarningTypeId, "Unknown"),
                Description = d.Description,
                Amount = d.Amount,
                OvertimeMultiplier = d.OvertimeMultiplier,
                OvertimeHours = d.OvertimeHours
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> SaveDefaultEarningsAsync(int businessId, int employeeId, List<EmployeeDefaultEarningInput> lines)
    {
        try
        {
            // Validate employee belongs to business
            var employee = await _repository.GetEmployeeByIdAsync(employeeId, businessId);
            if (employee == null)
                return ServiceResult.Fail("Employee not found.");

            // Delete existing defaults
            var existing = await _repository.GetDefaultEarningsByEmployeeAsync(employeeId);
            foreach (var item in existing)
            {
                await _repository.DeleteDefaultEarningAsync(item.Id);
            }

            // Insert new defaults
            foreach (var line in lines)
            {
                var entity = new EmployeeDefaultEarnings
                {
                    EmployeeId = employeeId,
                    EarningTypeId = line.EarningTypeId,
                    Description = line.Description,
                    Amount = line.Amount,
                    OvertimeMultiplier = line.OvertimeMultiplier,
                    OvertimeHours = line.OvertimeHours
                };

                await _repository.InsertDefaultEarningAsync(entity);
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Period Management

    private Dictionary<byte, string>? _statusNames;

    private async Task<Dictionary<byte, string>> GetStatusNamesAsync()
    {
        _statusNames ??= await _repository.GetStatusNamesAsync();
        return _statusNames;
    }

    public async Task<List<PayslipPeriodDto>> GetPeriodsAsync(int businessId)
    {
        try
        {
            var periods = await _repository.GetPeriodsByBusinessAsync(businessId);
            var statusNames = await GetStatusNamesAsync();
            var dtos = new List<PayslipPeriodDto>();

            foreach (var p in periods)
            {
                var payslips = await _repository.GetPayslipsByPeriodAsync(p.Id);

                dtos.Add(new PayslipPeriodDto
                {
                    Id = p.Id,
                    Year = p.Year,
                    Month = p.Month,
                    Status = statusNames.GetValueOrDefault(p.PayslipStatusTypeId, "Unknown"),
                    ProcessedAtUtc = p.ProcessedAtUtc,
                    PayslipCount = payslips.Count,
                    TotalNetSalary = payslips.Sum(s => s.NetSalary)
                });
            }

            return dtos;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PayslipPeriodDetailDto?> GetPeriodDetailAsync(int id, int businessId)
    {
        try
        {
            var period = await _repository.GetPeriodByIdAsync(id, businessId);
            if (period == null) return null;

            var payslips = await _repository.GetPayslipsByPeriodAsync(id);
            var departments = await _repository.GetDepartmentsByBusinessAsync(businessId);
            var statusNames = await GetStatusNamesAsync();
            var deptLookup = departments.ToDictionary(d => d.Id, d => d.Name);

            var summaries = new List<PayslipSummaryDto>();
            foreach (var p in payslips)
            {
                var employee = await _repository.GetEmployeeByIdAsync(p.EmployeeId, businessId);
                summaries.Add(new PayslipSummaryDto
                {
                    Id = p.Id,
                    EmployeeName = employee?.Name ?? "Unknown",
                    DepartmentName = employee?.DepartmentId != null && deptLookup.ContainsKey(employee.DepartmentId.Value)
                        ? deptLookup[employee.DepartmentId.Value] : null,
                    TotalEarnings = p.TotalEarnings,
                    TotalEmployeeDeductions = p.TotalEmployeeDeductions,
                    NetSalary = p.NetSalary,
                    TotalEmployerContributions = p.TotalEmployerContributions
                });
            }

            return new PayslipPeriodDetailDto
            {
                Id = period.Id,
                Year = period.Year,
                Month = period.Month,
                Status = statusNames.GetValueOrDefault(period.PayslipStatusTypeId, "Unknown"),
                ProcessedAtUtc = period.ProcessedAtUtc,
                Payslips = summaries
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> CreatePeriodAsync(int businessId, CreatePeriodRequest request)
    {
        try
        {
            if (request.Year < 2000 || request.Year > 2100)
                return ServiceResult.Fail("Invalid year.");

            if (request.Month < 1 || request.Month > 12)
                return ServiceResult.Fail("Invalid month.");

            var exists = await _repository.PeriodExistsAsync(businessId, request.Year, request.Month);
            if (exists)
                return ServiceResult.Fail("A period for this year and month already exists.");

            var entity = new PayslipPeriod
            {
                BusinessId = businessId,
                Year = request.Year,
                Month = request.Month,
                PayslipStatusTypeId = 1 // Draft
            };

            var id = await _repository.InsertPeriodAsync(entity);
            return ServiceResult.Ok(id);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> FinalisePeriodAsync(int id, int businessId)
    {
        try
        {
            var period = await _repository.GetPeriodByIdAsync(id, businessId);
            if (period == null)
                return ServiceResult.Fail("Period not found.");

            if (!_periodStatusService.IsTransitionAllowed(period.PayslipStatusTypeId, 3))
                return ServiceResult.Fail("Only periods in Preview status can be finalised.");

            using var transaction = await _portalDbContext.Database.BeginTransactionAsync();
            try
            {
                var updated = await _repository.UpdatePeriodStatusAsync(id, 3, period.PayslipStatusTypeId, DateTime.UtcNow);
                if (!updated)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.Fail("Period status has been changed by another user. Please refresh and try again.");
                }

                await _repository.UpdateAllPayslipStatusesInPeriodAsync(id, 3);

                // Phase B: Create P&L entries
                var pnlResult = await _pnlService.CreatePnlEntriesAsync(id, businessId);
                if (!pnlResult.Success)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.Fail(pnlResult.Message ?? "Failed to create P&L entries. Finalisation rolled back.");
                }

                await transaction.CommitAsync();

                // Phase D: Non-blocking compliance integration (after commit)
                try
                {
                    await _complianceIntegrationService.UpdateComplianceFilingFromPayrollAsync(id, businessId, "system");
                }
                catch
                {
                    // Non-blocking: compliance failure must not affect finalisation result
                }

                return ServiceResult.Ok();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Batch Payslip Generation

    public async Task<BatchGenerationPreview> GeneratePayslipsPreviewAsync(int periodId, int businessId)
    {
        try
        {
            var period = await _repository.GetPeriodByIdAsync(periodId, businessId);
            if (period == null)
            {
                return new BatchGenerationPreview
                {
                    PeriodId = periodId,
                    Errors = new List<BatchValidationError>
                    {
                        new BatchValidationError { Error = "Period not found." }
                    }
                };
            }

            if (period.PayslipStatusTypeId != 1) // Must be Draft
            {
                return new BatchGenerationPreview
                {
                    PeriodId = periodId,
                    Year = period.Year,
                    Month = period.Month,
                    Errors = new List<BatchValidationError>
                    {
                        new BatchValidationError { Error = "Batch generation can only be run on Draft periods." }
                    }
                };
            }

            var periodDate = new DateTime(period.Year, period.Month, 1);
            var employees = await _repository.GetActiveEmployeesForPeriodAsync(businessId, periodDate);
            var departments = await _repository.GetDepartmentsByBusinessAsync(businessId);
            var deptLookup = departments.ToDictionary(d => d.Id, d => d.Name);

            // Load earning types for code lookup
            var earningTypes = await _repository.GetAllEarningTypesAsync();
            var earningTypeLookup = earningTypes.ToDictionary(e => e.Id, e => e);

            // Load business deductions with rates
            var deductionTypes = await _repository.GetActiveDeductionsWithRatesAsync(businessId);
            var deductionsWithRates = new List<DeductionTypeWithHistory>();
            foreach (var dt in deductionTypes)
            {
                var rates = await _repository.GetRateHistoryAsync(dt.Id);
                deductionsWithRates.Add(new DeductionTypeWithHistory
                {
                    Id = dt.Id,
                    Name = dt.Name,
                    Code = dt.Code,
                    IsPercentage = dt.IsPercentage,
                    DeductionCategoryTypeId = dt.DeductionCategoryTypeId,
                    IsPayeDeductible = dt.IsPayeDeductible,
                    RateHistories = rates
                });
            }

            var preview = new BatchGenerationPreview
            {
                PeriodId = periodId,
                Year = period.Year,
                Month = period.Month
            };

            foreach (var employee in employees)
            {
                // Load default earnings or fallback to BaseSalary as Basic
                var defaultEarnings = await _repository.GetDefaultEarningsByEmployeeAsync(employee.Id);
                var earningLineInputs = new List<EarningLineInput>();

                if (defaultEarnings.Any())
                {
                    foreach (var de in defaultEarnings)
                    {
                        var et = earningTypeLookup.GetValueOrDefault(de.EarningTypeId);
                        earningLineInputs.Add(new EarningLineInput
                        {
                            EarningTypeId = de.EarningTypeId,
                            EarningTypeCode = et?.Code ?? "Basic",
                            Description = de.Description,
                            Amount = de.Amount,
                            OvertimeMultiplier = de.OvertimeMultiplier,
                            OvertimeHours = de.OvertimeHours
                        });
                    }
                }
                else
                {
                    // Fallback: BaseSalary as Basic earning
                    var basicType = earningTypes.FirstOrDefault(e => e.Code == "Basic");
                    earningLineInputs.Add(new EarningLineInput
                    {
                        EarningTypeId = basicType?.Id ?? 1,
                        EarningTypeCode = "Basic",
                        Description = "Basic Salary",
                        Amount = employee.BaseSalary,
                        OvertimeMultiplier = null,
                        OvertimeHours = null
                    });
                }

                // Run calculation engine
                var calcInput = new PayslipCalculationInput
                {
                    Employee = employee,
                    EarningLines = earningLineInputs,
                    ApplicableDeductions = deductionsWithRates,
                    PeriodDate = periodDate
                };

                var calcResult = await _orchestrator.CalculateWithPayeAsync(calcInput, employee.IsPayeApplicable);

                if (!calcResult.IsValid)
                {
                    preview.Errors.Add(new BatchValidationError
                    {
                        EmployeeId = employee.Id,
                        EmployeeName = employee.Name,
                        Error = calcResult.ValidationError ?? "Calculation failed."
                    });
                    continue;
                }

                var deptName = employee.DepartmentId.HasValue && deptLookup.ContainsKey(employee.DepartmentId.Value)
                    ? deptLookup[employee.DepartmentId.Value] : null;

                var earningLineDtos = calcResult.EarningLines.Select(el =>
                {
                    var et = earningTypeLookup.GetValueOrDefault(el.EarningTypeId);
                    return new EarningLineDto
                    {
                        EarningTypeId = el.EarningTypeId,
                        EarningTypeName = et?.Name ?? "Unknown",
                        EarningTypeCode = et?.Code ?? "Unknown",
                        Description = el.Description,
                        Amount = el.Amount,
                        OvertimeMultiplier = el.OvertimeMultiplier,
                        OvertimeHours = el.OvertimeHours
                    };
                }).ToList();

                var deductionLineDtos = calcResult.DeductionLines.Select(dl =>
                {
                    var dt = deductionsWithRates.FirstOrDefault(d => d.Id == dl.DeductionTypeId);
                    return new DeductionLineDto
                    {
                        DeductionTypeName = dt?.Name ?? "Unknown",
                        BaseAmount = dl.BaseAmount,
                        Rate = dl.Rate,
                        CalculatedAmount = dl.CalculatedAmount
                    };
                }).ToList();

                preview.Payslips.Add(new PayslipPreviewDto
                {
                    EmployeeId = employee.Id,
                    EmployeeName = employee.Name,
                    DepartmentName = deptName,
                    TotalEarnings = calcResult.TotalEarnings,
                    TotalEmployeeDeductions = calcResult.TotalEmployeeDeductions,
                    NetSalary = calcResult.NetSalary,
                    TotalEmployerContributions = calcResult.TotalEmployerContributions,
                    EarningLines = earningLineDtos,
                    DeductionLines = deductionLineDtos
                });
            }

            preview.TotalEmployeesProcessed = preview.Payslips.Count;
            preview.TotalEmployeesExcluded = preview.Errors.Count;
            preview.TotalPayrollCost = preview.Payslips.Sum(p => p.NetSalary);
            preview.TotalEmployerContributions = preview.Payslips.Sum(p => p.TotalEmployerContributions);

            return preview;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ConfirmBatchGenerationAsync(int periodId, int businessId)
    {
        try
        {
            var period = await _repository.GetPeriodByIdAsync(periodId, businessId);
            if (period == null)
                return ServiceResult.Fail("Period not found.");

            if (period.PayslipStatusTypeId != 1) // Must be Draft
                return ServiceResult.Fail("Batch can only be confirmed for Draft periods.");

            // Re-generate the preview to get the final data
            var preview = await GeneratePayslipsPreviewAsync(periodId, businessId);

            if (!preview.Payslips.Any())
                return ServiceResult.Fail("No valid payslips to generate.");

            // Load earning types for code lookup
            var earningTypes = await _repository.GetAllEarningTypesAsync();
            var earningTypeLookup = earningTypes.ToDictionary(e => e.Id, e => e);

            // Load business deductions with rates for re-calculation
            var deductionTypes = await _repository.GetActiveDeductionsWithRatesAsync(businessId);
            var periodDate = new DateTime(period.Year, period.Month, 1);

            foreach (var payslipPreview in preview.Payslips)
            {
                // Insert the payslip
                var payslip = new Payslip
                {
                    EmployeeId = payslipPreview.EmployeeId,
                    PayslipPeriodId = periodId,
                    TotalEarnings = payslipPreview.TotalEarnings,
                    TotalEmployeeDeductions = payslipPreview.TotalEmployeeDeductions,
                    NetSalary = payslipPreview.NetSalary,
                    TotalEmployerContributions = payslipPreview.TotalEmployerContributions,
                    ManagerNotes = null,
                    PayslipStatusTypeId = 2 // Preview
                };

                var payslipId = await _repository.InsertPayslipAsync(payslip);

                // Insert earning lines
                foreach (var el in payslipPreview.EarningLines)
                {
                    var earningType = earningTypes.FirstOrDefault(e => e.Name == el.EarningTypeName);
                    await _repository.InsertEarningLineAsync(new PayslipEarningLine
                    {
                        PayslipId = payslipId,
                        EarningTypeId = earningType?.Id ?? 1,
                        Description = el.Description,
                        Amount = el.Amount,
                        OvertimeMultiplier = el.OvertimeMultiplier,
                        OvertimeHours = el.OvertimeHours
                    });
                }

                // Insert deduction lines
                foreach (var dl in payslipPreview.DeductionLines)
                {
                    // Find the deduction type by name to get the ID
                    var dedType = deductionTypes.FirstOrDefault(d => d.Name == dl.DeductionTypeName);
                    if (dedType == null) continue;

                    // For PAYE lines (IsPercentage = false, Code = "PAYE"), DeductionRateHistoryId is null
                    int? deductionRateHistoryId = null;

                    if (dedType.Code != "PAYE")
                    {
                        // Find effective rate to get DeductionRateHistoryId
                        var rates = await _repository.GetRateHistoryAsync(dedType.Id);
                        var effectiveRate = rates.FirstOrDefault(r =>
                            r.EffectiveFromUtc <= periodDate &&
                            (r.EffectiveToUtc == null || r.EffectiveToUtc > periodDate));

                        if (effectiveRate == null) continue;
                        deductionRateHistoryId = effectiveRate.Id;
                    }

                    await _repository.InsertDeductionLineAsync(new PayslipDeductionLine
                    {
                        PayslipId = payslipId,
                        DeductionTypeId = dedType.Id,
                        BaseAmount = dl.BaseAmount,
                        Rate = dl.Rate,
                        CalculatedAmount = dl.CalculatedAmount,
                        DeductionCategoryTypeId = dedType.DeductionCategoryTypeId,
                        DeductionRateHistoryId = deductionRateHistoryId
                    });
                }
            }

            // Update period status to Preview
            await _repository.UpdatePeriodStatusAsync(periodId, 2, period.PayslipStatusTypeId, null); // 2 = Preview
            await _repository.UpdateAllPayslipStatusesInPeriodAsync(periodId, 2);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Payslip Detail

    public async Task<PayslipDetailDto?> GetPayslipDetailAsync(int id, int businessId)
    {
        try
        {
            var payslip = await _repository.GetPayslipDetailAsync(id, businessId);
            if (payslip == null) return null;

            var employee = await _repository.GetEmployeeByIdAsync(payslip.EmployeeId, businessId);
            var period = await _repository.GetPeriodByIdAsync(payslip.PayslipPeriodId, businessId);

            var earningLines = await _repository.GetEarningLinesByPayslipAsync(id);
            var deductionLines = await _repository.GetDeductionLinesByPayslipAsync(id);

            var earningTypes = await _repository.GetAllEarningTypesAsync();
            var earningTypeLookup = earningTypes.ToDictionary(e => e.Id, e => e);

            var deductionTypes = await _repository.GetDeductionTypesByBusinessAsync(businessId);
            var deductionTypeLookup = deductionTypes.ToDictionary(d => d.Id, d => d);

            var departments = await _repository.GetDepartmentsByBusinessAsync(businessId);
            var deptLookup = departments.ToDictionary(d => d.Id, d => d.Name);

            string? deptName = null;
            if (employee?.DepartmentId != null && deptLookup.ContainsKey(employee.DepartmentId.Value))
                deptName = deptLookup[employee.DepartmentId.Value];

            var earningLineDtos = earningLines.Select(el =>
            {
                var et = earningTypeLookup.GetValueOrDefault(el.EarningTypeId);
                return new EarningLineDto
                {
                    Id = el.Id,
                    EarningTypeId = el.EarningTypeId,
                    EarningTypeName = et?.Name ?? "Unknown",
                    EarningTypeCode = et?.Code ?? "Unknown",
                    Description = el.Description,
                    Amount = el.Amount,
                    OvertimeMultiplier = el.OvertimeMultiplier,
                    OvertimeHours = el.OvertimeHours
                };
            }).ToList();

            var employeeDeductions = deductionLines
                .Where(dl => dl.DeductionCategoryTypeId == 1)
                .Select(dl =>
                {
                    var dt = deductionTypeLookup.GetValueOrDefault(dl.DeductionTypeId);
                    return new DeductionLineDto
                    {
                        Id = dl.Id,
                        DeductionTypeName = dt?.Name ?? "Unknown",
                        BaseAmount = dl.BaseAmount,
                        Rate = dl.Rate,
                        CalculatedAmount = dl.CalculatedAmount
                    };
                }).ToList();

            var employerContributions = deductionLines
                .Where(dl => dl.DeductionCategoryTypeId == 2)
                .Select(dl =>
                {
                    var dt = deductionTypeLookup.GetValueOrDefault(dl.DeductionTypeId);
                    return new DeductionLineDto
                    {
                        Id = dl.Id,
                        DeductionTypeName = dt?.Name ?? "Unknown",
                        BaseAmount = dl.BaseAmount,
                        Rate = dl.Rate,
                        CalculatedAmount = dl.CalculatedAmount
                    };
                }).ToList();

            return new PayslipDetailDto
            {
                Id = payslip.Id,
                EmployeeName = employee?.Name ?? "Unknown",
                EmployeePosition = employee?.Position,
                SocialInsuranceNumber = employee?.SocialInsuranceNumber,
                IdNumber = employee?.IdNumber,
                DepartmentName = deptName,
                EmployeeEmail = employee?.Email,
                Year = period?.Year ?? 0,
                Month = period?.Month ?? 0,
                PeriodStatus = (await GetStatusNamesAsync()).GetValueOrDefault(period?.PayslipStatusTypeId ?? 0, "Unknown"),
                TotalEarnings = payslip.TotalEarnings,
                TotalEmployeeDeductions = payslip.TotalEmployeeDeductions,
                NetSalary = payslip.NetSalary,
                TotalEmployerContributions = payslip.TotalEmployerContributions,
                ManagerNotes = payslip.ManagerNotes,
                EarningLines = earningLineDtos,
                EmployeeDeductions = employeeDeductions,
                EmployerContributions = employerContributions
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> SaveEarningLinesAsync(int businessId, SaveEarningLinesRequest request)
    {
        try
        {
            if (request.Lines == null || !request.Lines.Any())
                return ServiceResult.Fail("At least one earning line is required.");

            // Validate payslip exists and belongs to business
            var payslip = await _repository.GetPayslipDetailAsync(request.PayslipId, businessId);
            if (payslip == null)
                return ServiceResult.Fail("Payslip not found.");

            // Check period is not finalised
            var period = await _repository.GetPeriodByIdAsync(payslip.PayslipPeriodId, businessId);
            if (period == null)
                return ServiceResult.Fail("Period not found.");

            if (!_periodStatusService.IsEditableStatus(period.PayslipStatusTypeId))
                return ServiceResult.Fail("Payslips in a finalised period cannot be modified. Unlock the period first.");

            // Get employee for calculation
            var employee = await _repository.GetEmployeeByIdAsync(payslip.EmployeeId, businessId);
            if (employee == null)
                return ServiceResult.Fail("Employee not found.");

            // Load earning types for code resolution
            var earningTypes = await _repository.GetAllEarningTypesAsync();
            var earningTypeLookup = earningTypes.ToDictionary(e => e.Id, e => e);

            // Resolve earning type codes for input lines
            foreach (var line in request.Lines)
            {
                var et = earningTypeLookup.GetValueOrDefault(line.EarningTypeId);
                if (et != null) line.EarningTypeCode = et.Code;
            }

            // Load deductions with rates for recalculation
            var deductionTypes = await _repository.GetActiveDeductionsWithRatesAsync(businessId);
            var periodDate = new DateTime(period.Year, period.Month, 1);
            var deductionsWithRates = new List<DeductionTypeWithHistory>();
            foreach (var dt in deductionTypes)
            {
                var rates = await _repository.GetRateHistoryAsync(dt.Id);
                deductionsWithRates.Add(new DeductionTypeWithHistory
                {
                    Id = dt.Id,
                    Name = dt.Name,
                    Code = dt.Code,
                    IsPercentage = dt.IsPercentage,
                    DeductionCategoryTypeId = dt.DeductionCategoryTypeId,
                    IsPayeDeductible = dt.IsPayeDeductible,
                    RateHistories = rates
                });
            }

            // Run calculation engine with new earning lines (orchestrator adds PAYE if applicable)
            var calcInput = new PayslipCalculationInput
            {
                Employee = employee,
                EarningLines = request.Lines,
                ApplicableDeductions = deductionsWithRates,
                PeriodDate = periodDate
            };

            var calcResult = await _orchestrator.CalculateWithPayeAsync(calcInput, employee.IsPayeApplicable);
            if (!calcResult.IsValid)
                return ServiceResult.Fail(calcResult.ValidationError ?? "Calculation failed.");

            // Capture old earning lines before modification (for audit tracking)
            var oldEarningLines = await _repository.GetEarningLinesByPayslipAsync(request.PayslipId);

            // Delete existing earning lines
            await _repository.DeleteEarningLinesByPayslipAsync(request.PayslipId);

            // Insert new earning lines
            foreach (var el in calcResult.EarningLines)
            {
                await _repository.InsertEarningLineAsync(new PayslipEarningLine
                {
                    PayslipId = request.PayslipId,
                    EarningTypeId = el.EarningTypeId,
                    Description = el.Description,
                    Amount = el.Amount,
                    OvertimeMultiplier = el.OvertimeMultiplier,
                    OvertimeHours = el.OvertimeHours
                });
            }

            // Delete existing deduction lines
            await _repository.DeleteDeductionLinesByPayslipAsync(request.PayslipId);

            // Insert new deduction lines
            foreach (var dl in calcResult.DeductionLines)
            {
                await _repository.InsertDeductionLineAsync(new PayslipDeductionLine
                {
                    PayslipId = request.PayslipId,
                    DeductionTypeId = dl.DeductionTypeId,
                    BaseAmount = dl.BaseAmount,
                    Rate = dl.Rate,
                    CalculatedAmount = dl.CalculatedAmount,
                    DeductionCategoryTypeId = dl.DeductionCategoryTypeId,
                    DeductionRateHistoryId = dl.DeductionRateHistoryId
                });
            }

            // Update payslip totals
            payslip.TotalEarnings = calcResult.TotalEarnings;
            payslip.TotalEmployeeDeductions = calcResult.TotalEmployeeDeductions;
            payslip.NetSalary = calcResult.NetSalary;
            payslip.TotalEmployerContributions = calcResult.TotalEmployerContributions;
            await _repository.UpdatePayslipTotalsAsync(payslip);

            // Audit tracking: only when period is Unlocked
            if (period.PayslipStatusTypeId == 4) // Unlocked
            {
                var newEarningLines = await _repository.GetEarningLinesByPayslipAsync(request.PayslipId);
                await _auditService.RecordEarningLineChangesAsync(
                    request.PayslipId,
                    "system", // userId will be passed from controller in a future refactor
                    oldEarningLines,
                    newEarningLines,
                    earningTypes);
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> SaveManagerNotesAsync(int businessId, SaveManagerNotesRequest request)
    {
        try
        {
            // Validate length
            if (request.Notes != null && request.Notes.Length > 2000)
                return ServiceResult.Fail("Manager notes cannot exceed 2000 characters.");

            // Validate payslip exists and belongs to business
            var payslip = await _repository.GetPayslipDetailAsync(request.PayslipId, businessId);
            if (payslip == null)
                return ServiceResult.Fail("Payslip not found.");

            // Check period is not finalised
            var period = await _repository.GetPeriodByIdAsync(payslip.PayslipPeriodId, businessId);
            if (period == null)
                return ServiceResult.Fail("Period not found.");

            if (!_periodStatusService.IsEditableStatus(period.PayslipStatusTypeId))
                return ServiceResult.Fail("Payslips in a finalised period cannot be modified. Unlock the period first.");

            // Capture old notes for audit tracking
            var oldNotes = payslip.ManagerNotes;

            await _repository.UpdateManagerNotesAsync(request.PayslipId, request.Notes);

            // Audit tracking: only when period is Unlocked
            if (period.PayslipStatusTypeId == 4) // Unlocked
            {
                await _auditService.RecordManagerNotesChangeAsync(
                    request.PayslipId,
                    "system", // userId will be passed from controller in a future refactor
                    oldNotes,
                    request.Notes);
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region PDF & Email (Stubs)

    public async Task<byte[]> GeneratePayslipPdfAsync(int payslipId, int businessId, bool includeSignature)
    {
        try
        {
            // Load payslip detail
            var payslip = await GetPayslipDetailAsync(payslipId, businessId);
            if (payslip == null)
                return Array.Empty<byte>();

            // Load business info
            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var profile = await _businessService.GetBusinessProfileAsync(businessId);
            var businessAddress = profile != null
                ? $"{profile.AddressLine1 ?? ""}, {profile.City ?? ""}, {profile.PostalCode ?? ""}, {profile.Country ?? ""}".Trim(',', ' ')
                : "";

            // Render HTML → PDF
            var html = await _renderer.RenderPayslipHtmlAsync(
                payslip, business?.Name ?? "Business", businessAddress, includeSignature);
            var pdfBytes = await _pdfService.GeneratePdfAsync(html);

            return pdfBytes;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> SendPayslipEmailAsync(int payslipId, int businessId, string userId, bool includeSignature)
    {
        try
        {
            // STUB: Email sending deferred to Email service task (Task 13)
            await Task.CompletedTask;
            return ServiceResult.Fail("PDF/Email service not yet implemented.");
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> SendAllPayslipEmailsAsync(int periodId, int businessId, string userId, bool includeSignature)
    {
        try
        {
            // STUB: Batch email sending deferred to Email service task (Task 13)
            await Task.CompletedTask;
            return ServiceResult.Fail("PDF/Email service not yet implemented.");
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Phase B: Unlock & Re-finalise

    public async Task<ServiceResult> UnlockPeriodAsync(int periodId, int businessId, string userId, string userRole)
    {
        return await _periodStatusService.UnlockPeriodAsync(periodId, businessId, userId, userRole);
    }

    public async Task<ServiceResult> RefinalisePeriodAsync(int periodId, int businessId, string userId, string userRole)
    {
        return await _periodStatusService.RefinalisePeriodAsync(periodId, businessId, userId, userRole);
    }

    #endregion

    #region Phase B: Audit History

    public async Task<List<PayslipAuditLogDto>> GetPayslipAuditHistoryAsync(int payslipId, int businessId)
    {
        return await _auditService.GetAuditHistoryAsync(payslipId, businessId);
    }

    public async Task<List<PeriodAuditGroupDto>> GetPeriodAuditSummaryAsync(int periodId, int businessId)
    {
        return await _auditService.GetPeriodAuditSummaryAsync(periodId, businessId);
    }

    #endregion

    #region Phase D: PAYE Toggle & Contribution Report

    public async Task<ServiceResult> UpdateEmployeePayeStatusAsync(int businessId, int employeeId, bool isPayeApplicable)
    {
        try
        {
            // Validate employee belongs to business
            var employee = await _repository.GetEmployeeByIdAsync(employeeId, businessId);
            if (employee == null)
                return ServiceResult.Fail("Employee not found.");

            await _repository.UpdateEmployeePayeStatusAsync(employeeId, businessId, isPayeApplicable);

            // Check if projected income is below threshold and return warning flag
            if (isPayeApplicable && employee.BaseSalary * 12 <= 19500)
            {
                return new ServiceResult
                {
                    Success = true,
                    Message = $"Warning: This employee's projected annual income (€{employee.BaseSalary * 12:N2}) does not exceed the PAYE threshold (€19,500). PAYE calculation will result in €0."
                };
            }

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ContributionReportDto?> GetContributionReportAsync(int periodId, int businessId)
    {
        try
        {
            var period = await _repository.GetPeriodByIdAsync(periodId, businessId);
            if (period == null) return null;

            var contributions = await _repository.GetEmployerContributionsForPeriodAsync(periodId, businessId);

            // Build type summaries
            var typeSummaries = contributions
                .GroupBy(c => new { c.DeductionTypeName, c.DeductionTypeCode })
                .Select(g => new ContributionTypeSummary
                {
                    DeductionTypeName = g.Key.DeductionTypeName,
                    Code = g.Key.DeductionTypeCode,
                    Total = g.Sum(c => c.CalculatedAmount)
                })
                .ToList();

            // Build per-employee details
            var employeeDetails = contributions
                .GroupBy(c => new { c.EmployeeId, c.EmployeeName })
                .Select(g => new EmployeeContributionDetail
                {
                    EmployeeId = g.Key.EmployeeId,
                    EmployeeName = g.Key.EmployeeName,
                    Contributions = g.Select(c => new ContributionLineItem
                    {
                        DeductionTypeName = c.DeductionTypeName,
                        Code = c.DeductionTypeCode,
                        Amount = c.CalculatedAmount
                    }).ToList(),
                    EmployeeTotal = g.Sum(c => c.CalculatedAmount)
                })
                .ToList();

            // Check for compliance filing link
            var filings = await _repository.GetComplianceFilingsByPeriodAsync(periodId);
            ComplianceFilingLinkDto? complianceFiling = null;

            if (filings.Any())
            {
                var latestFiling = filings.First(); // Already ordered DESC
                // Load the actual BusinessApplication for status
                complianceFiling = new ComplianceFilingLinkDto
                {
                    FilingId = latestFiling.ComplianceFilingId,
                    Status = "Linked",
                    DueDate = latestFiling.UpdatedAtUtc,
                    EstimatedAmount = latestFiling.ContributionTotal
                };
            }

            var monthName = System.Globalization.CultureInfo.InvariantCulture
                .DateTimeFormat.GetMonthName(period.Month);

            return new ContributionReportDto
            {
                PeriodId = periodId,
                Year = period.Year,
                Month = period.Month,
                MonthName = monthName,
                TypeSummaries = typeSummaries,
                EmployeeDetails = employeeDetails,
                GrandTotal = contributions.Sum(c => c.CalculatedAmount),
                ComplianceFiling = complianceFiling
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<PayslipPeriodComplianceFilingDto>> GetComplianceFilingHistoryAsync(int periodId, int businessId)
    {
        try
        {
            // Validate period belongs to business
            var period = await _repository.GetPeriodByIdAsync(periodId, businessId);
            if (period == null) return new List<PayslipPeriodComplianceFilingDto>();

            var filings = await _repository.GetComplianceFilingsByPeriodAsync(periodId);

            return filings.Select(f => new PayslipPeriodComplianceFilingDto
            {
                Id = f.Id,
                PayslipPeriodId = f.PayslipPeriodId,
                ComplianceFilingId = f.ComplianceFilingId,
                ContributionTotal = f.ContributionTotal,
                UpdatedAtUtc = f.UpdatedAtUtc,
                UpdatedByUserName = f.UpdatedByUserId, // TODO: Join to AspNetUsers for display name
                CreatedAtUtc = f.CreatedAtUtc
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region Earnings Override & Salary Register

    public async Task<RecalculationResult> RecalculateEmployeeAsync(int employeeId, int periodId, int businessId, List<EarningLineOverride> overriddenLines)
    {
        try
        {
            // 1. Fetch the employee record
            var employee = await _repository.GetEmployeeByIdAsync(employeeId, businessId);
            if (employee == null)
            {
                return new RecalculationResult
                {
                    Success = false,
                    Error = "Employee not found."
                };
            }

            // 2. Fetch the period record to get the period date
            var period = await _repository.GetPeriodByIdAsync(periodId, businessId);
            if (period == null)
            {
                return new RecalculationResult
                {
                    Success = false,
                    Error = "Period not found."
                };
            }

            var periodDate = new DateTime(period.Year, period.Month, 1);

            // 3. Load applicable deductions with rates (same logic as GeneratePayslipsPreviewAsync)
            var deductionTypes = await _repository.GetActiveDeductionsWithRatesAsync(businessId);
            var deductionsWithRates = new List<DeductionTypeWithHistory>();
            foreach (var dt in deductionTypes)
            {
                var rates = await _repository.GetRateHistoryAsync(dt.Id);
                deductionsWithRates.Add(new DeductionTypeWithHistory
                {
                    Id = dt.Id,
                    Name = dt.Name,
                    Code = dt.Code,
                    IsPercentage = dt.IsPercentage,
                    DeductionCategoryTypeId = dt.DeductionCategoryTypeId,
                    IsPayeDeductible = dt.IsPayeDeductible,
                    RateHistories = rates
                });
            }

            // 4. Load earning types for code lookup
            var earningTypes = await _repository.GetAllEarningTypesAsync();
            var earningTypeLookup = earningTypes.ToDictionary(e => e.Id, e => e);

            // 5. Map EarningLineOverride list to List<EarningLineInput>
            var earningLineInputs = overriddenLines.Select(ol =>
            {
                var et = earningTypeLookup.GetValueOrDefault(ol.EarningTypeId);
                return new EarningLineInput
                {
                    EarningTypeId = ol.EarningTypeId,
                    EarningTypeCode = et?.Code ?? "Basic",
                    Description = ol.Description,
                    Amount = ol.Amount,
                    OvertimeMultiplier = ol.OvertimeMultiplier,
                    OvertimeHours = ol.OvertimeHours
                };
            }).ToList();

            // 6. Build PayslipCalculationInput
            var calcInput = new PayslipCalculationInput
            {
                Employee = employee,
                EarningLines = earningLineInputs,
                ApplicableDeductions = deductionsWithRates,
                PeriodDate = periodDate
            };

            // 7. Call orchestrator
            var calcResult = await _orchestrator.CalculateWithPayeAsync(calcInput, employee.IsPayeApplicable);

            if (!calcResult.IsValid)
            {
                return new RecalculationResult
                {
                    Success = false,
                    Error = calcResult.ValidationError ?? "Calculation failed."
                };
            }

            // 8. Return RecalculationResult
            return new RecalculationResult
            {
                Success = true,
                TotalEarnings = calcResult.TotalEarnings,
                TotalEmployeeDeductions = calcResult.TotalEmployeeDeductions,
                NetSalary = calcResult.NetSalary,
                TotalEmployerContributions = calcResult.TotalEmployerContributions
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> ConfirmBatchGenerationWithOverridesAsync(int periodId, int businessId, List<EmployeeEarningsOverride> overrides)
    {
        try
        {
            var period = await _repository.GetPeriodByIdAsync(periodId, businessId);
            if (period == null)
                return ServiceResult.Fail("Period not found.");

            if (period.PayslipStatusTypeId != 1) // Must be Draft
                return ServiceResult.Fail("Batch can only be confirmed for Draft periods.");

            var periodDate = new DateTime(period.Year, period.Month, 1);
            var employees = await _repository.GetActiveEmployeesForPeriodAsync(businessId, periodDate);

            if (!employees.Any())
                return ServiceResult.Fail("No active employees found for this period.");

            // Load earning types for code lookup
            var earningTypes = await _repository.GetAllEarningTypesAsync();
            var earningTypeLookup = earningTypes.ToDictionary(e => e.Id, e => e);

            // Load business deductions with rates
            var deductionTypes = await _repository.GetActiveDeductionsWithRatesAsync(businessId);
            var deductionsWithRates = new List<DeductionTypeWithHistory>();
            foreach (var dt in deductionTypes)
            {
                var rates = await _repository.GetRateHistoryAsync(dt.Id);
                deductionsWithRates.Add(new DeductionTypeWithHistory
                {
                    Id = dt.Id,
                    Name = dt.Name,
                    Code = dt.Code,
                    IsPercentage = dt.IsPercentage,
                    DeductionCategoryTypeId = dt.DeductionCategoryTypeId,
                    IsPayeDeductible = dt.IsPayeDeductible,
                    RateHistories = rates
                });
            }

            // Build override lookup by EmployeeId
            var overrideLookup = overrides.ToDictionary(o => o.EmployeeId, o => o.EarningLines);

            foreach (var employee in employees)
            {
                var earningLineInputs = new List<EarningLineInput>();

                if (overrideLookup.TryGetValue(employee.Id, out var overriddenLines))
                {
                    // Use overridden earning lines
                    earningLineInputs = overriddenLines.Select(ol =>
                    {
                        var et = earningTypeLookup.GetValueOrDefault(ol.EarningTypeId);
                        return new EarningLineInput
                        {
                            EarningTypeId = ol.EarningTypeId,
                            EarningTypeCode = et?.Code ?? "Basic",
                            Description = ol.Description,
                            Amount = ol.Amount,
                            OvertimeMultiplier = ol.OvertimeMultiplier,
                            OvertimeHours = ol.OvertimeHours
                        };
                    }).ToList();
                }
                else
                {
                    // Use default earnings or BaseSalary fallback (same as GeneratePayslipsPreviewAsync)
                    var defaultEarnings = await _repository.GetDefaultEarningsByEmployeeAsync(employee.Id);

                    if (defaultEarnings.Any())
                    {
                        foreach (var de in defaultEarnings)
                        {
                            var et = earningTypeLookup.GetValueOrDefault(de.EarningTypeId);
                            earningLineInputs.Add(new EarningLineInput
                            {
                                EarningTypeId = de.EarningTypeId,
                                EarningTypeCode = et?.Code ?? "Basic",
                                Description = de.Description,
                                Amount = de.Amount,
                                OvertimeMultiplier = de.OvertimeMultiplier,
                                OvertimeHours = de.OvertimeHours
                            });
                        }
                    }
                    else
                    {
                        // Fallback: BaseSalary as Basic earning
                        var basicType = earningTypes.FirstOrDefault(e => e.Code == "Basic");
                        earningLineInputs.Add(new EarningLineInput
                        {
                            EarningTypeId = basicType?.Id ?? 1,
                            EarningTypeCode = "Basic",
                            Description = "Basic Salary",
                            Amount = employee.BaseSalary,
                            OvertimeMultiplier = null,
                            OvertimeHours = null
                        });
                    }
                }

                // Run calculation engine
                var calcInput = new PayslipCalculationInput
                {
                    Employee = employee,
                    EarningLines = earningLineInputs,
                    ApplicableDeductions = deductionsWithRates,
                    PeriodDate = periodDate
                };

                var calcResult = await _orchestrator.CalculateWithPayeAsync(calcInput, employee.IsPayeApplicable);

                if (!calcResult.IsValid)
                    continue; // Skip employees with calculation errors

                // Create payslip record
                var payslip = new Payslip
                {
                    EmployeeId = employee.Id,
                    PayslipPeriodId = periodId,
                    TotalEarnings = calcResult.TotalEarnings,
                    TotalEmployeeDeductions = calcResult.TotalEmployeeDeductions,
                    NetSalary = calcResult.NetSalary,
                    TotalEmployerContributions = calcResult.TotalEmployerContributions,
                    ManagerNotes = null,
                    PayslipStatusTypeId = 2 // Preview
                };

                var payslipId = await _repository.InsertPayslipAsync(payslip);

                // Insert earning lines
                foreach (var el in calcResult.EarningLines)
                {
                    await _repository.InsertEarningLineAsync(new PayslipEarningLine
                    {
                        PayslipId = payslipId,
                        EarningTypeId = el.EarningTypeId,
                        Description = el.Description,
                        Amount = el.Amount,
                        OvertimeMultiplier = el.OvertimeMultiplier,
                        OvertimeHours = el.OvertimeHours
                    });
                }

                // Insert deduction lines
                foreach (var dl in calcResult.DeductionLines)
                {
                    var dedType = deductionsWithRates.FirstOrDefault(d => d.Id == dl.DeductionTypeId);
                    if (dedType == null) continue;

                    int? deductionRateHistoryId = null;

                    if (dedType.Code != "PAYE")
                    {
                        var rates = await _repository.GetRateHistoryAsync(dedType.Id);
                        var effectiveRate = rates.FirstOrDefault(r =>
                            r.EffectiveFromUtc <= periodDate &&
                            (r.EffectiveToUtc == null || r.EffectiveToUtc > periodDate));

                        if (effectiveRate == null) continue;
                        deductionRateHistoryId = effectiveRate.Id;
                    }

                    await _repository.InsertDeductionLineAsync(new PayslipDeductionLine
                    {
                        PayslipId = payslipId,
                        DeductionTypeId = dedType.Id,
                        BaseAmount = dl.BaseAmount,
                        Rate = dl.Rate,
                        CalculatedAmount = dl.CalculatedAmount,
                        DeductionCategoryTypeId = dedType.DeductionCategoryTypeId,
                        DeductionRateHistoryId = deductionRateHistoryId
                    });
                }
            }

            // Update period status to Preview
            await _repository.UpdatePeriodStatusAsync(periodId, 2, period.PayslipStatusTypeId, null);
            await _repository.UpdateAllPayslipStatusesInPeriodAsync(periodId, 2);

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<SalaryRegisterViewModel> GetSalaryRegisterAsync(int businessId, int? departmentId, bool? isActive)
    {
        try
        {
            // Default isActive to true when null (initial page load shows active employees)
            var effectiveIsActive = isActive ?? true;

            // Get all employees for the business
            var connection = _portalDbContext.Database.GetDbConnection();

            var allEmployees = new List<SalaryRegisterRow>();
            var departments = new List<DepartmentDto>();

            try
            {
                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                // Get departments for filter dropdown
                using (var deptCommand = connection.CreateCommand())
                {
                    deptCommand.CommandText = @"
                        SELECT [payroll].[Department].[Id],
                               [payroll].[Department].[Name],
                               [payroll].[Department].[IsActive]
                        FROM [payroll].[Department]
                        WHERE [payroll].[Department].[BusinessId] = @BusinessId
                          AND [payroll].[Department].[IsActive] = 1
                        ORDER BY [payroll].[Department].[Name]";

                    var transaction = _portalDbContext.Database.CurrentTransaction;
                    if (transaction != null)
                        deptCommand.Transaction = transaction.GetDbTransaction();

                    deptCommand.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@BusinessId", businessId));

                    using var deptReader = await deptCommand.ExecuteReaderAsync();
                    while (await deptReader.ReadAsync())
                    {
                        departments.Add(new DepartmentDto
                        {
                            Id = deptReader.GetInt32(0),
                            Name = deptReader.GetString(1),
                            IsActive = deptReader.GetBoolean(2)
                        });
                    }
                }

                // Get employees with optional filters
                using (var empCommand = connection.CreateCommand())
                {
                    var sql = @"
                        SELECT [payroll].[Employee].[Id],
                               [payroll].[Employee].[Name],
                               [payroll].[Department].[Name],
                               [payroll].[Employee].[SalaryTypeId],
                               [payroll].[Employee].[BaseSalary],
                               [payroll].[Employee].[HourlyRate],
                               [payroll].[Employee].[IsActive]
                        FROM [payroll].[Employee]
                        LEFT JOIN [payroll].[Department]
                            ON [payroll].[Employee].[DepartmentId] = [payroll].[Department].[Id]
                        WHERE [payroll].[Employee].[BusinessId] = @BusinessId
                          AND [payroll].[Employee].[IsActive] = @IsActive";

                    if (departmentId.HasValue)
                    {
                        sql += " AND [payroll].[Employee].[DepartmentId] = @DepartmentId";
                    }

                    sql += " ORDER BY [payroll].[Employee].[Name] ASC";

                    empCommand.CommandText = sql;

                    var transaction = _portalDbContext.Database.CurrentTransaction;
                    if (transaction != null)
                        empCommand.Transaction = transaction.GetDbTransaction();

                    empCommand.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@BusinessId", businessId));
                    empCommand.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@IsActive", effectiveIsActive));

                    if (departmentId.HasValue)
                    {
                        empCommand.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@DepartmentId", departmentId.Value));
                    }

                    using var empReader = await empCommand.ExecuteReaderAsync();
                    while (await empReader.ReadAsync())
                    {
                        allEmployees.Add(new SalaryRegisterRow
                        {
                            EmployeeId = empReader.GetInt32(0),
                            EmployeeName = empReader.GetString(1),
                            DepartmentName = empReader.IsDBNull(2) ? null : empReader.GetString(2),
                            SalaryType = empReader.GetByte(3) == 1 ? "Monthly" : "Hourly",
                            BaseSalary = empReader.GetDecimal(4),
                            HourlyRate = empReader.IsDBNull(5) ? null : empReader.GetDecimal(5),
                            IsActive = empReader.GetBoolean(6)
                        });
                    }
                }
            }
            finally
            {
                if (connection.State == System.Data.ConnectionState.Open && _portalDbContext.Database.CurrentTransaction == null)
                    await connection.CloseAsync();
            }

            // Compute totals
            var totalEmployees = allEmployees.Count;
            var totalMonthlyPayroll = allEmployees
                .Where(e => e.SalaryType == "Monthly" && e.IsActive)
                .Sum(e => e.BaseSalary);

            return new SalaryRegisterViewModel
            {
                Employees = allEmployees,
                Departments = departments,
                SelectedDepartmentId = departmentId,
                SelectedIsActive = isActive,
                TotalEmployees = totalEmployees,
                TotalMonthlyPayroll = totalMonthlyPayroll
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<ServiceResult> UpdateBaseSalaryAsync(int employeeId, int businessId, decimal newSalary)
    {
        try
        {
            // 1. Validate newSalary > 0
            if (newSalary <= 0)
                return ServiceResult.Fail("Salary must be greater than zero.");

            // 2. Fetch employee to verify it exists and belongs to the business
            var employee = await _repository.GetEmployeeByIdAsync(employeeId, businessId);
            if (employee == null)
                return ServiceResult.Fail("Employee not found.");

            // 3. Update BaseSalary
            employee.BaseSalary = newSalary;
            await _repository.UpdateEmployeeAsync(employee);

            // 4. Return success
            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    #endregion
}
