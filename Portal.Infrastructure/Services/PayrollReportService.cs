using System.Globalization;
using System.IO.Compression;
using Portal.Infrastructure.Entities;
using Portal.Infrastructure.Models.Payroll;
using Portal.Infrastructure.Repositories;

namespace Portal.Infrastructure.Services;

public class PayrollReportService : IPayrollReportService
{
    private readonly PayrollRepository _payrollRepository;
    private readonly PayslipEmailLogRepository _emailLogRepository;
    private readonly IPayslipPdfService _pdfService;
    private readonly IPayslipRenderer _renderer;
    private readonly IBusinessService _businessService;

    public PayrollReportService(
        PayrollRepository payrollRepository,
        PayslipEmailLogRepository emailLogRepository,
        IPayslipPdfService pdfService,
        IPayslipRenderer renderer,
        IBusinessService businessService)
    {
        _payrollRepository = payrollRepository;
        _emailLogRepository = emailLogRepository;
        _pdfService = pdfService;
        _renderer = renderer;
        _businessService = businessService;
    }

    public async Task<EmployeePayslipHistoryDto> GetEmployeeHistoryAsync(int employeeId, int businessId, int? year)
    {
        try
        {
            var payslips = await _payrollRepository.GetPayslipsByEmployeeAsync(employeeId, businessId, year);
            var availableYears = await _payrollRepository.GetAvailableYearsForEmployeeAsync(employeeId, businessId);
            var employee = await _payrollRepository.GetEmployeeByIdAsync(employeeId, businessId);

            var statusNames = await _payrollRepository.GetStatusNamesAsync();

            // Pre-load periods to avoid N+1
            var periodIds = payslips.Select(p => p.PayslipPeriodId).Distinct().ToArray();
            var periodCache = new Dictionary<int, PayslipPeriod>();
            foreach (var pid in periodIds)
            {
                var per = await _payrollRepository.GetPeriodByIdAsync(pid, businessId);
                if (per != null) periodCache[pid] = per;
            }

            var items = new List<PayslipHistoryItemDto>();
            foreach (var p in payslips)
            {
                var period = periodCache.GetValueOrDefault(p.PayslipPeriodId);
                items.Add(new PayslipHistoryItemDto
                {
                    PayslipId = p.Id,
                    Year = period?.Year ?? 0,
                    Month = period?.Month ?? 0,
                    TotalEarnings = p.TotalEarnings,
                    NetSalary = p.NetSalary,
                    Status = statusNames.GetValueOrDefault(p.PayslipStatusTypeId, "Unknown")
                });
            }

            return new EmployeePayslipHistoryDto
            {
                EmployeeId = employeeId,
                EmployeeName = employee?.Name ?? "Unknown",
                FilteredYear = year,
                AvailableYears = availableYears,
                Payslips = items,
                SummaryTotalGross = items.Sum(i => i.TotalEarnings),
                SummaryTotalNet = items.Sum(i => i.NetSalary),
                SummaryCount = items.Count
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<AnnualSummaryDto> GetAnnualSummaryAsync(int employeeId, int businessId, int year)
    {
        try
        {
            var employee = await _payrollRepository.GetEmployeeByIdAsync(employeeId, businessId);
            var availableYears = await _payrollRepository.GetAvailableYearsForEmployeeAsync(employeeId, businessId);
            var payslips = await _payrollRepository.GetFinalisedPayslipsForEmployeeYearAsync(employeeId, businessId, year);

            if (!payslips.Any())
            {
                return new AnnualSummaryDto
                {
                    EmployeeId = employeeId,
                    EmployeeName = employee?.Name ?? "Unknown",
                    Year = year,
                    AvailableYears = availableYears
                };
            }

            var payslipIds = payslips.Select(p => p.Id).ToArray();
            var earningLines = await _payrollRepository.GetEarningLinesForPayslipsAsync(payslipIds);
            var deductionLines = await _payrollRepository.GetDeductionLinesForPayslipsAsync(payslipIds);
            var earningTypes = await _payrollRepository.GetAllEarningTypesAsync();
            var deductionTypes = await _payrollRepository.GetDeductionTypesByBusinessAsync(businessId);

            // Pre-load periods to avoid N+1
            var periodIdsForBreakdown = payslips.Select(p => p.PayslipPeriodId).Distinct().ToArray();
            var periodCacheForBreakdown = new Dictionary<int, PayslipPeriod>();
            foreach (var pid in periodIdsForBreakdown)
            {
                var per = await _payrollRepository.GetPeriodByIdAsync(pid, businessId);
                if (per != null) periodCacheForBreakdown[pid] = per;
            }

            // Monthly breakdown
            var monthlyBreakdown = new List<MonthlySummaryRow>();
            foreach (var p in payslips)
            {
                var period = periodCacheForBreakdown.GetValueOrDefault(p.PayslipPeriodId);
                monthlyBreakdown.Add(new MonthlySummaryRow
                {
                    Month = period?.Month ?? 0,
                    Gross = p.TotalEarnings,
                    Deductions = p.TotalEmployeeDeductions,
                    Net = p.NetSalary,
                    Contributions = p.TotalEmployerContributions
                });
            }

            // Earnings by type
            var earningTypeLookup = earningTypes.ToDictionary(e => e.Id, e => e.Name);
            var earningsBreakdown = earningLines
                .GroupBy(el => el.EarningTypeId)
                .Select(g => new EarningSummaryRow
                {
                    EarningTypeName = earningTypeLookup.GetValueOrDefault(g.Key, "Unknown"),
                    TotalAmount = g.Sum(el => el.Amount)
                })
                .OrderByDescending(e => e.TotalAmount)
                .ToList();

            var totalGross = payslips.Sum(p => p.TotalEarnings);
            foreach (var e in earningsBreakdown)
            {
                e.Percentage = totalGross > 0 ? Math.Round(e.TotalAmount / totalGross * 100, 1) : 0;
            }

            // Deductions breakdown (employee portion - DeductionCategoryTypeId = 1)
            var deductionTypeLookup = deductionTypes.ToDictionary(d => d.Id, d => d);
            var employeeDeductions = deductionLines.Where(dl => dl.DeductionCategoryTypeId == 1).ToList();
            var deductionBreakdown = employeeDeductions
                .GroupBy(dl => dl.DeductionTypeId)
                .Select(g =>
                {
                    var dt = deductionTypeLookup.GetValueOrDefault(g.Key);
                    return new DeductionSummaryRow
                    {
                        DeductionName = dt?.Name ?? "Unknown",
                        Rate = g.First().Rate,
                        TotalAmount = g.Sum(dl => dl.CalculatedAmount),
                        MonthsApplied = g.Select(dl => dl.PayslipId).Distinct().Count()
                    };
                })
                .OrderByDescending(d => d.TotalAmount)
                .ToList();

            // Contributions breakdown (employer portion - DeductionCategoryTypeId = 2)
            var employerContributions = deductionLines.Where(dl => dl.DeductionCategoryTypeId == 2).ToList();
            var contributionBreakdown = employerContributions
                .GroupBy(dl => dl.DeductionTypeId)
                .Select(g =>
                {
                    var dt = deductionTypeLookup.GetValueOrDefault(g.Key);
                    return new DeductionSummaryRow
                    {
                        DeductionName = dt?.Name ?? "Unknown",
                        Rate = g.First().Rate,
                        TotalAmount = g.Sum(dl => dl.CalculatedAmount),
                        MonthsApplied = g.Select(dl => dl.PayslipId).Distinct().Count()
                    };
                })
                .OrderByDescending(d => d.TotalAmount)
                .ToList();

            return new AnnualSummaryDto
            {
                EmployeeId = employeeId,
                EmployeeName = employee?.Name ?? "Unknown",
                Year = year,
                AvailableYears = availableYears,
                TotalGross = totalGross,
                TotalDeductions = payslips.Sum(p => p.TotalEmployeeDeductions),
                TotalNet = payslips.Sum(p => p.NetSalary),
                TotalContributions = payslips.Sum(p => p.TotalEmployerContributions),
                MonthlyBreakdown = monthlyBreakdown,
                DeductionBreakdown = deductionBreakdown,
                ContributionBreakdown = contributionBreakdown,
                EarningsBreakdown = earningsBreakdown
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<byte[]> GenerateAnnualSummaryPdfAsync(int employeeId, int businessId, int year)
    {
        try
        {
            var summary = await GetAnnualSummaryAsync(employeeId, businessId, year);
            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var employee = await _payrollRepository.GetEmployeeByIdAsync(employeeId, businessId);

            var model = new AnnualSummaryPdfViewModel
            {
                EmployeeName = summary.EmployeeName,
                EmployeeSin = employee?.SocialInsuranceNumber,
                BusinessName = business?.Name ?? "Business",
                Year = year,
                MonthlyBreakdown = summary.MonthlyBreakdown,
                DeductionBreakdown = summary.DeductionBreakdown,
                ContributionBreakdown = summary.ContributionBreakdown,
                TotalGross = summary.TotalGross,
                TotalDeductions = summary.TotalDeductions,
                TotalNet = summary.TotalNet,
                TotalContributions = summary.TotalContributions
            };

            var html = BuildAnnualSummaryHtml(model);
            return await _pdfService.GeneratePdfAsync(html);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<EarningsBreakdownDto> GetEarningsBreakdownAsync(int businessId, EarningsBreakdownFilter filter)
    {
        try
        {
            // Get all finalised payslips for the business within the filter date range
            var allPayslips = new List<Payslip>();
            var periods = await _payrollRepository.GetPeriodsByBusinessAsync(businessId);

            // Filter periods by date range
            var filteredPeriods = periods.Where(p =>
            {
                if (filter.FromYear.HasValue && filter.FromMonth.HasValue)
                {
                    if (p.Year < filter.FromYear.Value || (p.Year == filter.FromYear.Value && p.Month < filter.FromMonth.Value))
                        return false;
                }
                if (filter.ToYear.HasValue && filter.ToMonth.HasValue)
                {
                    if (p.Year > filter.ToYear.Value || (p.Year == filter.ToYear.Value && p.Month > filter.ToMonth.Value))
                        return false;
                }
                return true;
            }).ToList();

            foreach (var period in filteredPeriods)
            {
                var periodPayslips = await _payrollRepository.GetFinalisedPayslipsForPeriodAsync(period.Id, businessId);
                if (filter.EmployeeId.HasValue)
                    periodPayslips = periodPayslips.Where(p => p.EmployeeId == filter.EmployeeId.Value).ToList();
                allPayslips.AddRange(periodPayslips);
            }

            if (!allPayslips.Any())
                return new EarningsBreakdownDto { AppliedFilter = filter };

            // Get earning lines for all payslips
            var payslipIds = allPayslips.Select(p => p.Id).ToArray();
            var earningLines = await _payrollRepository.GetEarningLinesForPayslipsAsync(payslipIds);

            // Filter by earning type if specified
            if (filter.EarningTypeIds != null && filter.EarningTypeIds.Any())
                earningLines = earningLines.Where(el => filter.EarningTypeIds.Contains(el.EarningTypeId)).ToList();

            var earningTypes = await _payrollRepository.GetAllEarningTypesAsync();
            var earningTypeLookup = earningTypes.ToDictionary(e => e.Id, e => e.Name);

            // Build type summaries
            var typeSummaries = earningLines
                .GroupBy(el => el.EarningTypeId)
                .Select(g => new EarningTypeSummaryRow
                {
                    EarningTypeId = g.Key,
                    EarningTypeName = earningTypeLookup.GetValueOrDefault(g.Key, "Unknown"),
                    TotalAmount = g.Sum(el => el.Amount),
                    LineCount = g.Count()
                })
                .OrderByDescending(t => t.TotalAmount)
                .ToList();

            // Pre-load employees to avoid N+1 queries
            var employeeIds = allPayslips.Select(p => p.EmployeeId).Distinct().ToArray();
            var employeeCache = new Dictionary<int, Employee>();
            foreach (var empId in employeeIds)
            {
                var emp = await _payrollRepository.GetEmployeeByIdAsync(empId, businessId);
                if (emp != null) employeeCache[empId] = emp;
            }

            // Build detail rows
            var details = new List<EarningDetailRow>();
            foreach (var el in earningLines)
            {
                var payslip = allPayslips.First(p => p.Id == el.PayslipId);
                var period = filteredPeriods.FirstOrDefault(pd => pd.Id == payslip.PayslipPeriodId);
                var employee = employeeCache.GetValueOrDefault(payslip.EmployeeId);

                details.Add(new EarningDetailRow
                {
                    EmployeeName = employee?.Name ?? "Unknown",
                    Year = period?.Year ?? 0,
                    Month = period?.Month ?? 0,
                    EarningTypeName = earningTypeLookup.GetValueOrDefault(el.EarningTypeId, "Unknown"),
                    Description = el.Description,
                    Hours = el.OvertimeHours,
                    Multiplier = el.OvertimeMultiplier,
                    Amount = el.Amount
                });
            }

            // Sort details by date (newest first), then by employee name
            details = details.OrderByDescending(d => d.Year).ThenByDescending(d => d.Month).ThenBy(d => d.EmployeeName).ToList();

            return new EarningsBreakdownDto
            {
                TypeSummaries = typeSummaries,
                Details = details,
                AppliedFilter = filter
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<byte[]> ExportEarningsBreakdownToExcelAsync(int businessId, EarningsBreakdownFilter filter)
    {
        try
        {
            var data = await GetEarningsBreakdownAsync(businessId, filter);

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Earnings Breakdown");

            // Headers
            worksheet.Cell(1, 1).Value = "Employee Name";
            worksheet.Cell(1, 2).Value = "Period";
            worksheet.Cell(1, 3).Value = "Earning Type";
            worksheet.Cell(1, 4).Value = "Description";
            worksheet.Cell(1, 5).Value = "Amount";

            // Style header row
            var headerRange = worksheet.Range(1, 1, 1, 5);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#0D5EA6");
            headerRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

            // Data rows
            var row = 2;
            foreach (var detail in data.Details)
            {
                var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(detail.Month > 0 ? detail.Month : 1);
                worksheet.Cell(row, 1).Value = detail.EmployeeName;
                worksheet.Cell(row, 2).Value = $"{monthName} {detail.Year}";
                worksheet.Cell(row, 3).Value = detail.EarningTypeName;
                worksheet.Cell(row, 4).Value = detail.Description ?? "";
                worksheet.Cell(row, 5).Value = detail.Amount;
                worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PeriodSummaryDto> GetPeriodSummaryAsync(int periodId, int businessId, int? departmentId)
    {
        try
        {
            var period = await _payrollRepository.GetPeriodByIdAsync(periodId, businessId);
            var payslips = await _payrollRepository.GetFinalisedPayslipsForPeriodAsync(periodId, businessId);

            if (!payslips.Any())
            {
                return new PeriodSummaryDto
                {
                    PeriodId = periodId,
                    Year = period?.Year ?? 0,
                    Month = period?.Month ?? 0,
                    DepartmentFilter = departmentId
                };
            }

            // Pre-load employees and departments to avoid N+1 queries
            var empIds = payslips.Select(p => p.EmployeeId).Distinct().ToArray();
            var empCache = new Dictionary<int, Employee>();
            foreach (var empId in empIds)
            {
                var emp = await _payrollRepository.GetEmployeeByIdAsync(empId, businessId);
                if (emp != null) empCache[empId] = emp;
            }

            var deptIds = empCache.Values
                .Where(e => e.DepartmentId.HasValue)
                .Select(e => e.DepartmentId!.Value)
                .Distinct()
                .ToArray();
            var deptCache = new Dictionary<int, Department>();
            foreach (var deptId in deptIds)
            {
                var dept = await _payrollRepository.GetDepartmentByIdAsync(deptId, businessId);
                if (dept != null) deptCache[deptId] = dept;
            }

            var rows = new List<PeriodSummaryRow>();
            foreach (var payslip in payslips)
            {
                var employee = empCache.GetValueOrDefault(payslip.EmployeeId);
                if (employee == null) continue;

                // Apply department filter
                if (departmentId.HasValue && employee.DepartmentId != departmentId.Value)
                    continue;

                var department = employee.DepartmentId.HasValue
                    ? deptCache.GetValueOrDefault(employee.DepartmentId.Value)
                    : null;

                rows.Add(new PeriodSummaryRow
                {
                    EmployeeName = employee.Name ?? "Unknown",
                    DepartmentName = department?.Name,
                    TotalEarnings = payslip.TotalEarnings,
                    TotalDeductions = payslip.TotalEmployeeDeductions,
                    NetSalary = payslip.NetSalary,
                    EmployerContributions = payslip.TotalEmployerContributions,
                    TotalCost = payslip.TotalEarnings + payslip.TotalEmployerContributions
                });
            }

            rows = rows.OrderBy(r => r.EmployeeName).ToList();

            return new PeriodSummaryDto
            {
                PeriodId = periodId,
                Year = period?.Year ?? 0,
                Month = period?.Month ?? 0,
                DepartmentFilter = departmentId,
                Rows = rows,
                TotalGross = rows.Sum(r => r.TotalEarnings),
                TotalDeductions = rows.Sum(r => r.TotalDeductions),
                TotalNet = rows.Sum(r => r.NetSalary),
                TotalContributions = rows.Sum(r => r.EmployerContributions),
                TotalCost = rows.Sum(r => r.TotalCost)
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<byte[]> GeneratePeriodSummaryPdfAsync(int periodId, int businessId, int? departmentId)
    {
        try
        {
            var summary = await GetPeriodSummaryAsync(periodId, businessId, departmentId);
            var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(summary.Month > 0 ? summary.Month : 1);

            var html = BuildPeriodSummaryHtml(summary, monthName);
            return await _pdfService.GeneratePdfAsync(html);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<byte[]> ExportPeriodSummaryToExcelAsync(int periodId, int businessId, int? departmentId)
    {
        try
        {
            var data = await GetPeriodSummaryAsync(periodId, businessId, departmentId);
            var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(data.Month > 0 ? data.Month : 1);

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add($"Period Summary {monthName} {data.Year}");

            // Headers
            worksheet.Cell(1, 1).Value = "Employee Name";
            worksheet.Cell(1, 2).Value = "Department";
            worksheet.Cell(1, 3).Value = "Gross";
            worksheet.Cell(1, 4).Value = "Deductions";
            worksheet.Cell(1, 5).Value = "Net Salary";
            worksheet.Cell(1, 6).Value = "Employer Contributions";
            worksheet.Cell(1, 7).Value = "Total Cost";

            // Style header row
            var headerRange = worksheet.Range(1, 1, 1, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#0D5EA6");
            headerRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

            // Data rows
            var row = 2;
            foreach (var item in data.Rows)
            {
                worksheet.Cell(row, 1).Value = item.EmployeeName;
                worksheet.Cell(row, 2).Value = item.DepartmentName ?? "\u2014";
                worksheet.Cell(row, 3).Value = item.TotalEarnings;
                worksheet.Cell(row, 4).Value = item.TotalDeductions;
                worksheet.Cell(row, 5).Value = item.NetSalary;
                worksheet.Cell(row, 6).Value = item.EmployerContributions;
                worksheet.Cell(row, 7).Value = item.TotalCost;

                // Format numeric columns
                for (int col = 3; col <= 7; col++)
                    worksheet.Cell(row, col).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            // Totals row
            worksheet.Cell(row, 1).Value = "TOTALS";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 3).Value = data.TotalGross;
            worksheet.Cell(row, 4).Value = data.TotalDeductions;
            worksheet.Cell(row, 5).Value = data.TotalNet;
            worksheet.Cell(row, 6).Value = data.TotalContributions;
            worksheet.Cell(row, 7).Value = data.TotalCost;
            var totalsRange = worksheet.Range(row, 1, row, 7);
            totalsRange.Style.Font.Bold = true;
            for (int col = 3; col <= 7; col++)
                worksheet.Cell(row, col).Style.NumberFormat.Format = "#,##0.00";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<byte[]> GenerateEmployeeStatementPdfAsync(int employeeId, int businessId, int startYear, int startMonth, int endYear, int endMonth)
    {
        try
        {
            var employee = await _payrollRepository.GetEmployeeByIdAsync(employeeId, businessId);
            if (employee == null)
                return Array.Empty<byte>();

            // Get all finalised payslips within date range
            var allPayslips = await _payrollRepository.GetFinalisedPayslipsForEmployeeYearAsync(employeeId, businessId, startYear);

            // If range spans multiple years, get additional years
            if (endYear > startYear)
            {
                for (int y = startYear + 1; y <= endYear; y++)
                {
                    var yearPayslips = await _payrollRepository.GetFinalisedPayslipsForEmployeeYearAsync(employeeId, businessId, y);
                    allPayslips.AddRange(yearPayslips);
                }
            }

            // Filter to the exact month range
            var filteredPayslips = new List<Payslip>();
            foreach (var p in allPayslips)
            {
                var period = await _payrollRepository.GetPeriodByIdAsync(p.PayslipPeriodId, businessId);
                if (period == null) continue;

                var periodValue = period.Year * 12 + period.Month;
                var startValue = startYear * 12 + startMonth;
                var endValue = endYear * 12 + endMonth;

                if (periodValue >= startValue && periodValue <= endValue)
                    filteredPayslips.Add(p);
            }

            if (!filteredPayslips.Any())
                return Array.Empty<byte>();

            // Build statement data
            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var profile = await _businessService.GetBusinessProfileAsync(businessId);
            var businessName = business?.Name ?? "Business";
            var businessAddress = BuildBusinessAddress(profile);

            var earningTypes = await _payrollRepository.GetAllEarningTypesAsync();
            var deductionTypes = await _payrollRepository.GetDeductionTypesByBusinessAsync(businessId);
            var earningTypeLookup = earningTypes.ToDictionary(e => e.Id, e => e);
            var deductionTypeLookup = deductionTypes.ToDictionary(d => d.Id, d => d);

            // Build PayslipDetailDto for each filtered payslip
            var payslipDetails = new List<PayslipDetailDto>();
            foreach (var payslip in filteredPayslips)
            {
                var period = await _payrollRepository.GetPeriodByIdAsync(payslip.PayslipPeriodId, businessId);
                var earningLines = await _payrollRepository.GetEarningLinesByPayslipAsync(payslip.Id);
                var deductionLines = await _payrollRepository.GetDeductionLinesByPayslipAsync(payslip.Id);

                payslipDetails.Add(new PayslipDetailDto
                {
                    Id = payslip.Id,
                    EmployeeName = employee.Name,
                    EmployeePosition = employee.Position,
                    SocialInsuranceNumber = employee.SocialInsuranceNumber,
                    IdNumber = employee.IdNumber,
                    Year = period?.Year ?? 0,
                    Month = period?.Month ?? 0,
                    TotalEarnings = payslip.TotalEarnings,
                    TotalEmployeeDeductions = payslip.TotalEmployeeDeductions,
                    NetSalary = payslip.NetSalary,
                    TotalEmployerContributions = payslip.TotalEmployerContributions,
                    ManagerNotes = payslip.ManagerNotes,
                    EarningLines = earningLines.Select(el =>
                    {
                        var et = earningTypeLookup.GetValueOrDefault(el.EarningTypeId);
                        return new EarningLineDto
                        {
                            Id = el.Id,
                            EarningTypeName = et?.Name ?? "Unknown",
                            EarningTypeCode = et?.Code ?? "",
                            Description = el.Description,
                            Amount = el.Amount,
                            OvertimeMultiplier = el.OvertimeMultiplier,
                            OvertimeHours = el.OvertimeHours
                        };
                    }).ToList(),
                    EmployeeDeductions = deductionLines.Where(dl => dl.DeductionCategoryTypeId == 1).Select(dl =>
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
                    }).ToList(),
                    EmployerContributions = deductionLines.Where(dl => dl.DeductionCategoryTypeId == 2).Select(dl =>
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
                    }).ToList()
                });
            }

            var startMonthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(startMonth);
            var endMonthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(endMonth);

            var model = new EmployeeStatementPdfViewModel
            {
                EmployeeName = employee.Name,
                Position = employee.Position,
                SocialInsuranceNumber = employee.SocialInsuranceNumber,
                IdNumber = employee.IdNumber,
                BusinessName = businessName,
                BusinessAddress = businessAddress,
                PeriodFrom = $"{startMonthName} {startYear}",
                PeriodTo = $"{endMonthName} {endYear}",
                TotalGross = filteredPayslips.Sum(p => p.TotalEarnings),
                TotalDeductions = filteredPayslips.Sum(p => p.TotalEmployeeDeductions),
                TotalNet = filteredPayslips.Sum(p => p.NetSalary),
                TotalContributions = filteredPayslips.Sum(p => p.TotalEmployerContributions),
                Payslips = payslipDetails
            };

            var html = BuildEmployeeStatementHtml(model);

            return await _pdfService.GeneratePdfAsync(html);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<byte[]> GenerateAllPayslipsPdfZipAsync(int periodId, int businessId)
    {
        try
        {
            var payslips = await _payrollRepository.GetFinalisedPayslipsForPeriodAsync(periodId, businessId);
            if (!payslips.Any())
                return Array.Empty<byte>();

            var business = await _businessService.GetBusinessByIdAsync(businessId);
            var profile = await _businessService.GetBusinessProfileAsync(businessId);
            var businessName = business?.Name ?? "Business";
            var businessAddress = BuildBusinessAddress(profile);

            var period = await _payrollRepository.GetPeriodByIdAsync(periodId, businessId);

            // Render all HTML documents
            var htmlDocuments = new List<string>();
            var filenames = new List<string>();

            // Pre-load earning/deduction types outside loop to avoid N+1
            var earningTypes = await _payrollRepository.GetAllEarningTypesAsync();
            var deductionTypes = await _payrollRepository.GetDeductionTypesByBusinessAsync(businessId);
            var earningTypeLookup = earningTypes.ToDictionary(e => e.Id, e => e);
            var deductionTypeLookup = deductionTypes.ToDictionary(d => d.Id, d => d);

            foreach (var payslip in payslips)
            {
                var employee = await _payrollRepository.GetEmployeeByIdAsync(payslip.EmployeeId, businessId);
                var earningLines = await _payrollRepository.GetEarningLinesByPayslipAsync(payslip.Id);
                var deductionLines = await _payrollRepository.GetDeductionLinesByPayslipAsync(payslip.Id);

                var detail = new PayslipDetailDto
                {
                    Id = payslip.Id,
                    EmployeeName = employee?.Name ?? "Unknown",
                    EmployeePosition = employee?.Position,
                    SocialInsuranceNumber = employee?.SocialInsuranceNumber,
                    IdNumber = employee?.IdNumber,
                    Year = period?.Year ?? 0,
                    Month = period?.Month ?? 0,
                    TotalEarnings = payslip.TotalEarnings,
                    TotalEmployeeDeductions = payslip.TotalEmployeeDeductions,
                    NetSalary = payslip.NetSalary,
                    TotalEmployerContributions = payslip.TotalEmployerContributions,
                    ManagerNotes = payslip.ManagerNotes,
                    EarningLines = earningLines.Select(el =>
                    {
                        var et = earningTypeLookup.GetValueOrDefault(el.EarningTypeId);
                        return new EarningLineDto
                        {
                            Id = el.Id,
                            EarningTypeName = et?.Name ?? "Unknown",
                            EarningTypeCode = et?.Code ?? "",
                            Description = el.Description,
                            Amount = el.Amount,
                            OvertimeMultiplier = el.OvertimeMultiplier,
                            OvertimeHours = el.OvertimeHours
                        };
                    }).ToList(),
                    EmployeeDeductions = deductionLines.Where(dl => dl.DeductionCategoryTypeId == 1).Select(dl =>
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
                    }).ToList(),
                    EmployerContributions = deductionLines.Where(dl => dl.DeductionCategoryTypeId == 2).Select(dl =>
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
                    }).ToList()
                };

                var html = await _renderer.RenderPayslipHtmlAsync(detail, businessName, businessAddress, false);
                htmlDocuments.Add(html);

                var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(period?.Month ?? 1);
                filenames.Add($"{employee?.Name ?? "Unknown"}_Payslip_{monthName}_{period?.Year ?? 0}.pdf");
            }

            // Generate all PDFs with browser reuse
            var pdfBytesList = await _pdfService.GenerateBatchPdfAsync(htmlDocuments);

            // Package into ZIP
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                for (int i = 0; i < pdfBytesList.Count; i++)
                {
                    var entry = archive.CreateEntry(filenames[i], CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(pdfBytesList[i]);
                }
            }

            return memoryStream.ToArray();
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

    private static string BuildAnnualSummaryHtml(AnnualSummaryPdfViewModel model)
    {
        var monthNames = new[] { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><style>");
        sb.AppendLine("body{font-family:'Inter',sans-serif;font-size:13px;color:#1a2332;padding:48px 52px;}");
        sb.AppendLine("h1{font-family:'Manrope',sans-serif;font-size:20px;color:#0D5EA6;margin-bottom:8px;}");
        sb.AppendLine("h2{font-family:'Manrope',sans-serif;font-size:14px;color:#0D5EA6;margin-top:20px;}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin-bottom:24px;font-size:12.5px;}");
        sb.AppendLine("thead th{background:#0D5EA6;color:#fff;padding:9px 12px;text-align:left;font-size:11.5px;}");
        sb.AppendLine("tbody td{padding:8px 12px;border-bottom:1px solid #eef2f6;}");
        sb.AppendLine(".total-row td{background:#eef5fc;font-weight:700;color:#0D5EA6;border-top:2px solid #0D5EA6;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>{model.BusinessName} — Annual Summary {model.Year}</h1>");
        sb.AppendLine($"<p><strong>Employee:</strong> {model.EmployeeName} | <strong>SIN:</strong> {model.EmployeeSin ?? "—"}</p><br/>");

        // Monthly breakdown table
        sb.AppendLine("<table><thead><tr><th>Month</th><th style='text-align:right'>Gross</th><th style='text-align:right'>Deductions</th><th style='text-align:right'>Net</th><th style='text-align:right'>Contributions</th></tr></thead><tbody>");
        foreach (var row in model.MonthlyBreakdown)
        {
            var mn = row.Month >= 1 && row.Month <= 12 ? monthNames[row.Month] : "?";
            sb.AppendLine($"<tr><td>{mn}</td><td style='text-align:right'>&euro;{row.Gross:N2}</td><td style='text-align:right'>&euro;{row.Deductions:N2}</td><td style='text-align:right'>&euro;{row.Net:N2}</td><td style='text-align:right'>&euro;{row.Contributions:N2}</td></tr>");
        }
        sb.AppendLine($"<tr class='total-row'><td><strong>Total</strong></td><td style='text-align:right'>&euro;{model.TotalGross:N2}</td><td style='text-align:right'>&euro;{model.TotalDeductions:N2}</td><td style='text-align:right'>&euro;{model.TotalNet:N2}</td><td style='text-align:right'>&euro;{model.TotalContributions:N2}</td></tr>");
        sb.AppendLine("</tbody></table>");

        // Deductions
        if (model.DeductionBreakdown.Any())
        {
            sb.AppendLine("<h2>Employee Deductions</h2>");
            sb.AppendLine("<table><thead><tr><th>Name</th><th style='text-align:right'>Rate</th><th style='text-align:right'>Months</th><th style='text-align:right'>Total</th></tr></thead><tbody>");
            foreach (var d in model.DeductionBreakdown)
                sb.AppendLine($"<tr><td>{d.DeductionName}</td><td style='text-align:right'>{d.Rate:0.00}%</td><td style='text-align:right'>{d.MonthsApplied}</td><td style='text-align:right'>&euro;{d.TotalAmount:N2}</td></tr>");
            sb.AppendLine("</tbody></table>");
        }

        // Contributions
        if (model.ContributionBreakdown.Any())
        {
            sb.AppendLine("<h2>Employer Contributions</h2>");
            sb.AppendLine("<table><thead><tr><th>Name</th><th style='text-align:right'>Rate</th><th style='text-align:right'>Months</th><th style='text-align:right'>Total</th></tr></thead><tbody>");
            foreach (var c in model.ContributionBreakdown)
                sb.AppendLine($"<tr><td>{c.DeductionName}</td><td style='text-align:right'>{c.Rate:0.00}%</td><td style='text-align:right'>{c.MonthsApplied}</td><td style='text-align:right'>&euro;{c.TotalAmount:N2}</td></tr>");
            sb.AppendLine("</tbody></table>");
        }

        sb.AppendLine("<p style='font-size:11px;color:#8a96a4;margin-top:40px;text-align:center;'>Generated by 3 Inventors Portal &bull; Confidential</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string BuildPeriodSummaryHtml(PeriodSummaryDto model, string monthName)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><style>");
        sb.AppendLine("body{font-family:'Inter',sans-serif;font-size:13px;color:#1a2332;padding:48px 52px;}");
        sb.AppendLine("h1{font-family:'Manrope',sans-serif;font-size:20px;color:#0D5EA6;margin-bottom:8px;}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin-bottom:24px;font-size:12.5px;}");
        sb.AppendLine("thead th{background:#0D5EA6;color:#fff;padding:9px 12px;text-align:left;font-size:11.5px;}");
        sb.AppendLine("tbody td{padding:8px 12px;border-bottom:1px solid #eef2f6;}");
        sb.AppendLine(".total-row td{background:#eef5fc;font-weight:700;color:#0D5EA6;border-top:2px solid #0D5EA6;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>Period Summary — {monthName} {model.Year}</h1>");
        sb.AppendLine($"<p><strong>Employees:</strong> {model.Rows.Count} | <strong>Total Cost:</strong> &euro;{model.TotalCost:N2}</p><br/>");

        sb.AppendLine("<table><thead><tr><th>Employee</th><th>Department</th><th style='text-align:right'>Gross</th><th style='text-align:right'>Deductions</th><th style='text-align:right'>Net</th><th style='text-align:right'>Contributions</th><th style='text-align:right'>Total Cost</th></tr></thead><tbody>");
        foreach (var row in model.Rows)
        {
            sb.AppendLine($"<tr><td>{row.EmployeeName}</td><td>{row.DepartmentName ?? "—"}</td><td style='text-align:right'>&euro;{row.TotalEarnings:N2}</td><td style='text-align:right'>&euro;{row.TotalDeductions:N2}</td><td style='text-align:right'>&euro;{row.NetSalary:N2}</td><td style='text-align:right'>&euro;{row.EmployerContributions:N2}</td><td style='text-align:right'>&euro;{row.TotalCost:N2}</td></tr>");
        }
        sb.AppendLine($"<tr class='total-row'><td><strong>TOTALS</strong></td><td></td><td style='text-align:right'>&euro;{model.TotalGross:N2}</td><td style='text-align:right'>&euro;{model.TotalDeductions:N2}</td><td style='text-align:right'>&euro;{model.TotalNet:N2}</td><td style='text-align:right'>&euro;{model.TotalContributions:N2}</td><td style='text-align:right'>&euro;{model.TotalCost:N2}</td></tr>");
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<p style='font-size:11px;color:#8a96a4;margin-top:40px;text-align:center;'>Generated by 3 Inventors Portal &bull; Confidential</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string BuildEmployeeStatementHtml(EmployeeStatementPdfViewModel model)
    {
        var monthNames = new[] { "", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><style>");
        sb.AppendLine("body{font-family:'Inter',sans-serif;font-size:13px;color:#1a2332;padding:48px 52px;}");
        sb.AppendLine("h1{font-family:'Manrope',sans-serif;font-size:20px;color:#0D5EA6;margin-bottom:8px;}");
        sb.AppendLine("h2{font-family:'Manrope',sans-serif;font-size:14px;color:#0D5EA6;margin-top:20px;}");
        sb.AppendLine("table{width:100%;border-collapse:collapse;margin-bottom:24px;font-size:12.5px;}");
        sb.AppendLine("thead th{background:#0D5EA6;color:#fff;padding:9px 12px;text-align:left;font-size:11.5px;}");
        sb.AppendLine("tbody td{padding:8px 12px;border-bottom:1px solid #eef2f6;}");
        sb.AppendLine(".total-row td{background:#eef5fc;font-weight:700;color:#0D5EA6;border-top:2px solid #0D5EA6;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>{model.BusinessName} — Employee Statement</h1>");
        sb.AppendLine($"<p><strong>Employee:</strong> {model.EmployeeName}</p>");
        if (!string.IsNullOrWhiteSpace(model.Position))
            sb.AppendLine($"<p><strong>Position:</strong> {model.Position}</p>");
        if (!string.IsNullOrWhiteSpace(model.SocialInsuranceNumber))
            sb.AppendLine($"<p><strong>SIN:</strong> {model.SocialInsuranceNumber}</p>");
        sb.AppendLine($"<p><strong>Period:</strong> {model.PeriodFrom} to {model.PeriodTo}</p><br/>");

        // Summary totals
        sb.AppendLine("<table><thead><tr><th>Total Gross</th><th>Total Deductions</th><th>Total Net</th><th>Total Contributions</th></tr></thead><tbody>");
        sb.AppendLine($"<tr><td>&euro;{model.TotalGross:N2}</td><td>&euro;{model.TotalDeductions:N2}</td><td>&euro;{model.TotalNet:N2}</td><td>&euro;{model.TotalContributions:N2}</td></tr>");
        sb.AppendLine("</tbody></table>");

        // Payslip details
        sb.AppendLine("<h2>Payslip Details</h2>");
        sb.AppendLine("<table><thead><tr><th>Period</th><th style='text-align:right'>Gross</th><th style='text-align:right'>Deductions</th><th style='text-align:right'>Net</th><th style='text-align:right'>Contributions</th></tr></thead><tbody>");
        foreach (var p in model.Payslips)
        {
            var mn = p.Month >= 1 && p.Month <= 12 ? monthNames[p.Month] : "?";
            sb.AppendLine($"<tr><td>{mn} {p.Year}</td><td style='text-align:right'>&euro;{p.TotalEarnings:N2}</td><td style='text-align:right'>&euro;{p.TotalEmployeeDeductions:N2}</td><td style='text-align:right'>&euro;{p.NetSalary:N2}</td><td style='text-align:right'>&euro;{p.TotalEmployerContributions:N2}</td></tr>");
        }
        sb.AppendLine($"<tr class='total-row'><td><strong>Total</strong></td><td style='text-align:right'>&euro;{model.TotalGross:N2}</td><td style='text-align:right'>&euro;{model.TotalDeductions:N2}</td><td style='text-align:right'>&euro;{model.TotalNet:N2}</td><td style='text-align:right'>&euro;{model.TotalContributions:N2}</td></tr>");
        sb.AppendLine("</tbody></table>");

        sb.AppendLine($"<p style='font-size:11px;color:#8a96a4;margin-top:40px;text-align:center;'>Generated by 3 Inventors Portal &bull; Confidential</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public async Task<List<PayslipEmailLogDto>> GetEmailLogForPayslipAsync(int payslipId)
    {
        try
        {
            var logs = await _emailLogRepository.GetByPayslipIdAsync(payslipId);
            return logs.Select(l => new PayslipEmailLogDto
            {
                Id = l.Id,
                PayslipId = l.PayslipId,
                SentByUserName = l.SentByUserId,
                SentToEmail = l.SentToEmail,
                SentAtUtc = l.SentAtUtc,
                IsSuccess = l.IsSuccess,
                FailureReason = l.FailureReason
            }).ToList();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PayslipEmailSummaryDto> GetEmailSummaryForPeriodAsync(int periodId, int businessId)
    {
        try
        {
            return await _payrollRepository.GetEmailSummaryForPeriodAsync(periodId, businessId);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<PayslipEmailLogDto?> GetLastEmailForPayslipAsync(int payslipId)
    {
        try
        {
            var log = await _emailLogRepository.GetLastByPayslipIdAsync(payslipId);
            if (log == null) return null;

            return new PayslipEmailLogDto
            {
                Id = log.Id,
                PayslipId = log.PayslipId,
                SentByUserName = log.SentByUserId,
                SentToEmail = log.SentToEmail,
                SentAtUtc = log.SentAtUtc,
                IsSuccess = log.IsSuccess,
                FailureReason = log.FailureReason
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
