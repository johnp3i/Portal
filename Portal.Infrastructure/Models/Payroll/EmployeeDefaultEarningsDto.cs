namespace Portal.Infrastructure.Models.Payroll;

public class EmployeeDefaultEarningsDto
{
    public int Id { get; set; }
    public int EarningTypeId { get; set; }
    public string EarningTypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public decimal? OvertimeMultiplier { get; set; }
    public decimal? OvertimeHours { get; set; }
}

public class EmployeeDefaultEarningInput
{
    public int EarningTypeId { get; set; }
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public decimal? OvertimeMultiplier { get; set; }
    public decimal? OvertimeHours { get; set; }
}
