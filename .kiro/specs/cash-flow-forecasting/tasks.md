# Implementation Plan: Cash Flow Forecasting

## Overview

This plan implements the Cash Flow Forecasting module — a forward-looking projection feature that computes inflows (outstanding invoices weighted by customer payment behaviour), outflows (6-month purchase averages per expense category), and a running balance chart. The module is gated to the Professional plan via the existing `[ModuleAccess]` infrastructure and includes a dashboard widget and soft-gate teaser for Starter users.

Implementation follows a bottom-up approach: database → entity → DTOs → service → controller → views → integration.

## Tasks

- [x] 1. Database migration and entity setup
  - [x] 1.1 Create SQL migration file for `[cashflow]` schema and `CashFlowSettings` table
    - Create migration file `Portal.Database/Migrations/XXX_CreateCashFlowSettingsTable.sql`
    - Include `CREATE SCHEMA [cashflow]` with existence check
    - Create `[cashflow].[CashFlowSettings]` table with columns: Id (PK, identity), BusinessId (FK to [portal].[Business]), StartingBalance (decimal 18,2), AlertThreshold (decimal 18,2), CreatedAtUtc, UpdatedAtUtc
    - Add unique constraint on BusinessId, check constraints for non-negative values, index on BusinessId
    - Include `USE [Portal]` header per SQL standards
    - _Requirements: 1.1_

  - [x] 1.2 Create `CashFlowSettings` entity class
    - Create `Portal.Infrastructure/Entities/CashFlowSettings.cs`
    - Properties: Id, BusinessId, StartingBalance, AlertThreshold, CreatedAtUtc, UpdatedAtUtc
    - Navigation property: Business
    - _Requirements: 1.1_

  - [x] 1.3 Add EF Core configuration and DbSet registration
    - Add `ConfigureCashFlowSettings(modelBuilder)` method in PortalDbContext (or appropriate configuration class)
    - Map to table `CashFlowSettings` in schema `cashflow`
    - Configure HasOne relationship to Business, unique index on BusinessId, check constraints, default values
    - Add `DbSet<CashFlowSettings> CashFlowSettings` to PortalDbContext
    - _Requirements: 1.1_

- [x] 2. Create DTOs and service interface
  - [x] 2.1 Create all Cash Flow DTO classes
    - Create `Portal.Infrastructure/Models/CashFlow/` directory
    - Create `CashFlowProjectionDto.cs` with: StartingBalance, AlertThreshold, TotalInflows, TotalOutflows, ProjectedBalance, DailyBalances, Inflows, Outflows, AlertBreachDate
    - Create `DailyBalanceDto.cs` with: Date (DateOnly), Balance
    - Create `InflowItemDto.cs` with: InvoiceId, CustomerName, InvoiceNumber, OutstandingAmount, OriginalDueDate, AdjustedDueDate, DaysLateAverage
    - Create `OutflowCategoryDto.cs` with: ExpenseCategoryId, CategoryName, AverageMonthlyAmount, MonthsOfData
    - Create `CashFlowSettingsDto.cs` with: StartingBalance, AlertThreshold, UpdatedAtUtc
    - Create `CashFlowWidgetDto.cs` with: ProjectedBalance30Days, NetInflow, HasAlertBreach, AlertBreachDate, HasSettings
    - _Requirements: 2.1–2.6, 4.1–4.4, 5.1, 6.1–6.4, 8.1–8.5_

  - [x] 2.2 Create `ICashFlowService` interface
    - Create `Portal.Infrastructure/Services/ICashFlowService.cs`
    - Methods: GetProjectionAsync, GetSettingsAsync, SaveSettingsAsync, GetWidgetDataAsync
    - Include XML doc comments per design
    - _Requirements: 1.2, 1.5, 2.1, 5.1, 8.1, 12.1_

- [x] 3. Implement CashFlowService computation logic
  - [x] 3.1 Create `CashFlowService` class with constructor injection
    - Create `Portal.Infrastructure/Services/CashFlowService.cs`
    - Inject PortalDbContext
    - Implement `GetSettingsAsync` — return settings for businessId or null if none exist
    - Implement `SaveSettingsAsync` — upsert CashFlowSettings record (insert or update), set UpdatedAtUtc to DateTime.UtcNow
    - _Requirements: 1.1, 1.5, 11.4_

  - [x] 3.2 Implement inflow computation within `GetProjectionAsync`
    - Query outstanding invoices (InvoiceFinancialStatusTypeId in 1, 2, 4) filtered by businessId
    - Compute outstanding amount: TotalAmount for Unpaid/Overdue; TotalAmount − sum(non-voided payments) for PartiallyPaid
    - Exclude invoices in `excludedInvoiceIds` array
    - _Requirements: 2.1, 2.2, 2.3, 7.1, 7.2, 11.1, 11.2_

  - [x] 3.3 Implement customer confidence weighting (DaysLateAverage)
    - For each customer with outstanding invoices, compute mean of max(0, PaymentDateUtc − DueDate) in days across all non-voided payments
    - Round to nearest integer; default to 0 if no payment history
    - Compute AdjustedDueDate = DueDate + DaysLateAverage days
    - Position inflow at max(today, AdjustedDueDate)
    - Filter to only include inflows within projection horizon (today + daysAhead)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 2.4, 2.5, 2.6_

  - [x] 3.4 Implement outflow computation
    - Query non-cancelled purchases from last 6 months filtered by businessId
    - Group by ExpenseCategory; count distinct months per category
    - Exclude categories with fewer than 2 distinct months of data
    - Calculate monthly average = sum(TotalAmount) / distinctMonths
    - Spread daily: monthlyAverage / daysInMonth for each day in projection
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 11.3_

  - [x] 3.5 Implement running balance and alert breach detection
    - Build DailyBalances array: for each day, balance = StartingBalance + cumulative inflows − cumulative outflows
    - Compute TotalInflows, TotalOutflows, ProjectedBalance (final day balance)
    - Detect AlertBreachDate = first day where balance < AlertThreshold (null if never breached)
    - Sort Inflows by AdjustedDueDate ascending; sort Outflows by AverageMonthlyAmount descending
    - _Requirements: 5.1, 5.4, 5.5, 6.3, 6.4, 8.3_

  - [x] 3.6 Implement `GetWidgetDataAsync`
    - Call GetProjectionAsync with daysAhead=30 and no exclusions
    - Map to CashFlowWidgetDto: ProjectedBalance30Days, NetInflow, HasAlertBreach, AlertBreachDate, HasSettings
    - _Requirements: 8.1, 8.2, 8.3, 8.5_

- [x] 4. Create CashFlowController
  - [x] 4.1 Create controller with `[ModuleAccess(PortalModules.Cashflow)]`
    - Create `Portal.Web/Controllers/CashFlowController.cs`
    - Add `[Authorize]` and `[ModuleAccess(PortalModules.Cashflow)]` attributes
    - Inject ICashFlowService and ICurrentTenantService
    - Implement `Index()` — returns View
    - _Requirements: 9.2, 9.3_

  - [x] 4.2 Implement AJAX endpoints
    - `AxGetProjection(int daysAhead = 30, string? excludedInvoiceIds = null)` — parse excluded IDs from comma-separated string, call service, return JSON
    - `AxGetSettings()` — call service, return settings JSON
    - `AxPostSaveSettings(decimal startingBalance, decimal alertThreshold)` — validate non-negative, call service, return JSON with SweetAlert-compatible response
    - `AxGetWidget()` — call service, return widget DTO JSON
    - All endpoints use try/catch with `Json(new { success, message/data })` pattern
    - _Requirements: 1.2, 1.3, 1.4, 1.6, 5.3, 7.1, 12.1_

- [x] 5. Register service and add module constant
  - [x] 5.1 Register ICashFlowService in DI container
    - Add `builder.Services.AddScoped<ICashFlowService, CashFlowService>()` in Program.cs (or appropriate service registration location)
    - Verify `PortalModules.Cashflow` constant exists (should already be present per context)
    - _Requirements: 9.3_

- [x] 6. Checkpoint — backend complete
  - Ensure the project compiles, all DI registrations resolve, and the migration script is ready.
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Create the Cash Flow Index view
  - [x] 7.1 Create `Views/CashFlow/Index.cshtml` — page shell and layout
    - Topbar with breadcrumb: "Revenue Control" > "Cash Flow Forecast"
    - Hero card with projected balance summary and period selector (30/60/90 days)
    - KPI summary strip: Starting Balance, Total Inflows, Total Outflows, Projected Balance
    - Tip bar section
    - Flow visualization section (Money In vs Money Out)
    - All sections render as `.glass.card-pad` cards per layout standards
    - Include `@section Scripts` with Chart.js + chartjs-plugin-annotation CDN references
    - _Requirements: 5.2, 5.3, 5.6, 6.1, 6.2_

  - [x] 7.2 Implement inflow breakdown table with scenario toggle switches
    - Table columns: Customer Name, Invoice #, Outstanding Amount, Original Due Date, Adjusted Due Date, Days Late Avg, Include toggle switch
    - Toggle switch triggers scenario exclusion (client-side state update + re-fetch)
    - Visually indicate excluded invoices (dimmed row or strikethrough)
    - Ordered by AdjustedDueDate ascending
    - _Requirements: 6.1, 6.3, 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 7.3 Implement outflow breakdown table
    - Table columns: Category Name, Avg Monthly Amount, Months of Data
    - Ordered by AverageMonthlyAmount descending
    - _Requirements: 6.2, 6.4_

  - [x] 7.4 Implement settings card (inline on same page)
    - Starting Balance input (non-negative decimal)
    - Alert Threshold input (non-negative decimal)
    - Save button triggers `AxPostSaveSettings` with BlockUI + SweetAlert2 pattern
    - Display LastUpdatedUtc below inputs
    - _Requirements: 1.2, 1.3, 1.4, 1.5, 1.6_

- [x] 8. Implement Chart.js rendering and client-side logic
  - [x] 8.1 Implement Chart.js line chart with annotation plugin
    - Line chart with daily balances on x-axis (date), balance on y-axis (currency formatted)
    - Alert threshold as horizontal dashed line (colour: #C8912E)
    - Danger zone shading below threshold (colour: rgba(194, 74, 74, 0.06))
    - Chart.js line with fill, smooth tension (0.3), no point markers except on hover
    - Labelled data points at day 30, 60, 90 milestones
    - _Requirements: 5.2, 5.4, 5.5, 5.6_

  - [x] 8.2 Implement period selector interaction
    - Buttons for 30/60/90 days with active state indicator
    - Duplicate period selector near chart (sticky — v1 addition)
    - On selection change: re-call `AxGetProjection` with new daysAhead, re-render all sections
    - Uses BlockUI → fetch → BlockUI.hide() → update DOM pattern
    - _Requirements: 5.3_

  - [x] 8.3 Implement scenario toggle JavaScript
    - Maintain `excludedInvoiceIds` array in client-side state (not persisted)
    - On toggle switch change: add/remove invoice ID from array, re-call `AxGetProjection` with exclusion list
    - On page reload: reset exclusions to empty (all included)
    - BlockUI during re-fetch; no SweetAlert needed (quick operation with visual update)
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 9. Create dashboard widget
  - [x] 9.1 Create `Views/Shared/_CashFlowWidget.cshtml` partial
    - Compact card with mini Chart.js line chart (~60px height)
    - Display projected balance at day 30 as numeric value
    - Net inflow indicator
    - Warning badge if breach within 30 days (show breach date)
    - If no settings configured: show setup prompt with link to /CashFlow
    - Clickable: navigates to full Cash Flow page
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [x] 9.2 Integrate widget on Home Dashboard
    - Conditionally render widget when business has Professional/Enterprise plan AND CashFlow_Settings exists
    - Call `AxGetWidget` endpoint on dashboard load for widget data
    - Follow existing dashboard widget pattern for placement
    - _Requirements: 8.1, 8.5_

- [x] 10. Add navigation and plan gating UI
  - [x] 10.1 Add sidebar navigation link for Cash Flow
    - Add "Cash Flow" link under Revenue Control section in sidebar
    - Link to `/CashFlow`
    - Only visible when business has access to cashflow module
    - _Requirements: 9.2_

  - [x] 10.2 Add soft-gate teaser card on Revenue Dashboard for Starter users
    - Locked card with brief description of Cash Flow module
    - Shows only when business is on Starter plan
    - Click navigates to soft-gate upgrade view for cashflow module
    - Hidden when on Professional/Enterprise plan
    - _Requirements: 10.1, 10.2, 10.3_

- [x] 11. Checkpoint — full feature integration
  - Ensure all views render correctly, AJAX endpoints respond, Chart.js renders with sample data.
  - Verify plan gating: Starter blocked, Professional allowed.
  - Verify scenario toggles re-render the chart correctly.
  - Ensure all tests pass, ask the user if questions arise.

- [ ]* 12. Property-based tests — settings and validation
  - [ ]* 12.1 Write property test for settings round-trip
    - **Property 1: Settings persistence round-trip**
    - For any valid starting balance (≥ 0) and alert threshold (≥ 0), saving then retrieving SHALL return same values
    - Use FsCheck.Xunit with MaxTest = 100
    - **Validates: Requirements 1.5**

  - [ ]* 12.2 Write property test for non-negative validation
    - **Property 2: Non-negative validation rejects invalid inputs**
    - For any decimal < 0, submitting as StartingBalance or AlertThreshold SHALL fail validation
    - **Validates: Requirements 1.3, 1.4, 1.6**

- [ ]* 13. Property-based tests — inflow computation
  - [ ]* 13.1 Write property test for inflow status filtering
    - **Property 3: Inflow status filtering**
    - For any set of invoices, projection inflows SHALL contain only invoices with status 1, 2, or 4
    - **Validates: Requirements 2.1**

  - [ ]* 13.2 Write property test for outstanding amount calculation
    - **Property 4: Outstanding amount calculation**
    - For any invoice, projected amount SHALL equal TotalAmount minus sum of non-voided payments
    - **Validates: Requirements 2.2, 2.3**

  - [ ]* 13.3 Write property test for adjusted due date with today floor
    - **Property 5: Adjusted due date positioning with today floor**
    - For any invoice, projected date SHALL be max(today, DueDate + DaysLateAverage)
    - **Validates: Requirements 2.4, 2.5**

  - [ ]* 13.4 Write property test for horizon boundary filtering
    - **Property 6: Horizon boundary filtering**
    - For any projection, all inflows SHALL have AdjustedDueDate between today and today + daysAhead
    - **Validates: Requirements 2.6**

  - [ ]* 13.5 Write property test for days-late average computation
    - **Property 7: Days-late average computation**
    - For any customer payment history, DaysLateAverage SHALL equal round(mean(max(0, PaymentDate − DueDate)))
    - **Validates: Requirements 3.1, 3.2, 3.4**

- [ ]* 14. Property-based tests — outflow and running balance
  - [ ]* 14.1 Write property test for outflow category average with threshold
    - **Property 8: Outflow category average with minimum months threshold**
    - Categories with < 2 months data excluded; included categories have correct average
    - **Validates: Requirements 4.1, 4.3, 4.4**

  - [ ]* 14.2 Write property test for daily outflow distribution
    - **Property 9: Daily outflow distribution**
    - Total daily outflows for a category across a full month SHALL equal AverageMonthlyAmount (±0.01)
    - **Validates: Requirements 4.2**

  - [ ]* 14.3 Write property test for running balance invariant
    - **Property 10: Running balance invariant**
    - Balance on day N SHALL equal StartingBalance + sum(inflows days 1..N) − sum(outflows days 1..N)
    - **Validates: Requirements 5.1**

  - [ ]* 14.4 Write property test for scenario exclusion impact
    - **Property 11: Scenario exclusion impact**
    - Excluding invoices SHALL reduce projected balance by exactly their positioned inflow amounts
    - **Validates: Requirements 7.1, 7.2**

- [ ]* 15. Property-based tests — sorting, alerts, and isolation
  - [ ]* 15.1 Write property test for inflow sort order
    - **Property 12: Inflow sort order**
    - Inflows SHALL be ordered by AdjustedDueDate ascending
    - **Validates: Requirements 6.3**

  - [ ]* 15.2 Write property test for outflow sort order
    - **Property 13: Outflow sort order**
    - Outflow categories SHALL be ordered by AverageMonthlyAmount descending
    - **Validates: Requirements 6.4**

  - [ ]* 15.3 Write property test for alert threshold breach detection
    - **Property 14: Alert threshold breach detection**
    - AlertBreachDate SHALL be first day where balance < AlertThreshold, or null if no breach
    - **Validates: Requirements 8.3**

  - [ ]* 15.4 Write property test for tenant isolation
    - **Property 15: Tenant isolation**
    - Projection for businessId X SHALL only contain data from that business
    - **Validates: Requirements 11.1, 11.2, 11.3, 11.4**

  - [ ]* 15.5 Write property test for on-demand freshness
    - **Property 16: On-demand freshness**
    - Data mutation between two projection requests SHALL be reflected in the second request
    - **Validates: Requirements 12.1, 12.3**

- [x] 16. Final checkpoint
  - Ensure all tests pass, the full page renders with live data, widget appears on dashboard.
  - Verify plan permission gating end-to-end.
  - Verify scenario toggles, period selector, and settings save all function correctly.
  - Ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional property-based tests and can be skipped for faster MVP delivery
- The `cashflow` module key is already seeded in the PlanFeature table — no seeding migration needed
- Scenario exclusions are session-only (client-side JS array), never persisted to the database
- Chart.js + chartjs-plugin-annotation should be loaded via CDN in the view's Scripts section
- All AJAX follows the BlockUI → fetch → BlockUI.hide() → SweetAlert2 pattern per project standards
- Property tests use FsCheck.Xunit with `MaxTest = 100` iterations each
- The service computation logic should be structured with internal static/testable methods to enable PBT without database dependencies
