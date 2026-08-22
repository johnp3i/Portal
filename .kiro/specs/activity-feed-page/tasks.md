# Implementation Plan: Activity Feed Page

## Overview

This plan creates a dedicated `/Sales/Activity` page with full filtering, pagination, and AJAX loading for the global activity feed, plus a compact "Recent Lead Activity" widget on the Pipeline page. Implementation proceeds bottom-up: DTOs → Repository → Service → Controller → View → JS → Navigation → Pipeline integration. No database migrations are required — all changes target existing `[sales].[ActivityFeed]` table.

## Tasks

- [x] 1. Create new DTOs and filter model
  - [x] 1.1 Create ActivityFeedFilter and ActivityFeedPageDto classes
    - Create `ActivityFeedFilter` class in `Portal.Infrastructure/Models/Sales/` with properties: ActionType (string?), DateFrom (DateTime?), DateTo (DateTime?)
    - Create `ActivityFeedPageDto` class in `Portal.Infrastructure/Models/Sales/` with properties: Id (int), Action (string), Description (string), PerformedByName (string?), LeadName (string?), Metadata (string?), CreatedAtUtc (DateTime)
    - _Requirements: 4.1, 6.1, 6.2, 6.3_

- [x] 2. Repository layer — New query methods
  - [x] 2.1 Implement ActivityFeedRepository.GetPagedByBusinessIdAsync
    - Add method: `public async Task<List<ActivityFeedEntry>> GetPagedByBusinessIdAsync(int businessId, string? actionType, DateTime? dateFrom, DateTime? dateTo, int page, int pageSize)`
    - SQL: SELECT from `[sales].[ActivityFeed]` with WHERE conditions for BusinessId, optional ActionType (`@ActionType IS NULL OR [sales].[ActivityFeed].[Action] = @ActionType`), optional DateFrom (`@DateFrom IS NULL OR [sales].[ActivityFeed].[CreatedAtUtc] >= @DateFrom`), optional DateTo (`@DateTo IS NULL OR [sales].[ActivityFeed].[CreatedAtUtc] < @DateTo`)
    - ORDER BY `[sales].[ActivityFeed].[CreatedAtUtc] DESC` with `OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY`
    - Use full schema-qualified table name `[sales].[ActivityFeed].[Column]` in SELECT/WHERE/ORDER — no aliases
    - `catch (Exception ex) { throw; }`
    - _Requirements: 6.1, 6.2_

  - [x] 2.2 Implement ActivityFeedRepository.GetCountByBusinessIdAsync
    - Add method: `public async Task<int> GetCountByBusinessIdAsync(int businessId, string? actionType, DateTime? dateFrom, DateTime? dateTo)`
    - SQL: `SELECT COUNT(*) FROM [sales].[ActivityFeed]` with same WHERE conditions as GetPagedByBusinessIdAsync using `[sales].[ActivityFeed].[Column]` pattern
    - Use DbConnection command pattern (ExecuteScalarAsync) to return int
    - `catch (Exception ex) { throw; }`
    - _Requirements: 5.2, 6.3_

  - [x] 2.3 Implement ActivityFeedRepository.GetRecentByBusinessIdAsync
    - Add method: `public async Task<List<ActivityFeedEntry>> GetRecentByBusinessIdAsync(int businessId, int count)`
    - SQL: `SELECT TOP(@Count)` from `[sales].[ActivityFeed]` WHERE `[sales].[ActivityFeed].[BusinessId] = @BusinessId` ORDER BY `[sales].[ActivityFeed].[CreatedAtUtc] DESC`
    - Use full schema-qualified table name in all clauses
    - `catch (Exception ex) { throw; }`
    - _Requirements: 9.1, 9.2_

- [x] 3. Service layer — New methods on IActivityFeedService
  - [x] 3.1 Add GetPagedAsync to IActivityFeedService and ActivityFeedService
    - Add interface method: `Task<PagedResult<ActivityFeedPageDto>> GetPagedAsync(ActivityFeedFilter filter, int page = 1, int pageSize = 15);`
    - Implement in ActivityFeedService: call `_activityFeedRepository.GetCountByBusinessIdAsync` and `_activityFeedRepository.GetPagedByBusinessIdAsync` with businessId from tenant service
    - Map entities to `ActivityFeedPageDto`: batch-resolve LeadName by collecting distinct LeadRequestId values, query `[sales].[LeadRequest]` joined with `[sales].[Contact]` for contact full names, resolve PerformedByName via existing user name resolution pattern
    - Default to page 1 when page < 1
    - Return `PagedResult<ActivityFeedPageDto>` with Items, TotalCount, CurrentPage, TotalPages
    - `catch (Exception ex) { throw; }`
    - _Requirements: 3.1, 3.2, 5.1, 6.1, 6.2, 6.3_

  - [x] 3.2 Add GetRecentAsync to IActivityFeedService and ActivityFeedService
    - Add interface method: `Task<List<ActivityFeedDto>> GetRecentAsync(int count = 10);`
    - Implement: call `_activityFeedRepository.GetRecentByBusinessIdAsync(businessId, count)`
    - Map entities to existing `ActivityFeedDto` (Id, Action, Description, PerformedByName, Metadata, CreatedAtUtc)
    - Resolve PerformedByName using existing pattern
    - `catch (Exception ex) { throw; }`
    - _Requirements: 8.2, 9.1, 9.2_

  - [ ]* 3.3 Write property test for pagination invariants
    - **Property 1: Pagination respects page size and count invariants**
    - Generate random activity entry sets and random filter combinations, verify `Items.Count <= 15` and `TotalCount` equals actual count of matching entries
    - **Validates: Requirements 5.1, 6.2, 6.3**

  - [ ]* 3.4 Write property test for filter correctness
    - **Property 2: Filter correctness — results match specified criteria**
    - Generate random entries with mixed Action values and timestamps, apply random filter, verify all returned entries satisfy all active filter predicates conjunctively
    - **Validates: Requirements 3.1, 3.2, 6.2**

  - [ ]* 3.5 Write property test for timestamp ordering
    - **Property 3: Results are always ordered by timestamp descending**
    - Generate random entry sets, call GetPagedAsync and GetRecentAsync, verify for every consecutive pair: `entry[i].CreatedAtUtc >= entry[i+1].CreatedAtUtc`
    - **Validates: Requirements 4.3, 6.2, 9.2**

  - [ ]* 3.6 Write property test for recent activity bounded results
    - **Property 4: Recent activity returns bounded and ordered results**
    - Generate random entry sets, call GetRecentAsync(10), verify `Count <= 10` and entries are the 10 most recent ordered descending
    - **Validates: Requirements 8.2, 9.1, 9.2**

- [x] 4. Checkpoint — Ensure repository and service compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Controller layer — New endpoints and action
  - [x] 5.1 Implement Activity() page action in SalesController
    - Add `public IActionResult Activity()` that returns the `Activity` view
    - No ViewBag data required (action types are hardcoded in the dropdown or fetched via a separate endpoint)
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 5.2 Implement AxGetActivityFeedPage endpoint in SalesController
    - Add `[HttpGet] public async Task<IActionResult> AxGetActivityFeedPage(string? actionType, DateTime? dateFrom, DateTime? dateTo, int page = 1)`
    - Construct `ActivityFeedFilter` from params, call `_activityFeedService.GetPagedAsync(filter, page, 15)`
    - Return `Json(new { success = true, data = result.Items, totalCount = result.TotalCount, currentPage = result.CurrentPage, totalPages = result.TotalPages, pageSize = 15 })`
    - On exception: `_logger.LogError(ex, "Error loading activity feed page")`, return `Json(new { success = false, message = "An error occurred." })`
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 5.3 Implement AxGetRecentLeadActivity endpoint in SalesController
    - Add `[HttpGet] public async Task<IActionResult> AxGetRecentLeadActivity()`
    - Call `_activityFeedService.GetRecentAsync(10)`
    - Return `Json(new { success = true, data = result })`
    - On exception: `_logger.LogError(ex, "Error loading recent lead activity")`, return `Json(new { success = false, message = "An error occurred." })`
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [x] 6. View — Create Activity.cshtml
  - [x] 6.1 Create Activity.cshtml view following Meetings page pattern
    - Create `Portal.Web/Views/Sales/Activity.cshtml`
    - Topbar: eyebrow "Sales Pipeline", heading "Activity", subtitle "All lead activity events across your pipeline."
    - Filter Panel (`<section class="glass card-pad" style="margin-bottom:22px;">`): Action Type dropdown (All + hardcoded options: stage_changed, meeting_scheduled, response_sent, lead_created, task_completed, note_added), Date From input (type=date), Date To input (type=date), Filter button, Clear button
    - Quick Presets row: This Month, Last Month, Last 3 Months, Last 6 Months, This Year, Last Year, All Time (NO "Next Month")
    - Data Table (`<section class="glass card-pad">`): columns — Timestamp, Action (badge), Description, Contact/Lead, Performed By
    - Empty `<tbody id="activityTableBody">` for JS rendering
    - Pagination div: `<div id="activityPagination">` with info and controls sub-divs
    - Include `<script src="~/js/sales/activity-feed.js?v=1"></script>` in Scripts section
    - _Requirements: 1.1, 1.2, 1.3, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 4.1, 4.5, 5.1, 5.2, 5.3_

- [x] 7. JavaScript — Create activity-feed.js
  - [x] 7.1 Implement activity-feed.js with IIFE pattern
    - Create `Portal.Web/wwwroot/js/sales/activity-feed.js`
    - Use IIFE `(function () { 'use strict'; ... })();` pattern with `var` declarations (not let/const)
    - Implement `window.loadActivityPage(page)`: build URLSearchParams from filter fields (actionType, dateFrom, dateTo, page), show inline loading text in tbody (NOT BlockUI — BlockUI is for user-initiated actions only), fetch `/Sales/AxGetActivityFeedPage?` + params, call `renderActivityTable` on success, show error in tbody on failure
    - Implement `renderActivityTable(data, totalCount, currentPage, totalPages)`: build HTML rows with Timestamp (formatted), Action (badge via `getActionBadgeHtml`), Description (escaped), Contact/Lead name, Performed By; render pagination info ("Showing X–Y of Z") and windowed page buttons; show empty state "No activity found." when data is empty
    - Implement `renderPagination(currentPage, totalPages, totalCount)`: windowed pagination (max 7 visible) matching meetings.js pattern
    - Implement `getActionBadgeHtml(action)`: return styled badge span based on action type (stage_changed → blue, meeting_scheduled → cyan, lead_created → green, response_sent → amber, task_completed → green, note_added → neutral)
    - Implement `formatTimestamp(utcStr)`: format ISO date to "DD MMM YYYY HH:mm" locale string
    - Implement `escapeHtml(str)`: escape &, <, >, "
    - Implement `window.filterActivity()`: call `loadActivityPage(1)`
    - Implement `window.clearActivityFilters()`: reset all filter fields, remove active class from preset buttons, call `loadActivityPage(1)`
    - Implement `window.setQuickPeriod(e, preset)`: calculate dateFrom/dateTo for each preset (This Month, Last Month, Last 3 Months, Last 6 Months, This Year, Last Year, All Time — NO "Next Month"), populate fields, toggle active class, call `loadActivityPage(1)`
    - DOMContentLoaded: call `loadActivityPage(1)`
    - _Requirements: 1.3, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5_

- [x] 8. Navigation — Add Activity link to sidebar
  - [x] 8.1 Insert Activity nav link in ModuleNavigation/Default.cshtml
    - Open `Portal.Web/Views/Shared/Components/ModuleNavigation/Default.cshtml`
    - Add Activity link between Meetings and Tasks nav items
    - Add `isActivityActive` variable: `currentController.Equals("Sales") && currentAction.Equals("Activity")`
    - Use SVG icon: `<svg width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M22 12h-4l-3 9L9 3l-3 9H2"/></svg>`
    - Nav text: "Activity"
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 9. Checkpoint — Verify Activity page renders and loads data
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Pipeline page — Remove global feed and add Recent Lead Activity
  - [x] 10.1 Remove global activity feed from Pipeline
    - In `Pipeline.cshtml`: remove the `#globalActivityFeed` container section and any associated "Recent Activity" heading/card
    - In `pipeline.js`: remove the `loadGlobalActivityFeed()` function and its call on DOMContentLoaded
    - Do NOT remove the `AxGetGlobalActivityFeed` controller endpoint (other pages may reference it) — just remove the Pipeline page's usage
    - _Requirements: 7.1, 7.2_

  - [x] 10.2 Add Recent Lead Activity section to Pipeline view
    - In the Pipeline view (Pipeline.cshtml), add a new `<section class="glass card-pad" style="margin-top:22px;">` below the Pipeline KPI Footer
    - Section heading: "Recent Lead Activity"
    - Compact list container: `<div id="recentLeadActivityList">Loading...</div>`
    - No pagination controls
    - _Requirements: 8.1, 8.4, 8.5, 8.6_

  - [x] 10.3 Add loadRecentLeadActivity() to Pipeline JS
    - In the Pipeline page's JavaScript file, add `loadRecentLeadActivity()` function
    - Fetch `/Sales/AxGetRecentLeadActivity`, on success render compact list items (timestamp, action description, contact/lead name), on failure show "Unable to load recent activity." text
    - Call `loadRecentLeadActivity()` on DOMContentLoaded
    - Each entry rendered as a compact row: small timestamp, description text, lead name
    - No BlockUI for this background load (inline loading text only)
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

- [x] 11. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (FsCheck.Xunit)
- All SQL uses full table names (no aliases) per project standards
- All catch blocks use `catch (Exception ex) { throw; }` per coding golden rules
- All AJAX methods use AxGet prefix convention
- UI follows BlockUI + SweetAlert2 pattern (no native alerts)
- Page size is fixed at 15 items per page for the Activity page
- Recent Lead Activity returns last 10 items with no pagination
- The existing `PagedResult<T>` class is reused from `Portal.Infrastructure/Models/`
- The existing `ActivityFeedDto` is reused for the Recent endpoint
- Quick Presets: This Month, Last Month, Last 3 Months, Last 6 Months, This Year, Last Year, All Time (NO "Next Month")
- JS uses IIFE pattern with `var` (not let/const) and `window.functionName` for global functions
- Bottom-up ordering: DTOs → Repository → Service → Controller → View → JS → Navigation → Pipeline

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["3.1", "3.2"] },
    { "id": 3, "tasks": ["3.3", "3.4", "3.5", "3.6", "4"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3"] },
    { "id": 5, "tasks": ["6.1"] },
    { "id": 6, "tasks": ["7.1", "8.1"] },
    { "id": 7, "tasks": ["9"] },
    { "id": 8, "tasks": ["10.1"] },
    { "id": 9, "tasks": ["10.2", "10.3"] },
    { "id": 10, "tasks": ["11"] }
  ]
}
```
