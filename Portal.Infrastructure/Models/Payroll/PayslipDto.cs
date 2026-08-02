namespace Portal.Infrastructure.Models.Payroll;

public class PayslipSummaryDto
{
    public int Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalEmployeeDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public decimal TotalEmployerContributions { get; set; }
}

public class PayslipDetailDto
{
    public int Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeePosition { get; set; }
    public string? DepartmentName { get; set; }
    public string? EmployeeEmail { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string PeriodStatus { get; set; } = string.Empty;
    public decimal TotalEarnings { get; set; }
    public decimal TotalEmployeeDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public decimal TotalEmployerContributions { get; set; }
    public decimal TotalCostToBusiness => TotalEarnings + TotalEmployerContributions;
    public string? ManagerNotes { get; set; }
    public List<EarningLineDto> EarningLines { get; set; } = new();
    public List<DeductionLineDto> EmployeeDeductions { get; set; } = new();
    public List<DeductionLineDto> EmployerContributions { get; set; } = new();
}

public class EarningLineDto
{
    public int Id { get; set; }
    public string EarningTypeName { get; set; } = string.Empty;
    public string EarningTypeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal? OvertimeMultiplier { get; set; }
    public decimal? OvertimeHours { get; set; }
}

public class DeductionLineDto
{
    public int Id { get; set; }
    public string DeductionTypeName { get; set; } = string.Empty;
    public decimal BaseAmount { get; set; }
    public decimal Rate { get; set; }
    public decimal CalculatedAmount { get; set; }
}

public class SaveEarningLinesRequest
{
    public int PayslipId { get; set; }
    public List<EarningLineInput> Lines { get; set; } = new();
}

public class SaveManagerNotesRequest
{
    public int PayslipId { get; set; }
    public string? Notes { get; set; }
}
