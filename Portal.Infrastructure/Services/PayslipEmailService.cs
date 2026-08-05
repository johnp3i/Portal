using System.Globalization;
using Microsoft.Extensions.Options;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

public class PayslipEmailService : IPayslipEmailService
{
    private readonly IPayslipPdfService _pdfService;
    private readonly IPayslipRenderer _renderer;
    private readonly IEmailService _emailService;
    private readonly PayslipEmailLogRepository _emailLogRepository;
    private readonly PayrollRepository _payrollRepository;
    private readonly IBusinessService _businessService;
    private readonly PayrollSettings _settings;
    private readonly IPayrollProgressNotifier _progressNotifier;

    public PayslipEmailService(
        IPayslipPdfService pdfService,
        IPayslipRenderer renderer,
        IEmailService emailService,
        PayslipEmailLogRepository emailLogRepository,
        PayrollRepository payrollRepository,
        IBusinessService businessService,
        IOptions<PayrollSettings> payrollSettings,
        IPayrollProgressNotifier progressNotifier)
    {
        _pdfService = pdfService;
        _renderer = renderer;
        _emailService = emailService;
        _emailLogRepository = emailLogRepository;
        _payrollRepository = payrollRepository;
        _businessService = businessService;
        _settings = payrollSettings.Value;
        _progressNotifier = progressNotifier;
    }

    public async Task<ServiceResult> SendPayslipAsync(int payslipId, int businessId, string userId, bool includeSignature)
    {
        string? resolvedEmail = null;
        try
        {
            // 1. Load payslip detail (includes employee info and lines)
            var payslip = await GetPayslipDetailInternalAsync(payslipId, businessId);
            if (payslip == null)
                return ServiceResult.Fail("Payslip not found.");

            // 2. Validate employee has email
            if (string.IsNullOrWhiteSpace(payslip.EmployeeEmail))
                return ServiceResult.Fail("Employee email address not configured.");

            resolvedEmail = payslip.EmployeeEmail;

            // 3. Load business info
            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var profile = await _businessService.GetBusinessProfileAsync(businessId);
            var businessAddress = BuildBusinessAddress(profile);

            // 4. Render HTML → PDF
            var html = await _renderer.RenderPayslipHtmlAsync(
                payslip, business?.Name ?? "Business", businessAddress, includeSignature);
            var pdfBytes = await _pdfService.GeneratePdfAsync(html);

            // 5. Build filename
            var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(payslip.Month);
            var filename = $"{payslip.EmployeeName}_Payslip_{monthName}_{payslip.Year}.pdf";

            // 6. Send email with attachment
            await _emailService.SendPayslipEmailAsync(
                payslip.EmployeeEmail, payslip.EmployeeName, business?.Name ?? "Business",
                monthName, payslip.Year, pdfBytes, filename);

            // 7. Log success
            await _emailLogRepository.InsertAsync(new PayslipEmailLog
            {
                PayslipId = payslipId,
                SentByUserId = userId,
                SentToEmail = payslip.EmployeeEmail,
                SentAtUtc = DateTime.UtcNow,
                IsSuccess = true
            });

            return ServiceResult.Ok();
        }
        catch (Exception ex)
        {
            // Log failure
            await _emailLogRepository.InsertAsync(new PayslipEmailLog
            {
                PayslipId = payslipId,
                SentByUserId = userId,
                SentToEmail = resolvedEmail ?? "unknown",
                SentAtUtc = DateTime.UtcNow,
                IsSuccess = false,
                FailureReason = ex.Message
            });
            throw;
        }
    }

    public async Task<ServiceResult> SendAllPayslipsAsync(int periodId, int businessId, string userId, bool includeSignature)
    {
        try
        {
            // 1. Get all finalised payslips for period (PayslipStatusTypeId IN (3, 5))
            var finalisedPayslips = await _payrollRepository.GetFinalisedPayslipsForPeriodAsync(periodId, businessId);

            if (!finalisedPayslips.Any())
                return ServiceResult.Fail("No finalised payslips found for this period.");

            // 2. Check batch size against configured maximum
            if (finalisedPayslips.Count > _settings.BatchEmailMaxSize)
                return ServiceResult.Fail($"Batch size ({finalisedPayslips.Count}) exceeds maximum ({_settings.BatchEmailMaxSize}). Please send in smaller groups.");

            // 3. Iterate and send with delay and progress notifications
            int sent = 0, failed = 0, skipped = 0;

            foreach (var payslip in finalisedPayslips)
            {
                // Get employee to check email
                var employee = await _payrollRepository.GetEmployeeByIdAsync(payslip.EmployeeId, businessId);
                if (employee == null)
                {
                    skipped++;
                    await _progressNotifier.SendBatchEmailProgressAsync(
                        userId, sent + failed + skipped, finalisedPayslips.Count, "Unknown", "skipped");
                    continue;
                }

                // Skip if no email configured
                if (string.IsNullOrWhiteSpace(employee.Email))
                {
                    skipped++;
                    await _progressNotifier.SendBatchEmailProgressAsync(
                        userId, sent + failed + skipped, finalisedPayslips.Count, employee.Name ?? "Unknown", "skipped");
                    continue;
                }

                try
                {
                    await SendPayslipAsync(payslip.Id, businessId, userId, includeSignature);
                    sent++;
                    await _progressNotifier.SendBatchEmailProgressAsync(
                        userId, sent + failed + skipped, finalisedPayslips.Count, employee.Name ?? "Unknown", "sent");
                }
                catch (Exception ex)
                {
                    // Already logged in SendPayslipAsync's catch block
                    failed++;
                    await _progressNotifier.SendBatchEmailProgressAsync(
                        userId, sent + failed + skipped, finalisedPayslips.Count, employee.Name ?? "Unknown", "failed");
                }

                // Add delay between sends to prevent SMTP rate limiting
                await Task.Delay(_settings.BatchEmailDelayBetweenSendsMs);
            }

            var message = $"{sent} sent, {failed} failed, {skipped} skipped (no email)";
            return new ServiceResult { Success = true, Message = message };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static string BuildBusinessAddress(BusinessProfile? profile)
    {
        if (profile == null) return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.AddressLine1)) parts.Add(profile.AddressLine1);
        if (!string.IsNullOrWhiteSpace(profile.AddressLine2)) parts.Add(profile.AddressLine2);
        if (!string.IsNullOrWhiteSpace(profile.City)) parts.Add(profile.City);
        if (!string.IsNullOrWhiteSpace(profile.PostalCode)) parts.Add(profile.PostalCode);
        if (!string.IsNullOrWhiteSpace(profile.Country)) parts.Add(profile.Country);
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Builds a PayslipDetailDto by combining the payslip entity with employee, period, and line data.
    /// This avoids a circular dependency on IPayrollService.
    /// </summary>
    private async Task<PayslipDetailDto?> GetPayslipDetailInternalAsync(int payslipId, int businessId)
    {
        try
        {
            var payslip = await _payrollRepository.GetPayslipDetailAsync(payslipId, businessId);
            if (payslip == null) return null;

            var employee = await _payrollRepository.GetEmployeeByIdAsync(payslip.EmployeeId, businessId);
            var period = await _payrollRepository.GetPeriodByIdAsync(payslip.PayslipPeriodId, businessId);

            var earningLines = await _payrollRepository.GetEarningLinesByPayslipAsync(payslipId);
            var deductionLines = await _payrollRepository.GetDeductionLinesByPayslipAsync(payslipId);

            var earningTypes = await _payrollRepository.GetAllEarningTypesAsync();
            var earningTypeLookup = earningTypes.ToDictionary(e => e.Id, e => e);

            var deductionTypes = await _payrollRepository.GetDeductionTypesByBusinessAsync(businessId);
            var deductionTypeLookup = deductionTypes.ToDictionary(d => d.Id, d => d);

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
                SocialInsuranceNumber = employee?.SocialInsuranceNumber,
                IdNumber = employee?.IdNumber,
                EmployeeEmail = employee?.Email,
                Year = period?.Year ?? 0,
                Month = period?.Month ?? 0,
                PeriodStatus = "Finalised",
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
}
