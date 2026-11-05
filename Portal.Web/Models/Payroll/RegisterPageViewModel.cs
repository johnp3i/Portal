using Portal.Infrastructure.Models.Payroll;

namespace Portal.Web.Models.Payroll;

/// <summary>
/// View model for the Payroll Register selection page. Supplies the FROM/TO
/// month-year range defaults for the range selector, reusing the existing
/// <see cref="PayslipPeriodDto"/> for the period options and
/// <see cref="RegisterEmployeeOption"/> for the eligible-employee selector.
/// The default TO is the latest finalised period (year + month) and the default
/// FROM is January of that same year.
/// </summary>
public class RegisterPageViewModel
{
    public List<PayslipPeriodDto> Periods { get; set; } = new();
    public int? DefaultToYear { get; set; }
    public int? DefaultToMonth { get; set; }
    public int? DefaultFromYear { get; set; }
    public int DefaultFromMonth { get; set; } = 1;
    public List<RegisterEmployeeOption> EligibleEmployees { get; set; } = new();
    public bool HasFinalisedPeriod { get; set; }
}
