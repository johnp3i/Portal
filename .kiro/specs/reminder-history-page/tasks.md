# Implementation Plan: Reminder History Page

## Overview

Implement a dedicated Reminder History page under the Payment Reminders section that displays a paginated, filterable log of all payment reminders sent by the current business. The page follows the established MVC + Service pattern and reuses the standard layout (topbar → filter card → data table card → pagination).

## Tasks

- [ ] 1. Create DTOs and extend service interface
  - [ ] 1.1 Create ReminderHistoryItemDto and ReminderHistoryPageResult DTOs
    - Create `ReminderHistoryItemDto.cs` in `Portal.Infrastructure/Models/PaymentReminders/`
    - Create `ReminderHistoryPageResult.cs` in `Portal.Infrastructure/Models/PaymentReminders/`
    - ReminderHistoryItemDto includes: Id, SentAtUtc, InvoiceId, InvoiceNumber, CustomerName, EscalationTier, RecipientEmail, IsManualTrigger, IsTestSend, IsSentSuccessfully, IsOpened
    - ReminderHistoryPageResult includes: Items (List<ReminderHistoryItemDto>), TotalCount (int)
    - _Requirements: 5.2, 7.4, 8.14_

  - [ ] 1.2 Add GetAllReminderHistoryAsync method to IPaymentReminderService interface
    - Add method signature accepting businessId, tier, status, method, dateFrom, dateTo, customer, page, pageSize
    - Return type: `Task<ReminderHistoryPageResult>`
    - _Requirements: 8.1_

- [ ] 2. Implement service layer query
  - [ ] 2.1 Implement GetAllReminderHistoryAsync in PaymentReminderService
    - Build LINQ query against PaymentReminderLog DbSet with joins to Customer and Invoice
    - Filter by BusinessId for tenant isolation
    - Apply conditional filters: tier, status (Sent/Failed), method (Auto/Manual/Test), dateFrom, dateTo, customer (case-insensitive Contains)
    - Order by SentAtUtc descending
    - Count total matching records, then apply Skip/Take for pagination
    - Project results into ReminderHistoryItemDto
    - Wrap in try/catch with rethrow
    - _Requirements: 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10, 8.11, 8.12, 8.13, 8.14_

  - [ ]* 2.2 Write property tests for GetAllReminderHistoryAsync
    - **Property 1: Tenant Isolation** — all returned records have BusinessId == provided businessId
    - **Property 2: Tier Filter Correctness** — when tier is not "All"/null, all records have matching EscalationTier
    - **Property 3: Status Filter Correctness** — when "Sent", all have IsSentSuccessfully=true; when "Failed", all have IsSentSuccessfully=false
    - **Property 4: Method Filter Correctness** — Auto/Manual/Test conditions correctly enforced
    - **Property 5: Date Range Filter Correctness** — all records within dateFrom/dateTo bounds
    - **Property 6: Customer Text Search Correctness** — all records contain search string in CustomerName
    - **Property 7: Descending Chronological Order** — consecutive records are ordered by SentAtUtc desc
    - **Property 8: Pagination Slice Correctness** — returned item count equals min(pageSize, totalCount - offset)
    - **Validates: Requirements 7.3, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10, 8.11, 8.12, 8.13, 8.14**

- [ ] 3. Implement controller actions
  - [ ] 3.1 Add History() page action to PaymentReminderController
    - Add `[HttpGet]` action returning `View()`
    - Inherits class-level `[Authorize]` and `[ModuleAccess(PortalModules.PaymentReminderManual)]`
    - _Requirements: 1.2, 1.4_

  - [ ] 3.2 Add AxGetAllReminderHistory AJAX endpoint to PaymentReminderController
    - Add `[HttpGet]` action with parameters: tier, status, method, dateFrom, dateTo, customer, page (default 1), pageSize (default 20)
    - Get businessId from `_currentTenantService.CurrentBusinessId`
    - Call `_reminderService.GetAllReminderHistoryAsync(...)` and return JSON response
    - On success: `Json(new { success = true, data = result.Items, totalCount = result.TotalCount, page, pageSize })`
    - On error: `Json(new { success = false, message = "Failed to load reminder history." })`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [ ] 4. Checkpoint - Ensure backend compiles
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 5. Create the History view
  - [ ] 5.1 Create History.cshtml Razor view
    - Create `Portal.Web/Views/PaymentReminder/History.cshtml`
    - Topbar section: eyebrow "Payment Reminders", heading "Reminder History", muted description
    - Filter card (`.glass.card-pad`, `margin-bottom:22px`): Tier dropdown, Status dropdown, Method dropdown, Date From/To inputs, Customer text input, Filter/Clear buttons
    - Data table card (`.glass.card-pad`): table with columns Date, Invoice Number, Customer Name, Tier, Recipient Email, Method, Status, Opened
    - Empty state div hidden by default: "No reminders found matching your filters."
    - Pagination row below table: info text + page buttons
    - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 9.1, 9.2_

  - [ ] 5.2 Implement client-side JavaScript in History.cshtml
    - `loadHistory(page)`: builds URLSearchParams from filters, calls BlockUI.show(), fetches AxGetAllReminderHistory, calls BlockUI.hide(), then renderTable/renderPagination or shows Swal error
    - `renderTable(data)`: builds table rows with badge helpers for tier, method, status, opened columns; invoice number as hyperlink to `/Invoice/Detail/{InvoiceId}`
    - `renderPagination(totalCount, page, pageSize)`: renders "Showing X–Y of Z" info and page navigation buttons
    - Badge helper functions: `tierBadge`, `methodBadge`, `statusBadge`, `openedBadge`
    - `formatDate(dateStr)`: formats ISO date to `dd MMM yyyy`
    - `escapeHtml(str)`: XSS-safe escaping
    - `clearFilters()`: resets all filter values and calls loadHistory(1)
    - DOMContentLoaded calls `loadHistory(1)` automatically
    - _Requirements: 5.3, 5.4, 5.5, 6.1, 6.2, 6.3, 6.4_

- [ ] 6. Add sidebar navigation link
  - [ ] 6.1 Add "Reminder History" nav-sub-item to sidebar
    - Edit `Portal.Web/Views/Shared/Components/ModuleNavigation/Default.cshtml`
    - Add `<a class="nav-sub-item" href="/PaymentReminder/History">Reminder History</a>` under Payment Reminders section
    - Guarded by `hasPaymentReminderAccess` (same condition as existing Payment Reminder links)
    - _Requirements: 1.1, 1.3_

- [ ] 7. Final checkpoint - Ensure application compiles and page renders
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The page follows the identical layout pattern used by Upcoming.cshtml (topbar → filter card → data table card)
- All AJAX calls follow the BlockUI → fetch → BlockUI.hide() → Swal pattern per project standards
- Controller uses `AxGet` prefix per project Ajax naming convention

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "3.1", "3.2"] },
    { "id": 3, "tasks": ["5.1", "6.1"] },
    { "id": 4, "tasks": ["5.2"] }
  ]
}
```
