namespace Portal.Infrastructure.Models.Payroll;

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int EmployeeCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateDepartmentRequest
{
    public string Name { get; set; } = string.Empty;
}

public class UpdateDepartmentRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
