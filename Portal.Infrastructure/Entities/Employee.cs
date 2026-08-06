namespace Portal.Infrastructure.Entities;

public class Employee
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int? DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string SocialInsuranceNumber { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public byte SalaryTypeId { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal? HourlyRate { get; set; }
    public string? BankAccount { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPayeApplicable { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
