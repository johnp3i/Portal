namespace Portal.Infrastructure.Models.Payroll;

public class SalaryRegisterRow
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string SalaryType { get; set; } = string.Empty;
    public decimal BaseSalary { get; set; }
    public decimal? HourlyRate { get; set; }
    public bool IsActive { get; set; }
}

public class SalaryRegisterViewModel
{
    public List<SalaryRegisterRow> Employees { get; set; } = new();
    public List<DepartmentDto> Departments { get; set; } = new();
    public int? SelectedDepartmentId { get; set; }
    public bool? SelectedIsActive { get; set; }
    public int TotalEmployees { get; set; }
    public decimal TotalMonthlyPayroll { get; set; }
}

public class UpdateBaseSalaryRequest
{
    public int EmployeeId { get; set; }
    public decimal NewSalary { get; set; }
}
