namespace Portal.Infrastructure.Models.Payroll;

public class PayslipPeriodDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ProcessedAtUtc { get; set; }
    public int PayslipCount { get; set; }
    public decimal TotalNetSalary { get; set; }
}

public class PayslipPeriodDetailDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ProcessedAtUtc { get; set; }
    public List<PayslipSummaryDto> Payslips { get; set; } = new();
}

public class CreatePeriodRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
}
