using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.Infrastructure.Constants;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Services;
using Portal.Web.Security;

namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.Payroll)]
public class PayrollController : Controller
{
    private readonly IPayrollService _payrollService;
    private readonly ICurrentTenantService _tenantService;

    public PayrollController(
        IPayrollService payrollService,
        ICurrentTenantService tenantService)
    {
        _payrollService = payrollService;
        _tenantService = tenantService;
    }

    // === Page Actions ===

    [HttpGet]
    public async Task<IActionResult> Departments()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var departments = await _payrollService.GetDepartmentsAsync(businessId);
            return View(departments);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Employees(string? search, int? departmentId, bool? isActive, int page = 1)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.GetEmployeesAsync(businessId, search, departmentId, isActive, page, 15);

            ViewBag.Departments = await _payrollService.GetDepartmentsAsync(businessId);
            ViewBag.EarningTypes = await _payrollService.GetEarningTypesAsync();
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentDepartmentId = departmentId;
            ViewBag.CurrentIsActive = isActive;

            return View(result);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> EmployeeForm(int? id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            ViewBag.Departments = await _payrollService.GetDepartmentsAsync(businessId);
            ViewBag.EarningTypes = await _payrollService.GetEarningTypesAsync();

            if (id.HasValue)
            {
                var employee = await _payrollService.GetEmployeeByIdAsync(id.Value, businessId);
                if (employee == null)
                    return NotFound();

                ViewBag.DefaultEarnings = await _payrollService.GetDefaultEarningsAsync(id.Value, businessId);
                return View(employee);
            }

            return View();
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Periods()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var periods = await _payrollService.GetPeriodsAsync(businessId);
            return View(periods);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> PeriodDetail(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var detail = await _payrollService.GetPeriodDetailAsync(id, businessId);

            if (detail == null)
                return NotFound();

            var isOwner = User.HasClaim("IsOwner", "true");
            var isSuperAdmin = User.IsInRole("SuperAdmin");

            ViewBag.CanUnlock = isOwner || isSuperAdmin;
            ViewBag.CanSendEmail = isOwner || isSuperAdmin;
            ViewBag.PeriodStatus = detail.Status;
            ViewBag.PeriodId = detail.Id;
            ViewBag.MonthName = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(detail.Month);
            ViewBag.Year = detail.Year;

            return View(detail);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> PayslipDetail(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var detail = await _payrollService.GetPayslipDetailAsync(id, businessId);

            if (detail == null)
                return NotFound();

            var isOwner = User.HasClaim("IsOwner", "true");
            var isSuperAdmin = User.IsInRole("SuperAdmin");
            ViewBag.CanSendEmail = isOwner || isSuperAdmin;

            return View(detail);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> BatchGenerate(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var preview = await _payrollService.GeneratePayslipsPreviewAsync(periodId, businessId);
            ViewBag.PeriodId = periodId;
            return View(preview);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> DeductionConfig()
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var deductionTypes = await _payrollService.GetDeductionTypesForBusinessAsync(businessId);
            return View(deductionTypes);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    // === AJAX Endpoints ===

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateDepartment([FromBody] CreateDepartmentRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.CreateDepartmentAsync(businessId, request);

            if (result.Success)
                return Json(new { success = true, message = "Department created successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateDepartment([FromBody] UpdateDepartmentRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.UpdateDepartmentAsync(businessId, request);

            if (result.Success)
                return Json(new { success = true, message = "Department updated successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostToggleDepartment(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.ToggleDepartmentAsync(id, businessId);

            if (result.Success)
                return Json(new { success = true, message = "Department status updated." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateEmployee([FromBody] CreateEmployeeRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.CreateEmployeeAsync(businessId, request);

            if (result.Success)
                return Json(new { success = true, message = "Employee created successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUpdateEmployee([FromBody] UpdateEmployeeRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.UpdateEmployeeAsync(businessId, request);

            if (result.Success)
                return Json(new { success = true, message = "Employee updated successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostToggleEmployee(int id)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.ToggleEmployeeAsync(id, businessId);

            if (result.Success)
                return Json(new { success = true, message = "Employee status updated." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreatePeriod([FromBody] CreatePeriodRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.CreatePeriodAsync(businessId, request);

            if (result.Success)
                return Json(new { success = true, message = "Period created successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostGeneratePayslips(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.ConfirmBatchGenerationAsync(periodId, businessId);

            if (result.Success)
                return Json(new { success = true, message = "Payslips generated successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostConfirmBatch(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.ConfirmBatchGenerationAsync(periodId, businessId);

            if (result.Success)
                return Json(new { success = true, message = "Batch confirmed successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostFinalisePeriod(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.FinalisePeriodAsync(periodId, businessId);

            if (result.Success)
                return Json(new { success = true, message = "Period finalised successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSaveEarningLines([FromBody] SaveEarningLinesRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.SaveEarningLinesAsync(businessId, request);

            if (result.Success)
                return Json(new { success = true, message = "Earning lines saved successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSaveManagerNotes([FromBody] SaveManagerNotesRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.SaveManagerNotesAsync(businessId, request);

            if (result.Success)
                return Json(new { success = true, message = "Manager notes saved successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSaveDefaultEarnings(int employeeId, [FromBody] List<EmployeeDefaultEarningInput> lines)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.SaveDefaultEarningsAsync(businessId, employeeId, lines);

            if (result.Success)
                return Json(new { success = true, message = "Default earnings saved successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostImportDeductionTemplates([FromBody] int[] templateIds)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.ImportDeductionTemplatesAsync(businessId, templateIds);

            if (result.Success)
                return Json(new { success = true, message = "Deduction templates imported successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostCreateDeductionType([FromBody] CreateDeductionTypeRequest request)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var result = await _payrollService.CreateDeductionTypeAsync(businessId, request);

            if (result.Success)
                return Json(new { success = true, message = "Deduction type created successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostToggleDeductionType(int id)
    {
        try
        {
            var result = await _payrollService.ToggleDeductionTypeAsync(id);

            if (result.Success)
                return Json(new { success = true, message = "Deduction type status updated." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostAddRateHistory([FromBody] AddRateHistoryRequest request)
    {
        try
        {
            var result = await _payrollService.AddRateHistoryAsync(request);

            if (result.Success)
                return Json(new { success = true, message = "Rate history entry added successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetDownloadPayslipPdf(int payslipId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var pdfBytes = await _payrollService.GeneratePayslipPdfAsync(payslipId, businessId, true);

            if (pdfBytes == null || pdfBytes.Length == 0)
                return NotFound();

            return File(pdfBytes, "application/pdf", $"payslip_{payslipId}.pdf");
        }
        catch (Exception ex)
        {
            return NotFound();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSendPayslipEmail(int payslipId, bool includeSignature)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _payrollService.SendPayslipEmailAsync(payslipId, businessId, userId, includeSignature);

            if (result.Success)
                return Json(new { success = true, message = "Payslip email sent successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSendAllPayslipEmails(int periodId, bool includeSignature)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _payrollService.SendAllPayslipEmailsAsync(periodId, businessId, userId, includeSignature);

            if (result.Success)
                return Json(new { success = true, message = "All payslip emails sent successfully." });

            return Json(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetDeductionTemplates(string country)
    {
        try
        {
            var templates = await _payrollService.GetDeductionTemplatesAsync(country);
            return Json(new { success = true, data = templates });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetRateHistory(int deductionTypeId)
    {
        try
        {
            var history = await _payrollService.GetRateHistoryAsync(deductionTypeId);
            return Json(new { success = true, data = history });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    #region Phase B: Unlock & Re-finalise

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostUnlockPeriod(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            // Role detection: Owner is a claim, SuperAdmin is a role
            var isOwner = User.HasClaim("IsOwner", "true");
            var isSuperAdmin = User.IsInRole("SuperAdmin");

            if (!isOwner && !isSuperAdmin)
                return Json(new { success = false, message = "Only the business owner or a SuperAdmin can perform this action." });

            var userRole = isSuperAdmin ? "SuperAdmin" : isOwner ? "Owner" : "User";

            var result = await _payrollService.UnlockPeriodAsync(periodId, businessId, userId, userRole);
            return Json(new { success = result.Success, message = result.Message ?? "Period unlocked successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong. Please try again." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostRefinalisePeriod(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            // Role detection: Owner is a claim, SuperAdmin is a role
            var isOwner = User.HasClaim("IsOwner", "true");
            var isSuperAdmin = User.IsInRole("SuperAdmin");

            if (!isOwner && !isSuperAdmin)
                return Json(new { success = false, message = "Only the business owner or a SuperAdmin can perform this action." });

            var userRole = isSuperAdmin ? "SuperAdmin" : isOwner ? "Owner" : "User";

            var result = await _payrollService.RefinalisePeriodAsync(periodId, businessId, userId, userRole);
            return Json(new { success = result.Success, message = result.Message ?? "Period re-finalised successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong. Please try again." });
        }
    }

    #endregion

    #region Phase B: Audit History

    [HttpGet]
    public async Task<IActionResult> AxGetAuditHistory(int payslipId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var entries = await _payrollService.GetPayslipAuditHistoryAsync(payslipId, businessId);
            return Json(new { success = true, data = entries });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load audit history." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetPeriodAuditSummary(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var groups = await _payrollService.GetPeriodAuditSummaryAsync(periodId, businessId);
            return Json(new { success = true, data = groups });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load period audit summary." });
        }
    }

    public async Task<IActionResult> PayslipAuditHistory(int payslipId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var entries = await _payrollService.GetPayslipAuditHistoryAsync(payslipId, businessId);
            ViewBag.PayslipId = payslipId;
            ViewBag.AuditEntries = entries;
            return View();
        }
        catch (Exception ex)
        {
            return RedirectToAction("Periods");
        }
    }

    public async Task<IActionResult> PeriodAuditSummary(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var groups = await _payrollService.GetPeriodAuditSummaryAsync(periodId, businessId);
            ViewBag.PeriodId = periodId;
            ViewBag.AuditGroups = groups;
            return View();
        }
        catch (Exception ex)
        {
            return RedirectToAction("Periods");
        }
    }

    #endregion
}