namespace Portal.Infrastructure.Models.Payroll;

public class EmployeeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? DepartmentName { get; set; }
    public string SalaryTypeName { get; set; } = string.Empty;
    public decimal BaseSalary { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartDate { get; set; }
}

public class EmployeeDetailDto
{
    public int Id { get; set; }
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
    public bool IsActive { get; set; }
}

public class CreateEmployeeRequest
{
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
}

public class UpdateEmployeeRequest : CreateEmployeeRequest
{
    public int Id { get; set; }
}
