# Implementation Plan: Product Detail & Insights Page

## Overview

Creates a dedicated Product Detail page (`/Product/Detail/{id}`) with sales performance KPIs, customer insights, monthly trend chart, price history, demand forecasting, and pipeline activity. No new tables — all data derived from existing invoice lines, payments, and sales pipeline records.

## Tasks

- [ ] 1. Controller & routing
  - [ ] 1.1 Add `Detail(int id)` action to `ProductController`
  - [ ] 1.2 Create `ProductDetailViewModel` with all sections' data
  - [ ] 1.3 Build query service methods for KPI computation

- [ ] 2. Product Detail Service
  - [ ] 2.1 Create `IProductInsightsService` / `ProductInsightsService`
  - [ ] 2.2 `GetSalesKpisAsync(productId, businessId)` — revenue, units, avg price, margin, last sold
  - [ ] 2.3 `GetTopCustomersAsync(productId, businessId, top=5)` — customer name, units, revenue, last purchase
  - [ ] 2.4 `GetMonthlyTrendAsync(productId, businessId, months=12)` — monthly revenue array
  - [ ] 2.5 `GetForecastAsync(productId, businessId)` — 30/60/90 day projections
  - [ ] 2.6 `GetPipelineActivityAsync(productId, businessId)` — active leads, estimated value, conversion

- [ ] 3. View
  - [ ] 3.1 Create `Views/Product/Detail.cshtml` with all sections
  - [ ] 3.2 Product header with edit button + breadcrumb
  - [ ] 3.3 KPI cards row (Revenue, Units, Avg Price, Margin, Last Sold)
  - [ ] 3.4 Top Customers table
  - [ ] 3.5 Monthly Trend chart (Chart.js)
  - [ ] 3.6 Price History table (from ProductPriceHistory)
  - [ ] 3.7 Forecast section (Professional gated)
  - [ ] 3.8 Pipeline Activity section (conditional — only if linked)

- [ ] 4. Navigation updates
  - [ ] 4.1 Product list: make product code/name clickable → Detail page
  - [ ] 4.2 Sales Products "Linked Catalogue" column → Detail page
  - [ ] 4.3 Invoice Detail line items → link to Product Detail (if product code matches)

- [ ] 5. DI & registration
  - [ ] 5.1 Register ProductInsightsService in Program.cs

- [ ] 6. Plan gating
  - [ ] 6.1 Forecast section: check plan, show soft-gate teaser on Foundation
  - [ ] 6.2 All other sections available on Foundation

- [ ] 7. Verification
  - [ ] 7.1 Verify KPIs compute correctly against test invoices
  - [ ] 7.2 Verify trend chart renders with Chart.js
  - [ ] 7.3 Verify pipeline section shows only when linked
  - [ ] 7.4 Verify tenant isolation (can't view another business's product)

## Notes

- No new database tables — all data comes from existing entities:
  - Revenue/Units: `[invoice].[InvoiceLine]` joined with `[invoice].[Invoice]` (issued, non-deleted)
  - Customers: from Invoice → Customer join
  - Price History: from `[product].[ProductPriceHistory]`
  - Pipeline: from `[sales].[LeadRequest]` → `[sales].[Product]` where `ProductId` matches
- The product is identified in invoice lines via `ProductCode` match (not a direct FK) — queries will JOIN on ProductCode
- Chart.js already available (used in Cash Flow and Revenue modules)
- Forecast is simple: `avg_monthly_units × (days/30)` for each projection window
- Conversion rate = leads with status "Won" / total leads with this product

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5", "2.6"] },
    { "id": 1, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4", "3.5", "3.6", "3.7", "3.8"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3"] },
    { "id": 4, "tasks": ["5.1", "6.1", "6.2"] },
    { "id": 5, "tasks": ["7.1", "7.2", "7.3", "7.4"] }
  ]
}
```
