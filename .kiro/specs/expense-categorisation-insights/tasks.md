# Implementation Plan: Expense Categorisation Insights

## Overview

This plan implements the Expense Categorisation Insights module — a visual analytics and budget management feature that aggregates Purchase and ExpenseCategory data to deliver spend-by-category charts, trend analysis, budget threshold alerts, supplier breakdowns, and CSV export. The implementation follows the existing MVC + Service Layer architecture, reuses `PnlPeriodType` for period filtering, and integrates with the existing tenant isolation and plan gating infrastructure.

## Tasks

- [x] 1. Create DTOs and Models
  - [x] 1.1 Create request/response DTOs in Portal.Infrastructure/Models/ExpenseInsights/
    - Create `ExpenseInsightsPeriodRequest.cs` with PeriodType, CustomStartDate, CustomEndDate properties
    - Create `ExpenseInsightsDateRange.cs` with StartDate, EndDate (DateOnly)
    - Create `ExpenseInsightsValidationResult.cs` with IsValid, ErrorMessage
    - Create `ExpenseInsightsDto.cs` with Summary, Categories list, Period, BudgetExceededCount, BudgetApproachingCount, HasData, and static Empty() factory
    - Create `ExpenseInsightsSummaryDto.cs` with TotalSpend, CategoriesWithSpend, TopCategoryName, AveragePerCategory
    - Create `ExpenseCategoryBreakdownDto.cs` with all breakdown fields (CategoryName, ExpenseTypeName, TotalSpend, PercentageOfTotal, Variance, VarianceValue, BudgetLimit, BudgetStatus, TopSuppliers)
    - Create `TopSupplierDto.cs` with SupplierId, SupplierName, TotalSpend, PercentageOfCategory
    - Create `BudgetStatus` enum (NoLimit, WithinLimit, Approaching, Exceeded)
    - Create `ExpenseInsightsTrendDto.cs` with MonthLabels, Series list, HasSufficientData
    - Create `TrendCategorySeriesDto.cs` with CategoryName, MonthlyTotals list
    - Create `UpdateBudgetLimitRequest.cs` with ExpenseCategoryId, PeriodLimitEur
    - Create `ExportResult.cs` with Content (byte[]), FileName, ContentType
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 5.1, 5.4, 7.1, 7.2, 8.1, 10.2_

  - [x] 1.2 Create view model in Portal.Web/Models/
    - Create `ExpenseInsightsViewModel.cs` with InsightsData, TrendData, BudgetConfig, CurrencySymbol, SelectedPeriod
    - Create `ExpenseCategoryLimitViewModel.cs` with CategoryId, CategoryName, CurrentLimit, CurrentSpend, BudgetStatus
    - _Requirements: 3.1, 3.2, 6.1_

- [x] 2. Implement IExpenseInsightsService interface and core service
  - [x] 2.1 Create IExpenseInsightsService interface in Portal.Infrastructure/Services/
    - Define GetInsightsDataAsync, GetTrendDataAsync, UpsertBudgetLimitAsync, ResolvePeriod, ValidateCustomRange, ExportCsvAsync methods as specified in design
    - _Requirements: 1.1, 2.1, 5.1, 6.2, 10.1_

  - [x] 2.2 Implement ExpenseInsightsService — period resolution and validation
    - Implement ResolvePeriod method for CurrentMonth, PreviousMonth, CurrentQuarter, CurrentYear using PnlPeriodType enum
    - Implement ValidateCustomRange with start <= end and max 366 days rules
    - Obtain BusinessId from ICurrentTenantService.CurrentBusinessId; return empty results if BusinessId == 0
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 13.4, 13.5_

  - [x] 2.3 Write property test for period resolution (Property 3)
    - **Property 3: Period resolution correctness**
    - For any reference date and period type, verify resolved start/end dates match the spec rules
    - **Validates: Requirements 2.2, 2.3, 2.4, 2.5**

  - [x] 2.4 Write property test for custom range validation (Property 4)
    - **Property 4: Custom range validation**
    - For any pair of DateOnly values, verify accept/reject logic matches spec
    - **Validates: Requirements 2.6, 2.7**

  - [x] 2.5 Implement ExpenseInsightsService — category aggregation (GetInsightsDataAsync)
    - Query non-cancelled purchases filtered by BusinessId, InvoiceDate within [startDate, endDate]
    - Group by ExpenseCategoryId, sum TotalAmount per group
    - Compute PercentageOfTotal rounded to 2dp
    - Map ExpenseType to name ("Services", "Goods", or "Uncategorised" for null)
    - Order by TotalSpend descending
    - Include inactive categories if they have purchases in period
    - Return empty breakdown with zero totals when no data exists
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 13.1, 13.2_

  - [x] 2.6 Write property test for category aggregation (Property 1)
    - **Property 1: Category aggregation correctness**
    - Generate random Purchase records, verify sum/order/filter/grouping
    - **Validates: Requirements 1.1, 1.4, 1.5**

  - [x] 2.7 Write property test for percentage invariant (Property 2)
    - **Property 2: Percentage invariant**
    - For any non-empty breakdown, verify sum of PercentageOfTotal ≈ 100.0
    - **Validates: Requirements 1.2**

- [x] 3. Implement variance, budget, and supplier logic
  - [x] 3.1 Implement Month-Over-Month variance computation
    - Fetch previous month purchases (calendar month immediately preceding the period's first month)
    - Compute variance per category using the formula: ((current - previous) / previous) × 100 rounded to 1dp
    - Handle special cases: "N/A" (no prior data), "New" (prev=0, curr>0), "—" (both zero), "-100.0" (curr=0, prev>0)
    - _Requirements: 9.1, 9.4, 9.5, 9.6, 9.7_

  - [x] 3.2 Write property test for MoM variance (Property 7)
    - **Property 7: Month-over-month variance computation**
    - For any pair (currentSpend, previousSpend) and hasPreviousData flag, verify formula and special cases
    - **Validates: Requirements 9.1, 9.4, 9.5, 9.6, 9.7**

  - [x] 3.3 Implement budget status computation and UpsertBudgetLimitAsync
    - Fetch ExpenseCategoryLimit records for BusinessId
    - Classify each category: Exceeded (≥100%), Approaching (≥80% and <100%), WithinLimit (<80%), NoLimit (null limit)
    - Implement UpsertBudgetLimitAsync: create new record if none exists, update if exists, set null to clear
    - Validate budget values: positive, >0, ≤999,999,999.99
    - Compute BudgetExceededCount and BudgetApproachingCount for summary banner
    - _Requirements: 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 7.1, 7.2, 7.3, 7.4, 7.6_

  - [x] 3.4 Write property test for budget threshold classification (Property 5)
    - **Property 5: Budget status threshold classification**
    - For any spend ≥ 0 and any limit (positive or null), verify classification matches spec
    - **Validates: Requirements 7.1, 7.2, 7.3**

  - [x] 3.5 Implement top suppliers per category
    - Group purchases within each category by SupplierId
    - Sum TotalAmount per supplier, order descending by spend then ascending by SupplierId for ties
    - Take top 3, compute PercentageOfCategory rounded to 1dp
    - Return fewer than 3 if category has fewer suppliers; return empty list if category total is zero
    - Filter by BusinessId for tenant isolation
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 13.3_

  - [x] 3.6 Write property test for top suppliers ranking (Property 6)
    - **Property 6: Top suppliers ranking**
    - Generate random multi-supplier purchases, verify top-3 order, tie-breaking, and percentage
    - **Validates: Requirements 8.1, 8.3, 8.4**

- [x] 4. Implement trend data and CSV export
  - [x] 4.1 Implement GetTrendDataAsync
    - Compute monthly totals per category for the last 12 calendar months (ending at current UTC month)
    - Include zero-spend months as 0 (no gaps)
    - Limit to top 5 categories by total 12-month spend; exclude categories with zero total across entire window
    - Set HasSufficientData = false if fewer than 2 distinct months have data
    - Format month labels as "MMM yyyy"
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 4.2 Implement ExportCsvAsync
    - Generate UTF-8 comma-delimited CSV with header: "Category Name,Expense Type,Total Spend,Percentage of Total,Month-Over-Month Variance,Budget Limit,Budget Status"
    - One data row per category with spend in period
    - Numeric values: TotalSpend 2dp no symbol, PercentageOfTotal 1dp no %, Variance 2dp with minus prefix, BudgetLimit 2dp or empty
    - BudgetStatus text: "Exceeded", "Approaching", "Within Limit", "No Limit"
    - Filename format: "ExpenseInsights_[BusinessName]_[StartDate]_[EndDate].csv" (YYYYMMDD, spaces→underscores, special chars removed)
    - Escape fields per RFC 4180
    - Return header-only CSV if no data; default to current month if no period selected
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 10.6, 10.7_

  - [x] 4.3 Write property test for CSV export round-trip (Property 8)
    - **Property 8: CSV export round-trip**
    - Generate random breakdown data, verify CSV parses back to same count/values
    - **Validates: Requirements 10.1, 10.2, 10.4**

  - [x] 4.4 Write property test for tenant isolation (Property 9)
    - **Property 9: Tenant isolation invariant**
    - Generate multi-tenant purchase data, verify service never returns cross-tenant records
    - **Validates: Requirements 13.1, 13.2, 13.3, 13.4**

- [x] 5. Checkpoint — Ensure all service-layer tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement ExpenseInsightController
  - [x] 6.1 Create ExpenseInsightController with [ModuleAccess(PortalModules.ExpenseInsights)]
    - Inject IExpenseInsightsService, ICurrentTenantService, and any required helpers
    - Implement Index action: resolve CurrentMonth, call GetInsightsDataAsync + GetTrendDataAsync, build ExpenseInsightsViewModel, fetch BudgetConfig (active categories with current limits), return View
    - _Requirements: 3.1, 3.2, 11.3_

  - [x] 6.2 Implement AxGetInsightsData endpoint
    - Accept PnlPeriodType, optional startDate, endDate
    - Validate custom range if applicable; return JSON error if invalid
    - Call GetInsightsDataAsync, return JSON { success, data }
    - Wrap in try/catch returning error JSON on exception
    - _Requirements: 2.8, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

  - [x] 6.3 Implement AxGetTrendData endpoint
    - Call GetTrendDataAsync, return JSON { success, data }
    - _Requirements: 5.1, 5.2, 5.5_

  - [x] 6.4 Implement ExportCsv endpoint
    - Accept PnlPeriodType, optional dates; call ExportCsvAsync
    - Return File result with Content-Disposition attachment
    - _Requirements: 10.1, 10.3_

  - [x] 6.5 Implement AxPostUpdateBudget endpoint
    - Accept expenseCategoryId, periodLimitEur
    - Validate budget value (positive, ≤999,999,999.99 or null to clear)
    - Call UpsertBudgetLimitAsync, return JSON success/error
    - Include [ValidateAntiForgeryToken]
    - _Requirements: 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

- [x] 7. Implement Views and JavaScript
  - [x] 7.1 Create Index.cshtml for Expense Insights
    - Topbar with eyebrow "Purchasing Analytics", heading "Expense Insights", subtitle
    - Period selector card with buttons (Current Month, Previous Month, Current Quarter, Current Year, Custom) and custom date inputs
    - Export CSV button in period row
    - Budget alerts banner showing exceeded/approaching counts
    - Summary cards grid (Total Spend, Categories Active, Top Category, Avg Per Category)
    - Charts grid: pie chart canvas + bar chart canvas (side by side on desktop, stacked on mobile)
    - Category breakdown table with expandable rows (chevron, columns: Category, Type, Spend, % of Total, MoM Variance, Budget, Status)
    - Sub-rows for top suppliers (hidden by default, shown on expand)
    - Trend line chart section
    - Budget configuration section with editable limit inputs and save buttons
    - Empty state messaging when HasData is false
    - Responsive: stack charts vertically <768px, horizontal scroll on table <768px, 44px touch targets
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 5.2, 5.5, 6.1, 7.1, 7.2, 7.3, 7.4, 7.5, 8.2, 9.2, 9.3, 14.1, 14.2, 14.3, 14.4, 14.5, 14.6_

  - [x] 7.2 Implement client-side JavaScript for Expense Insights
    - AJAX period switching: on period button click, BlockUI.show → fetch AxGetInsightsData → BlockUI.hide → update DOM (summary cards, charts, breakdown table, alerts banner)
    - Custom date range: show/hide date inputs, validate client-side (start ≤ end), trigger reload
    - Chart.js initialization: pie chart (doughnut type), bar chart (horizontal bars sorted desc), line chart (trend, top 5 categories, one line per category)
    - Chart tooltips showing category name, formatted spend (currency symbol + 2dp), and percentage (1dp)
    - Colour palette from MyChair Design System: Primary Blue, Cyan, Green, Amber, then accent cycle
    - Row expansion: toggle chevron, show/hide supplier sub-rows for clicked category
    - CSV export: navigate to ExportCsv URL with current period params
    - Budget save: on save click, BlockUI.show → POST AxPostUpdateBudget with antiforgery token → BlockUI.hide → Swal.fire success/error
    - Handle empty states: hide charts/table, show message when no data
    - Trend chart: show "insufficient data" message when HasSufficientData is false
    - _Requirements: 2.8, 4.5, 4.6, 5.2, 6.2, 6.3, 7.5, 10.1_

- [x] 8. DI Registration and Navigation
  - [x] 8.1 Register IExpenseInsightsService in Program.cs
    - Add `builder.Services.AddScoped<IExpenseInsightsService, ExpenseInsightsService>();`
    - _Requirements: 1.1, 2.1_

  - [x] 8.2 Add sidebar navigation link for Expense Insights
    - Add "Expense Insights" link in the Finance/Purchasing section of the sidebar
    - Link to /ExpenseInsight route
    - Use appropriate icon consistent with existing sidebar style
    - Show link only when business has access to the module (check plan)
    - _Requirements: 11.2, 11.3_

  - [x] 8.3 Add Purchase list soft-gate teaser for Starter plan users
    - Add locked teaser card below purchase data table on the Purchase list page
    - Show lock icon, "Expense Insights" heading, value description, "Learn More" CTA
    - Link CTA to PlanSoftGate view with module key `expense_insights`
    - Only render when business is on Starter plan; hide for Professional/Enterprise
    - Do not render if ISubscriptionPlanService is unavailable or no active plan
    - _Requirements: 12.1, 12.2, 12.3, 12.4_

- [x] 9. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The design uses C# throughout — no language selection required
- No new database tables needed; all data sourced from existing entities (Purchase, ExpenseCategory, ExpenseCategoryLimit, Supplier, ExpenseType)
- BudgetStatus enum is used internally; CSV output maps to display strings ("Exceeded", "Approaching", "Within Limit", "No Limit")
- The `[ModuleAccess(PortalModules.ExpenseInsights)]` attribute and `PortalModules.ExpenseInsights` constant already exist per context
- Follow BlockUI + SweetAlert2 patterns for all AJAX interactions per UI Feedback standards
- Use full table names in any raw SQL queries per repository standards

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "2.5"] },
    { "id": 3, "tasks": ["2.3", "2.4", "2.6", "2.7", "3.1", "3.3", "3.5"] },
    { "id": 4, "tasks": ["3.2", "3.4", "3.6", "4.1", "4.2"] },
    { "id": 5, "tasks": ["4.3", "4.4"] },
    { "id": 6, "tasks": ["6.1", "6.2", "6.3", "6.4", "6.5"] },
    { "id": 7, "tasks": ["7.1", "7.2", "8.1", "8.2", "8.3"] }
  ]
}
```
