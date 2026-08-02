namespace Portal.Infrastructure.Entities;

public class Payslip
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int PayslipPeriodId { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal TotalEmployeeDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public decimal TotalEmployerContributions { get; set; }
    public string? ManagerNotes { get; set; }
    public byte PayslipStatusTypeId { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
}
