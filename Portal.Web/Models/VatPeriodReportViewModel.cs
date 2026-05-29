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

    // Section 2: Purchases by month
    public List<MonthlyAmountRow> PurchasesByMonth { get; set; } = new();

    // Section 3: Purchases by origin per month
    public List<MonthlyOriginRow> PurchasesByOriginPerMonth { get; set; } = new();

    // Section 4: Period totals by origin
    public List<OriginTotalRow> PeriodTotalsByOrigin { get; set; } = new();
}

public class MonthlyAmountRow
{
    public string MonthName { get; set; } = null!; // e.g. "March 2024"
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
}

public class OriginTotalRow
{
    public string OriginName { get; set; } = null!;
    public decimal Net { get; set; }
    public decimal Vat { get; set; }
    public decimal Gross { get; set; }
    public int Count { get; set; }
}
