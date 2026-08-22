# Implementation Plan: Meetings Page Enhancements

## Overview

This plan converts the `/Sales/Meetings` page from a server-rendered flat list to an AJAX-driven, filterable, paginated view matching the established Tasks page pattern. Implementation proceeds bottom-up: DTOs → Repository → Service → Controller → View restructure → JS rewrite (filter + table + pagination + edit modal + bug fix). No database migrations are required — all changes are code-level.

## Tasks

- [x] 1. Create new DTOs
  - [x] 1.1 Create MeetingFilter and MeetingPagedListDto classes
    - Create `MeetingFilter` class in `Portal.Infrastructure/Models/Sales/` with properties: Status (string?), MeetingTypeId (int?), DateFrom (DateTime?), DateTo (DateTime?)
    - Create `MeetingPagedListDto` class in `Portal.Infrastructure/Models/Sales/` with properties: Id, Subject, MeetingTypeName, MeetingTypeId, ContactName, ContactId, LeadRequestId (int?), ScheduledAtUtc, DurationMinutes, Location, Notes, Outcome, IsCancelled, Urgency (string)
    - _Requirements: 1.2, 1.3, 2.1, 2.2, 2.3, 2.4, 6.1–6.5_

- [x] 2. Repository layer — GetPagedAsync
  - [x] 2.1 Implement MeetingRepository.GetPagedAsync method
    - Add method: `public async Task<(List<Meeting> Items, int TotalCount)> GetPagedAsync(int businessId, string? status, int? meetingTypeId, DateTime? dateFrom, DateTime? dateTo, int page, int pageSize)`
    - Follow `FollowUpTaskRepository.GetPagedAsync` pattern exactly: dynamic WHERE clause with parameter list, COUNT query first via DbConnection command, then data query with OFFSET/FETCH
    - Always filter by `[BusinessId] = @BusinessId AND [IsActive] = 1`
    - Status filter logic: "upcoming" → `[ScheduledAtUtc] > GETUTCDATE() AND [IsCancelled] = 0`; "completed" → `[ScheduledAtUtc] < GETUTCDATE() AND [IsCancelled] = 0`; "cancelled" → `[IsCancelled] = 1`
    - MeetingTypeId filter: `[MeetingTypeId] = @MeetingTypeId` when provided
    - DateFrom: `[ScheduledAtUtc] >= @DateFrom`; DateTo: `[ScheduledAtUtc] <= @DateTo` (add 1 day for end-of-day inclusive)
    - Order: `[ScheduledAtUtc] DESC`
    - Use full table name `[sales].[Meeting]`, no aliases, `catch (Exception ex) { throw; }`
    - _Requirements: 1.1, 1.2, 1.3, 2.7, 2.8, 2.9, 2.10, 2.11, 4.1, 5.1_

  - [ ]* 2.2 Write property test for status filter correctness
    - **Property 1: Status filter correctness**
    - Generate random lists of meetings with varying ScheduledAtUtc and IsCancelled values, apply each status filter predicate, verify only matching meetings are included
    - **Validates: Requirements 2.7, 2.8, 2.9**

  - [ ]* 2.3 Write property test for date range filter correctness
    - **Property 2: Date range filter correctness**
    - Generate random meetings and random date ranges, apply date filter logic, verify all returned meetings have ScheduledAtUtc within bounds
    - **Validates: Requirements 2.10, 2.11**

  - [ ]* 2.4 Write property test for page size invariant
    - **Property 3: Page size invariant**
    - Generate random total counts (0–200), verify response contains at most 15 items and totalPages = ceil(totalCount / 15)
    - **Validates: Requirements 4.1, 4.2**

  - [ ]* 2.5 Write property test for sort order invariant
    - **Property 4: Sort order invariant**
    - Generate random lists of meetings, apply sort, verify each ScheduledAtUtc >= next (descending order)
    - **Validates: Requirements 5.1**

- [x] 3. Service layer — GetMeetingsPagedAsync
  - [x] 3.1 Add GetMeetingsPagedAsync to IMeetingService and MeetingService
    - Add interface method: `Task<PagedResult<MeetingPagedListDto>> GetMeetingsPagedAsync(MeetingFilter filter, int page, int pageSize);`
    - Implement in MeetingService: call `_meetingRepository.GetPagedAsync` with businessId from `_tenantService.CurrentBusinessId`, filter params, page, pageSize=15
    - Map Meeting entities to `MeetingPagedListDto`: batch-fetch ContactNames via `_contactRepository.GetByIdsAsync(contactIds, businessId)` (NOT per-iteration GetByIdAsync), resolve MeetingTypeName via `_meetingTypeRepository.GetAllAsync`, compute Urgency string
    - Urgency logic: IsCancelled → "cancelled"; ScheduledAtUtc.Date == today && !IsCancelled → "today"; ScheduledAtUtc > now && !IsCancelled → "upcoming"; ScheduledAtUtc < now && !IsCancelled && Outcome == null → "needs_outcome"; ScheduledAtUtc < now && !IsCancelled && Outcome != null → "completed"
    - Include `LeadRequestId` in `MeetingPagedListDto` for "View Lead" link in the table
    - Return `PagedResult<MeetingPagedListDto>` with Items, TotalCount, CurrentPage, TotalPages (ceil(totalCount/15))
    - `catch (Exception ex) { throw; }`
    - _Requirements: 1.1–1.5, 2.7–2.11, 4.1, 5.1, 6.1–6.5_

  - [ ]* 3.2 Write property test for urgency classification
    - **Property 5: Urgency classification completeness and correctness**
    - Generate random meetings with varying ScheduledAtUtc, IsCancelled, and Outcome values, apply urgency logic, verify each meeting receives exactly one classification
    - **Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5**

- [x] 4. Checkpoint — Ensure repository and service compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Controller layer — New AJAX endpoints
  - [x] 5.1 Implement AxGetMeetingsPaged endpoint in SalesController
    - Add `[HttpGet] public async Task<IActionResult> AxGetMeetingsPaged(string? status, int? meetingTypeId, DateTime? dateFrom, DateTime? dateTo, int page = 1)`
    - Construct `MeetingFilter` from params, call `_meetingService.GetMeetingsPagedAsync(filter, page, 15)`
    - Return `Json(new { success = true, data = result.Items, totalCount = result.TotalCount, currentPage = result.CurrentPage, totalPages = result.TotalPages })`
    - On exception: log via `_logger.LogError`, return `Json(new { success = false, message = "An error occurred." })`
    - _Requirements: 1.1, 1.2, 1.3, 4.1_

  - [x] 5.2 Implement AxGetMeetingDetail endpoint in SalesController
    - Add `[HttpGet] public async Task<IActionResult> AxGetMeetingDetail(int id)`
    - Call existing `_meetingService.GetByIdAsync(id)`, return `Json(new { success = true, data = detail })`
    - If null: return `Json(new { success = false, message = "Meeting not found." })`
    - On exception: log, return `Json(new { success = false, message = "An error occurred." })`
    - _Requirements: 8.1, 8.2_

- [x] 6. View restructure — Meetings.cshtml
  - [x] 6.1 Restructure Meetings.cshtml for AJAX-driven rendering
    - Remove `@model List<MeetingListDto>` and server-side `@foreach` table body
    - Change model to a simple ViewData-driven page (no model, or minimal ViewModel with MeetingTypes for the filter dropdown)
    - Add a filter panel in a separate `<section class="glass card-pad" style="margin-bottom:22px;">` above the data table card
    - Filter panel contains: Status dropdown (All/Upcoming/Completed/Cancelled), Meeting Type dropdown (populated from ViewBag.MeetingTypes), Date From input (type=date), Date To input (type=date), Filter button, Clear button
    - Add Quick Date Presets row below filters: This Month, Last Month, Last 3 Months, Last 6 Months, This Year, Last Year, All Time
    - Keep existing table `<thead>` structure; replace `<tbody>` with empty `<tbody id="meetingsTableBody"></tbody>`
    - Add pagination div below table: `<div id="meetingsPagination" style="display:flex;justify-content:space-between;align-items:center;margin-top:18px;flex-wrap:wrap;gap:12px;">`
    - Add Edit Meeting modal (same structure as Create modal, plus Outcome textarea, pre-populated via JS)
    - Keep existing Create Meeting modal unchanged
    - Pass MeetingTypes JSON array to JS via a `<script>` block for filter dropdown
    - _Requirements: 1.1, 2.1–2.6, 3.1–3.4, 4.2–4.6, 8.1–8.3_

- [x] 7. JavaScript rewrite — meetings.js
  - [x] 7.1 Implement core AJAX loading and table rendering
    - Rewrite `DOMContentLoaded` handler: call `loadMeetingsPage(1)` on load AND keep calling `loadContactsForMeetingForm()` for the Create modal contact dropdown
    - Implement `loadMeetingsPage(page)`: build query params from filter fields (status, meetingTypeId, dateFrom, dateTo, page), show loading indicator in table body, fetch `/Sales/AxGetMeetingsPaged`, call `renderMeetingsTable` on success, show error message in table body on failure
    - Implement `renderMeetingsTable(data, totalCount, currentPage, totalPages)`: build HTML rows with columns (Subject, Type, Contact, Scheduled + relative time label, Duration, Outcome, Urgency pill, Actions with Edit/Calendar Task/Cancel or Activate buttons, "View Lead" link when LeadRequestId is present), render pagination info ("Showing X–Y of Z meetings") and page buttons, show empty state "No meetings found." when data is empty and hide pagination
    - Implement `getUrgencyBadgeHtml(urgency)`: "today" → amber pill "Today"; "upcoming" → blue pill "Upcoming"; "needs_outcome" → red pill "Needs Outcome"; "completed" → green pill "Completed"; "cancelled" → red pill "Cancelled"
    - Implement `getRelativeTimeLabel(scheduledAtUtc)`: compute difference from now; <24h → "in X hours" / "X hours ago"; >=24h → "in X days" / "X days ago"
    - Update `cancelMeeting` and `reactivateMeeting` functions to call `loadMeetingsPage(_currentPage)` instead of `window.location.reload()` after success
    - Do NOT use BlockUI for table loading (use inline skeleton/loading text instead — BlockUI is for user-initiated actions only)
    - _Requirements: 1.1, 1.4, 1.5, 4.2–4.6, 5.1, 6.1–6.5, 7.1–7.5_

  - [ ]* 7.2 Write property test for relative time label format
    - **Property 6: Relative time label format**
    - Generate random DateTimes (past and future), apply label logic, verify hours when |diff| < 24h and days when >= 24h with correct prefix/suffix
    - **Validates: Requirements 7.2, 7.3, 7.4, 7.5**

  - [x] 7.3 Implement filter and quick preset logic
    - Implement `filterMeetings()`: read filter field values, call `loadMeetingsPage(1)`
    - Implement `clearMeetingFilters()`: reset all filter fields to defaults (Status = empty, MeetingTypeId = empty, DateFrom = empty, DateTo = empty), call `loadMeetingsPage(1)`
    - Implement `setQuickPeriod(e, preset)`: accept event parameter explicitly (Firefox compatibility — do NOT rely on implicit `window.event`), calculate dateFrom/dateTo for each preset (This Month, Last Month, Last 3 Months, Last 6 Months, This Year, Last Year, All Time), populate Date From/Date To fields, "All Time" clears both fields, call `loadMeetingsPage(1)`, use `e.target` for active class toggling
    - Wire Filter button onclick to `filterMeetings()`, Clear button onclick to `clearMeetingFilters()`, preset buttons onclick to `setQuickPeriod(event, 'name')`
    - _Requirements: 2.5, 2.6, 3.1–3.4_

  - [x] 7.4 Implement edit modal logic
    - Implement `openEditMeetingModal(id)`: BlockUI.show → fetch `/Sales/AxGetMeetingDetail?id=` → BlockUI.hide → populate edit form fields (Subject, MeetingTypeId, ScheduledAtUtc, DurationMinutes, Location, Notes, Outcome) → display Contact name as read-only text above the form (not editable, just visible for context) → show edit modal
    - Implement `submitEditMeeting()`: validate Subject and ScheduledAtUtc not empty (Swal.fire warning if invalid), BlockUI.show('Updating...') → POST to `/Sales/AxPostUpdateMeeting` with JSON body → BlockUI.hide → on success: close modal + Swal.fire success + call `loadMeetingsPage(_currentPage)`; on failure: Swal.fire error with message
    - Implement `closeEditMeetingModal()`: hide edit modal
    - Include antiforgery token in POST header (`RequestVerificationToken`)
    - _Requirements: 8.1–8.8_

  - [x] 7.5 Fix create modal re-opening bug (URL parameter stripping)
    - In `submitMeeting()` success handler: after Swal.fire success, redirect to `/Sales/Meetings` (clean path, no query params) instead of `window.location.reload()`
    - This strips leadRequestId and contactId query params, preventing the modal from auto-opening on reload
    - _Requirements: 9.1, 9.2, 9.3_

  - [ ]* 7.6 Write property test for URL parameter stripping
    - **Property 7: URL parameter stripping**
    - Generate random URL strings containing leadRequestId and/or contactId query parameters, apply stripping logic, verify result is `/Sales/Meetings` with no query string
    - **Validates: Requirements 9.1, 9.2**

- [x] 8. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (FsCheck.Xunit)
- All SQL uses full table names (no aliases) per project standards
- All catch blocks use `catch (Exception ex) { throw; }` per coding golden rules
- All AJAX methods use AxPost/AxGet prefix convention
- UI follows BlockUI + SweetAlert2 pattern (no native alerts)
- Page size is fixed at 15 items per page
- The existing `AxPostUpdateMeeting` endpoint is reused — only the UI (edit modal) is new
- The existing `PagedResult<T>` class is reused from `Portal.Infrastructure/Models/`
- Bottom-up ordering: DTOs → Repository → Service → Controller → View → JS

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "2.5", "3.1"] },
    { "id": 3, "tasks": ["3.2"] },
    { "id": 4, "tasks": ["4"] },
    { "id": 5, "tasks": ["5.1", "5.2"] },
    { "id": 6, "tasks": ["6.1"] },
    { "id": 7, "tasks": ["7.1", "7.3", "7.4", "7.5"] },
    { "id": 8, "tasks": ["7.2", "7.6"] },
    { "id": 9, "tasks": ["8"] }
  ]
}
```
