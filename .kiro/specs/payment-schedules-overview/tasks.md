# Implementation Plan: Payment Schedules Overview

## Overview

This plan implements a read-only overview page at `/Revenue/PaymentSchedules` that aggregates active payment schedule data into KPI cards, a monthly timeline, and a filterable/paginated table. No new database tables are needed — the page reuses existing `[revenue].[PaymentSchedule]` and `[revenue].[PaymentScheduleInstalment]` entities with a single JOIN query.

## Tasks

- [x] 1. Create DTOs and raw row model
  - [x] 1.1 Create `ScheduleOverviewRawRow` model class
    - Add to `Portal.Infrastructure/Models/` (or appropriate subfolder)
    - Properties: `ScheduleId`, `InvoiceId`, `InvoiceNumber`, `CustomerName`, `CustomerId`, `InstalmentId`, `Amount`, `MatchedAmount`, `DueDate` (DateOnly?), `SequenceNumber`
    - _Requirements: 5.1, 5.3, 5.4, 5.5, 5.6_
  - [x] 1.2 Create response DTOs (`PaymentScheduleOverviewDto`, `OverviewKpiDto`, `MonthlyTimelineEntryDto`, `ScheduleTableRowDto`)
    - Add to `Portal.Infrastructure/Models/`
    - `PaymentScheduleOverviewDto`: contains `Kpis`, `Timeline`, `Schedules`, `AvailableYears`, `CurrencySymbol`
    - `OverviewKpiDto`: `TotalScheduled`, `Collected`, `DueThisMonth`, `Overdue`
    - `MonthlyTimelineEntryDto`: `Year`, `Month`, `MonthName`, `TotalAmount`, `InstalmentCount`, `HasOverdue`, `IsNoDueDate`
    - `ScheduleTableRowDto`: `ScheduleId`, `InvoiceId`, `InvoiceNumber`, `CustomerName`, `ScheduleTotal`, `Paid`, `Remaining`, `NextDue`, `ProgressPercentage`, `Status`
    - _Requirements: 2.1–2.7, 3.1–3.9, 5.1–5.11_

- [x] 2. Implement repository
  - [x] 2.1 Create `PaymentScheduleOverviewRepository`
    - Add to `Portal.Infrastructure/Repositories/`
    - Extends `GenericStoredProcedureRepository<ScheduleOverviewRawRow>`
    - Implement `GetActiveSchedulesWithInstalmentsAsync(int businessId)` method
    - Single SQL query with JOINs across `[revenue].[PaymentSchedule]`, `[revenue].[PaymentScheduleInstalment]`, `[revenue].[Invoice]`, `[customer].[Customer]`
    - Filter: `WHERE [revenue].[PaymentSchedule].[BusinessId] = @BusinessId AND [revenue].[PaymentSchedule].[IsActive] = 1`
    - Order by `[revenue].[PaymentSchedule].[Id]`, `[revenue].[PaymentScheduleInstalment].[SequenceNumber]`
    - Use full table names in SQL (no aliases), try/catch with `(Exception ex) { throw; }`
    - _Requirements: 7.1, 7.5_

- [x] 3. Implement service layer
  - [x] 3.1 Create `IPaymentScheduleOverviewService` interface
    - Add to `Portal.Infrastructure/Services/`
    - Single method: `Task<PaymentScheduleOverviewDto> GetOverviewAsync(int businessId)`
    - _Requirements: 2.1–2.7, 3.1–3.9, 5.1–5.11_
  - [x] 3.2 Create `PaymentScheduleOverviewService` implementation
    - Inject `PaymentScheduleOverviewRepository`, `IInstalmentStatusEngine`, `IBusinessService`
    - Fetch raw rows from repository
    - Compute instalment status using `InstalmentStatusEngine.DetermineStatus()` for each instalment
    - Group rows by `ScheduleId`
    - Aggregate KPIs: `TotalScheduled` (sum of amounts), `Collected` (sum of matched), `DueThisMonth` (current month due/overdue/pending), `Overdue` (amount minus matched for overdue instalments)
    - Build monthly timeline: group by year/month, separate null-date instalments into "No date assigned" entry, compute `HasOverdue` per month, extract `AvailableYears`
    - Build table rows: compute `ScheduleTotal`, `Paid`, `Remaining`, `NextDue` (earliest due/overdue/pending date, overdue priority), `ProgressPercentage` (capped at 100), `Status` ("Completed"/"Has Overdue"/"On Track")
    - Sort table: overdue-first, then by `NextDue` ascending
    - Retrieve `CurrencySymbol` from business profile (default `€`)
    - _Requirements: 2.1–2.7, 3.1–3.9, 5.1–5.11_
  - [ ]* 3.3 Write unit tests for `PaymentScheduleOverviewService`
    - Test empty state (no active schedules → zero KPIs, empty arrays)
    - Test single schedule KPI calculation
    - Test overdue amount calculation (only overdue instalments)
    - Test due-this-month calculation (current month only)
    - Test timeline grouping by month and null-date handling
    - Test table sort order (overdue-first, then by NextDue)
    - Test progress percentage calculation and 100% cap
    - Test schedule status determination (Completed/Has Overdue/On Track)
    - _Requirements: 2.1–2.7, 3.1–3.9, 5.1–5.11_

- [x] 4. Checkpoint - Ensure service layer compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Add controller endpoints
  - [x] 5.1 Add `PaymentSchedules` page action to `RevenueController`
    - `[HttpGet]` action returning `View()`
    - Permission check: lookup `schedule_payments` access level via `_permissionService.GetAccessLevelAsync()`
    - If access level is "none", redirect to `Dashboard`
    - _Requirements: 1.3, 1.4_
  - [x] 5.2 Add `AxGetPaymentSchedulesOverview` AJAX endpoint to `RevenueController`
    - `[HttpGet]` action returning `Json(new { success, data })`
    - Permission check: return `{ success: false, message: "..." }` if no access
    - Call `_paymentScheduleOverviewService.GetOverviewAsync(businessId)`
    - Wrap in try/catch with `(Exception ex) { throw; }` pattern — catch returns error JSON
    - _Requirements: 7.1, 7.5_
  - [x] 5.3 Inject `IPaymentScheduleOverviewService` into `RevenueController`
    - Add constructor parameter and private field
    - _Requirements: 7.5_

- [x] 6. Create Razor view
  - [x] 6.1 Create `Views/Revenue/PaymentSchedules.cshtml`
    - Topbar with eyebrow "Revenue", heading "Payment Schedules" (42px), subtitle "Monitor all instalment plans and expected payments across your invoices."
    - KPI cards section: 4 cards in a horizontal row (Total Scheduled/blue, Collected/green, Due This Month/amber, Overdue/red)
    - Monthly Payment Plan section: year selector buttons, horizontal bar chart rows (month name, proportional bar, amount, count), "No date assigned" row
    - Filter section: Status dropdown (All/Has Overdue/On Track/Completed), Invoice text input, Customer text input, Filter button, Clear button
    - Active Schedules table: columns Invoice (link), Customer, Schedule Total, Paid, Remaining, Next Due, Progress (mini bar + %), Status (colour badge)
    - Pagination: "Showing X-Y of Z" info + page number buttons
    - Empty state message when no schedules
    - Responsive: KPI 2×2 grid at ≤768px, hide instalment count, smaller heading
    - Follow `.glass.card-pad` card structure, filter card `margin-bottom:22px`, pagination `margin-top:18px`
    - _Requirements: 1.5, 1.6, 2.1–2.7, 3.1–3.9, 4.1–4.7, 5.1–5.11, 6.1–6.5, 8.1–8.3_

- [x] 7. Create JavaScript module
  - [x] 7.1 Create `wwwroot/js/payment-schedules-overview.js`
    - On page load: `BlockUI.show('Loading payment schedules...')` → fetch `/Revenue/AxGetPaymentSchedulesOverview` → `BlockUI.hide()` → render
    - If `!data.success`: show `Swal.fire({ icon: 'error', title: 'Error', text: data.message, confirmButtonColor: '#0D5EA6' })`
    - Render KPI cards with currency symbol formatting
    - Render monthly timeline: year selector buttons (default current year), proportional bars (max month = full width), overdue months in red with "(overdue)" suffix, "No date assigned" row
    - Year selector click: filter timeline to selected year (client-side, no server call)
    - Render Active Schedules table rows with: invoice link (`/Revenue/InvoiceDetail/{id}`), currency formatting, progress bar, colour-coded status badge
    - Client-side filtering: Status dropdown, Invoice text (case-insensitive contains), Customer text (case-insensitive contains)
    - Filter button: apply all filters simultaneously, reset to page 1
    - Clear button: reset all filters to defaults, show all rows, reset to page 1
    - Client-side pagination: 10 rows per page, "Showing X-Y of Z" info, page number buttons
    - Use vanilla `fetch` API (no jQuery for AJAX)
    - _Requirements: 3.2–3.9, 4.1–4.7, 5.2, 5.7–5.10, 6.1–6.5, 7.1–7.5_

- [x] 8. Sidebar navigation integration
  - [x] 8.1 Add "Payment Schedules" link to sidebar navigation
    - Position after the Cash Flow link in the Finance/Revenue section
    - Gate visibility with `schedule_payments` permission check
    - Add active-state class when on `PaymentSchedules` action
    - Use appropriate icon (checklist/schedule style SVG)
    - _Requirements: 1.1, 1.2_

- [x] 9. Dependency injection registration
  - [x] 9.1 Register `PaymentScheduleOverviewRepository` and `IPaymentScheduleOverviewService` in DI container
    - Register `PaymentScheduleOverviewRepository` as scoped
    - Register `PaymentScheduleOverviewService` as `IPaymentScheduleOverviewService` (scoped)
    - Add to the appropriate service registration file/method used by the project
    - _Requirements: 7.5_

- [x] 10. Final checkpoint - Build verification
  - Ensure the project compiles without errors
  - Verify the page loads correctly at `/Revenue/PaymentSchedules`
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- No new database tables or migrations are needed — this is a read-only aggregation page
- The `InstalmentStatusEngine` is already implemented and tested — this feature only calls it
- Client-side filtering and pagination keeps the implementation simple since the dataset is bounded (active schedules per business)
- All AJAX follows the BlockUI → fetch → unblock → SweetAlert2 pattern
- Currency formatting uses the business profile's `CurrencySymbol` (defaults to `€`)
