# Implementation Plan: Sales Tasks & Meetings Enhancements

## Overview

This plan implements four enhancements to the Sales/Opportunities module: Task "Unprocessed" status closure, optional time-of-day scheduling on tasks, a meetings brief panel on the Pipeline page, and a Today's Brief section on the Home dashboard. Implementation proceeds bottom-up: database migrations → entity changes → DTOs → service methods → controller endpoints → views → client-side JavaScript.

## Tasks

- [x] 1. Database migrations and entity layer
  - [x] 1.1 Create database migration: Add TaskOutcome column to FollowUpTask
    - Create SQL migration script with `USE [Portal]` header
    - ALTER TABLE `[sales].[FollowUpTask]` ADD `TaskOutcome NVARCHAR(20) NULL`
    - Column stores closure classification: "Completed", "Unprocessed", or NULL (open)
    - Include data migration: `UPDATE [sales].[FollowUpTask] SET [TaskOutcome] = 'Completed' WHERE [IsCompleted] = 1` to backfill existing completed tasks
    - Wrap in IF NOT EXISTS check for idempotency
    - _Requirements: 1.1_

  - [x] 1.2 Create database migration: Add ScheduledTimeUtc column to FollowUpTask
    - Create SQL migration script with `USE [Portal]` header
    - ALTER TABLE `[sales].[FollowUpTask]` ADD `ScheduledTimeUtc TIME(0) NULL`
    - NULL indicates all-day task; non-null stores time-of-day (no fractional seconds)
    - _Requirements: 3.1_

  - [x] 1.3 Extend FollowUpTask entity with new properties
    - Add `public string? TaskOutcome { get; set; }` to the existing FollowUpTask entity class
    - Add `public TimeOnly? ScheduledTimeUtc { get; set; }` to the existing FollowUpTask entity class
    - Update DbContext configuration for the new columns (nvarchar(20) nullable, time(0) nullable)
    - Update ALL repository SELECT statements for FollowUpTask to include `[TaskOutcome]` and `[ScheduledTimeUtc]` in the column list (critical — EF FromSqlRaw requires all mapped columns in results)
    - _Requirements: 1.1, 3.1_

  - [x] 1.4 Create new DTOs for enhancements
    - Create `MeetingBriefDto` with: Id, LeadRequestId (int?), Subject, ContactName, MeetingTypeName, ScheduledAtUtc, DurationMinutes, Location
    - Create `DashboardTaskBriefDto` with: Id, Title, TaskType, DueAtUtc, ScheduledTimeUtc (TimeOnly?), ContactName, Urgency (string)
    - Create `DashboardMeetingBriefDto` with: Id, Subject, ContactName, MeetingTypeName, ScheduledAtUtc, DurationMinutes, Urgency (string)
    - Extend existing `FollowUpTaskDto` with `TaskOutcome` (string?) and `ScheduledTimeUtc` (TimeOnly?)
    - Extend existing `CreateFollowUpTaskRequest` with `ScheduledTimeUtc` (TimeOnly?)
    - Extend existing `UpdateFollowUpTaskRequest` with `ScheduledTimeUtc` (TimeOnly?)
    - _Requirements: 1.6, 3.6, 5.2, 7.3, 7.4_

- [x] 2. Service layer — FollowUpTaskService enhancements
  - [x] 2.1 Implement MarkTaskUnprocessedAsync in FollowUpTaskService
    - Add method to `IFollowUpTaskService` interface: `Task<ServiceResult> MarkTaskUnprocessedAsync(int taskId)`
    - Implementation: validate task exists and IsCompleted == false; UPDATE SET IsCompleted = 1, CompletedAtUtc = GETUTCDATE(), TaskOutcome = 'Unprocessed' WHERE Id = @id AND IsCompleted = 0
    - Return `ServiceResult.Fail("Task not found.")` if task doesn't exist
    - Return `ServiceResult.Fail("Task is already closed.")` if IsCompleted == true
    - Use full table names in SQL, catch (Exception ex), rethrow
    - _Requirements: 1.3, 1.4_

  - [x] 2.2 Modify CompleteTaskAsync to set TaskOutcome
    - Extend existing `CompleteTaskAsync` method to also SET `TaskOutcome = 'Completed'` when completing a task
    - Existing IsCompleted = 1, CompletedAtUtc = GETUTCDATE() logic remains unchanged
    - _Requirements: 1.2_

  - [x] 2.3 Modify ReopenTaskAsync to clear TaskOutcome
    - Extend existing `ReopenTaskAsync` method to also SET `TaskOutcome = NULL` when reopening a task
    - Existing IsCompleted = 0, CompletedAtUtc = NULL logic remains unchanged
    - _Requirements: 1.5_

  - [x] 2.4 Modify CreateTaskAsync and UpdateTaskAsync for ScheduledTimeUtc
    - Extend `CreateTaskAsync` to accept and persist `ScheduledTimeUtc` from the CreateFollowUpTaskRequest
    - Extend `UpdateTaskAsync` to accept and persist `ScheduledTimeUtc` from the UpdateFollowUpTaskRequest (supports clearing to null)
    - _Requirements: 3.2, 3.3, 3.4, 3.5_

  - [x] 2.5 Modify GetTodaysActionsAsync ordering for ScheduledTimeUtc
    - Within each urgency group (overdue, today, tomorrow, upcoming), order tasks with non-null ScheduledTimeUtc before all-day tasks (null)
    - Within the timed subset, order by ScheduledTimeUtc ascending
    - Update ORDER BY clause in the query
    - _Requirements: 4.5_

  - [x] 2.6 Implement GetDashboardBriefAsync in FollowUpTaskService
    - Add method to `IFollowUpTaskService` interface: `Task<List<DashboardTaskBriefDto>> GetDashboardBriefAsync(int businessId)`
    - SELECT incomplete tasks (IsCompleted = 0) where DueAtUtc date is today or tomorrow for the given BusinessId
    - Order by DueAtUtc ascending, then ScheduledTimeUtc ascending (NULLS last)
    - Include: Id, Title, TaskType (from TaskType navigation), DueAtUtc, ScheduledTimeUtc, ContactName (from Contact navigation), Urgency ("today" or "tomorrow" based on DueAtUtc date)
    - Use full table names in SQL, catch (Exception ex)
    - _Requirements: 7.1, 7.3_

  - [x]* 2.7 Write property test for Task Closure State Transition (Property 1)
    - **Property 1: Task Closure State Transition**
    - Test: For any active task, closing via Complete sets IsCompleted=true, CompletedAtUtc non-null, TaskOutcome="Completed"; closing via Unprocessed sets same fields with TaskOutcome="Unprocessed"
    - **Validates: Requirements 1.2, 1.3**

  - [x]* 2.8 Write property test for Already-Closed Task Rejects Unprocessed (Property 2)
    - **Property 2: Already-Closed Task Rejects Unprocessed**
    - Test: For any closed task, MarkTaskUnprocessedAsync returns Success=false and task state is unchanged
    - **Validates: Requirements 1.4**

  - [x]* 2.9 Write property test for Reopen Clears All Closure Fields (Property 3)
    - **Property 3: Reopen Clears All Closure Fields**
    - Test: For any closed task (Completed or Unprocessed), ReopenTaskAsync sets IsCompleted=false, CompletedAtUtc=null, TaskOutcome=null
    - **Validates: Requirements 1.5**

  - [x]* 2.10 Write property test for ScheduledTimeUtc Round-Trip (Property 4)
    - **Property 4: ScheduledTimeUtc Round-Trip Preservation**
    - Test: For any valid TimeOnly? value, create/update with that value then query returns same ScheduledTimeUtc
    - **Validates: Requirements 3.2, 3.4, 3.5, 3.6**

  - [x]* 2.11 Write property test for Task Ordering Within Urgency Group (Property 5)
    - **Property 5: Task Ordering Within Urgency Group**
    - Test: Within same urgency group, timed tasks appear before all-day tasks; timed tasks are ordered by ScheduledTimeUtc ascending
    - **Validates: Requirements 4.5**

  - [x]* 2.12 Write property test for TaskOutcome Filter Correctness (Property 6)
    - **Property 6: TaskOutcome Filter Correctness**
    - Test: Filtering by "Completed" returns only TaskOutcome="Completed"; filtering by "Unprocessed" returns only TaskOutcome="Unprocessed"; filtering by "All" returns all
    - **Validates: Requirements 2.5**

- [x] 3. Service layer — MeetingService enhancements
  - [x] 3.1 Implement GetUpcomingMeetingsBriefAsync in MeetingService
    - Add method to `IMeetingService` interface: `Task<List<MeetingBriefDto>> GetUpcomingMeetingsBriefAsync(int businessId)`
    - SELECT TOP 10 meetings WHERE IsActive = 1 AND IsCancelled = 0 AND BusinessId = @businessId AND ScheduledAtUtc >= @todayStart AND ScheduledAtUtc < @todayStart + 4 days
    - Order by ScheduledAtUtc ascending
    - Include: Id, LeadRequestId, Subject, ContactName (resolved from Contact), MeetingTypeName (resolved from MeetingType), ScheduledAtUtc, DurationMinutes, Location
    - Use full table names in SQL, catch (Exception ex)
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 3.2 Implement GetDashboardMeetingsBriefAsync in MeetingService
    - Add method to `IMeetingService` interface: `Task<List<DashboardMeetingBriefDto>> GetDashboardMeetingsBriefAsync(int businessId)`
    - SELECT meetings WHERE IsActive = 1 AND IsCancelled = 0 AND BusinessId = @businessId AND ScheduledAtUtc date is today or tomorrow
    - Order by ScheduledAtUtc ascending
    - Include: Id, Subject, ContactName, MeetingTypeName, ScheduledAtUtc, DurationMinutes, Urgency ("today" or "tomorrow")
    - Use full table names in SQL, catch (Exception ex)
    - _Requirements: 7.2, 7.4_

  - [x]* 3.3 Write property test for Upcoming Meetings Brief Query (Property 7)
    - **Property 7: Upcoming Meetings Brief Query Correctness**
    - Test: Returns only active, non-cancelled meetings for matching BusinessId within today+3 days window; ordered by ScheduledAtUtc ascending; capped at 10
    - **Validates: Requirements 5.1, 5.3, 5.4**

  - [x]* 3.4 Write property test for Dashboard Tasks Brief Query (Property 8)
    - **Property 8: Dashboard Tasks Brief Query Correctness**
    - Test: Returns only incomplete tasks for matching BusinessId due today/tomorrow; ordered by DueAtUtc ascending then ScheduledTimeUtc ascending (nulls last)
    - **Validates: Requirements 7.1**

  - [x]* 3.5 Write property test for Dashboard Meetings Brief Query (Property 9)
    - **Property 9: Dashboard Meetings Brief Query Correctness**
    - Test: Returns only active, non-cancelled meetings for matching BusinessId scheduled today/tomorrow; ordered by ScheduledAtUtc ascending
    - **Validates: Requirements 7.2**

- [x] 4. Checkpoint — Ensure all service layer changes compile and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Controller layer
  - [x] 5.1 Add AxPostMarkTaskUnprocessed endpoint to SalesController
    - `[HttpPost] [ValidateAntiForgeryToken] public async Task<IActionResult> AxPostMarkTaskUnprocessed(int id)`
    - Call `_followUpTaskService.MarkTaskUnprocessedAsync(id)`
    - Parameter `id` binds from query string (JS calls `?id=X`, consistent with existing AxPostCompleteTask pattern)
    - Return `Json(new { success = true, message = "Task marked as unprocessed." })` on success
    - Return `Json(new { success = false, message = result.Message })` on service failure
    - Catch (Exception ex): log error, return `Json(new { success = false, message = "Something went wrong. Please try again." })`
    - _Requirements: 2.2, 2.3_

  - [x] 5.2 Extend Pipeline action to include meetings brief data via AJAX endpoint
    - Add new `[HttpGet] AxGetUpcomingMeetingsBrief()` endpoint in SalesController
    - Call `_meetingService.GetUpcomingMeetingsBriefAsync(businessId)`
    - Return `Json(new { success = true, data = meetings })` — consistent with the existing AJAX-loaded Today's Actions pattern
    - If meeting service throws, log error and return `Json(new { success = true, data = new List<MeetingBriefDto>() })` (graceful degradation)
    - _Requirements: 6.1_

  - [x] 5.3 Extend HomeController Index action for Today's Brief
    - Inject `IFollowUpTaskService` and `IMeetingService` into HomeController's constructor (these are Sales-specific services not currently in HomeController)
    - Call `_followUpTaskService.GetDashboardBriefAsync(businessId)` and `_meetingService.GetDashboardMeetingsBriefAsync(businessId)`
    - Add results to the DashboardViewModel (new `List<DashboardTaskBriefDto> BriefTasks` and `List<DashboardMeetingBriefDto> BriefMeetings` properties)
    - If either service throws, log error and set respective list to empty (graceful degradation)
    - Only load brief data if the user has Sales module access (check via IPlanCheckService)
    - _Requirements: 8.1_

- [x] 6. Views — Pipeline page modifications
  - [x] 6.1 Add Upcoming Meetings panel to Pipeline.cshtml
    - Add new `<section id="upcomingMeetingsPanel" class="glass card-pad">` below existing Today's Actions panel
    - Include collapse/expand chevron toggle, heading "Upcoming Meetings"
    - Panel loads via AJAX (call `AxGetUpcomingMeetingsBrief`) matching the Today's Actions pattern — include a skeleton placeholder while loading
    - Render each meeting as a card/row: Subject, Contact name, Meeting type badge, "dd MMM HH:mm" formatted date, Duration
    - Show "No upcoming meetings scheduled" when list is empty
    - Link each meeting entry: navigate to lead detail for LeadRequestId if present, otherwise to contact detail
    - Store collapse state in localStorage key `upcomingMeetingsCollapsed`
    - _Requirements: 6.2, 6.3, 6.4, 6.5, 6.6_

  - [x] 6.2 Add Today's Brief section to Home/Index.cshtml
    - Add new `<section class="glass card-pad">` for "Today's Brief" positioned at the TOP of the dashboard content (above existing KPI cards) for maximum visibility
    - Only render the section if the user has Sales module access (wrap in plan check)
    - Tasks subsection: list tasks with Title, TaskType badge, "Today"/"Tomorrow" indicator, and "HH:mm" time when ScheduledTimeUtc is set
    - Meetings subsection: list meetings with Subject, Contact name, Meeting type badge, "HH:mm" scheduled time
    - For tomorrow meetings: display preparation reminder text "Prepare for tomorrow's meeting with {ContactName} at {Time}"
    - Show "All clear — no tasks or meetings for today and tomorrow" when both lists are empty
    - Separate today and tomorrow items with date group headers
    - _Requirements: 8.2, 8.3, 8.4, 8.5, 8.6, 8.7_

- [x] 7. Client-side JavaScript — follow-up-tasks.js modifications
  - [x] 7.1 Add "Unprocessed" button to active task cards
    - In `renderTaskCard(t)` (or equivalent), add an "Unprocessed" button alongside the existing "Complete" button for active (non-completed) tasks
    - Style consistently with existing action buttons
    - _Requirements: 2.1_

  - [x] 7.2 Implement markTaskUnprocessed function
    - `async function markTaskUnprocessed(taskId)` following BlockUI + SweetAlert2 pattern:
    - BlockUI.show('Processing...') → fetch POST to `/Sales/AxPostMarkTaskUnprocessed?id=` + taskId with `RequestVerificationToken` header → BlockUI.hide() → on success: Swal.fire success + refresh task list → on error: Swal.fire error
    - _Requirements: 2.2_

  - [x] 7.3 Display ScheduledTimeUtc on task cards
    - In `renderTaskCard(t)`, display the ScheduledTimeUtc formatted as "HH:mm" next to the task title when value is non-null
    - Show no time indicator for all-day tasks (null ScheduledTimeUtc)
    - _Requirements: 4.1, 4.2_

  - [x] 7.4 Display TaskOutcome badge on completed task cards
    - In `renderTaskCard(t)`, when task IsCompleted is true, show a TaskOutcome badge ("Completed" or "Unprocessed") indicating closure type
    - _Requirements: 2.4_

  - [x] 7.5 Add time picker to task create/edit forms
    - Add optional "Scheduled Time" time picker field to the create task form
    - Add same field to the edit task form, pre-populated with existing ScheduledTimeUtc value when set
    - Include the value in create/update request payloads
    - _Requirements: 4.3, 4.4_

- [x] 8. Client-side JavaScript — Upcoming Meetings panel
  - [x] 8.1 Implement collapse/expand toggle for Upcoming Meetings panel
    - Wire chevron click to toggle panel visibility
    - Read/write localStorage key `upcomingMeetingsCollapsed`
    - On page load: restore collapsed/expanded state from localStorage
    - _Requirements: 6.5_

- [x] 9. Final checkpoint — Ensure all changes compile and function correctly
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (FsCheck.Xunit)
- All SQL uses full table names (no aliases) per project standards
- All catch blocks use `catch (Exception ex)` per coding golden rules
- All AJAX methods use AxPost/AxGet prefix convention
- UI follows BlockUI + SweetAlert2 pattern (no native alerts)
- Database is named "Portal" — use `USE [Portal]` in migration scripts
- Bottom-up ordering: DB → Entities → DTOs → Services → Controller → Views → JS
- Pipeline meetings panel mirrors existing Today's Actions pattern (glass card-pad, collapse toggle, localStorage)
- Dashboard Today's Brief is server-rendered (consistent with existing KPI sections)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4"] },
    { "id": 2, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5", "2.6", "3.1", "3.2"] },
    { "id": 3, "tasks": ["2.7", "2.8", "2.9", "2.10", "2.11", "2.12", "3.3", "3.4", "3.5"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3"] },
    { "id": 5, "tasks": ["6.1", "6.2"] },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3", "7.4", "7.5", "8.1"] }
  ]
}
```
