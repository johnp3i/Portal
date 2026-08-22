# Design Document: Meetings Page Enhancements

## Overview

This design converts the `/Sales/Meetings` page from a server-rendered flat list to an AJAX-driven, filterable, paginated view following the established pattern from `/Sales/Tasks`. The enhancement adds a filter panel, pagination, urgency indicators, relative time labels, an edit modal, and a bug fix for the create modal re-opening after successful creation.

The architecture follows the existing Controller → Service → Repository pattern with raw SQL, matching the FollowUpTask implementation.

## Architecture

```mermaid
flowchart TD
    subgraph Browser
        A[Meetings.cshtml] --> B[meetings.js]
        B -->|fetch| C[/Sales/AxGetMeetingsPaged]
        B -->|fetch| D[/Sales/AxPostUpdateMeeting]
    end

    subgraph Controller
        C --> E[SalesController.AxGetMeetingsPaged]
        D --> F[SalesController.AxPostUpdateMeeting]
    end

    subgraph Service
        E --> G[MeetingService.GetMeetingsPagedAsync]
        F --> H[MeetingService.UpdateMeetingAsync]
    end

    subgraph Repository
        G --> I[MeetingRepository.GetPagedAsync]
    end

    subgraph Database
        I --> J[(sales.Meeting)]
    end
```

**Key design decisions:**

1. **Follow the Tasks page pattern exactly** — The `FollowUpTaskRepository.GetPagedAsync` pattern (dynamic WHERE clause + COUNT query + OFFSET/FETCH) is reused for meetings. This ensures consistency and reduces cognitive load.

2. **Server computes urgency** — The urgency field is computed server-side in the service layer and returned in the DTO, keeping the JS renderer simple and ensuring consistent business logic.

3. **No new DTO for the edit modal** — The existing `MeetingDetailDto` (fetched via `GetByIdAsync`) provides all fields needed for pre-populating the edit form. The existing `UpdateMeetingRequest` DTO handles the submission.

4. **Bug fix is JS-only** — The create modal re-opening bug is fixed by stripping query parameters from `window.location` after successful creation, then redirecting to the clean URL.

5. **"Completed" filter means "past, non-cancelled"** — The status filter groups by time/cancellation state (upcoming/past/cancelled). Within the "Completed" results, the urgency column further distinguishes "Completed" (has outcome) from "Needs Outcome" (no outcome). This avoids a fourth filter value while keeping the visual distinction clear.

6. **Cancel/Reactivate use AJAX reload** — After cancel or reactivate, the table reloads the current page via `loadMeetingsPage(_currentPage)` instead of `window.location.reload()`.

7. **"View Lead" link in table** — When a meeting has a `LeadRequestId`, the Actions column includes a link to `/Sales/LeadDetail/{id}` for quick navigation context.

## Components and Interfaces

### Repository Layer

**New method: `MeetingRepository.GetPagedAsync`**

```csharp
public async Task<(List<Meeting> Items, int TotalCount)> GetPagedAsync(
    int businessId, string? status, int? meetingTypeId,
    DateTime? dateFrom, DateTime? dateTo, int page, int pageSize)
```

- Builds a dynamic WHERE clause based on provided filters
- Status filter logic:
  - `"upcoming"` → `ScheduledAtUtc > GETUTCDATE() AND IsCancelled = 0`
  - `"completed"` → `ScheduledAtUtc < GETUTCDATE() AND IsCancelled = 0`
  - `"cancelled"` → `IsCancelled = 1`
  - Empty/null → no status filter (returns all active)
- Date range filters: `ScheduledAtUtc >= @DateFrom`, `ScheduledAtUtc <= @DateTo` (end of day)
- MeetingTypeId filter: exact match when provided
- Always filters by `BusinessId` and `IsActive = 1`
- Executes a COUNT query first, then a data query with `ORDER BY ScheduledAtUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY`
- Uses full table name `[sales].[Meeting]` — no aliases
- Parameters use `SqlParameter` with `?? (object)DBNull.Value` for nullable values

### Service Layer

**New method: `IMeetingService.GetMeetingsPagedAsync`**

```csharp
Task<PagedResult<MeetingPagedListDto>> GetMeetingsPagedAsync(MeetingFilter filter, int page, int pageSize);
```

- Calls `MeetingRepository.GetPagedAsync`
- Maps `Meeting` entities to `MeetingPagedListDto` (enriched with contact name, meeting type name, and computed urgency)
- Batch-fetches contact names via `_contactRepository.GetByIdsAsync(contactIds, businessId)` — NOT per-iteration GetByIdAsync (avoids N+1)
- Computes urgency classification for each meeting:
  - `IsCancelled = true` → `"cancelled"`
  - `ScheduledAtUtc.Date == today && !IsCancelled` → `"today"`
  - `ScheduledAtUtc > now && !IsCancelled` → `"upcoming"`
  - `ScheduledAtUtc < now && !IsCancelled && Outcome == null` → `"needs_outcome"`
  - `ScheduledAtUtc < now && !IsCancelled && Outcome != null` → `"completed"`
- Returns `PagedResult<T>` with Items, TotalCount, CurrentPage, TotalPages

### Controller Layer

**New endpoint: `AxGetMeetingsPaged`**

```csharp
[HttpGet]
public async Task<IActionResult> AxGetMeetingsPaged(
    string? status, int? meetingTypeId, DateTime? dateFrom, DateTime? dateTo, int page = 1)
```

Returns JSON: `{ success, data, totalCount, currentPage, totalPages }`

**Existing endpoint used: `AxPostUpdateMeeting`** (already exists in the controller via `UpdateMeetingAsync`)

**New endpoint: `AxGetMeetingDetail`**

```csharp
[HttpGet]
public async Task<IActionResult> AxGetMeetingDetail(int id)
```

Returns the meeting detail for pre-populating the edit modal.

### New DTOs

**`MeetingFilter`**

```csharp
public class MeetingFilter
{
    public string? Status { get; set; }
    public int? MeetingTypeId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
```

**`MeetingPagedListDto`**

```csharp
public class MeetingPagedListDto
{
    public int Id { get; set; }
    public string Subject { get; set; }
    public string MeetingTypeName { get; set; }
    public int MeetingTypeId { get; set; }
    public string ContactName { get; set; }
    public int ContactId { get; set; }
    public int? LeadRequestId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    public string? Outcome { get; set; }
    public bool IsCancelled { get; set; }
    public string Urgency { get; set; }  // "today", "upcoming", "needs_outcome", "completed", "cancelled"
}
```

### View Layer (Meetings.cshtml)

The Razor view is restructured to:
1. Remove the server-rendered `@foreach` table body
2. Add a filter panel in a separate `glass card-pad` section (margin-bottom:22px)
3. Add an empty `<tbody id="meetingsTableBody">` for JS rendering
4. Add pagination div below the table (margin-top:18px)
5. Add an Edit Meeting modal (same structure as Create, plus Outcome field)
6. Keep the existing Create Meeting modal unchanged
7. Pass `MeetingTypes` to JS via a script-rendered JSON array for the filter dropdown

### JavaScript Layer (meetings.js)

The JS file is rewritten to:
1. On `DOMContentLoaded`: call `loadMeetingsPage(1)` (default status empty = all)
2. `loadMeetingsPage(page)` — builds query params from filter fields, fetches `AxGetMeetingsPaged`, renders table
3. `renderMeetingsTable(data, totalCount, currentPage, totalPages)` — builds HTML rows with urgency pills, relative time labels, and action buttons
4. `getUrgencyBadgeHtml(urgency)` — returns styled pill HTML based on urgency string
5. `getRelativeTimeLabel(scheduledAtUtc)` — computes "in X hours/days" or "X hours/days ago"
6. `clearMeetingFilters()` — resets filter fields and reloads page 1
7. `setQuickPeriod(preset)` — calculates date range and populates From/To fields, then reloads
8. `openEditMeetingModal(id)` — fetches meeting detail via `AxGetMeetingDetail`, populates edit modal
9. `submitEditMeeting()` — validates required fields, POSTs to `AxPostUpdateMeeting`, reloads current page on success
10. **Bug fix in `submitMeeting()`** — after successful creation, redirect to `/Sales/Meetings` (no query params) instead of `window.location.reload()`

## Data Models

### Existing Tables Used (No Migrations)

**`[sales].[Meeting]`** — Primary table queried by `GetPagedAsync`

| Column | Type | Used In Filter |
|--------|------|---------------|
| Id | INT | Primary key |
| BusinessId | INT | Always filtered (tenant isolation) |
| MeetingTypeId | INT | meetingTypeId filter |
| ScheduledAtUtc | DATETIME | status filter, dateFrom/dateTo |
| IsCancelled | BIT | status filter |
| IsActive | BIT | Always filtered (= 1) |
| Outcome | NVARCHAR | Urgency computation |
| Subject, DurationMinutes, Location, Notes | Various | Returned in DTO |
| ContactId | INT | JOIN for contact name |

**`[sales].[MeetingType]`** — Lookup for dropdown population and display name

**`[sales].[SalesContact]`** — JOIN for contact display name

### PagedResult Generic

Reuses the existing `PagedResult<T>` pattern (same as FollowUpTask):

```csharp
public class PagedResult<T>
{
    public List<T> Items { get; set; }
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Status filter correctness

*For any* set of meetings and any status filter value ("upcoming", "completed", "cancelled"), a meeting is included in the filtered results if and only if it satisfies the filter predicate: upcoming → ScheduledAtUtc > now AND IsCancelled = false; completed → ScheduledAtUtc < now AND IsCancelled = false; cancelled → IsCancelled = true.

**Validates: Requirements 2.7, 2.8, 2.9**

### Property 2: Date range filter correctness

*For any* date range (dateFrom, dateTo) and any set of meetings, all meetings returned by the endpoint have ScheduledAtUtc >= dateFrom (when dateFrom is provided) AND ScheduledAtUtc <= end of dateTo (when dateTo is provided).

**Validates: Requirements 2.10, 2.11**

### Property 3: Page size invariant

*For any* valid request to the paged endpoint, the data array in the response contains at most 15 items, and the totalPages value equals ceil(totalCount / 15).

**Validates: Requirements 4.1, 4.2**

### Property 4: Sort order invariant

*For any* response from the paged endpoint containing more than one meeting, each meeting's ScheduledAtUtc is greater than or equal to the next meeting's ScheduledAtUtc (descending order).

**Validates: Requirements 5.1**

### Property 5: Urgency classification completeness and correctness

*For any* meeting, the urgency classification is exactly one of five values determined by: (1) IsCancelled = true → "cancelled"; (2) ScheduledAtUtc.Date == today AND !IsCancelled → "today"; (3) ScheduledAtUtc > now AND !IsCancelled → "upcoming"; (4) ScheduledAtUtc < now AND !IsCancelled AND Outcome is null → "needs_outcome"; (5) ScheduledAtUtc < now AND !IsCancelled AND Outcome is not null → "completed". No meeting is ever unclassified.

**Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5**

### Property 6: Relative time label format

*For any* time difference between now and a meeting's ScheduledAtUtc, the relative time label uses "hours" when |difference| < 24 hours and "days" when |difference| >= 24 hours, with "in X" prefix for future meetings and "X ago" suffix for past meetings.

**Validates: Requirements 7.2, 7.3, 7.4, 7.5**

### Property 7: URL parameter stripping

*For any* URL containing leadRequestId and/or contactId query parameters, after stripping, the resulting URL path is `/Sales/Meetings` with no query string.

**Validates: Requirements 9.1, 9.2**

## Error Handling

| Layer | Error Scenario | Handling |
|-------|---------------|----------|
| Repository | SQL exception in GetPagedAsync | `catch (Exception ex) { throw; }` — propagates to service/controller |
| Service | Repository exception | Propagates to controller |
| Controller (AxGetMeetingsPaged) | Any exception | Log via `_logger.LogError`, return `{ success: false, message: "An error occurred." }` |
| Controller (AxPostUpdateMeeting) | Meeting not found | Return `{ success: false, message: "Meeting not found." }` |
| Controller (AxPostUpdateMeeting) | Validation failure | Return `{ success: false, message: "<specific error>" }` |
| JavaScript (fetch) | Network error / non-200 | `BlockUI.hide()`, then `Swal.fire` with error icon and generic message |
| JavaScript (table render) | Empty results | Show "No meetings found." in table body, hide pagination |
| JavaScript (edit modal) | Missing required fields | `Swal.fire` with warning icon, prevent submission |

## Testing Strategy

### Unit Tests (Example-Based)

- **Filter panel rendering**: Verify DOM structure of filter controls (status dropdown options, meeting type dropdown, date inputs, buttons)
- **Quick date presets**: Verify each preset calculates correct dateFrom/dateTo values
- **Edit modal pre-population**: Verify all fields are correctly populated from meeting detail response
- **Bug fix**: Verify URL stripping removes query parameters and redirects to clean path
- **Empty state**: Verify empty message appears and pagination hides when no results

### Property-Based Tests (FsCheck)

The project uses **FsCheck** with **xUnit** for property-based testing (already in the test project's dependencies).

Each property test runs a minimum of **100 iterations**.

- **Property 1**: Generate random lists of `MeetingPagedListDto` with varying dates and cancellation states, apply each status filter function, verify only matching meetings are returned.
  - Tag: `Feature: meetings-page-enhancements, Property 1: Status filter correctness`

- **Property 2**: Generate random meetings and random date ranges, apply date filter logic, verify all returned meetings fall within the range.
  - Tag: `Feature: meetings-page-enhancements, Property 2: Date range filter correctness`

- **Property 3**: Generate random total counts (0–200), verify page size logic returns correct data length (≤ 15) and totalPages = ceil(total / 15).
  - Tag: `Feature: meetings-page-enhancements, Property 3: Page size invariant`

- **Property 4**: Generate random lists of meetings, apply sort logic, verify descending order by ScheduledAtUtc.
  - Tag: `Feature: meetings-page-enhancements, Property 4: Sort order invariant`

- **Property 5**: Generate random meetings with varying ScheduledAtUtc, IsCancelled, and Outcome values, apply urgency classification, verify each meets exactly one classification rule and no meeting is unclassified.
  - Tag: `Feature: meetings-page-enhancements, Property 5: Urgency classification completeness and correctness`

- **Property 6**: Generate random DateTime values (past and future within reasonable bounds), apply relative time label logic, verify format uses hours when |diff| < 24h and days when >= 24h with correct prefix/suffix.
  - Tag: `Feature: meetings-page-enhancements, Property 6: Relative time label format`

- **Property 7**: Generate random URL strings containing leadRequestId and/or contactId query parameters, apply stripping logic, verify the result is `/Sales/Meetings` with no query string.
  - Tag: `Feature: meetings-page-enhancements, Property 7: URL parameter stripping`

### Integration Tests

- **AxGetMeetingsPaged endpoint**: Verify correct JSON shape returned with real database (success flag, data array, totalCount, currentPage, totalPages)
- **AxPostUpdateMeeting endpoint**: Verify meeting fields are persisted and response is correct
- **AxGetMeetingDetail endpoint**: Verify correct meeting detail is returned for a given ID
