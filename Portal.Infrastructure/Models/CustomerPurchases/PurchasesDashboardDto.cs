namespace Portal.Infrastructure.Models;

/// <summary>
/// Complete response for the Customer Purchases dashboard. Serialised with <see cref="PurchasesJson.Options"/>.
/// </summary>
public sealed class PurchasesDashboardDto
{
    public int CustomerId { get; init; }
    public string CustomerName { get; init; } = "";
    public bool HasAnyInvoices { get; init; }
    public PeriodDto Period { get; init; } = new();
    /// <summary>Distinct currencies, ordinal ascending.</summary>
    public List<string> Currencies { get; init; } = new();
    public string? SelectedCurrency { get; init; }
    /// <summary>True when <see cref="Currencies"/> has more than one entry.</summary>
    public bool ShowCurrencySwitch { get; init; }
    public KpiDto Kpis { get; init; } = new();
    /// <summary>Always 12 items, oldest first.</summary>
    public List<TrendMonthDto> Trend { get; init; } = new();
    public ProductMixDto ProductMix { get; init; } = new();
    public List<DonutSliceDto> Donut { get; init; } = new();
    public BehaviourDto Behaviour { get; init; } = new();
    public List<PendingInvoiceDto> PendingInvoices { get; init; } = new();
    public decimal PendingTotal { get; init; }
}

/// <summary>Resolved period (inclusive). From/To are null for "all".</summary>
public sealed class PeriodDto
{
    public string Key { get; init; } = PurchasesQuery.PeriodLast12Months;
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class KpiDto
{
    public Rev12Dto Rev12 { get; init; } = new();
    public LifetimeDto Lifetime { get; init; } = new();
    public AvgInvoiceDto AvgInvoice { get; init; } = new();
    public LastPurchaseDto Last { get; init; } = new();
    public PendingKpiDto Pending { get; init; } = new();
    public OverdueKpiDto Overdue { get; init; } = new();
}

public sealed class Rev12Dto
{
    public decimal Value { get; init; }
    public decimal Comparison { get; init; }
    public bool HasPrior { get; init; }
    public decimal? ChangePercent { get; init; }
    /// <summary>"up" | "down" | "flat" | "none"</summary>
    public string Direction { get; init; } = "none";
}

public sealed class LifetimeDto
{
    public decimal Value { get; init; }
    public int InvoiceCount { get; init; }
    public DateOnly? FirstInvoiceDate { get; init; }
}

public sealed class AvgInvoiceDto
{
    public decimal? Value { get; init; }
    public int InvoiceCount { get; init; }
}

public sealed class LastPurchaseDto
{
    public DateOnly? Date { get; init; }
    /// <summary>0 means "Today".</summary>
    public int? DaysAgo { get; init; }
    public int? FrequencyDays { get; init; }
}

public sealed class PendingKpiDto
{
    public int Count { get; init; }
    public decimal Amount { get; init; }
}

public sealed class OverdueKpiDto
{
    public int Count { get; init; }
    public decimal Amount { get; init; }
    public int? DaysLate { get; init; }
}

public sealed class TrendMonthDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public bool IsCurrent { get; init; }
    public decimal Revenue { get; init; }
    public int InvoiceCount { get; init; }
    public decimal PreviousYearRevenue { get; init; }
}

public sealed class ProductMixDto
{
    public List<ProductRowDto> Rows { get; init; } = new();
    public decimal TotalRevenue { get; init; }
    /// <summary>Null means "-".</summary>
    public decimal? MarginCoveragePercent { get; init; }
}

public sealed class ProductRowDto
{
    /// <summary>"C:&lt;UPPER code&gt;" or "D:&lt;UPPER description&gt;"; opaque to the client.</summary>
    public string GroupKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Code { get; init; }
    public bool IsCustomItem { get; init; }
    public decimal Quantity { get; init; }
    public decimal Revenue { get; init; }
    public decimal SharePercent { get; init; }
    /// <summary>Null means "-".</summary>
    public decimal? AveragePricePaid { get; init; }
    public decimal? ListPrice { get; init; }
    public decimal? PriceDifferencePercent { get; init; }
    public DateOnly LastBought { get; init; }
    public decimal? MarginPercent { get; init; }
    public bool IsMarginPartial { get; init; }
}

public sealed class DonutSliceDto
{
    public string Label { get; init; } = "";
    public decimal Revenue { get; init; }
    public decimal SharePercent { get; init; }
    public bool IsOther { get; init; }
}

public sealed class BehaviourDto
{
    /// <summary>At most 5 items.</summary>
    public List<RhythmItemDto> Rhythm { get; init; } = new();
    public PaymentBehaviourDto Payment { get; init; } = new();
}

public sealed class RhythmItemDto
{
    public string Name { get; init; } = "";
    public int TypicalGapDays { get; init; }
    public int DaysSinceLast { get; init; }
    /// <summary>"onTrack" | "reorderAround" | "reorderSoon"</summary>
    public string Chip { get; init; } = "onTrack";
    public DateOnly ExpectedDate { get; init; }
}

public sealed class PaymentBehaviourDto
{
    public bool HasSettled { get; init; }
    public int? AvgDaysToPay { get; init; }
    public int? AvgTermsDays { get; init; }
    public int OnTimeCount { get; init; }
    public int SettledCount { get; init; }
    public decimal? OnTimePercent { get; init; }
    public decimal OverdueAmount { get; init; }
    public int CreditNoteCount { get; init; }
    public decimal CreditNoteTotal { get; init; }
}

public sealed class PendingInvoiceDto
{
    public int InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = "";
    public DateOnly IssueDate { get; init; }
    public DateOnly DueDate { get; init; }
    public decimal Total { get; init; }
    public decimal Paid { get; init; }
    public decimal CreditNotes { get; init; }
    public decimal Outstanding { get; init; }
    /// <summary>"overdue" | "partial" | "unpaid"</summary>
    public string StatusKind { get; init; } = "unpaid";
    /// <summary>Overdue days, or days until due.</summary>
    public int Days { get; init; }
    /// <summary>Exact pill text, built server-side.</summary>
    public string StatusText { get; init; } = "";
}

/// <summary>Drill-down lines of one product group. There is deliberately no per-product unpaid field.</summary>
public sealed class ProductPurchaseLinesDto
{
    public int TotalLineCount { get; init; }
    /// <summary>At most 200, newest first.</summary>
    public List<ProductPurchaseLineDto> Lines { get; init; } = new();
}

public sealed class ProductPurchaseLineDto
{
    public DateOnly Date { get; init; }
    public int InvoiceId { get; init; }
    public string InvoiceNumber { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal LineTotal { get; init; }
    /// <summary>Unpaid | Partially paid | Paid | Overdue | Written off</summary>
    public string Status { get; init; } = "";
}
