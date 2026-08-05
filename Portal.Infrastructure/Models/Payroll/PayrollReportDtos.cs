namespace Portal.Infrastructure.Models.Payroll;

// --- Employee History ---
public class EmployeePayslipHistoryDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int? FilteredYear { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public List<PayslipHistoryItemDto> Payslips { get; set; } = new();
    public decimal SummaryTotalGross { get; set; }
    public decimal SummaryTotalNet { get; set; }
    public int SummaryCount { get; set; }
}

public class PayslipHistoryItemDto
{
    public int PayslipId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal NetSalary { get; set; }
    public string Status { get; set; } = string.Empty;
}

// --- Annual Summary ---
public class AnnualSummaryDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public List<int> AvailableYears { get; set; } = new();
    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalContributions { get; set; }
    public List<MonthlySummaryRow> MonthlyBreakdown { get; set; } = new();
    public List<DeductionSummaryRow> DeductionBreakdown { get; set; } = new();
    public List<DeductionSummaryRow> ContributionBreakdown { get; set; } = new();
    public List<EarningSummaryRow> EarningsBreakdown { get; set; } = new();
}

/// <summary>
/// Monthly breakdown row for annual summary reports.
/// </summary>
public class MonthlySummaryRow
{
    public int Month { get; set; }
    public decimal Gross { get; set; }
    public decimal Deductions { get; set; }
    public decimal Net { get; set; }
    public decimal Contributions { get; set; }
}

/// <summary>
/// Deduction/contribution summary row for annual reports.
/// </summary>
public class DeductionSummaryRow
{
    public string DeductionName { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal TotalAmount { get; set; }
    public int MonthsApplied { get; set; }
}

/// <summary>
/// Earning type summary row for annual reports.
/// </summary>
public class EarningSummaryRow
{
    public string EarningTypeName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal Percentage { get; set; }
}

// --- Earnings Breakdown Report ---
public class EarningsBreakdownFilter
{
    public int? FromYear { get; set; }
    public int? FromMonth { get; set; }
    public int? ToYear { get; set; }
    public int? ToMonth { get; set; }
    public int? EmployeeId { get; set; }
    public List<int>? EarningTypeIds { get; set; }
}

public class EarningsBreakdownDto
{
    public List<EarningTypeSummaryRow> TypeSummaries { get; set; } = new();
    public List<EarningDetailRow> Details { get; set; } = new();
    public EarningsBreakdownFilter AppliedFilter { get; set; } = new();
}

public class EarningTypeSummaryRow
{
    public int EarningTypeId { get; set; }
    public string EarningTypeName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int LineCount { get; set; }
}

public class EarningDetailRow
{
    public string EmployeeName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public string EarningTypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? Hours { get; set; }
    public decimal? Multiplier { get; set; }
    public decimal Amount { get; set; }
}

// --- Period Summary ---
public class PeriodSummaryDto
{
    public int PeriodId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int? DepartmentFilter { get; set; }
    public List<PeriodSummaryRow> Rows { get; set; } = new();
    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalContributions { get; set; }
    public decimal TotalCost { get; set; }
}

public class PeriodSummaryRow
{
    public string EmployeeName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public decimal EmployerContributions { get; set; }
    public decimal TotalCost { get; set; }
}

// --- Email Log ---
public class PayslipEmailLogDto
{
    public int Id { get; set; }
    public int PayslipId { get; set; }
    public string SentByUserName { get; set; } = string.Empty;
    public string SentToEmail { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public bool IsSuccess { get; set; }
    public string? FailureReason { get; set; }
}

public class PayslipEmailSummaryDto
{
    public int TotalSent { get; set; }
    public int TotalSuccessful { get; set; }
    public int TotalFailed { get; set; }
}
