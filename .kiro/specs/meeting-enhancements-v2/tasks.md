# Implementation Plan: Meeting Enhancements v2

## Overview

Two additive enhancements: (1) Change meeting task due date from date-only to datetime-local so users can specify time, and (2) Add a MeetingOutcomeClassification lookup + dropdown alongside the existing free-text Outcome field, with coloured pills on the Meetings list page and a new filter. Implementation proceeds: DB migration → Entity → Repository → Service → Controller → View → JS.

## Tasks

- [x] 1. Meeting Task Time Picker — View and JS changes
  - [x] 1.1 Change meeting task due date input to datetime-local
    - In `Portal.Web/Views/Sales/Meetings.cshtml`, find the inline task creation form (`#meetingTaskForm`)
    - Change `<input type="date" id="meetingTaskDueDate" />` to `<input type="datetime-local" id="meetingTaskDueDate" />`
    - Change the label from "Due Date *" to "Due Date & Time *"
    - _Requirements: 1.1, 1.6_

  - [x] 1.2 Update submitMeetingTask to parse datetime and send scheduledTimeUtc
    - In `Portal.Web/wwwroot/js/sales/meetings.js`, find `submitMeetingTask` function
    - Parse the `datetime-local` value: extract date portion for `dueAtUtc`, extract hours/minutes for `scheduledTimeUtc`
    - If hours and minutes are both 0, send `scheduledTimeUtc: null` (all-day task)
    - Otherwise send `scheduledTimeUtc` as "HH:mm" string
    - Add `scheduledTimeUtc` to the payload object sent to `/Sales/AxPostCreateTask`
    - _Requirements: 1.2, 1.3_

  - [x] 1.3 Update renderMeetingTasks to display time when present
    - In `meetings.js`, find `renderMeetingTasks` function
    - When rendering each task's due date: if `task.scheduledTimeUtc` is not null, format as "DD MMM YYYY, HH:mm"
    - If `task.scheduledTimeUtc` is null, format as "DD MMM YYYY" (date only)
    - Also update the date format to include the year (currently only shows "DD MMM")
    - _Requirements: 1.4, 1.5_

  - [x] 1.4 Add ScheduledTimeUtc to MeetingTaskBriefDto and update service mapping
    - Add `public TimeOnly? ScheduledTimeUtc { get; set; }` to `MeetingTaskBriefDto` in `Portal.Infrastructure/Models/Sales/MeetingDtos.cs`
    - Update the mapping in `MeetingService.GetByIdAsync` (or wherever `MeetingTaskBriefDto` is populated from `FollowUpTask` entities) to include `ScheduledTimeUtc = task.ScheduledTimeUtc`
    - Without this, `renderMeetingTasks` in the JS cannot access the time value
    - _Requirements: 1.4, 1.5_

- [x] 2. Checkpoint — Verify task time picker works
  - Bump meetings.js version query string for cache-busting
  - Verify creating a task with time stores `ScheduledTimeUtc` correctly
  - Verify creating a task at midnight (00:00) stores `ScheduledTimeUtc` as NULL
  - Verify task list shows time when set and date-only when not
  - Ask the user if questions arise

- [x] 3. Database migration — Create MeetingOutcomeClassification table and alter Meeting
  - [x] 3.1 Create SQL migration script
    - Create `Portal.Database/Migrations/XXX_CreateMeetingOutcomeClassification.sql`
    - Add `USE [Portal]` at top of script
    - Create `[sales].[MeetingOutcomeClassification]` table: Id (INT IDENTITY PK), Name (NVARCHAR(50) NOT NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
    - Seed values: (1, 'Positive'), (2, 'Neutral'), (3, 'Negative'), (4, 'Rescheduled'), (5, 'No Show')
    - ALTER TABLE `[sales].[Meeting]` ADD `[MeetingOutcomeClassificationId] INT NULL`
    - Add FK constraint: `FK_Meeting_MeetingOutcomeClassification` referencing `[sales].[MeetingOutcomeClassification]([Id])`
    - Wrap in IF NOT EXISTS checks for idempotency
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 4. Entity and DTO changes
  - [x] 4.1 Create MeetingOutcomeClassification entity
    - Create `Portal.Infrastructure/Entities/Sales/MeetingOutcomeClassification.cs`
    - Properties: `int Id`, `string Name`, `DateTime CreatedAtUtc`
    - _Requirements: 2.1_

  - [x] 4.2 Add MeetingOutcomeClassificationId to Meeting entity
    - Add `public int? MeetingOutcomeClassificationId { get; set; }` to `Portal.Infrastructure/Entities/Sales/Meeting.cs`
    - _Requirements: 2.3_

  - [x] 4.3 Update DTOs with classification fields
    - Add `int? MeetingOutcomeClassificationId` and `string? OutcomeClassificationName` to `MeetingPagedListDto` in `MeetingDtos.cs`
    - Add `int? MeetingOutcomeClassificationId` to `MeetingDetailDto` in `MeetingDtos.cs` (for edit modal pre-select)
    - Add `int? MeetingOutcomeClassificationId` to `UpdateMeetingRequest` in `MeetingDtos.cs` (for saving classification)
    - Add `int? OutcomeClassificationId` to `MeetingFilter` in `MeetingDtos.cs` (for list page filtering)
    - _Requirements: 2.7, 2.8, 2.10, 2.12_

- [x] 5. Repository layer — Update MeetingRepository
  - [x] 5.1 Update all SELECT queries to include MeetingOutcomeClassificationId
    - Add `[MeetingOutcomeClassificationId]` to ALL existing SELECT column lists in MeetingRepository
    - Use full table names in queries, `catch (Exception ex)`, rethrow
    - _Requirements: 2.3, 2.12_

  - [x] 5.2 Update UpdateAsync to include MeetingOutcomeClassificationId
    - Add `[MeetingOutcomeClassificationId] = @MeetingOutcomeClassificationId` to the UPDATE SET clause
    - Add SqlParameter for the new column with null-safe `?? (object)DBNull.Value`
    - _Requirements: 2.7_

  - [x] 5.3 Add classification filter to GetPagedAsync
    - In the `GetPagedAsync` method, add handling for `MeetingFilter.OutcomeClassificationId`: when provided, append `AND [MeetingOutcomeClassificationId] = @OutcomeClassificationId` to the WHERE clause
    - Add SqlParameter for the filter value
    - No method signature change needed — the filter comes through the existing `MeetingFilter` model
    - _Requirements: 2.10_

- [x] 6. Service layer — Update MeetingService
  - [x] 6.1 Update GetMeetingsPagedAsync to resolve classification name
    - The classification filter already flows through `MeetingFilter.OutcomeClassificationId` to the repository
    - When mapping to `MeetingPagedListDto`, resolve `OutcomeClassificationName` using a static dictionary: `{ 1: "Positive", 2: "Neutral", 3: "Negative", 4: "Rescheduled", 5: "No Show" }`
    - _Requirements: 2.10, 2.12_

  - [x] 6.2 Update meeting update logic to persist classification
    - The `UpdateMeetingRequest` now has `MeetingOutcomeClassificationId` — pass it through to the repository update method
    - _Requirements: 2.7_

  - [x] 6.3 Update GetByIdAsync to map classification to MeetingDetailDto
    - When mapping the Meeting entity to `MeetingDetailDto`, include `MeetingOutcomeClassificationId`
    - _Requirements: 2.8_

- [x] 7. Checkpoint — Ensure migration, entities, repository, and service compile
  - Ensure all tests pass, ask the user if questions arise

- [x] 8. Controller layer — Update SalesController
  - [x] 8.1 Update AxGetMeetingsPaged to accept classification filter
    - Add `int? outcomeClassificationId` query parameter to the method signature
    - Populate `filter.OutcomeClassificationId = outcomeClassificationId` when constructing the `MeetingFilter`
    - _Requirements: 2.10_

  - [x] 8.2 Verify AxPostUpdateMeeting flows classification through
    - The controller already accepts `[FromBody] UpdateMeetingRequest` — the new `MeetingOutcomeClassificationId` property flows through automatically after the DTO update in task 4.3
    - No controller code change needed — just verify JSON deserialization works
    - _Requirements: 2.7_

  - [x] 8.3 Verify AxGetMeetingDetail returns classification
    - The controller already returns `Json(new { success = true, data = detail })` where `detail` is `MeetingDetailDto`
    - After the DTO + service mapping updates in tasks 4.3 and 6.3, the classification is included automatically
    - No controller code change needed — just verify
    - _Requirements: 2.8_

- [x] 9. View changes — Meetings.cshtml
  - [x] 9.1 Add Classification dropdown to edit meeting modal
    - Insert a Classification `<select>` between the Notes textarea and the Outcome textarea
    - Options: empty (unselected), Positive, Neutral, Negative, Rescheduled, No Show
    - Id: `editMeetingClassification`
    - _Requirements: 2.4, 2.5, 2.6_

  - [x] 9.2 Add Classification filter to the filter panel
    - Insert a Classification dropdown in the filter bar between Meeting Type and Date From
    - Options: All (empty), Positive, Neutral, Negative, Rescheduled, No Show
    - Id: `filterOutcomeClassification`
    - _Requirements: 2.10_

  - [x] 9.3 Add Classification column header to the meetings table
    - Insert `<th>Classification</th>` between Duration and Outcome in the thead
    - _Requirements: 2.9_

- [x] 10. JS changes — meetings.js
  - [x] 10.1 Pre-select classification in openEditMeetingModal
    - After loading meeting detail, set `document.getElementById('editMeetingClassification').value = m.meetingOutcomeClassificationId || ''`
    - _Requirements: 2.8_

  - [x] 10.2 Include classification in submitEditMeeting payload
    - Add `meetingOutcomeClassificationId: parseInt(document.getElementById('editMeetingClassification').value) || null` to the payload
    - _Requirements: 2.7_

  - [x] 10.3 Add getClassificationPillHtml helper function
    - Create function that returns a coloured pill span for each classification name, or a dash for null
    - Colour mapping: Positive→green, Neutral→blue, Negative→red, Rescheduled→amber, No Show→red
    - _Requirements: 2.9, 2.11_

  - [x] 10.4 Render Classification column in renderMeetingsTable
    - Add a `<td>` with `getClassificationPillHtml(m.outcomeClassificationName)` between Duration and Outcome columns
    - _Requirements: 2.9_

  - [x] 10.5 Include classification filter in loadMeetingsPage
    - Read `filterOutcomeClassification` dropdown value, add as `outcomeClassificationId` query parameter
    - _Requirements: 2.10_

  - [x] 10.6 Reset classification filter in clearMeetingFilters
    - Set `document.getElementById('filterOutcomeClassification').value = ''`
    - _Requirements: 2.10_

- [x] 11. Final checkpoint — Ensure everything compiles and renders correctly
  - Bump meetings.js version query string for cache-busting
  - Verify classification dropdown appears in edit modal and pre-selects on load
  - Verify classification saves and displays as coloured pill in the meetings table
  - Verify classification filter works on the Meetings page
  - Verify task time picker sends scheduledTimeUtc correctly
  - Ensure all tests pass, ask the user if questions arise

## Notes

- No property-based tests — this feature is UI/filter/rendering with minimal pure business logic
- The `ScheduledTimeUtc` column already exists on `[sales].[FollowUpTask]` — no migration needed for Feature 1
- Classification values are hardcoded in JS dropdowns (5 static values) — no endpoint needed to fetch them
- Classification name resolution in the service uses a static dictionary, not a DB join — keeps queries simple
- All SQL uses full table names (no aliases) per project standards
- All catch blocks use `catch (Exception ex) { throw; }` per coding golden rules
- All AJAX methods use AxPost/AxGet prefix convention
- UI follows BlockUI + SweetAlert2 pattern (no native alerts)
- Bottom-up ordering: DB → Entities → Repository → Service → Controller → View → JS

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3"] },
    { "id": 5, "tasks": ["6.1", "6.2", "6.3"] },
    { "id": 6, "tasks": ["7"] },
    { "id": 7, "tasks": ["8.1", "8.2", "8.3"] },
    { "id": 8, "tasks": ["9.1", "9.2", "9.3"] },
    { "id": 9, "tasks": ["10.1", "10.2", "10.3", "10.4", "10.5", "10.6"] },
    { "id": 10, "tasks": ["11"] }
  ]
}
```
