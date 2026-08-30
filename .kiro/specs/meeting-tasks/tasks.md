# Implementation Plan: Meeting Tasks

## Overview

This plan links FollowUpTask to Meeting via a nullable `MeetingId` FK, enabling task creation from meeting context, task viewing inside the Edit Meeting modal, task count badges on meeting rows, and meeting reference labels on task views. Implementation proceeds bottom-up: migration → entity/EF config → DTOs → repository → service → controller tweaks → view/JS enhancements.

## Tasks

- [ ] 1. Database migration — Add MeetingId to FollowUpTask
  - [ ] 1.1 Create migration 178_AddMeetingIdToFollowUpTask.sql
    - Add `[MeetingId] INT NULL` column to `[sales].[FollowUpTask]` with FK constraint `[FK_FollowUpTask_Meeting]` referencing `[sales].[Meeting]([Id])`
    - Create filtered nonclustered index `[IX_FollowUpTask_MeetingId]` on `[MeetingId]` WHERE `[MeetingId] IS NOT NULL`
    - Include `USE [Portal]` header per SQL script standards
    - _Requirements: 1.1, 1.7_

- [ ] 2. Entity and EF Core configuration
  - [ ] 2.1 Add MeetingId property and Meeting navigation to FollowUpTask entity
    - Add `public int? MeetingId { get; set; }` to `Portal.Infrastructure/Entities/Sales/FollowUpTask.cs`
    - Add `public Meeting? Meeting { get; set; }` navigation property
    - _Requirements: 1.2, 1.3_

  - [ ] 2.2 Add Tasks navigation collection to Meeting entity
    - Add `public ICollection<FollowUpTask> Tasks { get; set; } = new List<FollowUpTask>();` to `Portal.Infrastructure/Entities/Sales/Meeting.cs`
    - _Requirements: 1.4_

  - [ ] 2.3 Add EF Core FK configuration for FollowUpTask → Meeting
    - In `PortalDbContext.ConfigureFollowUpTask`, add relationship configuration:
      ```csharp
      entity.HasOne(e => e.Meeting)
          .WithMany(m => m.Tasks)
          .HasForeignKey(e => e.MeetingId)
          .IsRequired(false)
          .OnDelete(DeleteBehavior.NoAction);
      ```
    - _Requirements: 1.1, 1.3, 1.4_

- [ ] 3. DTO changes
  - [ ] 3.1 Add MeetingId to CreateFollowUpTaskRequest
    - Add `public int? MeetingId { get; set; }` to `CreateFollowUpTaskRequest` in `Portal.Infrastructure/Models/Sales/FollowUpTaskDtos.cs`
    - _Requirements: 1.5_

  - [ ] 3.2 Add MeetingId and MeetingSubject to FollowUpTaskDto
    - Add `public int? MeetingId { get; set; }` to `FollowUpTaskDto`
    - Add `public string? MeetingSubject { get; set; }` to `FollowUpTaskDto`
    - _Requirements: 6.1_

  - [ ] 3.3 Create MeetingTaskBriefDto
    - Create `MeetingTaskBriefDto` class in `Portal.Infrastructure/Models/Sales/MeetingDtos.cs` with properties: Id, Title, TaskType, DueAtUtc, IsCompleted, CompletedAtUtc, TaskOutcome
    - _Requirements: 7.2_

  - [ ] 3.4 Add Tasks collection to MeetingDetailDto
    - Add `public List<MeetingTaskBriefDto> Tasks { get; set; } = new();` to `MeetingDetailDto`
    - _Requirements: 7.1_

  - [ ] 3.5 Add TaskCount and PendingTaskCount to MeetingPagedListDto
    - Add `public int TaskCount { get; set; }` and `public int PendingTaskCount { get; set; }` to `MeetingPagedListDto`
    - _Requirements: 5.2, 5.3_

- [ ] 4. Repository layer
  - [ ] 4.1 Modify FollowUpTaskRepository.InsertAsync to include MeetingId
    - Add `[MeetingId]` to the INSERT column list and VALUES list
    - Add `new SqlParameter("@MeetingId", entity.MeetingId ?? (object)DBNull.Value)` to the parameter set
    - _Requirements: 1.5, 2.4_

  - [ ] 4.2 Add `[MeetingId]` to all existing SELECT queries in FollowUpTaskRepository
    - The following methods use explicit column lists that must include `[MeetingId]`:
      - `GetByIdAsync` — SELECT at line ~198
      - `GetByLeadRequestIdAsync` — SELECT at line ~264
      - `GetDashboardBriefAsync` — SELECT at line ~355
      - `GetPagedAsync` — SELECT at line ~471
    - Without this, `entity.MeetingId` will always be null even for tasks that have a meeting link, breaking the MeetingSubject enrichment in MapToDto
    - _Requirements: 1.2, 6.1_

  - [ ] 4.3 Add FollowUpTaskRepository.GetByMeetingIdAsync
    - New method: `public async Task<List<FollowUpTask>> GetByMeetingIdAsync(int meetingId, int businessId)`
    - Query `[sales].[FollowUpTask]` WHERE `[MeetingId] = @MeetingId AND [BusinessId] = @BusinessId`
    - Order: `[IsCompleted] ASC, CASE WHEN [IsCompleted] = 0 THEN [DueAtUtc] END ASC, CASE WHEN [IsCompleted] = 1 THEN [CompletedAtUtc] END DESC`
    - Use full table name, `catch (Exception ex) { throw; }`
    - Follow the same DbConnection command pattern as `GetByLeadRequestIdAsync`
    - Include `[MeetingId]` in the SELECT column list
    - _Requirements: 3.1, 3.3, 3.5, 7.4_

  - [ ] 4.4 Add FollowUpTaskRepository.GetTaskCountsByMeetingIdsAsync
    - New method: `public async Task<Dictionary<int, (int Total, int Pending)>> GetTaskCountsByMeetingIdsAsync(IEnumerable<int> meetingIds, int businessId)`
    - Single query: `SELECT [MeetingId], COUNT(*) AS Total, SUM(CASE WHEN [IsCompleted] = 0 THEN 1 ELSE 0 END) AS Pending FROM [sales].[FollowUpTask] WHERE [MeetingId] IN (...) AND [BusinessId] = @BusinessId GROUP BY [MeetingId]`
    - Build parameterised IN clause from the meeting IDs list
    - Return empty dictionary if input collection is empty (guard clause)
    - `catch (Exception ex) { throw; }`
    - _Requirements: 5.2, 5.3, 5.4, 5.5_

  - [ ] 4.5 Add MeetingRepository.GetSubjectsByIdsAsync
    - New method: `public async Task<Dictionary<int, string>> GetSubjectsByIdsAsync(IEnumerable<int> ids, int businessId)`
    - Query `[sales].[Meeting]` for `[Id]` and `[Subject]` WHERE `[Id] IN (...) AND [BusinessId] = @BusinessId`
    - Return dictionary keyed by Meeting.Id
    - Return empty dictionary if input is empty
    - `catch (Exception ex) { throw; }`
    - _Requirements: 6.1, 6.2_

- [ ] 5. Service layer
  - [ ] 5.1 Modify FollowUpTaskService.CreateTaskAsync to pass MeetingId
    - Set `MeetingId = request.MeetingId` on the FollowUpTask entity before calling `InsertAsync`
    - No additional validation needed — FK constraint handles integrity
    - _Requirements: 1.5, 2.4_

  - [ ] 5.2 Modify FollowUpTaskService.MapToDto to include MeetingSubject
    - Add `Dictionary<int, string>? meetingSubjectsLookup = null` parameter to `MapToDto`
    - When entity has `MeetingId` and lookup contains a match, set `MeetingSubject` on the DTO
    - Also set `MeetingId` on the DTO
    - _Requirements: 6.1, 6.2_

  - [ ] 5.3 Modify FollowUpTaskService.GetTasksPagedAsync to batch-fetch meeting subjects
    - After fetching paged tasks, collect distinct non-null `MeetingId` values
    - Call `_meetingRepository.GetSubjectsByIdsAsync(meetingIds, businessId)` to get subjects
    - Pass the lookup to `MapToDto` for each task
    - _Requirements: 6.1, 6.2_

  - [ ] 5.4 Modify FollowUpTaskService.GetTodaysActionsAsync to batch-fetch meeting subjects
    - Same pattern as 5.3: collect MeetingIds → batch fetch subjects → pass to MapToDto
    - _Requirements: 6.1, 6.2_

  - [ ] 5.5 Modify FollowUpTaskService.GetByLeadIdAsync to batch-fetch meeting subjects
    - Same pattern as 5.3: after fetching tasks for a lead, collect distinct non-null MeetingId values, batch-fetch subjects, pass to MapToDto
    - Without this, meeting-originated tasks on the Lead Detail page will have MeetingSubject = null
    - _Requirements: 6.1, 6.2_

  - [ ] 5.6 Inject MeetingRepository into FollowUpTaskService
    - Add `private readonly MeetingRepository _meetingRepository;` field
    - Add `MeetingRepository meetingRepository` to the constructor and assign it
    - The service currently only has FollowUpTaskRepository, SalesContactRepository, and ICurrentTenantService — it needs MeetingRepository for tasks 5.3, 5.4, and 5.5
    - _Requirements: 6.1_

  - [ ] 5.7 Inject FollowUpTaskRepository into MeetingService
    - Add `private readonly FollowUpTaskRepository _followUpTaskRepository;` field
    - Add `FollowUpTaskRepository followUpTaskRepository` to the constructor and assign it
    - The service currently does not have FollowUpTaskRepository — it needs it for tasks 5.8 and 5.9
    - _Requirements: 7.1, 5.2_

  - [ ] 5.8 Modify MeetingService.GetByIdAsync to include linked tasks
    - After fetching product requests and opportunities, call `_followUpTaskRepository.GetByMeetingIdAsync(id, businessId)`
    - Map results to `List<MeetingTaskBriefDto>` and set on `MeetingDetailDto.Tasks`
    - Order: pending first (by DueAtUtc ASC), then completed (by CompletedAtUtc DESC)
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [ ] 5.9 Modify MeetingService.GetMeetingsPagedAsync to include task counts
    - After fetching paged meetings, collect meeting IDs
    - Call `_followUpTaskRepository.GetTaskCountsByMeetingIdsAsync(meetingIds, businessId)`
    - Set `TaskCount` and `PendingTaskCount` on each `MeetingPagedListDto`
    - Default to 0 for meetings not in the dictionary
    - _Requirements: 5.2, 5.3, 5.4, 5.5_

- [ ] 6. Checkpoint — Build verification
  - Build the solution and verify all backend changes compile cleanly. Fix any issues before proceeding to frontend.

- [ ] 7. Controller layer — Activity feed enhancement
  - [ ] 7.1 Update AxPostCreateTask to include meeting subject in activity description
    - When `request.MeetingId` is provided, fetch meeting subject via `_meetingService.GetByIdAsync(request.MeetingId.Value)`
    - Update activity description: `$"Follow-up task created from meeting: {meetingSubject}"` instead of `$"Follow-up task created: {request.Title}"`
    - Fallback to task title if meeting lookup fails
    - _Requirements: 8.1, 8.2_

- [ ] 8. View changes — Meetings.cshtml
  - [ ] 8.1 Add Meeting Tasks section to Edit Meeting modal
    - Add a tasks section below the Outcome textarea in the `editMeetingModal` div
    - Section heading: "Meeting Tasks" with a count in parentheses and an "Add Task" button
    - Task list container: `<div id="editMeetingTasksList"></div>` for JS rendering
    - Inline task creation form (hidden by default): Title input, TaskType dropdown (Call, Email, Follow-up, Meeting Prep, Other), Due Date input, Notes textarea, Cancel and Create Task buttons
    - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.6_

- [ ] 9. JavaScript changes — meetings.js
  - [ ] 9.1 Render tasks in Edit Meeting modal
    - In `openEditMeetingModal`, after populating meeting fields, render the tasks section from `result.data.tasks`
    - Render pending tasks first, then completed tasks (muted style: `opacity:0.5; text-decoration:line-through`)
    - Each pending task shows: checkbox-style Complete button, Title, TaskType badge, Due Date
    - Each completed task shows: checkmark icon, Title (struck through), completed date
    - Show "No tasks yet." when tasks array is empty
    - Update the section heading count: "Meeting Tasks (N)"
    - Store `meetingId`, `contactId`, and `leadRequestId` from the meeting detail response in module-level variables for task creation
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.6_

  - [ ] 9.2 Implement inline task creation from meeting context
    - `showMeetingTaskForm()`: show the inline form, focus the title input
    - `hideMeetingTaskForm()`: hide the form, clear its fields
    - `submitMeetingTask()`: validate Title and Due Date (Swal.fire warning if empty), build request payload with `meetingId`, `contactId`, `leadRequestId` from stored meeting context, POST to `/Sales/AxPostCreateTask`, on success: hide form + re-fetch meeting detail to refresh task list + Swal.fire success, on error: Swal.fire error
    - Use BlockUI.show/hide around the AJAX call
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [ ] 9.3 Implement complete task from meeting modal
    - `completeMeetingTask(taskId)`: BlockUI.show → POST to `/Sales/AxPostCompleteTask?id=` → BlockUI.hide → re-fetch meeting detail to refresh task list (no Swal — quick operation)
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [ ] 9.4 Add task count badge to meeting table rows
    - In `renderMeetingsTable`, for each meeting row, check `m.taskCount`
    - If `taskCount > 0` and `pendingTaskCount > 0`: show blue badge `[N tasks]` after subject
    - If `taskCount > 0` and `pendingTaskCount === 0`: show green/muted badge `[N ✓]` after subject
    - If `taskCount === 0`: no badge
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [ ] 9.5 Update meetings.js version query string
    - Bump the script version in `Meetings.cshtml` from `meetings.js?v=6` to `meetings.js?v=7` to bust browser cache
    - _Requirements: N/A (deployment hygiene)_

- [ ] 10. Meeting reference on task views
  - [ ] 10.1 Add meeting reference label to Tasks list page rendering
    - In the Tasks page JS (tasks rendering), when a task has `meetingSubject`, display a subtle label below the title: "from: {MeetingSubject}"
    - Styled: `font-size:11px; color:#8a9bab; margin-top:2px;` (same as relative time label pattern)
    - _Requirements: 6.2, 6.3_

  - [ ] 10.2 Add meeting reference label to Today's Actions panel rendering
    - In the Pipeline page Today's Actions panel JS, when a task has `meetingSubject`, display the same subtle reference label
    - _Requirements: 6.2, 6.3_

- [ ] 11. Final checkpoint — End-to-end verification
  - Build the solution, verify no compile errors
  - Verify the Edit Meeting modal displays tasks section correctly
  - Verify inline task creation populates MeetingId, ContactId, LeadRequestId
  - Verify task count badges render on meeting rows
  - Verify existing tasks without MeetingId still work unchanged (backward compatibility)

## Notes

- No new controllers, services, or entities are created — all changes are additive to existing classes
- The existing `AxPostCreateTask` and `AxPostCompleteTask` endpoints are reused without modification (only the request DTO gains `MeetingId`)
- Migration 178 is the next available migration number (after 177_BackfillLeadTrackingHistory.sql)
- All SQL uses full table names (no aliases) per project standards
- All catch blocks use `catch (Exception ex) { throw; }` per coding golden rules
- All AJAX methods follow BlockUI + SweetAlert2 pattern (no native alerts)
- Bottom-up ordering: DB → Entity → DTO → Repository → Service → Controller → View → JS
- Backward compatibility is a hard requirement — existing tasks without MeetingId must be unaffected

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4", "3.5"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3", "4.4", "4.5"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.6", "5.7"] },
    { "id": 5, "tasks": ["5.3", "5.4", "5.5", "5.8", "5.9"] },
    { "id": 6, "tasks": ["6"] },
    { "id": 7, "tasks": ["7.1"] },
    { "id": 8, "tasks": ["8.1"] },
    { "id": 9, "tasks": ["9.1", "9.2", "9.3", "9.4", "9.5"] },
    { "id": 10, "tasks": ["10.1", "10.2"] },
    { "id": 11, "tasks": ["11"] }
  ]
}
```
