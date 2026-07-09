namespace Portal.Infrastructure.Models;

// === Raw Row Model ===

/// <summary>
/// Flat row returned by the overview query — one row per instalment.
/// Grouped in-memory by ScheduleId to build the overview.
/// </summary>
public class ScheduleOverviewRawRow
{
    public int ScheduleId { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public int CustomerId { get; set; }
    public int InstalmentId { get; set; }
    public decimal Amount { get; set; }
    public decimal MatchedAmount { get; set; }
    public DateOnly? DueDate { get; set; }
    public int SequenceNumber { get; set; }
}

// === Response DTOs ===

/// <summary>
/// Top-level response DTO for the Payment Schedules Overview page.
/// Contains all data needed by the JS module to render KPIs, timeline, and table.
/// </summary>
public class PaymentScheduleOverviewDto
{
    public OverviewKpiDto Kpis { get; set; } = new();
    public List<MonthlyTimelineEntryDto> Timeline { get; set; } = new();
    public List<ScheduleTableRowDto> Schedules { get; set; } = new();
    public List<int> AvailableYears { get; set; } = new();
    public string CurrencySymbol { get; set; } = "€";
}

/// <summary>
/// KPI summary metrics for the overview page.
/// </summary>
public class OverviewKpiDto
{
    public decimal TotalScheduled { get; set; }
    public decimal Collected { get; set; }
    public decimal DueThisMonth { get; set; }
    public decimal Overdue { get; set; }
}

/// <summary>
/// A single month row in the monthly payment timeline.
/// </summary>
public class MonthlyTimelineEntryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int InstalmentCount { get; set; }
    public bool HasOverdue { get; set; }
    public bool IsNoDueDate { get; set; }
}

/// <summary>
/// A single row in the Active Schedules table.
/// </summary>
public class ScheduleTableRowDto
{
    public int ScheduleId { get; set; }
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public decimal ScheduleTotal { get; set; }
    public decimal Paid { get; set; }
    public decimal Remaining { get; set; }
    public string? NextDue { get; set; }
    public int ProgressPercentage { get; set; }
    public string Status { get; set; } = null!;
}
