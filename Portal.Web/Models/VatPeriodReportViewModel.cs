namespace Portal.Web.Models;

public class VatPeriodReportViewModel
{
    public string PeriodLabel { get; set; } = null!;
    public DateOnly PeriodStartDate { get; set; }
    public DateOnly PeriodEndDate { get; set; }
    public int PeriodId { get; set; }
    public string CurrencySymbol { get; set; } = "€";

    // Summary
    public decimal OutputVat { get; set; }
    public decimal InputVat { get; set; }
    public decimal TaxOwed { get; set; }

    // Section 1: Sales by month
    public List<MonthlyAmountRow> SalesByMonth { get; set; } = new();

    // Section 1b: External Revenue (Z-Reports)
    public List<ZReportPeriodRow> ZReportRows { get; set; } = new();
    public bool IsZReportEnabled { get; set; }

    // Section 2: Purchases by month
    public List<MonthlyAmountRow> PurchasesByMonth { get; set; } = new();

    // Section 3: Purchases by origin per month
    public List<MonthlyOriginRow> PurchasesByOriginPerMonth { get; set; } = new();

    // Expense type lookup for section 3 sub-rows (loaded dynamically from DB)
    public List<ExpenseTypeLookup> ExpenseTypes { get; set; } = new();

    // Section 4: Period totals by origin
    public List<OriginTotalRow> PeriodTotalsByOrigin { get; set; } = new();
}

public class MonthlyAmountRow
{
    public string MonthName { get; set; } = null!;
    public decimal Net { get; set; }
    public decimal Vat { get; set; }
    public decimal Gross { get; set; }
    public int Count { get; set; }
}

public class MonthlyOriginRow
{
    public string MonthName { get; set; } = null!;
    public decimal Domestic { get; set; }
    public decimal EuReverseCharge { get; set; }
    public decimal NonEu { get; set; }
    public decimal Total { get; set; }

    /// <summary>
    /// Dynamic expense type breakdown per origin.
    /// Key = ExpenseTypeId, Value = amounts per origin type.
    /// </summary>
    public List<OriginExpenseTypeRow> ExpenseTypeRows { get; set; } = new();
}

/// <summary>
/// One row of the expense type sub-breakdown for a given month.
/// Contains the expense type name and its amount per origin column.
/// </summary>
public class OriginExpenseTypeRow
{
    public int ExpenseTypeId { get; set; }
    public string ExpenseTypeName { get; set; } = null!;
    public decimal Domestic { get; set; }
    public decimal EuReverseCharge { get; set; }
    public decimal NonEu { get; set; }
}

/// <summary>
/// Lookup record for expense types used in the report.
/// </summary>
public class ExpenseTypeLookup
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}

public class OriginTotalRow
{
    public string OriginName { get; set; } = null!;
    public decimal Net { get; set; }
    public decimal Vat { get; set; }
    public decimal Gross { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// One Z-Report row for the VAT Period Report section.
/// </summary>
public class ZReportPeriodRow
{
    public string SourceName { get; set; } = null!;
    public string? ZReportNumber { get; set; }
    public string PeriodDisplay { get; set; } = null!;
    public decimal Net { get; set; }
    public decimal Vat { get; set; }
    public decimal Gross { get; set; }
    public decimal? Discount { get; set; }
}
