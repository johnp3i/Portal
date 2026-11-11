# Requirements — Expense Trend Chart on the Purchases page

Status: **Draft for review** · Module: Purchase · Date: 2026-11-04

## Introduction

Add a line chart to the existing **Purchases** page (`/Purchase`) that plots expenses over the
selected period, broken down **by expense category** or **by supplier** (user-switchable). It is
hosted inside a **collapsible section** on the Purchases page — **collapsed by default** — rather
than a new page, because the Purchases page already owns the filtering, period, and totals model the
chart should reflect.

The chart must reflect **the same filtered set** the user is already looking at (supplier, category,
origin, type, VAT period, date range, search, include-payroll). It is a visualization of the current
filter — not an independent query with its own filters.

## Grounding in the existing page (verified in source)

- `PurchaseController.Index` already accepts: `supplierId`, `expenseCategoryId`, `purchaseOriginTypeId`,
  `purchaseTypeId`, `vatPeriodId`, `dateFrom`, `dateTo`, `searchTerm`, `sortBy`, `includePayroll`,
  `page`, and persists the filter query in a cookie.
- `PurchaseListFilter` carries all of those (except paging/sort) and is the shared filter object.
- `PurchaseRepository.BuildListFromWhere(businessId, filter, params, includeVatJoin)` is the shared
  SQL WHERE builder used by the page, count, export, and aggregate queries. Spend aggregates use
  `SUM(TotalAmount)` with `IsCancelled = 0` (cancelled excluded) and payroll excluded unless
  `IncludePayroll`. The time column is `InvoiceDate`. **The chart must reuse this exact WHERE** so its
  numbers reconcile with the KPI cards.
- Spend figure = `TotalAmount` (VAT-inclusive), matching the "Total Spend" KPI card.
- Dimensions available: `ExpenseCategoryId` → category name; `SupplierId` → supplier name
  (name lookups exist: `GetExpenseCategoryNamesAsync`, `GetSupplierNamesAsync`, incl. inactive).

## Functional Requirements

- **FR-1** A collapsible section titled (e.g.) "Expense Trend" appears on the Purchases page,
  **collapsed by default**. Expanding it loads/renders the chart; collapsing hides it. The
  collapsed/expanded state may be remembered client-side (localStorage) but defaults to collapsed on
  first visit.
- **FR-2** The chart is a **line chart** (Chart.js, already used elsewhere) with:
  - X axis = time buckets across the selected period (see FR-5 bucketing).
  - Y axis = spend (`SUM(TotalAmount)`), currency-formatted with the business currency symbol.
  - One **line per series**, where a series is a category or a supplier (per FR-3).
- **FR-3** A **dimension toggle** inside the section lets the user switch between **By Category** and
  **By Supplier**. Default: **By Category**.
- **FR-4** The data honors **all current page filters** (the same `PurchaseListFilter` the list uses).
  Changing a page filter and reloading updates the chart to match. (The chart reads the same filter
  state; it does not add its own filter controls beyond the dimension toggle and FR-6 top-N.)
- **FR-5** **Time bucketing** is derived from the effective date range so the x-axis is readable:
  - range ≤ ~31 days → **daily** buckets;
  - range ≤ ~ ~24 months → **monthly** buckets;
  - longer → **yearly** buckets.
  When no date range is set, the effective range is the min/max `InvoiceDate` of the filtered set
  (fallback: last 12 months). Buckets with no spend render as 0 (continuous line), not gaps.
- **FR-6** To keep the chart legible, show at most the **top N series by total spend** (default N = 8)
  within the filtered set; remaining series are summed into an **"Other"** line. (A category/supplier
  filter that narrows to one naturally yields a single line.)
- **FR-7** Data is fetched via a dedicated **AJAX JSON endpoint** on `PurchaseController`
  (e.g. `AxGetExpenseTrend`) that takes the same filter parameters plus `dimension`
  (`category|supplier`), returns `{ labels: [...], series: [{ name, data:[...] }] }`, and is gated by
  the Purchase module. Returning JSON (camelCase) keeps it consistent with other chart endpoints.
- **FR-8** Empty state: when the filtered set has no spend, the section shows a friendly "No expense
  data for the selected filters." message instead of an empty chart.
- **FR-9** The section follows the established AJAX + feedback conventions: BlockUI/spinner while
  loading (lightweight — a local spinner in the section is fine), graceful error message on failure.

## Non-Functional Requirements

- **NFR-1 Consistency** — the chart's totals reconcile with the page's "Total Spend" KPI for the same
  filters (same WHERE, same `TotalAmount`, cancelled excluded, payroll per toggle).
- **NFR-2 Performance** — the trend is computed in **one SQL aggregation** (GROUP BY time bucket +
  dimension), not by loading rows into memory. Top-N/Other is computed from the grouped result.
- **NFR-3 Tenant isolation** — scoped by `BusinessId` via the shared WHERE builder.
- **NFR-4 House standards** — `AxGet` endpoint naming, `catch (Exception ex)`, raw-SQL repo style,
  Chart.js from the existing CDN, currency via the business profile symbol.
- **NFR-5 No change to existing list behaviour** — the list, KPIs, filters, export, and cookie-restore
  are untouched; the section is purely additive and collapsed by default.

## Acceptance Criteria

1. The Purchases page shows a collapsible "Expense Trend" section, collapsed on first load.
2. Expanding it renders a line chart of spend over the selected period, defaulting to **By Category**.
3. Toggling to **By Supplier** re-renders the same period with one line per supplier.
4. Applying a page filter (e.g. a date range, or a single category) and reloading updates the chart to
   match the filtered set; the chart's summed spend equals the "Total Spend" KPI for those filters.
5. A short range (≤ ~month) shows daily buckets; a multi-month range shows monthly; a multi-year
   range shows yearly. Empty buckets render as 0, keeping each line continuous.
6. With many categories/suppliers, only the top 8 by spend get their own line and the rest roll into
   an "Other" line.
7. Cancelled purchases are excluded; payroll-generated purchases are included only when the page's
   "include payroll" is on — matching the list/KPIs.
8. With no matching spend, the section shows an empty-state message, not a broken/empty chart.
9. Collapsing the section hides the chart; the default remains collapsed; the list/KPIs/filters are
   unchanged.
10. Solution builds with `0 Error(s)`.

## Out of Scope

- A separate analytics page (explicitly hosting this inside Purchases).
- New filter controls beyond the dimension toggle + top-N (the chart reuses the page filters).
- Drill-down/click-through from a line to the filtered list (possible later enhancement).
- Exporting the chart as an image.
- Changing the spend definition (stays `TotalAmount`, cancelled-excluded, to match the KPI).

## Open Items for design review

1. **Reflecting live filter changes without a reload.** Simplest: the section reads the server-rendered
   current filter and fetches on expand; changing a filter reloads the whole page (as today) and the
   chart re-fetches on next expand. A richer option is to re-fetch the chart live as filters change
   client-side. Recommendation: **fetch-on-expand using the current server filter** (simplest, matches
   the page's server-driven filter model); live re-fetch can be a later enhancement.
2. **Top-N value and "Other".** Default N = 8 + "Other". Confirm N.
3. **Spend basis.** Use `TotalAmount` (VAT-inclusive, matches the Total Spend KPI). Alternative would
   be `AmountExcludingVat`. Recommendation: `TotalAmount` for KPI consistency.
4. **Bucketing thresholds** (daily ≤31d, monthly ≤~24mo, else yearly) — confirm the cutoffs.
