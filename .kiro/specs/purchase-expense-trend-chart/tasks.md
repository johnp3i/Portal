# Tasks — Expense Trend Chart on the Purchases page

Derived from `requirements.md` and `design.md`.

- [x] 1. DTOs — `Portal.Infrastructure/Models/PurchaseTrendModels.cs`
  - [x] 1.1 `ExpenseTrendPoint { DateOnly Bucket; int SeriesId; string SeriesName; decimal Amount; }`
  - [x] 1.2 `ExpenseTrendSeries { string Name; List<decimal> Data; }`
  - [x] 1.3 `ExpenseTrendResult { List<string> Labels; List<ExpenseTrendSeries> Series; }`
  - _FR-2, FR-7_

- [x] 2. Repository — `PurchaseRepository.GetExpenseTrendAsync(businessId, filter, dimension, bucket)`
  - [x] 2.1 Reuse `BuildListFromWhere(...)`; append `AND IsCancelled = 0`.
  - [x] 2.2 Whitelisted `dimension` (category|supplier) → GROUP BY + name join; whitelisted `bucket`
        (day|month|year) → `CONVERT(date,...)` / `DATEFROMPARTS(...)`.
  - [x] 2.3 `SELECT Bucket, SeriesId, SeriesName, SUM(TotalAmount) ... GROUP BY ... ORDER BY Bucket`.
  - _NFR-1, NFR-2, NFR-3, NFR-4_

- [x] 3. Service — `IPurchaseService.GetExpenseTrendAsync(filter, dimension)` + impl
  - [x] 3.1 Effective range (filter dates, else min/max InvoiceDate, else last 12 months).
  - [x] 3.2 Bucket from span: ≤31d day; ≤~24mo month; else year.
  - [x] 3.3 Call repo; build ordered label axis; zero-fill empty buckets.
  - [x] 3.4 Top-8 series by total + "Other"; pivot to `ExpenseTrendResult`.
  - _FR-5, FR-6, NFR-1, NFR-2_

- [x] 4. Controller — `PurchaseController.AxGetExpenseTrend(...)`
  - [x] 4.1 Map all filter params → `PurchaseListFilter`; whitelist `dimension`.
  - [x] 4.2 `Json(new { success, data })`; `catch (Exception ex)`; existing module gate.
  - _FR-7, NFR-4_

- [x] 5. View + JS
  - [x] 5.1 `Index.cshtml`: collapsible "Expense Trend" section, collapsed by default, with a
        By Category / By Supplier toggle, a `<canvas>`, current-filter `data-*` attributes, currency.
  - [x] 5.2 `purchase-expense-trend.js`: expand→fetch (first expand), dimension toggle re-fetch,
        local spinner, empty state, Chart.js line render (destroy/recreate), currency-formatted axis.
  - [x] 5.3 Persist collapsed/expanded in localStorage; default collapsed.
  - _FR-1, FR-2, FR-3, FR-8, FR-9, NFR-5_

- [x] 6. Verify
  - [x] 6.1 Build `Portal.Web` (RazorCompileOnBuild) → 0 errors. **Verified 2026-10-02: "Build succeeded"
        with 0 errors (Razor compiled). Warnings only (all pre-existing).**
  - [x] 6.2 Walk the 10 acceptance criteria: collapsed default; category default; supplier toggle;
        KPI reconciliation; bucketing; top-N/Other; cancelled/payroll parity; empty state; list
        untouched. Covered by implementation; Chart.js self-hosted at
        `wwwroot/js/vendor/chart.umd.min.js` (no third-party CDN).
  - _AC-1..AC-10_

## Note — Chart.js hosting

Chart.js (v4.4.6 UMD) is **self-hosted** at `Portal.Web/wwwroot/js/vendor/chart.umd.min.js` rather than
loaded from a CDN, to avoid supply-chain / privacy / availability exposure from third-party libraries.
Follow-up (not done): ~7 existing views still reference the jsDelivr Chart.js CDN and could be migrated
to this self-hosted copy.
