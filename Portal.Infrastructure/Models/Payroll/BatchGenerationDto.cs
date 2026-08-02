namespace Portal.Infrastructure.Models.Payroll;

public class BatchGenerationPreview
{
    public int PeriodId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public List<PayslipPreviewDto> Payslips { get; set; } = new();
    public List<BatchValidationError> Errors { get; set; } = new();
    public decimal TotalPayrollCost { get; set; }
    public decimal TotalEmployerContributions { get; set; }
    public int TotalEmployeesProcessed { get; set; }
    public int TotalEmployeesExcluded { get; set; }
}

public class PayslipPreviewDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalEmployeeDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public decimal TotalEmployerContributions { get; set; }
    public List<EarningLineDto> EarningLines { get; set; } = new();
    public List<DeductionLineDto> DeductionLines { get; set; } = new();
}

public class BatchValidationError
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
