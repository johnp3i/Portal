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
public class PayrollReportController : Controller
{
    private readonly IPayrollReportService _reportService;
    private readonly IPayslipEmailService _emailService;
    private readonly ICurrentTenantService _tenantService;

    public PayrollReportController(
        IPayrollReportService reportService,
        IPayslipEmailService emailService,
        ICurrentTenantService tenantService)
    {
        _reportService = reportService;
        _emailService = emailService;
        _tenantService = tenantService;
    }

    #region Page Actions

    [HttpGet]
    public async Task<IActionResult> EmployeeHistory(int employeeId, int? year)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var dto = await _reportService.GetEmployeeHistoryAsync(employeeId, businessId, year);
            ViewBag.CanSendEmail = User.HasClaim("IsOwner", "true") || User.IsInRole("SuperAdmin");
            return View(dto);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> AnnualSummary(int employeeId, int? year)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var currentYear = year ?? DateTime.UtcNow.Year;
            var dto = await _reportService.GetAnnualSummaryAsync(employeeId, businessId, currentYear);
            ViewBag.CanSendEmail = User.HasClaim("IsOwner", "true") || User.IsInRole("SuperAdmin");
            return View(dto);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> EarningsBreakdown(EarningsBreakdownFilter? filter)
    {
        // TODO: Add server-side pagination for large datasets in a future iteration.
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var appliedFilter = filter ?? new EarningsBreakdownFilter();
            var dto = await _reportService.GetEarningsBreakdownAsync(businessId, appliedFilter);
            return View(dto);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> PeriodSummary(int periodId, int? departmentId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var dto = await _reportService.GetPeriodSummaryAsync(periodId, businessId, departmentId);
            ViewBag.CanSendEmail = User.HasClaim("IsOwner", "true") || User.IsInRole("SuperAdmin");
            return View(dto);
        }
        catch (Exception ex)
        {
            return View("Error");
        }
    }

    #endregion

    #region Download & Export Endpoints
    // TODO: Consider rate limiting for PDF/ZIP generation endpoints in production — these are expensive operations.

    [HttpGet]
    public async Task<IActionResult> AxGetDownloadAllPayslipsPdf(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var zipBytes = await _reportService.GenerateAllPayslipsPdfZipAsync(periodId, businessId);

            if (zipBytes == null || zipBytes.Length == 0)
                return Json(new { success = false, message = "No payslips found for this period." });

            return File(zipBytes, "application/zip", $"Payslips_{periodId}.zip");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong generating the ZIP." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetDownloadAnnualSummaryPdf(int employeeId, int year)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var pdfBytes = await _reportService.GenerateAnnualSummaryPdfAsync(employeeId, businessId, year);

            if (pdfBytes == null || pdfBytes.Length == 0)
                return Json(new { success = false, message = "No data available for this year." });

            return File(pdfBytes, "application/pdf", $"AnnualSummary_{year}.pdf");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong generating the PDF." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetDownloadPeriodSummaryPdf(int periodId, int? departmentId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var pdfBytes = await _reportService.GeneratePeriodSummaryPdfAsync(periodId, businessId, departmentId);

            if (pdfBytes == null || pdfBytes.Length == 0)
                return Json(new { success = false, message = "No data available for this period." });

            return File(pdfBytes, "application/pdf", $"PeriodSummary_{periodId}.pdf");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong generating the PDF." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetDownloadEmployeeStatement(int employeeId, int startYear, int startMonth, int endYear, int endMonth)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var pdfBytes = await _reportService.GenerateEmployeeStatementPdfAsync(employeeId, businessId, startYear, startMonth, endYear, endMonth);

            if (pdfBytes == null || pdfBytes.Length == 0)
                return Json(new { success = false, message = "No finalised payslips found in the selected date range." });

            return File(pdfBytes, "application/pdf", $"Statement_{startMonth}{startYear}_to_{endMonth}{endYear}.pdf");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong generating the statement." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetExportEarningsBreakdown(EarningsBreakdownFilter? filter)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var appliedFilter = filter ?? new EarningsBreakdownFilter();
            var excelBytes = await _reportService.ExportEarningsBreakdownToExcelAsync(businessId, appliedFilter);

            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "EarningsBreakdown.xlsx");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong generating the export." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetExportPeriodSummary(int periodId, int? departmentId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var excelBytes = await _reportService.ExportPeriodSummaryToExcelAsync(periodId, businessId, departmentId);

            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PeriodSummary.xlsx");
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong generating the export." });
        }
    }

    #endregion

    #region Email Endpoints (Owner/SuperAdmin only)

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSendPayslipEmail(int payslipId, bool confirmResend = false)
    {
        try
        {
            var isOwner = User.HasClaim("IsOwner", "true");
            var isSuperAdmin = User.IsInRole("SuperAdmin");

            if (!isOwner && !isSuperAdmin)
                return Json(new { success = false, message = "Only the business owner or a SuperAdmin can send payslip emails." });

            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            // Check for duplicate send (unless confirmResend is true)
            if (!confirmResend)
            {
                var lastEmail = await _reportService.GetLastEmailForPayslipAsync(payslipId);
                if (lastEmail != null)
                {
                    return Json(new { success = false, alreadySent = true, sentDate = lastEmail.SentAtUtc.ToString("dd MMM yyyy HH:mm") });
                }
            }

            var result = await _emailService.SendPayslipAsync(payslipId, businessId, userId, false);
            return Json(new { success = result.Success, message = result.Message ?? "Payslip emailed successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong sending the email." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSendAllPayslipEmails(int periodId)
    {
        try
        {
            var isOwner = User.HasClaim("IsOwner", "true");
            var isSuperAdmin = User.IsInRole("SuperAdmin");

            if (!isOwner && !isSuperAdmin)
                return Json(new { success = false, message = "Only the business owner or a SuperAdmin can send payslip emails." });

            var businessId = _tenantService.CurrentBusinessId;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var result = await _emailService.SendAllPayslipsAsync(periodId, businessId, userId, false);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Something went wrong sending the emails." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetEmailLog(int payslipId)
    {
        try
        {
            var entries = await _reportService.GetEmailLogForPayslipAsync(payslipId);
            return Json(new { success = true, data = entries });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load email log." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> AxGetPeriodEmailSummary(int periodId)
    {
        try
        {
            var businessId = _tenantService.CurrentBusinessId;
            var summary = await _reportService.GetEmailSummaryForPeriodAsync(periodId, businessId);
            return Json(new { success = true, data = summary });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load email summary." });
        }
    }

    #endregion
}
