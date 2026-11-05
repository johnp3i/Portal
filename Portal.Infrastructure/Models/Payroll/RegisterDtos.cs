using Portal.Infrastructure.Entities;

namespace Portal.Infrastructure.Models.Payroll;

/// <summary>
/// Lightweight selector option for the Payroll Register employee picker.
/// <see cref="EmployeeId"/> is the reliable key passed to the generate endpoint.
/// </summary>
public class RegisterEmployeeOption
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}

/// <summary>
/// One pivoted row within a section (Earnings, Employee Deductions, or Employer Contributions):
/// a single line item with its per-period amounts, its across-range total, and the latest
/// in-range rate ("%"). The rate is <c>null</c> for earnings.
/// </summary>
public class RegisterLineRow
{
    /// <summary>EarningTypeId or DeductionTypeId identifying this line item.</summary>
    public int LineKey { get; set; }

    /// <summary>Display name of the earning/deduction type.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Per-period amounts keyed by periodId (periodId → amount).</summary>
    public Dictionary<int, decimal> AmountByPeriodId { get; set; } = new();

    /// <summary>Sum of the amounts across all Range_Periods.</summary>
    public decimal Total { get; set; }

    /// <summary>Latest in-range Rate shown as a percentage; <c>null</c> for earnings.</summary>
    public decimal? Percentage { get; set; }
}

/// <summary>
/// In-memory range aggregate for a single employee — the single source of truth for both the
/// Summary row and the detail sheet. Built once per generation by pivoting each section's lines
/// across the Range_Periods.
/// </summary>
public class RegisterEmployeeAggregate
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>
    /// The ordered Range_Periods (oldest → newest) that define the Month_Columns.
    /// </summary>
    public List<PayslipPeriod> RangePeriods { get; set; } = new();

    /// <summary>Earning line rows. Percentage is blank/null for earnings.</summary>
    public List<RegisterLineRow> Earnings { get; set; } = new();

    /// <summary>Employee deduction line rows (DeductionCategoryTypeId = 1).</summary>
    public List<RegisterLineRow> EmployeeDeductions { get; set; } = new();

    /// <summary>Employer contribution line rows (DeductionCategoryTypeId = 2).</summary>
    public List<RegisterLineRow> EmployerContributions { get; set; } = new();

    // Across-range Employee_Totals (independent of the displayed %):

    /// <summary>Total earnings summed across the range.</summary>
    public decimal TotalEarnings { get; set; }

    /// <summary>Total employee deductions summed across the range.</summary>
    public decimal TotalEmployeeDeductions { get; set; }

    /// <summary>Net salary summed across the range (TotalEarnings − TotalEmployeeDeductions).</summary>
    public decimal NetSalary { get; set; }

    /// <summary>Total employer contributions summed across the range.</summary>
    public decimal TotalEmployerContributions { get; set; }

    /// <summary>
    /// Total cost to the employer (TotalEarnings + TotalEmployerContributions).
    /// Left as a plain settable property; the service sets it.
    /// </summary>
    public decimal TotalCost { get; set; }
}

/// <summary>
/// The result of <c>GetRegisterDataAsync</c>: the ordered Range_Periods and the per-employee
/// aggregates. The workbook (Download) is built from these; the preview JSON (Preview) is projected
/// from these — so both actions agree cell-for-cell and can never drift (R15.12, R12.6).
/// <see cref="RangePeriods"/> is ordered oldest → newest; <see cref="Aggregates"/> is in selection
/// order, eligible employees only. A non-null <see cref="RegisterData"/> always carries at least one
/// aggregate.
/// </summary>
public class RegisterData
{
    /// <summary>The ordered Range_Periods (oldest → newest) that define the Month_Columns.</summary>
    public List<PayslipPeriod> RangePeriods { get; set; } = new();

    /// <summary>The per-employee aggregates in selection order (eligible employees only).</summary>
    public List<RegisterEmployeeAggregate> Aggregates { get; set; } = new();
}
