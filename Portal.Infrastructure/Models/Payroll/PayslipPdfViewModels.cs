namespace Portal.Infrastructure.Models.Payroll;

public class PayslipPdfViewModel
{
    public PayslipDetailDto Payslip { get; set; } = null!;
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessAddress { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public bool IncludeSignature { get; set; }
}

public class AnnualSummaryPdfViewModel
{
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeSin { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public int Year { get; set; }
    public List<MonthlySummaryRow> MonthlyBreakdown { get; set; } = new();
    public List<DeductionSummaryRow> DeductionBreakdown { get; set; } = new();
    public List<DeductionSummaryRow> ContributionBreakdown { get; set; } = new();
    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalContributions { get; set; }
}

public class EmployeeStatementPdfViewModel
{
    public string EmployeeName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? SocialInsuranceNumber { get; set; }
    public string? IdNumber { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string BusinessAddress { get; set; } = string.Empty;
    public string PeriodFrom { get; set; } = string.Empty;
    public string PeriodTo { get; set; } = string.Empty;
    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public decimal TotalContributions { get; set; }
    public List<PayslipDetailDto> Payslips { get; set; } = new();
}
