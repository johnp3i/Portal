# Technical Design — Expense Trend Chart on the Purchases page

Status: **Draft for review** · Module: Purchase · Date: 2026-11-04

## Overview

Add a collapsible "Expense Trend" section (collapsed by default) to the Purchases page that renders a
Chart.js line chart of spend over the selected period, grouped by **expense category** or **supplier**.
The chart reuses the page's existing `PurchaseListFilter` and the repository's shared WHERE builder so
its numbers reconcile exactly with the "Total Spend" KPI. One SQL aggregation produces the time-series;
top-N + "Other" is applied in the service; a thin AJAX endpoint serves the JSON on expand.

Tech is locked to what the app already uses: ASP.NET Core MVC, raw-SQL repository over
`PortalDbContext`, Chart.js (CDN, as on the Expense Insights / Revenue dashboards), vanilla JS,
BlockUI/local spinner, business currency symbol from `BusinessProfile`.

## Components (file inventory)

| Layer | File | Change |
|-------|------|--------|
| DTO | `Portal.Infrastructure/Models/PurchaseTrendModels.cs` | **new** — `ExpenseTrendPoint` (raw grouped row), `ExpenseTrendResult` (labels + series) |
| Repo | `Portal.Infrastructure/Repositories/PurchaseRepository.cs` | **new** `GetExpenseTrendAsync(businessId, filter, dimension, bucket)` — one GROUP BY query reusing `BuildListFromWhere` |
| Service iface | `Portal.Infrastructure/Services/IPurchaseService.cs` | add `GetExpenseTrendAsync(filter, dimension)` |
| Service | `Portal.Infrastructure/Services/PurchaseService.cs` | pick bucket from range, call repo, resolve names, apply top-N/"Other", shape `ExpenseTrendResult` |
| Controller | `Portal.Web/Controllers/PurchaseController.cs` | **new** `AxGetExpenseTrend(...)` JSON endpoint (same filter params + `dimension`) |
| View | `Portal.Web/Views/Purchase/Index.cshtml` | collapsible section + `<canvas>` + dimension toggle; pass current filter to JS |
| JS | `Portal.Web/wwwroot/js/purchase-expense-trend.js` | **new** — expand→fetch→render Chart.js; dimension toggle; empty state; spinner |

## Data flow

1. The view renders the section **collapsed**, with the **current filter** values embedded as data-
   attributes (read from the same values the controller already passes to the list: supplierId,
   expenseCategoryId, originTypeId, typeId, vatPeriodId, dateFrom, dateTo, searchTerm, includePayroll).
2. On first expand, the JS reads those data-attributes + the current `dimension` toggle and calls
   `GET /Purchase/AxGetExpenseTrend?...` (same filter query + `dimension`).
3. The controller builds a `PurchaseListFilter` from the params (identical to `Index`), calls
   `IPurchaseService.GetExpenseTrendAsync(filter, dimension)`, returns
   `Json(new { success = true, data = result })` (camelCase → `labels`, `series[].name`,
   `series[].data`).
4. JS renders/updates the Chart.js line chart. Toggling the dimension re-fetches.

Fetch-on-expand uses the **server's current filter** (Open Item 1, recommended). Changing a page
filter reloads the page as today; the chart re-fetches from the new filter when next expanded. (The
collapsed/expanded preference is remembered in localStorage but defaults to collapsed.)

## Repository — `GetExpenseTrendAsync`

Signature:
```csharp
Task<List<ExpenseTrendPoint>> GetExpenseTrendAsync(
    int businessId, PurchaseListFilter filter, string dimension, string bucket);
// dimension: "category" | "supplier"  ·  bucket: "day" | "month" | "year"
```

- **Reuse the shared WHERE**: call `BuildListFromWhere(businessId, filter, params, includeVatJoin:false)`
  so every page filter + the payroll rule apply identically. **Add `AND IsCancelled = 0`** to match the
  aggregate's cancelled-exclusion (the KPI sums only non-cancelled). (The list builder does not add
  IsCancelled itself — the aggregate applies it via CASE; for the trend we add it to the WHERE since we
  are summing only real spend.)
- **Dimension key/label**:
  - category → `GROUP BY ExpenseCategoryId`, join `[purchase].[ExpenseCategory]` for the name.
  - supplier → `GROUP BY SupplierId`, join `[purchase].[Supplier]` for the name.
- **Time bucket** (on `InvoiceDate`), computed in SQL so empty buckets can be filled in the service:
  - day → `CONVERT(date, InvoiceDate)`
  - month → `DATEFROMPARTS(YEAR(InvoiceDate), MONTH(InvoiceDate), 1)`
  - year → `DATEFROMPARTS(YEAR(InvoiceDate), 1, 1)`
- **Select**: `bucketStart (date)`, `seriesId (int)`, `seriesName (nvarchar)`,
  `amount = SUM(TotalAmount)`; `GROUP BY bucketStart, seriesId, seriesName`.
- Returns the raw grouped rows (`ExpenseTrendPoint { DateOnly Bucket; int SeriesId; string SeriesName; decimal Amount; }`).

SQL shape (category example, month bucket):
```sql
SELECT DATEFROMPARTS(YEAR([purchase].[Purchase].[InvoiceDate]), MONTH([purchase].[Purchase].[InvoiceDate]), 1) AS [Bucket],
       [purchase].[Purchase].[ExpenseCategoryId] AS [SeriesId],
       [purchase].[ExpenseCategory].[Name]       AS [SeriesName],
       ISNULL(SUM([purchase].[Purchase].[TotalAmount]), 0) AS [Amount]
FROM [purchase].[Purchase]
LEFT JOIN [purchase].[Supplier]        ON ... (from BuildListFromWhere)
LEFT JOIN [purchase].[ExpenseCategory] ON [purchase].[ExpenseCategory].[Id] = [purchase].[Purchase].[ExpenseCategoryId]
WHERE <BuildListFromWhere predicate> AND [purchase].[Purchase].[IsCancelled] = 0
GROUP BY DATEFROMPARTS(...), [purchase].[Purchase].[ExpenseCategoryId], [purchase].[ExpenseCategory].[Name]
ORDER BY [Bucket];
```
`dimension`/`bucket` select the column expressions via a safe server-side `switch` (never string-
interpolated from client input — only the fixed enum branches are emitted).

## Service — `GetExpenseTrendAsync`

1. Determine the **effective date range**: use `filter.DateFrom/DateTo` when set; else query the
   min/max `InvoiceDate` of the filtered set (or default to the last 12 months when the set is empty).
2. Pick the **bucket** from the range span: ≤ 31 days → `day`; ≤ ~24 months → `month`; else `year`
   (thresholds per requirements, Open Item 4).
3. Call `GetExpenseTrendAsync(businessId, filter, dimension, bucket)` → raw points.
4. Build the **ordered bucket axis** (`labels`) by stepping from range start to end by the bucket unit,
   so empty buckets become 0 (continuous lines) rather than gaps.
5. **Top-N + Other**: sum each series' total; keep the top 8; fold the rest into an "Other" series.
6. Pivot into `ExpenseTrendResult { List<string> Labels; List<ExpenseTrendSeries> Series; }`
   where `ExpenseTrendSeries { string Name; List<decimal> Data; }` (Data aligned to Labels).

`ExpenseTrendResult` and the point DTO live in `PurchaseTrendModels.cs`.

## Controller — `AxGetExpenseTrend`

```csharp
[HttpGet]
public async Task<IActionResult> AxGetExpenseTrend(
    int? supplierId, int? expenseCategoryId, int? purchaseOriginTypeId, int? purchaseTypeId,
    int? vatPeriodId, DateOnly? dateFrom, DateOnly? dateTo, string? searchTerm,
    bool includePayroll = false, string dimension = "category")
{
    try
    {
        var filter = new PurchaseListFilter { /* map all params; SortBy irrelevant */ };
        var dim = dimension == "supplier" ? "supplier" : "category"; // whitelist
        var result = await _purchaseService.GetExpenseTrendAsync(filter, dim);
        return Json(new { success = true, data = result });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = "Failed to load the expense trend." });
    }
}
```
Gated by the controller's existing `[ModuleAccess(PortalModules.Purchase)]`. No new DI.

## View + JS

**View (`Index.cshtml`)** — add a `.glass.card-pad` collapsible section (header with a chevron +
"Expense Trend" + a By Category / By Supplier segmented toggle; body hidden by default). The section
root carries the current filter as `data-*` attributes (same values the controller already has in
scope) and the business currency symbol. Place it near the KPI cards / above the table (final spot per
the layout-standards card spacing).

**JS (`purchase-expense-trend.js`)** —
- On header click: toggle the body; persist state to `localStorage` (default collapsed); on first
  expand, fetch.
- `fetchTrend()`: read filter data-attributes + active dimension, show a local spinner, GET
  `AxGetExpenseTrend`, hide spinner, render.
- `render(data)`: if no series/labels → empty-state text; else build the Chart.js line chart (one
  dataset per series, palette consistent with the app's chart colours, currency-formatted Y ticks and
  tooltips). Destroy/recreate on re-render (dimension toggle), as the dashboards do.
- Dimension toggle: set active, re-fetch.

Follows UI-feedback conventions (local spinner + SweetAlert/inline error on failure). No full-page
BlockUI needed for a passive chart.

## Correctness notes

- **KPI reconciliation (NFR-1):** summing all series' data over all buckets equals the page's
  "Total Spend" for the same filters, because both use `BuildListFromWhere` + `SUM(TotalAmount)` +
  `IsCancelled = 0` + the payroll rule. (Top-N/"Other" only regroups; the grand total is preserved.)
- **Injection safety:** `dimension` and `bucket` are server-side whitelisted enums that choose fixed
  SQL fragments; all user values flow through `SqlParameter` via `BuildListFromWhere`.
- **Null series key:** every non-cancelled purchase has a non-null `SupplierId` and
  `ExpenseCategoryId` (both are required FKs on Purchase), so there is no "null category" bucket to
  handle; names come from the joined reference tables.

## Testability

- Service: given grouped points across a known range, the pivot produces labels covering every bucket
  (zeros where empty) and top-N/"Other" preserves the grand total.
- Repo (integration/manual): the trend's summed amount equals `GetAggregatesAsync(...).TotalSpend`
  for the same filter.
- Build gate: `dotnet build Portal.Web/Portal.Web.csproj -c Debug -p:RazorCompileOnBuild=true` → 0
  errors.

## Build order

1. DTOs (`PurchaseTrendModels.cs`).
2. Repo `GetExpenseTrendAsync` (reuse `BuildListFromWhere`; whitelisted dimension/bucket).
3. Service iface + impl (range → bucket, names, top-N/Other, pivot).
4. Controller `AxGetExpenseTrend`.
5. View collapsible section + JS.
6. Build + verify the 10 acceptance criteria (KPI reconciliation, bucketing, top-N, empty state,
   collapsed-by-default).
