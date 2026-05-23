# Implementation Plan: Supplier Dashboard

## Overview

This plan implements the Supplier Dashboard analytics page at `/Supplier/Dashboard/{id}`. It introduces a dedicated `ISupplierDashboardService` with `SupplierDashboardService`, new view models, a `Dashboard` action on the existing `SupplierController`, and a new Razor view. All metrics are computed from existing purchase data via EF Core LINQ — no schema changes required. The plan also adds a "Suppliers" sidebar navigation entry and a "Dashboard" link to the Supplier Index view.

## Tasks

- [x] 1. Add view models and supporting types
  - [x] 1.1 Create `SupplierDashboardViewModel` and supporting models in `Portal.Infrastructure/Models/SupplierDashboardViewModel.cs`
    - Implement `SupplierDashboardViewModel` with supplier info fields (`SupplierId`, `SupplierName`, `CollaborationSince`, `IsActive`, `CurrencySymbol`), period filter fields (`SelectedPeriodId`, `Periods`), KPI fields (`TotalSpend`, `TotalPurchases`, `AverageMonthlySpend`), chart data lists (`SpendShareData`, `MonthlySpendData`, `PeriodSpendData`), and pagination fields (`Purchases`, `CurrentPage`, `TotalPages`, `TotalRecords`)
    - Implement `VatPeriodOption` with `Id` and `Label`
    - Implement `SpendShareSlice` with `SupplierName`, `Amount`, `IsCurrentSupplier`
    - Implement `MonthlySpendBar` with `MonthLabel` and `Amount`
    - Implement `PeriodSpendBar` with `PeriodId`, `PeriodLabel`, `Amount`, `IsSelected`
    - Implement `PurchaseTableRow` with `InvoiceDate`, `Description`, `Category`, `AmountExcludingVat`, `VatAmount`, `TotalAmount`
    - _Requirements: 4.1, 4.2, 4.3, 5.1, 5.2, 5.3, 5.4, 6.1, 7.1, 8.1, 9.1, 10.1_

- [x] 2. Create `ISupplierDashboardService` interface and `SupplierDashboardService` implementation
  - [x] 2.1 Create `ISupplierDashboardService` interface in `Portal.Infrastructure/Services/ISupplierDashboardService.cs`
    - Declare `Task<SupplierDashboardViewModel> GetDashboardAsync(int supplierId, int? periodId, int page)`
    - _Requirements: 3.1, 13.1, 13.3, 13.4_

  - [x] 2.2 Create `SupplierDashboardService` in `Portal.Infrastructure/Services/SupplierDashboardService.cs`
    - Inject `PortalDbContext` and `ICurrentTenantService`
    - Define `PageSize = 10` constant
    - Implement `GetDashboardAsync`: build base query (`SupplierId == supplierId && !IsCancelled`), apply optional `periodId` filter, compute KPIs, spend share, monthly spend, period spend, paginated purchases, and period dropdown
    - Implement private `ComputeKpis`: sum `AmountExcludingVat` for Total Spend, count for Total Purchases, divide by distinct calendar months for Average Monthly Spend; return zeros when no purchases exist
    - Implement private `ComputeSpendShare`: query all non-cancelled purchases grouped by supplier, rank by descending spend, return current supplier slice + top 5 others + "Others" aggregate if more than 5 remain
    - Implement private `ComputeMonthlySpend`: group base query by year+month, return one `MonthlySpendBar` per month
    - Implement private `ComputePeriodSpend`: group all non-cancelled purchases for the supplier by `VatSubmissionPeriodId`, join with all business periods (including zero-spend periods), mark `IsSelected`
    - Implement private `GetPurchasesPage`: sort by `InvoiceDate` ascending, apply `Skip`/`Take`, clamp page to valid range
    - Implement private `GetPeriodOptions`: query all `VatSubmissionPeriod` records for the business ordered by `PeriodStartDate` ascending
    - Fetch `CurrencySymbol` from `BusinessProfile`; default to `"€"` if null
    - Ignore `periodId` values that do not belong to the current business (treat as "All Time")
    - _Requirements: 3.1, 3.3, 3.4, 5.2, 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 7.2, 7.3, 7.5, 8.2, 8.3, 8.4, 9.2, 9.3, 10.2, 10.3, 10.4, 13.1, 13.2, 13.3, 13.4, 13.5_

- [x] 3. Register `SupplierDashboardService` in DI and extend `SupplierController`
  - [x] 3.1 Register `ISupplierDashboardService` → `SupplierDashboardService` in `Portal.Web/Program.cs`
    - Add `builder.Services.AddScoped<ISupplierDashboardService, SupplierDashboardService>()`
    - _Requirements: 3.1_

  - [x] 3.2 Add `Dashboard` action to `Portal.Web/Controllers/SupplierController.cs`
    - Inject `ISupplierDashboardService` via constructor
    - Add `[HttpGet] public async Task<IActionResult> Dashboard(int id, int? periodId = null, int page = 1)`
    - Call `_supplierService.GetSupplierByIdAsync(id)`; return `NotFound()` if null
    - Call `_dashboardService.GetDashboardAsync(id, periodId, page)` and return `View(dashboard)`
    - _Requirements: 3.1, 3.2, 3.3_

- [x] 4. Checkpoint — Ensure infrastructure and controller layers compile
  - Build the solution and confirm zero errors before proceeding to the view layer

- [x] 5. Create `Dashboard.cshtml` Razor view
  - [x] 5.1 Create `Portal.Web/Views/Supplier/Dashboard.cshtml` with topbar section
    - Set `@model SupplierDashboardViewModel`
    - Render eyebrow label "Supplier Dashboard", supplier name as 42px Manrope heading, "Collaboration since" date formatted "dd MMM yyyy", and Active/Inactive pill badge
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 14.1, 14.4_

  - [x] 5.2 Add period filter section to `Dashboard.cshtml`
    - Render a `.glass.card-pad` filter card with `margin-bottom:22px`
    - Render a `<select>` dropdown with "All Time" as first option followed by `Model.Periods` entries using `PeriodLabel`
    - Render a "Filter" submit button and a "Clear" link that navigates to `/Supplier/Dashboard/{id}` without query parameters
    - Filter submits as GET to the same route with `?periodId=X`
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 14.2, 14.3_

  - [x] 5.3 Add KPI cards row to `Dashboard.cshtml`
    - Render three equal-width `.glass.card-pad` cards in a flex row
    - Card 1: "Total Spend (Excl. VAT)" — formatted with `Model.CurrencySymbol` and two decimal places
    - Card 2: "Total Purchases" — integer count
    - Card 3: "Average Monthly Spend" — formatted with `Model.CurrencySymbol` and two decimal places
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.6, 14.1, 14.2, 14.3_

  - [x] 5.4 Add Chart.js CDN script reference and chart canvas elements to `Dashboard.cshtml`
    - Add `<script src="https://cdn.jsdelivr.net/npm/chart.js@4/dist/chart.umd.min.js" onerror="showChartsUnavailable()"></script>` in a `@section Scripts` block
    - Render three `<canvas>` elements: `spendShareCanvas` (left 33%), `monthlySpendCanvas` (upper right), `periodSpendCanvas` (lower right)
    - Add `<div class="chart-fallback" style="display:none;">Charts unavailable</div>` adjacent to each canvas
    - Add `showChartsUnavailable()` JS function that hides canvases and shows fallback divs
    - _Requirements: 12.1, 12.2, 12.3_

  - [x] 5.5 Add Chart.js initialization scripts to `Dashboard.cshtml`
    - Serialize `Model.SpendShareData`, `Model.MonthlySpendData`, and `Model.PeriodSpendData` as JSON using `System.Text.Json`
    - Initialize donut chart for spend share: current supplier slice in `#0D5EA6`, others in palette colors, legend below
    - Initialize monthly spend bar chart: bars in `#0D5EA6`, border-radius 6px, abbreviated month labels
    - Initialize period spend bar chart: selected period bar in `#0D5EA6`, all others in `#57B8E8` (or all `#0D5EA6` when "All Time")
    - All chart initialization inside `document.addEventListener('DOMContentLoaded', ...)`
    - _Requirements: 7.1, 7.4, 7.6, 8.1, 8.5, 9.1, 9.4, 9.5, 12.2_

  - [x] 5.6 Add purchases table and pagination to `Dashboard.cshtml`
    - Render a `.glass.card-pad` table card with columns: Date, Description, Category, Excl. VAT, VAT, Total
    - Format `InvoiceDate` as "dd MMM yyyy"; right-align and format monetary columns with `Model.CurrencySymbol`
    - Render pagination controls below the table: "Showing X–Y of N purchases" info text and Previous/Next page links preserving `periodId` query parameter
    - Show empty state message when `Model.Purchases` is empty
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7, 10.8, 14.2_

  - [x] 5.7 Add "Back to Suppliers" link to `Dashboard.cshtml`
    - Render a right-aligned outlined button link below the purchases table card navigating to `/Supplier`
    - Style as outlined button per MyChair Design System
    - _Requirements: 11.1, 11.2, 11.3_

- [x] 6. Add "Suppliers" sidebar navigation entry and "Dashboard" link to Supplier Index
  - [x] 6.1 Add "Suppliers" nav item to `Portal.Web/Views/Shared/Components/ModuleNavigation/Default.cshtml`
    - Add `{ "purchase", ("Supplier", "Index", "Suppliers") }` entry to `moduleLinks` (replacing or supplementing the existing `"purchase"` → `Purchase` entry so both Purchases and Suppliers appear for users with purchase module access)
    - Ensure the nav item highlights as active when `currentController == "Supplier"`
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 6.2 Add "Dashboard" action link to `Portal.Web/Views/Supplier/Index.cshtml`
    - Add a "Dashboard" link in the Actions column for each supplier row, pointing to `/Supplier/Dashboard/{supplier.Id}`
    - Only render the link when `supplier.IsActive == true`
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 7. Checkpoint — Ensure full application compiles and renders correctly
  - Build the solution, run the app, navigate to `/Supplier/Dashboard/{id}` with seed data, and verify KPI cards, charts, and table render without errors

- [x] 8. Write unit tests for `SupplierDashboardService`
  - [x] 8.1 Create `Portal.Tests/Unit/Services/SupplierDashboardServiceTests.cs`
    - Test: Dashboard action returns `NotFound` for supplier ID not belonging to current business
    - Test: Dashboard action returns view with zero-value KPIs when supplier has no purchases
    - Test: Period dropdown always has "All Time" as first option
    - Test: Spend share includes "Others" slice when more than 5 other suppliers exist
    - Test: Spend share handles current supplier with zero spend in selected period
    - Test: Monthly chart shows correct abbreviated month labels for a 3-month period
    - Test: Period chart marks the selected period bar as `IsSelected = true`
    - Test: Table rows are empty when supplier has no non-cancelled purchases
    - Test: Invalid `periodId` (not belonging to business) is treated as "All Time"
    - Test: Page clamped to 1 when requested page is less than 1
    - Test: Page clamped to last page when requested page exceeds total pages
    - Use EF Core InMemory provider for `PortalDbContext`; mock `ICurrentTenantService`
    - _Requirements: 3.3, 3.4, 6.1, 7.2, 7.5, 8.2, 9.4_

- [x] 9. Write property-based tests for `SupplierDashboardService`
  - [x]* 9.1 Write property test for Cancelled Purchase Exclusion
    - **Property 1: Cancelled Purchase Exclusion**
    - Generate random sets of purchases with mixed `IsCancelled` values; verify Total Spend, Total Purchases, Average Monthly Spend, all chart bar values, and all table rows only reflect purchases where `IsCancelled = false`
    - **Validates: Requirements 5.2, 5.3, 10.2, 13.2**

  - [x]* 9.2 Write property test for Business Scoping Invariant
    - **Property 2: Business Scoping Invariant**
    - Generate purchases belonging to two different `BusinessId` values; verify all returned KPIs, chart data, table rows, and period options contain only records for the authenticated business
    - **Validates: Requirements 3.3, 13.3**

  - [x]* 9.3 Write property test for Period Filter Scoping
    - **Property 3: Period Filter Scoping**
    - Generate purchases across multiple `VatSubmissionPeriodId` values; verify that when a `periodId` is selected all metrics only include purchases with that `VatSubmissionPeriodId`, and when `periodId` is null all non-cancelled purchases are included
    - **Validates: Requirements 5.5, 6.2, 6.3, 7.3**

  - [x]* 9.4 Write property test for Total Spend Computation
    - **Property 4: Total Spend Computation**
    - Generate random lists of non-cancelled purchases with arbitrary `AmountExcludingVat` values; verify `TotalSpend == purchases.Sum(p => p.AmountExcludingVat)` and equals zero when the list is empty
    - **Validates: Requirements 5.2**

  - [x]* 9.5 Write property test for Total Purchases Count
    - **Property 5: Total Purchases Count**
    - Generate random lists of non-cancelled purchases; verify `TotalPurchases == purchases.Count`
    - **Validates: Requirements 5.3**

  - [x]* 9.6 Write property test for Average Monthly Spend Computation
    - **Property 6: Average Monthly Spend Computation**
    - Generate random purchases with varying `InvoiceDate` values; verify `AverageMonthlySpend == TotalSpend / distinctMonths` where `distinctMonths` is the count of unique (year, month) pairs; verify result is zero when no purchases exist
    - **Validates: Requirements 5.4**

  - [x]* 9.7 Write property test for Spend Share Ranking and Aggregation
    - **Property 7: Spend Share Ranking and Aggregation**
    - Generate random sets of suppliers with purchases; verify: exactly one slice for the current supplier, at most 5 slices for other suppliers ordered by descending spend, an "Others" slice when more than 5 other suppliers exist whose amount equals the sum of remaining suppliers, and the sum of all slices equals total spend across all suppliers
    - **Validates: Requirements 7.2, 13.5**

  - [x]* 9.8 Write property test for Monthly Spend Bar Values
    - **Property 8: Monthly Spend Bar Values**
    - Generate random purchases grouped by calendar month; verify each `MonthlySpendBar.Amount` equals the sum of `AmountExcludingVat` for purchases in that month
    - **Validates: Requirements 8.2, 8.3, 8.4**

  - [x]* 9.9 Write property test for Period Spend Bar Values
    - **Property 9: Period Spend Bar Values**
    - Generate random VAT periods and purchases; verify each `PeriodSpendBar.Amount` equals the sum of `AmountExcludingVat` for non-cancelled purchases assigned to that period, and periods with no purchases show zero
    - **Validates: Requirements 9.2, 9.3**

  - [x]* 9.10 Write property test for Purchases Table Sorting
    - **Property 10: Purchases Table Sorting**
    - Generate random purchases with arbitrary `InvoiceDate` values; verify the returned `Purchases` list is sorted by `InvoiceDate` ascending for any page
    - **Validates: Requirements 10.3**

  - [x]* 9.11 Write property test for Pagination Correctness
    - **Property 11: Pagination Correctness**
    - Generate random total record counts N and page numbers P; verify the returned page contains exactly `min(10, N - (P-1)*10)` records (or 0 if P exceeds total pages), and pagination info correctly reports X = (P-1)*10 + 1 and Y = min(P*10, N)
    - **Validates: Requirements 10.4, 10.5**

  - [x]* 9.12 Write property test for Period Dropdown Ordering
    - **Property 12: Period Dropdown Ordering**
    - Generate random sets of `VatSubmissionPeriod` records with varying `PeriodStartDate` values; verify the `Periods` list is ordered by `PeriodStartDate` ascending and "All Time" is always represented by a null `SelectedPeriodId` default
    - **Validates: Requirements 6.1**

- [x] 10. Final checkpoint — Ensure all tests pass
  - Run the full test suite; confirm all unit and property-based tests pass; ask the user if any questions arise

## Notes

- Tasks marked with `*` are property-based tests using FsCheck + xUnit (already installed in `Portal.Tests`)
- Property tests target `SupplierDashboardService` directly using the EF Core InMemory provider — no controller mocking needed for data correctness tests
- The `purchase` module key in `ModuleNavigation` currently maps to `PurchaseController`; task 6.1 adds a separate "Suppliers" entry alongside it — both remain visible to users with purchase module access
- Chart rendering is client-side (Chart.js) and is not tested server-side; only the serialized data passed to the view is validated
- The `GetSupplierByIdAsync` method already exists on `ISupplierService` — no changes to that interface are needed
- All EF Core queries use LINQ (no raw SQL) and rely on the global `BusinessId` query filter via `ICurrentTenantService` for multi-tenancy scoping
- `CurrencySymbol` defaults to `"€"` when `BusinessProfile` returns null, consistent with the existing platform pattern

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2"] },
    { "id": 3, "tasks": ["3.1", "3.2"] },
    { "id": 4, "tasks": ["4"] },
    { "id": 5, "tasks": ["5.1", "5.2", "5.3", "5.4", "6.1", "6.2"] },
    { "id": 6, "tasks": ["5.5", "5.6", "5.7"] },
    { "id": 7, "tasks": ["7"] },
    { "id": 8, "tasks": ["8.1"] },
    { "id": 9, "tasks": ["9.1", "9.2", "9.3", "9.4", "9.5", "9.6", "9.7", "9.8", "9.9", "9.10", "9.11", "9.12"] },
    { "id": 10, "tasks": ["10"] }
  ]
}
```
