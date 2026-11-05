using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Services;
using Portal.Web.Models.Payroll;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Payroll)]
public class PayrollRegisterController : Controller
{
    private readonly IPayrollReportService _reportService;
    private readonly IPayrollService _payrollService;
    private readonly ICurrentTenantService _tenantService;

    public PayrollRegisterController(
        IPayrollReportService reportService,
        IPayrollService payrollService,
        ICurrentTenantService tenantService)
    {
        _reportService = reportService;
        _payrollService = payrollService;
        _tenantService = tenantService;
    }

    #region Page Actions

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            if (businessId <= 0)
                return View("Error");

            var periods = await _payrollService.GetPeriodsAsync(businessId);

            // Resolve the default TO range from the latest finalised period.
            var latestFinalisedPeriodId = await _reportService.GetLatestFinalisedPeriodIdAsync(businessId);
            var hasFinalisedPeriod = latestFinalisedPeriodId.HasValue;

            int? defaultToYear = null;
            int? defaultToMonth = null;
            int? defaultFromYear = null;
            const int defaultFromMonth = 1;

            if (hasFinalisedPeriod)
            {
                var latestPeriod = periods.FirstOrDefault(p => p.Id == latestFinalisedPeriodId.Value);
                if (latestPeriod != null)
                {
                    // TO = latest finalised period's year+month; FROM = January of that same year (R2.2).
                    defaultToYear = latestPeriod.Year;
                    defaultToMonth = latestPeriod.Month;
                    defaultFromYear = latestPeriod.Year;
                }
            }

            var eligibleEmployees = hasFinalisedPeriod
                && defaultFromYear.HasValue && defaultToYear.HasValue && defaultToMonth.HasValue
                ? await _reportService.GetRegisterEligibleEmployeesAsync(
                    defaultFromYear.Value, defaultFromMonth, defaultToYear.Value, defaultToMonth.Value, businessId)
                : new List<RegisterEmployeeOption>();

            var viewModel = new RegisterPageViewModel
            {
                Periods = periods,
                DefaultToYear = defaultToYear,
                DefaultToMonth = defaultToMonth,
                DefaultFromYear = defaultFromYear,
                DefaultFromMonth = defaultFromMonth,
                EligibleEmployees = eligibleEmployees,
                HasFinalisedPeriod = hasFinalisedPeriod
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    #endregion

    #region Ajax Endpoints

    [HttpGet]
    public async Task<IActionResult> AxGetEmployeesForPeriod(int fromYear, int fromMonth, int toYear, int toMonth)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var employees = await _reportService.GetRegisterEligibleEmployeesAsync(
                fromYear, fromMonth, toYear, toMonth, businessId);
            return Json(new { success = true, data = employees });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load employees." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetGenerateRegister(int fromYear, int fromMonth, int toYear, int toMonth, int[] employeeIds)
    {
        try
        {
            // Validate the range order before touching the service (R2.4, R10.5).
            var fromOrdinal = fromYear * 12 + fromMonth;
            var toOrdinal = toYear * 12 + toMonth;
            if (fromOrdinal > toOrdinal)
                return Json(new { success = false, message = "The From month must not be later than the To month." });

            if (employeeIds == null || employeeIds.Length == 0)
                return Json(new { success = false, message = "At least one employee is required." });

            var businessId = _tenantService.CurrentBusinessId;
            var workbookBytes = await _reportService.GenerateRegisterWorkbookAsync(
                fromYear, fromMonth, toYear, toMonth, businessId, employeeIds);

            if (workbookBytes == null || workbookBytes.Length == 0)
                return Json(new { success = false, message = "No finalised payslip data is available for the selected range and employees." });

            return File(
                workbookBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"PayrollRegister_{fromYear}{fromMonth}-{toYear}{toMonth}.xlsx");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong generating the register." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetPreviewRegister(int fromYear, int fromMonth, int toYear, int toMonth, int[] employeeIds)
    {
        try
        {
            // Validate the range order before touching the service (R15.4).
            var fromOrdinal = fromYear * 12 + fromMonth;
            var toOrdinal = toYear * 12 + toMonth;
            if (fromOrdinal > toOrdinal)
                return Json(new { success = false, message = "The From month must not be later than the To month." });

            if (employeeIds == null || employeeIds.Length == 0)
                return Json(new { success = false, message = "At least one employee is required." });

            var businessId = _tenantService.CurrentBusinessId;
            var data = await _reportService.GetRegisterDataAsync(
                fromYear, fromMonth, toYear, toMonth, businessId, employeeIds);

            if (data == null)
                return Json(new { success = false, message = "No finalised payslip data is available for the selected range and employees." });

            // Project the shared aggregates (in returned order, no reordering) into a camelCase JSON
            // payload. No computation is added here — values are read straight off the aggregates so
            // the preview and the download can never drift (R15.12, R15.2).
            var projection = data.Aggregates.Select(a => new
            {
                employeeId = a.EmployeeId,
                employeeName = a.EmployeeName,
                rangePeriods = a.RangePeriods.Select(p => new { periodId = p.Id, year = p.Year, month = p.Month }),
                earnings = a.Earnings.Select(r => new { name = r.Name, amountByPeriodId = r.AmountByPeriodId, total = r.Total, percentage = r.Percentage }),
                employeeDeductions = a.EmployeeDeductions.Select(r => new { name = r.Name, amountByPeriodId = r.AmountByPeriodId, total = r.Total, percentage = r.Percentage }),
                employerContributions = a.EmployerContributions.Select(r => new { name = r.Name, amountByPeriodId = r.AmountByPeriodId, total = r.Total, percentage = r.Percentage }),
                totalEarnings = a.TotalEarnings,
                totalEmployeeDeductions = a.TotalEmployeeDeductions,
                netSalary = a.NetSalary,
                totalEmployerContributions = a.TotalEmployerContributions,
                totalCost = a.TotalCost
            });

            return Json(new { success = true, data = projection });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong loading the preview." });
        }
    }

    #endregion
}
