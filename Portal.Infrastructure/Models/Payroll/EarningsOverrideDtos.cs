namespace Portal.Infrastructure.Models.Payroll;

public class EarningLineOverride
{
    public int EarningTypeId { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public decimal? OvertimeMultiplier { get; set; }
    public decimal? OvertimeHours { get; set; }
}

public class RecalculateEmployeeRequest
{
    public int EmployeeId { get; set; }
    public int PeriodId { get; set; }
    public List<EarningLineOverride> EarningLines { get; set; } = new();
}

public class RecalculationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalEmployeeDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public decimal TotalEmployerContributions { get; set; }
}

public class EmployeeEarningsOverride
{
    public int EmployeeId { get; set; }
    public List<EarningLineOverride> EarningLines { get; set; } = new();
}

public class ConfirmBatchWithOverridesRequest
{
    public int PeriodId { get; set; }
    public List<EmployeeEarningsOverride> Overrides { get; set; } = new();
}
