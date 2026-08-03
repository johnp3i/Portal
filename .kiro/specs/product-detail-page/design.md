# Design Document: Product Detail & Insights Page

## Overview

The Product Detail page transforms the Product Catalogue from a simple CRUD list into a revenue intelligence tool. Each product gets a dedicated page showing sales performance, customer insights, pricing history, trend analysis, demand forecasting, and pipeline activity — all derived from existing data without new tables.

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| No new database tables | All metrics are computed from existing invoice lines, payments, price history, and sales pipeline records |
| Product matched via ProductCode | Invoice lines reference products by `ProductCode` (string match), not a direct FK — queries must JOIN on this field |
| Foundation gets core metrics | KPIs, customers, trends, and price history are valuable for all businesses |
| Forecasting gated to Professional | Predictive features justify the Professional tier value proposition |
| Pipeline section is conditional | Only appears when the product is linked to Sales Products via `ProductId` FK |
| Chart.js for visualisation | Consistent with Cash Flow and Revenue modules — already bundled |

### Mockup Reference

- **Product Detail Page:** `.kiro/docs/mockups/product-detail-page.html`

---

## Architecture

```
ProductController.Detail(id)
    → ProductInsightsService.GetSalesKpisAsync()
    → ProductInsightsService.GetTopCustomersAsync()
    → ProductInsightsService.GetMonthlyTrendAsync()
    → ProductInsightsService.GetForecastAsync()       [Professional only]
    → ProductInsightsService.GetPipelineActivityAsync() [conditional]
    → ProductPriceHistoryRepository.GetByProductIdAsync()
    → View: Product/Detail.cshtml
```

### Layer Responsibilities

| Layer | Responsibility |
|-------|---------------|
| `ProductController` | HTTP routing, plan check, view model assembly |
| `ProductInsightsService` | Query orchestration, KPI computation, forecast calculations |
| `ProductPriceHistoryRepository` | Price history data access (existing) |
| `PortalDbContext` | LINQ queries for invoice lines, customers, sales pipeline |
| `Views/Product/Detail.cshtml` | Razor view with Chart.js integration |

---

## Data Model (Queries Only — No New Tables)

### Sales KPIs Query

```sql
SELECT
    SUM([invoice].[InvoiceLine].[LineTotal]) AS [TotalRevenue],
    SUM([invoice].[InvoiceLine].[Quantity]) AS [TotalUnits],
    MAX([invoice].[Invoice].[InvoiceDate]) AS [LastSoldDate]
FROM [invoice].[InvoiceLine]
INNER JOIN [invoice].[Invoice]
    ON [invoice].[InvoiceLine].[InvoiceId] = [invoice].[Invoice].[Id]
WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
  AND [invoice].[Invoice].[InvoiceStatusTypeId] = 2
  AND [invoice].[Invoice].[IsDeleted] = 0
  AND [invoice].[InvoiceLine].[ProductCode] = @ProductCode
```

### Top Customers Query

```sql
SELECT TOP 5
    [customer].[Customer].[Name],
    SUM([invoice].[InvoiceLine].[Quantity]) AS [Units],
    SUM([invoice].[InvoiceLine].[LineTotal]) AS [Revenue],
    MAX([invoice].[Invoice].[InvoiceDate]) AS [LastPurchase]
FROM [invoice].[InvoiceLine]
INNER JOIN [invoice].[Invoice]
    ON [invoice].[InvoiceLine].[InvoiceId] = [invoice].[Invoice].[Id]
INNER JOIN [customer].[Customer]
    ON [invoice].[Invoice].[CustomerId] = [customer].[Customer].[Id]
WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
  AND [invoice].[Invoice].[InvoiceStatusTypeId] = 2
  AND [invoice].[Invoice].[IsDeleted] = 0
  AND [invoice].[InvoiceLine].[ProductCode] = @ProductCode
GROUP BY [customer].[Customer].[Name]
ORDER BY [Revenue] DESC
```

### Monthly Trend Query

```sql
SELECT
    YEAR([invoice].[Invoice].[InvoiceDate]) AS [Year],
    MONTH([invoice].[Invoice].[InvoiceDate]) AS [Month],
    SUM([invoice].[InvoiceLine].[LineTotal]) AS [Revenue]
FROM [invoice].[InvoiceLine]
INNER JOIN [invoice].[Invoice]
    ON [invoice].[InvoiceLine].[InvoiceId] = [invoice].[Invoice].[Id]
WHERE [invoice].[Invoice].[BusinessId] = @BusinessId
  AND [invoice].[Invoice].[InvoiceStatusTypeId] = 2
  AND [invoice].[Invoice].[IsDeleted] = 0
  AND [invoice].[InvoiceLine].[ProductCode] = @ProductCode
  AND [invoice].[Invoice].[InvoiceDate] >= @TwelveMonthsAgo
GROUP BY YEAR([invoice].[Invoice].[InvoiceDate]), MONTH([invoice].[Invoice].[InvoiceDate])
ORDER BY [Year], [Month]
```

### Forecast Calculation

```
avg_monthly_units = total_units_last_6_months / 6
avg_monthly_revenue = total_revenue_last_6_months / 6

forecast_30 = { units: avg_monthly_units, revenue: avg_monthly_revenue }
forecast_60 = { units: avg_monthly_units × 2, revenue: avg_monthly_revenue × 2 }
forecast_90 = { units: avg_monthly_units × 3, revenue: avg_monthly_revenue × 3 }
```

### Pipeline Activity Query

```sql
SELECT
    [sales].[LeadRequest].[Title],
    [sales].[LeadStatusType].[Name] AS [Stage],
    [sales].[LeadRequest].[EstimatedValue],
    -- Assigned team member name
FROM [sales].[LeadRequest]
INNER JOIN [sales].[LeadRequestProduct]
    ON [sales].[LeadRequest].[Id] = [sales].[LeadRequestProduct].[LeadRequestId]
INNER JOIN [sales].[Product]
    ON [sales].[LeadRequestProduct].[SalesProductId] = [sales].[Product].[Id]
INNER JOIN [sales].[LeadStatusType]
    ON [sales].[LeadRequest].[StatusTypeId] = [sales].[LeadStatusType].[Id]
WHERE [sales].[Product].[ProductId] = @CatalogProductId
  AND [sales].[LeadRequest].[BusinessId] = @BusinessId
  AND [sales].[LeadStatusType].[Name] NOT IN ('Won', 'Lost', 'Inactive')
```

---

## Components and Interfaces

### ProductInsightsService

```csharp
public interface IProductInsightsService
{
    Task<ProductKpiDto> GetSalesKpisAsync(int productId, string productCode, int businessId);
    Task<List<ProductCustomerDto>> GetTopCustomersAsync(string productCode, int businessId, int top = 5);
    Task<ProductCustomerSummaryDto> GetCustomerSummaryAsync(string productCode, int businessId);
    Task<List<MonthlyRevenueDto>> GetMonthlyTrendAsync(string productCode, int businessId, int months = 12);
    Task<ProductForecastDto> GetForecastAsync(string productCode, int businessId);
    Task<ProductPipelineDto?> GetPipelineActivityAsync(int productId, int businessId);
}
```

### DTOs

```csharp
public class ProductKpiDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalUnits { get; set; }
    public decimal AvgSellingPrice { get; set; }
    public decimal GrossMargin { get; set; }
    public decimal MarginPercentage { get; set; }
    public DateTime? LastSoldDate { get; set; }
    public int InvoiceCount { get; set; }
}

public class ProductCustomerDto
{
    public string CustomerName { get; set; } = null!;
    public decimal Units { get; set; }
    public decimal Revenue { get; set; }
    public DateTime LastPurchase { get; set; }
}

public class ProductCustomerSummaryDto
{
    public int UniqueCustomerCount { get; set; }
    public decimal RepeatPurchaseRate { get; set; }  // 0-100
}

public class ProductForecastDto
{
    public decimal AvgMonthlyUnits { get; set; }
    public decimal AvgMonthlyRevenue { get; set; }
    public decimal Forecast30Revenue { get; set; }
    public decimal Forecast60Revenue { get; set; }
    public decimal Forecast90Revenue { get; set; }
    public decimal Forecast30Units { get; set; }
    public decimal Forecast60Units { get; set; }
    public decimal Forecast90Units { get; set; }
}

public class ProductPipelineDto
{
    public int ActiveLeadCount { get; set; }
    public decimal EstimatedValue { get; set; }
    public decimal ConversionRate { get; set; }  // 0-100
    public List<PipelineLeadDto> Leads { get; set; } = new();
}

public class PipelineLeadDto
{
    public int LeadId { get; set; }
    public string Title { get; set; } = null!;
    public string Stage { get; set; } = null!;
    public decimal EstimatedValue { get; set; }
    public string? AssignedTo { get; set; }
}
```

### ProductDetailViewModel

```csharp
public class ProductDetailViewModel
{
    public Product Product { get; set; } = null!;
    public string? SupplierName { get; set; }
    public string? ProductTypeName { get; set; }
    public string CurrencySymbol { get; set; } = "€";

    // KPIs
    public ProductKpiDto Kpis { get; set; } = new();

    // Customers
    public List<ProductCustomerDto> TopCustomers { get; set; } = new();
    public ProductCustomerSummaryDto CustomerSummary { get; set; } = new();

    // Trend
    public List<MonthlyRevenueDto> MonthlyTrend { get; set; } = new();

    // Price History
    public List<ProductPriceHistory> PriceHistory { get; set; } = new();

    // Forecast (null if not Professional)
    public ProductForecastDto? Forecast { get; set; }

    // Pipeline (null if no linked sales products)
    public ProductPipelineDto? Pipeline { get; set; }

    // Plan info
    public bool IsProfessional { get; set; }
}
```

---

## UI Layout

```
┌─────────────────────────────────────────────────────────────────────┐
│ EYEBROW: Product Catalogue                                          │
│ H1: WorkforcePi                                                     │
│ Subtitle: PROD-001 · Services · Active                              │
│ Breadcrumb: Catalogue > Products > PROD-001                         │
├─────────────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────── [Edit] ──────┐ │
│ │ Product Header: Code, Price, Cost, VAT, Supplier, Last Used     │ │
│ └─────────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────────┤
│ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐                      │
│ │Revenue│ │Units │ │AvgPr │ │Margin│ │ Last │  ← KPI Cards          │
│ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘                      │
├─────────────────────────────────────────────────────────────────────┤
│ ┌───────────────────────┐ ┌───────────────────────┐                 │
│ │ Top Customers (table) │ │ Monthly Trend (chart) │  ← Two columns  │
│ └───────────────────────┘ └───────────────────────┘                 │
├─────────────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────────────────┐ │
│ │ Price History (table)                                           │ │
│ └─────────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────────────────┐ │
│ │ Demand Forecast [Professional]   30/60/90 day cards             │ │
│ └─────────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────────┤
│ ┌─────────────────────────────────────────────────────────────────┐ │
│ │ Pipeline Activity (conditional — only if linked to Sales Prods) │ │
│ └─────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Tier Placement

| Section | Foundation | Professional | Enterprise |
|---------|-----------|-------------|-----------|
| Product Header + Edit | ✅ | ✅ | ✅ |
| Sales KPIs | ✅ | ✅ | ✅ |
| Top Customers | ✅ | ✅ | ✅ |
| Monthly Trend Chart | ✅ | ✅ | ✅ |
| Price History | ✅ | ✅ | ✅ |
| Demand Forecast | ❌ (soft-gate) | ✅ | ✅ |
| Pipeline Activity | ✅ | ✅ | ✅ |

---

## Error Handling

| Scenario | Behaviour |
|----------|-----------|
| Product not found | Return 404 |
| Product belongs to different business | Return 404 (no information leak) |
| No invoice data exists | KPIs show €0 / 0 units, trend chart flat, customers empty |
| No linked Sales Products | Pipeline section hidden entirely |
| Plan is Foundation | Forecast section shows soft-gate teaser |

---

## Performance Considerations

- All queries are indexed via existing indexes on `BusinessId`, `InvoiceStatusTypeId`, `ProductCode`
- Monthly trend limited to 12 months (bounded query)
- Top customers limited to 5 (TOP N query)
- Pipeline query limited to active leads only
- Consider caching KPIs for products with high invoice volume (future optimisation)
