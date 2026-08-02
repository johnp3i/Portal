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

    public PayrollService(
        PayrollRepository repository,
        IPayslipCalculationEngine calculationEngine)
    {
        _repository = repository;
        _calculationEngine = calculationEngine;
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
                IsActive = employee.IsActive
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

    private static readonly Dictionary<byte, string> PeriodStatusNames = new()
    {
        { 1, "Draft" },
        { 2, "Preview" },
        { 3, "Finalised" }
    };

    public async Task<List<PayslipPeriodDto>> GetPeriodsAsync(int businessId)
    {
        try
        {
            var periods = await _repository.GetPeriodsByBusinessAsync(businessId);
            var dtos = new List<PayslipPeriodDto>();

            foreach (var p in periods)
            {
                var payslips = await _repository.GetPayslipsByPeriodAsync(p.Id);

                dtos.Add(new PayslipPeriodDto
                {
                    Id = p.Id,
                    Year = p.Year,
                    Month = p.Month,
                    Status = PeriodStatusNames.GetValueOrDefault(p.PayslipStatusTypeId, "Unknown"),
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
                Status = PeriodStatusNames.GetValueOrDefault(period.PayslipStatusTypeId, "Unknown"),
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

            if (period.PayslipStatusTypeId != 2) // Must be Preview
                return ServiceResult.Fail("Only periods in Preview status can be finalised.");

            await _repository.UpdatePeriodStatusAsync(id, 3, DateTime.UtcNow); // 3 = Finalised
            return ServiceResult.Ok();
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

                var calcResult = _calculationEngine.Calculate(calcInput);

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

                    // Find effective rate to get DeductionRateHistoryId
                    var rates = await _repository.GetRateHistoryAsync(dedType.Id);
                    var effectiveRate = rates.FirstOrDefault(r =>
                        r.EffectiveFromUtc <= periodDate &&
                        (r.EffectiveToUtc == null || r.EffectiveToUtc > periodDate));

                    if (effectiveRate == null) continue;

                    await _repository.InsertDeductionLineAsync(new PayslipDeductionLine
                    {
                        PayslipId = payslipId,
                        DeductionTypeId = dedType.Id,
                        BaseAmount = dl.BaseAmount,
                        Rate = dl.Rate,
                        CalculatedAmount = dl.CalculatedAmount,
                        DeductionCategoryTypeId = dedType.DeductionCategoryTypeId,
                        DeductionRateHistoryId = effectiveRate.Id
                    });
                }
            }

            // Update period status to Preview
            await _repository.UpdatePeriodStatusAsync(periodId, 2, null); // 2 = Preview

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
                DepartmentName = deptName,
                EmployeeEmail = employee?.Email,
                Year = period?.Year ?? 0,
                Month = period?.Month ?? 0,
                PeriodStatus = PeriodStatusNames.GetValueOrDefault(period?.PayslipStatusTypeId ?? 0, "Unknown"),
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

            if (period.PayslipStatusTypeId == 3) // Finalised
                return ServiceResult.Fail("Cannot modify earning lines on a finalised payslip.");

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
                    RateHistories = rates
                });
            }

            // Run calculation engine with new earning lines
            var calcInput = new PayslipCalculationInput
            {
                Employee = employee,
                EarningLines = request.Lines,
                ApplicableDeductions = deductionsWithRates,
                PeriodDate = periodDate
            };

            var calcResult = _calculationEngine.Calculate(calcInput);
            if (!calcResult.IsValid)
                return ServiceResult.Fail(calcResult.ValidationError ?? "Calculation failed.");

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

            if (period.PayslipStatusTypeId == 3) // Finalised
                return ServiceResult.Fail("Cannot modify notes on a finalised payslip.");

            await _repository.UpdateManagerNotesAsync(request.PayslipId, request.Notes);
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
            // STUB: PDF generation deferred to PDF service task (Task 13)
            // Returns empty byte array until IPayslipPdfService is implemented
            await Task.CompletedTask;
            return Array.Empty<byte>();
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
}
