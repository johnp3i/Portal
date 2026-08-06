namespace Portal.Infrastructure.Models.Payroll;

/// <summary>
/// DTO for PAYE tax band display and management.
/// </summary>
public class PayeTaxBandDto
{
    public int Id { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public decimal Rate { get; set; }
    public int EffectiveFromYear { get; set; }
    public int? EffectiveToYear { get; set; }
}

/// <summary>
/// DTO for country deduction template display and management.
/// </summary>
public class CountryDeductionTemplateDto
{
    public int Id { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string DeductionName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsPercentage { get; set; }
    public byte DeductionCategoryTypeId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DefaultRate { get; set; }
    public bool IsPayeDeductible { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for the employer contribution report (summary level).
/// </summary>
public class ContributionReportDto
{
    public int PeriodId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public List<ContributionTypeSummary> TypeSummaries { get; set; } = new();
    public List<EmployeeContributionDetail> EmployeeDetails { get; set; } = new();
    public decimal GrandTotal { get; set; }
    public ComplianceFilingLinkDto? ComplianceFiling { get; set; }
}

/// <summary>
/// Summary for a single contribution type (e.g., SI Employer total).
/// </summary>
public class ContributionTypeSummary
{
    public string DeductionTypeName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

/// <summary>
/// Per-employee contribution breakdown row.
/// </summary>
public class EmployeeContributionDetail
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public List<ContributionLineItem> Contributions { get; set; } = new();
    public decimal EmployeeTotal { get; set; }
}

/// <summary>
/// Single contribution line for an employee.
/// </summary>
public class ContributionLineItem
{
    public string DeductionTypeName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>
/// Link to a compliance filing record.
/// </summary>
public class ComplianceFilingLinkDto
{
    public int FilingId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public decimal? EstimatedAmount { get; set; }
}

/// <summary>
/// DTO for payslip period compliance filing cross-reference display.
/// </summary>
public class PayslipPeriodComplianceFilingDto
{
    public int Id { get; set; }
    public int PayslipPeriodId { get; set; }
    public int ComplianceFilingId { get; set; }
    public decimal ContributionTotal { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string UpdatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}


/// <summary>
/// Raw row returned from GetEmployerContributionsForPeriodAsync repository method.
/// Internal model for building ContributionReportDto.
/// </summary>
public class EmployerContributionRow
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string DeductionTypeName { get; set; } = string.Empty;
    public string DeductionTypeCode { get; set; } = string.Empty;
    public decimal CalculatedAmount { get; set; }
}
