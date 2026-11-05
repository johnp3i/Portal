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

    public async Task<int?> GetLatestFinalisedPeriodIdAsync(int businessId)
    {
        try
        {
            var periods = await _payrollRepository.GetPeriodsByBusinessAsync(businessId);
            var finalised = periods
                .Where(p => p.PayslipStatusTypeId >= 3) // Finalised or later
                .OrderByDescending(p => p.Year)
                .ThenByDescending(p => p.Month)
                .FirstOrDefault();

            return finalised?.Id;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<RegisterEmployeeOption>> GetRegisterEligibleEmployeesAsync(
        int fromYear, int fromMonth, int toYear, int toMonth, int businessId)
    {
        try
        {
            // Resolve the ordered Range_Periods for this range (oldest -> newest).
            var rangePeriods = await ResolveRangePeriodsAsync(fromYear, fromMonth, toYear, toMonth, businessId);

            if (rangePeriods.Count == 0)
                return new List<RegisterEmployeeOption>();

            // Union the distinct employee Ids that have a finalised payslip in ANY Range_Period
            // (finalised = PayslipStatusTypeId IN (3,5), tenant-scoped via the PayslipPeriod.BusinessId
            // join inside GetFinalisedPayslipsForPeriodAsync). One call per Range_Period.
            var distinctEmployeeIdSet = new HashSet<int>();
            foreach (var period in rangePeriods)
            {
                var finalisedPayslips = await _payrollRepository.GetFinalisedPayslipsForPeriodAsync(period.Id, businessId);
                if (finalisedPayslips == null)
                    continue;

                foreach (var payslip in finalisedPayslips)
                    distinctEmployeeIdSet.Add(payslip.EmployeeId);
            }

            if (distinctEmployeeIdSet.Count == 0)
                return new List<RegisterEmployeeOption>();

            var distinctEmployeeIds = distinctEmployeeIdSet.ToArray();

            // Resolve names in ONE batched call. GetEmployeesByIdsAsync is used (not
            // GetActiveEmployeesForPeriodAsync) so terminated/inactive employees that still have an
            // in-range finalised payslip are retained — satisfying R3.1 / R8.5.
            var employees = await _payrollRepository.GetEmployeesByIdsAsync(distinctEmployeeIds, businessId);
            var nameLookup = employees.ToDictionary(e => e.Id, e => e.Name);

            // Project to lightweight options sorted alphabetically by name. If an employee row is
            // somehow missing, still include the Id with a best-effort name so counts stay consistent.
            var options = distinctEmployeeIds
                .Select(id => new RegisterEmployeeOption
                {
                    EmployeeId = id,
                    EmployeeName = nameLookup.GetValueOrDefault(id) ?? "Unknown"
                })
                .OrderBy(o => o.EmployeeName, StringComparer.Ordinal)
                .ToList();

            return options;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Resolves the Range_Periods for a FROM/TO month-year range (payroll-register-report R2.5/R2.6).
    /// Calls <c>GetPeriodsByBusinessAsync</c> ONCE, filters to the periods whose ordinal
    /// <c>Year*12 + Month</c> falls within <c>[fromYear*12+fromMonth .. toYear*12+toMonth]</c>, and
    /// orders them chronologically OLDEST -> NEWEST. The ordered list defines the Month_Columns for
    /// every detail sheet. Tenant-scoped by <paramref name="businessId"/> (R8.1).
    /// </summary>
    private async Task<List<PayslipPeriod>> ResolveRangePeriodsAsync(
        int fromYear, int fromMonth, int toYear, int toMonth, int businessId)
    {
        try
        {
            var fromOrdinal = fromYear * 12 + fromMonth;
            var toOrdinal = toYear * 12 + toMonth;

            var allPeriods = await _payrollRepository.GetPeriodsByBusinessAsync(businessId);

            var rangePeriods = allPeriods
                .Where(p =>
                {
                    var ordinal = p.Year * 12 + p.Month;
                    return ordinal >= fromOrdinal && ordinal <= toOrdinal;
                })
                .OrderBy(p => p.Year)
                .ThenBy(p => p.Month)
                .ToList();

            return rangePeriods;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Builds the finalised-payslip map for the whole range ONCE and returns both:
    ///  - a nested map employeeId -> (periodId -> Payslip): for each ordered Range_Period, the
    ///    finalised payslips are read once and grouped by employee. Where a single period yields more
    ///    than one finalised payslip for the same employee the payslips are summed into one synthetic
    ///    payslip for that (employee, period) so the pivot stays well-defined (no single-period
    ///    multiple-payslip guard is needed any more — see task 6), and
    ///  - a batched employee lookup (employeeId -> Employee) resolved via ONE GetEmployeesByIdsAsync
    ///    call over the distinct in-range employee Ids that have a finalised payslip.
    ///
    /// Names are deliberately resolved via GetEmployeesByIdsAsync (NOT GetActiveEmployeesForPeriodAsync),
    /// which filters IsActive = 1 AND EndDate >= periodStart and would silently drop a
    /// terminated/inactive employee who still has an in-range finalised payslip — breaking R3.1 / R8.5.
    /// </summary>
    private async Task<(Dictionary<int, Dictionary<int, Payslip>> payslipsByEmployeePeriod, Dictionary<int, Employee> employeesById)>
        BuildEmployeePayslipMapAsync(List<PayslipPeriod> rangePeriods, int businessId)
    {
        try
        {
            // employeeId -> (periodId -> Payslip). One finalised payslip per (employee, period); if a
            // period yields more than one for the same employee their monetary totals are summed.
            var payslipsByEmployeePeriod = new Dictionary<int, Dictionary<int, Payslip>>();

            foreach (var period in rangePeriods)
            {
                // Finalised = PayslipStatusTypeId IN (3,5), tenant-scoped via the PayslipPeriod
                // .BusinessId join inside GetFinalisedPayslipsForPeriodAsync. One call per period.
                var finalisedPayslips = await _payrollRepository.GetFinalisedPayslipsForPeriodAsync(period.Id, businessId);
                if (finalisedPayslips == null)
                    continue;

                foreach (var payslip in finalisedPayslips)
                {
                    if (!payslipsByEmployeePeriod.TryGetValue(payslip.EmployeeId, out var byPeriod))
                    {
                        byPeriod = new Dictionary<int, Payslip>();
                        payslipsByEmployeePeriod[payslip.EmployeeId] = byPeriod;
                    }

                    // Keep the first payslip seen for this (employee, period). A duplicate within the
                    // same period is accumulated at the LINE level in the pivot (task 3.3), which reads
                    // the line rows of every payslip for the (employee, period) pair; storing the first
                    // payslip here is sufficient to carry the PayslipPeriodId / stored totals, and the
                    // pivot sums the lines. For robustness against the rare duplicate, we keep the one
                    // with the greater Id (latest) so stored header totals are deterministic.
                    if (!byPeriod.TryGetValue(payslip.PayslipPeriodId, out var existing) || payslip.Id > existing.Id)
                        byPeriod[payslip.PayslipPeriodId] = payslip;
                }
            }

            // Resolve names/department ids in ONE batched call over the distinct in-range employee Ids
            // that have a finalised payslip. Not GetActiveEmployeesForPeriodAsync (see summary above).
            var distinctEmployeeIds = payslipsByEmployeePeriod.Keys.ToArray();
            var employeesById = new Dictionary<int, Employee>();
            if (distinctEmployeeIds.Length > 0)
            {
                var employees = await _payrollRepository.GetEmployeesByIdsAsync(distinctEmployeeIds, businessId);
                foreach (var employee in employees)
                {
                    employeesById[employee.Id] = employee;
                }
            }

            return (payslipsByEmployeePeriod, employeesById);
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// The shared reference lookups needed to assemble per-employee range aggregates, each loaded
    /// ONCE for the whole generation (N+1 avoidance, R14.3). Carried as a single value so the workbook
    /// orchestrator (task 7.1) can load once and pass the same set into every pivot without re-querying
    /// reference data per period or per employee.
    /// </summary>
    private sealed class RegisterReferenceLookups
    {
        public IReadOnlyDictionary<int, EarningType> EarningTypesById { get; init; } = new Dictionary<int, EarningType>();
        public IReadOnlyDictionary<int, DeductionType> DeductionTypesById { get; init; } = new Dictionary<int, DeductionType>();
        public IReadOnlyDictionary<int, string> DepartmentNamesById { get; init; } = new Dictionary<int, string>();
        public IReadOnlyDictionary<byte, string> StatusNames { get; init; } = new Dictionary<byte, string>();
    }

    /// <summary>
    /// Loads the shared reference lookups ONCE for the whole register generation (earning types,
    /// deduction types, department names, payslip-status names), so the per-employee pivot never
    /// re-queries reference data per period or per employee (N+1 avoidance, R14.3). The Range_Periods
    /// are already known from <see cref="ResolveRangePeriodsAsync"/>, so no per-period
    /// GetPeriodByIdAsync is performed here.
    /// </summary>
    private async Task<RegisterReferenceLookups> LoadRegisterReferenceLookupsAsync(int businessId)
    {
        try
        {
            var earningTypes = await _payrollRepository.GetAllEarningTypesAsync();
            var earningTypesById = earningTypes.ToDictionary(e => e.Id, e => e);

            var deductionTypes = await _payrollRepository.GetDeductionTypesByBusinessAsync(businessId);
            var deductionTypesById = deductionTypes.ToDictionary(d => d.Id, d => d);

            var departments = await _payrollRepository.GetDepartmentsByBusinessAsync(businessId);
            var departmentNamesById = departments.ToDictionary(d => d.Id, d => d.Name);

            var statusNames = await _payrollRepository.GetStatusNamesAsync();

            return new RegisterReferenceLookups
            {
                EarningTypesById = earningTypesById,
                DeductionTypesById = deductionTypesById,
                DepartmentNamesById = departmentNamesById,
                StatusNames = statusNames
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Pivots a single employee's finalised payslips across the ordered Range_Periods into a
    /// <see cref="RegisterEmployeeAggregate"/> — the single source for both the Summary row and the
    /// employee detail sheet (payroll-register-report R6, R13). Reads only the inherent per-payslip
    /// line rows (<c>GetEarningLinesByPayslipAsync</c> / <c>GetDeductionLinesByPayslipAsync</c>); all
    /// reference data comes from the PRE-LOADED <paramref name="refs"/> (N+1 avoidance, R14.3).
    ///
    /// For each section a dictionary <c>lineKey -&gt; RegisterLineRow</c> is built:
    ///  - Earnings are keyed by <c>EarningTypeId</c>; Percentage stays null (R6.6/R13.4).
    ///  - Employee deductions = raw lines with <c>DeductionCategoryTypeId == 1</c>, keyed by
    ///    <c>DeductionTypeId</c>; Employer contributions = <c>DeductionCategoryTypeId == 2</c>.
    ///
    /// CORRECTNESS: the employee-vs-employer split is performed on the RAW
    /// <see cref="PayslipDeductionLine"/> entity (its <c>DeductionCategoryTypeId</c>) BEFORE any
    /// mapping — never on a mapped DTO field, which is intentionally unset (0) and would misclassify
    /// every deduction into one bucket.
    ///
    /// Per line, <c>AmountByPeriodId[periodId] += lineAmount</c> (earnings use
    /// <c>PayslipEarningLine.Amount</c>; deductions/contributions use
    /// <c>PayslipDeductionLine.CalculatedAmount</c>); the row <c>Total</c> is the sum across periods.
    /// The displayed <c>Percentage</c> for a deduction/contribution line is the <c>Rate</c> from the
    /// line in the NEWEST Range_Period in which it appears (latest in-range rate, R13.2); earnings
    /// have no rate. All aggregate totals (TotalEarnings, TotalEmployeeDeductions, NetSalary,
    /// TotalEmployerContributions, TotalCost) are summed from the per-period payslip header totals,
    /// independently of the displayed % (R13.3).
    /// </summary>
    private async Task<RegisterEmployeeAggregate> AssembleEmployeeAggregateAsync(
        int employeeId,
        string employeeName,
        Dictionary<int, Payslip> periodPayslips,
        List<PayslipPeriod> rangePeriods,
        RegisterReferenceLookups refs)
    {
        try
        {
            // Per-section accumulation keyed by line identity (EarningTypeId / DeductionTypeId).
            var earningRows = new Dictionary<int, RegisterLineRow>();
            var employeeDeductionRows = new Dictionary<int, RegisterLineRow>();
            var employerContributionRows = new Dictionary<int, RegisterLineRow>();

            // Track, per deduction/contribution lineKey, the ordinal of the newest period in which it
            // appeared, so Percentage = the Rate from that latest in-range occurrence (R13.2).
            var employeeDeductionLatestOrdinal = new Dictionary<int, int>();
            var employerContributionLatestOrdinal = new Dictionary<int, int>();

            decimal totalEarnings = 0m;
            decimal totalEmployeeDeductions = 0m;
            decimal netSalary = 0m;
            decimal totalEmployerContributions = 0m;

            // Walk the Range_Periods oldest -> newest so "latest in-range" rate resolution is simple.
            foreach (var period in rangePeriods)
            {
                if (!periodPayslips.TryGetValue(period.Id, out var payslip))
                    continue; // employee has no finalised payslip for this period -> absent month

                var periodOrdinal = period.Year * 12 + period.Month;

                // Header totals summed across periods, independent of displayed % (R13.3).
                totalEarnings += payslip.TotalEarnings;
                totalEmployeeDeductions += payslip.TotalEmployeeDeductions;
                netSalary += payslip.NetSalary;
                totalEmployerContributions += payslip.TotalEmployerContributions;

                // The only per-payslip data access: inherent line rows for THIS payslip.
                var earningLines = await _payrollRepository.GetEarningLinesByPayslipAsync(payslip.Id);
                var deductionLines = await _payrollRepository.GetDeductionLinesByPayslipAsync(payslip.Id);

                // Earnings: keyed by EarningTypeId. Percentage null for earnings (R6.6/R13.4).
                foreach (var el in earningLines)
                {
                    if (!earningRows.TryGetValue(el.EarningTypeId, out var row))
                    {
                        var et = refs.EarningTypesById.GetValueOrDefault(el.EarningTypeId);
                        row = new RegisterLineRow
                        {
                            LineKey = el.EarningTypeId,
                            Name = et?.Name ?? "Unknown",
                            Percentage = null
                        };
                        earningRows[el.EarningTypeId] = row;
                    }

                    row.AmountByPeriodId.TryGetValue(period.Id, out var current);
                    row.AmountByPeriodId[period.Id] = current + el.Amount;
                    row.Total += el.Amount;
                }

                // CORRECTNESS: split on the RAW PayslipDeductionLine.DeductionCategoryTypeId (1 vs 2).
                foreach (var dl in deductionLines)
                {
                    if (dl.DeductionCategoryTypeId == 1)
                    {
                        AccumulateDeductionLine(
                            employeeDeductionRows, employeeDeductionLatestOrdinal,
                            dl, period, periodOrdinal, refs);
                    }
                    else if (dl.DeductionCategoryTypeId == 2)
                    {
                        AccumulateDeductionLine(
                            employerContributionRows, employerContributionLatestOrdinal,
                            dl, period, periodOrdinal, refs);
                    }
                }
            }

            return new RegisterEmployeeAggregate
            {
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                RangePeriods = rangePeriods,
                Earnings = earningRows.Values.ToList(),
                EmployeeDeductions = employeeDeductionRows.Values.ToList(),
                EmployerContributions = employerContributionRows.Values.ToList(),
                TotalEarnings = totalEarnings,
                TotalEmployeeDeductions = totalEmployeeDeductions,
                NetSalary = netSalary,
                TotalEmployerContributions = totalEmployerContributions,
                TotalCost = totalEarnings + totalEmployerContributions
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Accumulates a single raw <see cref="PayslipDeductionLine"/> into a section's pivot rows:
    /// adds the line's <c>CalculatedAmount</c> into the row's month bucket + Total, and updates the
    /// displayed <c>Percentage</c> to the line's <c>Rate</c> when this period is the newest one in
    /// which this lineKey has appeared so far (latest in-range rate, R13.2). Called with an empty-safe
    /// name fallback via the passed-in reference lookups.
    /// </summary>
    private static void AccumulateDeductionLine(
        Dictionary<int, RegisterLineRow> rows,
        Dictionary<int, int> latestOrdinalByLineKey,
        PayslipDeductionLine dl,
        PayslipPeriod period,
        int periodOrdinal,
        RegisterReferenceLookups refs)
    {
        if (!rows.TryGetValue(dl.DeductionTypeId, out var row))
        {
            var dt = refs.DeductionTypesById.GetValueOrDefault(dl.DeductionTypeId);
            row = new RegisterLineRow
            {
                LineKey = dl.DeductionTypeId,
                Name = dt?.Name ?? "Unknown",
                Percentage = dl.Rate
            };
            rows[dl.DeductionTypeId] = row;
            latestOrdinalByLineKey[dl.DeductionTypeId] = periodOrdinal;
        }

        row.AmountByPeriodId.TryGetValue(period.Id, out var current);
        row.AmountByPeriodId[period.Id] = current + dl.CalculatedAmount;
        row.Total += dl.CalculatedAmount;

        // Latest in-range rate: update % only when this period is newer than the last recorded one.
        if (latestOrdinalByLineKey.TryGetValue(dl.DeductionTypeId, out var recordedOrdinal))
        {
            if (periodOrdinal >= recordedOrdinal)
            {
                row.Percentage = dl.Rate;
                latestOrdinalByLineKey[dl.DeductionTypeId] = periodOrdinal;
            }
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

    // ---------------------------------------------------------------------
    // Payroll Register — per-employee detail sheet builder (payroll-register-report R6)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Writes ONE employee's detail worksheet into the supplied <paramref name="workbook"/> from a
    /// pre-assembled <see cref="RegisterEmployeeAggregate"/> (produced by
    /// <see cref="AssembleEmployeeAggregateAsync"/>). Called once per eligible selected employee by
    /// the workbook orchestrator (task 7.1); this helper owns ONLY the single worksheet — never the
    /// workbook lifecycle, the Summary sheet, or byte serialisation.
    ///
    /// Ledger layout (design.md "Per-employee detail sheet", R6):
    ///  - col 1 = line item name; cols 2..(1+N) = one Month_Column per Range_Period oldest→newest
    ///    (header = abbreviated month + year); col (2+N) = Total; col (3+N) = "%".
    ///  - Three sections (Earnings / Employee Deductions / Employer Contributions). Each section
    ///    renders a #0D5EA6/white-bold header spanning the used columns, then one row per
    ///    <see cref="RegisterLineRow"/>: the month cells read from <c>AmountByPeriodId</c> (an absent
    ///    month renders 0.00, R6.4); the Total cell = the row's <c>Total</c>; the "%" cell shows the
    ///    row's <c>Percentage</c> for deductions/contributions and is BLANK for earnings
    ///    (R6.5/R6.6/R13.4). Each section ends with a bold section-total row summing every
    ///    Month_Column and the Total column (R6.7). An empty section renders its header + a single
    ///    "No earnings" / "No employee deductions" / "No employer contributions" row + a 0.00 section
    ///    total (R6.9).
    ///  - Employee_Totals block (R6.8): Total Earnings, Total Employee Deductions, Net Salary, Total
    ///    Employer Contributions, Total Cost — bold, "#,##0.00".
    ///
    /// The employee-vs-employer split is NOT re-derived here: the two section lists are already
    /// correctly split by <see cref="AssembleEmployeeAggregateAsync"/> on the raw entity's
    /// <c>DeductionCategoryTypeId</c>; this helper simply renders each list.
    /// </summary>
    /// <param name="workbook">The existing workbook to add the worksheet into (owned by the caller).</param>
    /// <param name="aggregate">The pre-assembled per-employee range aggregate to render.</param>
    /// <param name="rangePeriods">The ordered Range_Periods (oldest→newest) that define the Month_Columns.</param>
    /// <param name="usedSheetNames">Worksheet names already used in the workbook; updated with the chosen name.</param>
    private void BuildEmployeeDetailSheet(
        ClosedXML.Excel.XLWorkbook workbook,
        RegisterEmployeeAggregate aggregate,
        List<PayslipPeriod> rangePeriods,
        ISet<string> usedSheetNames)
    {
        try
        {
            // 1. Sheet name from the employee name via the shared sanitizer (R7: ≤31 chars, prohibited
            //    chars stripped, unique within this workbook).
            var sheetName = SanitizeWorksheetName(aggregate.EmployeeName, usedSheetNames);
            var worksheet = workbook.Worksheets.Add(sheetName);

            const string moneyFormat = "#,##0.00";

            var periodCount = rangePeriods.Count;
            var totalCol = 2 + periodCount;     // column index of the Total column
            var percentCol = 3 + periodCount;   // column index of the "%" column
            var lastUsedCol = percentCol;

            // 2. Column header row (R6.3): name | one abbreviated month+year per period | Total | %.
            var headerRow = 1;
            worksheet.Cell(headerRow, 1).Value = "Line Item";
            for (var i = 0; i < periodCount; i++)
            {
                var period = rangePeriods[i];
                var monthAbbrev = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(
                    period.Month >= 1 && period.Month <= 12 ? period.Month : 1);
                worksheet.Cell(headerRow, 2 + i).Value = $"{monthAbbrev} {period.Year}";
            }
            worksheet.Cell(headerRow, totalCol).Value = "Total";
            worksheet.Cell(headerRow, percentCol).Value = "%";

            var columnHeaderRange = worksheet.Range(headerRow, 1, headerRow, lastUsedCol);
            columnHeaderRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#0D5EA6");
            columnHeaderRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            columnHeaderRange.Style.Font.Bold = true;

            var row = headerRow + 1;

            // Local helper: render one section (section header + rows + section total).
            void WriteSection(string sectionTitle, List<RegisterLineRow> lines, string emptyLabel, bool showPercentage)
            {
                // Section header spanning the used columns.
                var sectionHeaderRow = row;
                worksheet.Cell(sectionHeaderRow, 1).Value = sectionTitle;
                var sectionHeaderRange = worksheet.Range(sectionHeaderRow, 1, sectionHeaderRow, lastUsedCol);
                sectionHeaderRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#0D5EA6");
                sectionHeaderRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                sectionHeaderRange.Style.Font.Bold = true;
                row++;

                var monthTotals = new decimal[periodCount];
                decimal sectionTotal = 0m;

                if (lines == null || lines.Count == 0)
                {
                    worksheet.Cell(row, 1).Value = emptyLabel;
                    for (var i = 0; i < periodCount; i++)
                    {
                        worksheet.Cell(row, 2 + i).Value = 0m;
                        worksheet.Cell(row, 2 + i).Style.NumberFormat.Format = moneyFormat;
                    }
                    worksheet.Cell(row, totalCol).Value = 0m;
                    worksheet.Cell(row, totalCol).Style.NumberFormat.Format = moneyFormat;
                    row++;
                }
                else
                {
                    foreach (var line in lines)
                    {
                        worksheet.Cell(row, 1).Value = line.Name;
                        for (var i = 0; i < periodCount; i++)
                        {
                            var period = rangePeriods[i];
                            line.AmountByPeriodId.TryGetValue(period.Id, out var monthAmount); // absent -> 0.00
                            worksheet.Cell(row, 2 + i).Value = monthAmount;
                            worksheet.Cell(row, 2 + i).Style.NumberFormat.Format = moneyFormat;
                            monthTotals[i] += monthAmount;
                        }

                        worksheet.Cell(row, totalCol).Value = line.Total;
                        worksheet.Cell(row, totalCol).Style.NumberFormat.Format = moneyFormat;
                        sectionTotal += line.Total;

                        // "%" column: the latest in-range rate for deductions/contributions; blank for
                        // earnings (R6.5/R6.6/R13.4).
                        if (showPercentage && line.Percentage.HasValue)
                        {
                            worksheet.Cell(row, percentCol).Value = line.Percentage.Value;
                            worksheet.Cell(row, percentCol).Style.NumberFormat.Format = moneyFormat;
                        }

                        row++;
                    }
                }

                // Bold section-total row summing each Month_Column and the Total column (R6.7).
                worksheet.Cell(row, 1).Value = $"Total {sectionTitle}";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                for (var i = 0; i < periodCount; i++)
                {
                    worksheet.Cell(row, 2 + i).Value = monthTotals[i];
                    worksheet.Cell(row, 2 + i).Style.NumberFormat.Format = moneyFormat;
                    worksheet.Cell(row, 2 + i).Style.Font.Bold = true;
                }
                worksheet.Cell(row, totalCol).Value = sectionTotal;
                worksheet.Cell(row, totalCol).Style.NumberFormat.Format = moneyFormat;
                worksheet.Cell(row, totalCol).Style.Font.Bold = true;
                row += 2; // section total + spacer
            }

            // 3. Three sections.
            WriteSection("Earnings", aggregate.Earnings, "No earnings", showPercentage: false);
            WriteSection("Employee Deductions", aggregate.EmployeeDeductions, "No employee deductions", showPercentage: true);
            WriteSection("Employer Contributions", aggregate.EmployerContributions, "No employer contributions", showPercentage: true);

            // 4. Employee totals block (R6.8) — bold, "#,##0.00".
            void WriteTotal(string label, decimal value)
            {
                worksheet.Cell(row, 1).Value = label;
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Cell(row, totalCol).Value = value;
                worksheet.Cell(row, totalCol).Style.Font.Bold = true;
                worksheet.Cell(row, totalCol).Style.NumberFormat.Format = moneyFormat;
                row++;
            }

            WriteTotal("Total Earnings", aggregate.TotalEarnings);
            WriteTotal("Total Employee Deductions", aggregate.TotalEmployeeDeductions);
            WriteTotal("Net Salary", aggregate.NetSalary);
            WriteTotal("Total Employer Contributions", aggregate.TotalEmployerContributions);
            WriteTotal("Total Cost", aggregate.TotalCost);

            // 5. Auto-size columns.
            worksheet.Columns().AdjustToContents();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // ---------------------------------------------------------------------
    // Payroll Register — worksheet-name helper (payroll-register-report R7)
    // ---------------------------------------------------------------------

    /// <summary>
    /// The characters that are not permitted in an .xlsx worksheet name
    /// (backslash, forward slash, question mark, asterisk, open/close square
    /// brackets, colon). See Requirement 7.4.
    /// </summary>
    private static readonly char[] WorksheetProhibitedChars = { '\\', '/', '?', '*', '[', ']', ':' };

    /// <summary>
    /// The maximum length Excel allows for a worksheet name.
    /// </summary>
    private const int WorksheetNameMaxLength = 31;

    /// <summary>
    /// Sanitizes a raw name into a valid, unique .xlsx worksheet name for the
    /// Payroll Register workbook builder, and records the chosen name into
    /// <paramref name="usedNames"/> so subsequent calls stay unique within the
    /// same workbook (Requirement 7.1–7.4).
    ///
    /// Behavior:
    /// - Removes the .xlsx-prohibited characters (<c>\ / ? * [ ] :</c>).
    /// - Truncates the result to at most 31 characters (Excel's limit).
    /// - Guarantees uniqueness within the workbook: if the sanitized name
    ///   collides with one already recorded in <paramref name="usedNames"/>,
    ///   a numeric suffix starting at "2" is appended (" 2", " 3", ...),
    ///   trimming the base so the suffixed name stays ≤ 31 characters.
    /// - Never throws and never fails generation: an empty/whitespace result
    ///   falls back to a default base so a valid retained name is always
    ///   produced.
    ///
    /// The chosen (returned) name is added to <paramref name="usedNames"/>
    /// before returning, so callers simply invoke it once per sheet.
    /// </summary>
    /// <param name="rawName">The raw desired worksheet name (e.g. an employee name or a summary label).</param>
    /// <param name="usedNames">The set of worksheet names already used in the workbook. The chosen name is recorded here.</param>
    /// <returns>A valid, unique worksheet name (1–31 chars, no prohibited characters).</returns>
    private static string SanitizeWorksheetName(string rawName, ISet<string> usedNames)
    {
        var used = usedNames ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Remove the .xlsx-prohibited characters.
        var sanitizedBuilder = new System.Text.StringBuilder((rawName ?? string.Empty).Length);
        foreach (var ch in rawName ?? string.Empty)
        {
            if (Array.IndexOf(WorksheetProhibitedChars, ch) < 0)
                sanitizedBuilder.Append(ch);
        }

        var sanitized = sanitizedBuilder.ToString().Trim();

        // Fallback so we always have a valid, non-empty base to retain the sheet (R7.4).
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "Sheet";

        // 2. Truncate to the Excel limit.
        if (sanitized.Length > WorksheetNameMaxLength)
            sanitized = sanitized.Substring(0, WorksheetNameMaxLength);

        // 3. Ensure uniqueness within the workbook.
        var unique = EnsureUniqueWorksheetName(sanitized, used);

        used.Add(unique);
        return unique;
    }

    /// <summary>
    /// Produces a name unique within <paramref name="usedNames"/> by appending
    /// a numeric suffix starting at 2 and incrementing, trimming the base so the
    /// whole suffixed name stays ≤ 31 characters (Requirement 7.3).
    /// </summary>
    private static string EnsureUniqueWorksheetName(string baseName, ISet<string> usedNames)
    {
        if (!usedNames.Contains(baseName))
            return baseName;

        for (var suffix = 2; ; suffix++)
        {
            var suffixText = " " + suffix.ToString(CultureInfo.InvariantCulture);
            var maxBaseLength = WorksheetNameMaxLength - suffixText.Length;
            if (maxBaseLength < 0)
                maxBaseLength = 0;

            var trimmedBase = baseName.Length > maxBaseLength
                ? baseName.Substring(0, maxBaseLength)
                : baseName;

            var candidate = (trimmedBase + suffixText).Trim();

            // Guard against the degenerate case where trimming leaves nothing and
            // the suffix alone is needed; candidate is still ≤ 31 chars.
            if (candidate.Length == 0)
                candidate = suffix.ToString(CultureInfo.InvariantCulture);

            if (!usedNames.Contains(candidate))
                return candidate;
        }
    }

    /// <summary>
    /// Builds the single Payroll Register .xlsx workbook for the selected FROM/TO range and employees,
    /// scoped to the business (task 7.1). Stitches together the range helpers:
    ///   - <see cref="ResolveRangePeriodsAsync"/> (the ordered Range_Periods = Month_Columns),
    ///   - <see cref="BuildEmployeePayslipMapAsync"/> (finalised-payslip map + batched employee lookup),
    ///   - <see cref="LoadRegisterReferenceLookupsAsync"/> (reference data loaded ONCE, R14.3),
    ///   - <see cref="AssembleEmployeeAggregateAsync"/> (per-employee range pivot), and
    ///   - <see cref="BuildEmployeeDetailSheet"/> (one detail ledger worksheet per eligible employee).
    ///
    /// Eligibility (R9.3): an employee is eligible iff they have >= 1 finalised payslip in ANY
    /// Range_Period. Ineligible selections are omitted entirely (no Summary row, no detail sheet).
    /// Selection ORDER from <paramref name="employeeIds"/> is preserved and de-duplicated.
    ///
    /// Summary (C1, R5): one row per eligible employee aggregated across the range; one column per
    /// employer Contribution_Type (the union across the eligible selection, ordered by name — the
    /// DeductionType entity has no sort-order field); a bold grand-totals row summing every monetary
    /// column (including each contribution column), so each contribution's grand total appears on the
    /// first page.
    ///
    /// Decline paths (R2.6 / R4.7 / R8.2 / R9.5): a non-positive business id, a null/empty selection,
    /// a range that resolves to no Range_Period, or a selection where no employee is eligible all
    /// return <c>null</c> so the controller can respond with a JSON "no data" message. No partial
    /// workbook is persisted on any decline path.
    ///
    /// Sheet order (R5.1 / R7.5): the Summary sheet is physically FIRST; detail sheets follow in the
    /// supplied selection order.
    /// </summary>
    public async Task<RegisterData?> GetRegisterDataAsync(
        int fromYear, int fromMonth, int toYear, int toMonth, int businessId, int[] employeeIds)
    {
        try
        {
            // 1. Business-id guard (R8.2).
            if (businessId <= 0)
                return null;

            // 2. Null/empty selection guard (R4.2 defensive; controller also enforces).
            if (employeeIds == null || employeeIds.Length == 0)
                return null;

            // 3. Resolve the ordered Range_Periods (oldest -> newest). If the range resolves to no
            //    period -> decline so the controller returns a JSON message (R2.6).
            var rangePeriods = await ResolveRangePeriodsAsync(fromYear, fromMonth, toYear, toMonth, businessId);
            if (rangePeriods.Count == 0)
                return null;

            // 4. Load the finalised-payslip map and the shared reference lookups ONCE (N+1 avoidance).
            var (payslipsByEmployeePeriod, employeesById) = await BuildEmployeePayslipMapAsync(rangePeriods, businessId);
            var refs = await LoadRegisterReferenceLookupsAsync(businessId);

            // 5. Determine the ELIGIBLE selected employees, preserving the incoming order and
            //    de-duplicating. Eligible iff the map holds >= 1 in-range finalised payslip for the id.
            //    Build each RegisterEmployeeAggregate via the range pivot.
            var aggregates = new List<RegisterEmployeeAggregate>();
            var seen = new HashSet<int>();
            foreach (var id in employeeIds)
            {
                if (!seen.Add(id))
                    continue; // de-duplicate while preserving first-seen order

                if (!payslipsByEmployeePeriod.TryGetValue(id, out var periodPayslips) || periodPayslips.Count == 0)
                    continue; // R9.3: no in-range finalised payslip -> omit entirely

                var employeeName = employeesById.GetValueOrDefault(id)?.Name ?? "Unknown";
                var aggregate = await AssembleEmployeeAggregateAsync(id, employeeName, periodPayslips, rangePeriods, refs);
                aggregates.Add(aggregate);
            }

            // 6. If NO selected employee is eligible -> decline (R4.7 / R9.5). No data.
            if (aggregates.Count == 0)
                return null;

            return new RegisterData { RangePeriods = rangePeriods, Aggregates = aggregates };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<byte[]?> GenerateRegisterWorkbookAsync(
        int fromYear, int fromMonth, int toYear, int toMonth, int businessId, int[] employeeIds)
    {
        try
        {
            // 1-6. Resolve the shared data (guards, Range_Periods, payslip map + reference lookups,
            //       per-employee aggregates, decline paths) via the single GetRegisterDataAsync so the
            //       Download and Preview actions read the identical RegisterData (R15.12 / R12.6).
            var data = await GetRegisterDataAsync(fromYear, fromMonth, toYear, toMonth, businessId, employeeIds);
            if (data == null)
                return null; // same decline behavior as before (bad business id, empty selection,
                             // no Range_Period, no eligible aggregates).

            var rangePeriods = data.RangePeriods;
            var aggregates = data.Aggregates;

            // 7. Create the workbook and the shared used-name set.
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            ISet<string> usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            const string moneyFormat = "#,##0.00";

            // 8. Build the Contribution_Type column set: the UNION of employer-contribution
            //    DeductionTypeIds across all eligible aggregates, ordered by the deduction type's
            //    name (DeductionType has NO sort-order field, so name only — R5.3). A stable ordered
            //    list of (deductionTypeId, name).
            var contributionColumns = aggregates
                .SelectMany(a => a.EmployerContributions)
                .GroupBy(r => r.LineKey)
                .Select(g => new { DeductionTypeId = g.Key, Name = g.First().Name })
                .OrderBy(c => c.Name, StringComparer.Ordinal)
                .ToList();

            // 9. Build the SUMMARY sheet FIRST so it is physically at position 1 (R5.1 / R7.5).
            //    Name reflects the range: "Summary {FromMon}-{ToMon} {Year}" (R7.1 / R5.2).
            var fromAbbrev = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(
                fromMonth >= 1 && fromMonth <= 12 ? fromMonth : 1);
            var toAbbrev = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(
                toMonth >= 1 && toMonth <= 12 ? toMonth : 1);
            string summaryLabel = fromYear == toYear
                ? $"Summary {fromAbbrev}-{toAbbrev} {toYear}"
                : $"Summary {fromAbbrev}{fromYear}-{toAbbrev}{toYear}";

            var summarySheetName = SanitizeWorksheetName(summaryLabel, usedSheetNames);
            var summarySheet = workbook.Worksheets.Add(summarySheetName);

            // 10. Header row (fill #0D5EA6, white bold) — R5.2:
            //     Employee Name | Total Earnings | Total Employee Deductions | Net Salary |
            //     [one per contribution type] | Total Employer Contributions | Total Cost.
            summarySheet.Cell(1, 1).Value = "Employee Name";
            summarySheet.Cell(1, 2).Value = "Total Earnings";
            summarySheet.Cell(1, 3).Value = "Total Employee Deductions";
            summarySheet.Cell(1, 4).Value = "Net Salary";

            var firstContribCol = 5;
            for (var c = 0; c < contributionColumns.Count; c++)
                summarySheet.Cell(1, firstContribCol + c).Value = contributionColumns[c].Name;

            var totalContribCol = firstContribCol + contributionColumns.Count;   // Total Employer Contributions
            var totalCostCol = totalContribCol + 1;                               // Total Cost
            summarySheet.Cell(1, totalContribCol).Value = "Total Employer Contributions";
            summarySheet.Cell(1, totalCostCol).Value = "Total Cost";

            var summaryHeaderRange = summarySheet.Range(1, 1, 1, totalCostCol);
            summaryHeaderRange.Style.Font.Bold = true;
            summaryHeaderRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#0D5EA6");
            summaryHeaderRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

            // 11. One row per eligible aggregate (in supplied order). Each contribution cell = that
            //     aggregate's EmployerContributions row Total for that DeductionTypeId, else 0.00
            //     (R5.4 / R5.6). Accumulate grand totals over every monetary column (R5.7).
            var summaryRow = 2;
            decimal gtEarnings = 0m;
            decimal gtEmployeeDeductions = 0m;
            decimal gtNetSalary = 0m;
            decimal gtEmployerContributions = 0m;
            decimal gtTotalCost = 0m;
            var gtContrib = new decimal[contributionColumns.Count];

            foreach (var aggregate in aggregates)
            {
                summarySheet.Cell(summaryRow, 1).Value = aggregate.EmployeeName;
                summarySheet.Cell(summaryRow, 2).Value = aggregate.TotalEarnings;
                summarySheet.Cell(summaryRow, 3).Value = aggregate.TotalEmployeeDeductions;
                summarySheet.Cell(summaryRow, 4).Value = aggregate.NetSalary;

                var contribByKey = aggregate.EmployerContributions.ToDictionary(r => r.LineKey, r => r.Total);
                for (var c = 0; c < contributionColumns.Count; c++)
                {
                    contribByKey.TryGetValue(contributionColumns[c].DeductionTypeId, out var cellValue); // absent -> 0.00
                    summarySheet.Cell(summaryRow, firstContribCol + c).Value = cellValue;
                    gtContrib[c] += cellValue;
                }

                summarySheet.Cell(summaryRow, totalContribCol).Value = aggregate.TotalEmployerContributions;
                summarySheet.Cell(summaryRow, totalCostCol).Value = aggregate.TotalCost;

                for (int col = 2; col <= totalCostCol; col++)
                    summarySheet.Cell(summaryRow, col).Style.NumberFormat.Format = moneyFormat;

                gtEarnings += aggregate.TotalEarnings;
                gtEmployeeDeductions += aggregate.TotalEmployeeDeductions;
                gtNetSalary += aggregate.NetSalary;
                gtEmployerContributions += aggregate.TotalEmployerContributions;
                gtTotalCost += aggregate.TotalCost;

                summaryRow++;
            }

            // 12. Bold Grand_Totals_Row: arithmetic SUM of every monetary column, incl. each
            //     contribution column (R5.7).
            summarySheet.Cell(summaryRow, 1).Value = "TOTALS";
            summarySheet.Cell(summaryRow, 2).Value = gtEarnings;
            summarySheet.Cell(summaryRow, 3).Value = gtEmployeeDeductions;
            summarySheet.Cell(summaryRow, 4).Value = gtNetSalary;
            for (var c = 0; c < contributionColumns.Count; c++)
                summarySheet.Cell(summaryRow, firstContribCol + c).Value = gtContrib[c];
            summarySheet.Cell(summaryRow, totalContribCol).Value = gtEmployerContributions;
            summarySheet.Cell(summaryRow, totalCostCol).Value = gtTotalCost;

            var totalsRange = summarySheet.Range(summaryRow, 1, summaryRow, totalCostCol);
            totalsRange.Style.Font.Bold = true;
            for (int col = 2; col <= totalCostCol; col++)
                summarySheet.Cell(summaryRow, col).Style.NumberFormat.Format = moneyFormat;

            // 13. Fit the Summary sheet.
            summarySheet.Columns().AdjustToContents();

            // 14. Append detail sheets AFTER the Summary (Summary stays at position 1), in order,
            //     sharing the one used-name set seeded with the Summary name.
            foreach (var aggregate in aggregates)
            {
                BuildEmployeeDetailSheet(workbook, aggregate, rangePeriods, usedSheetNames);
            }

            // 15. Serialize and return. No partial workbook is persisted anywhere.
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
