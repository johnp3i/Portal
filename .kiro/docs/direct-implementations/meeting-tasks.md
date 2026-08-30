# Meeting Tasks — Direct Implementation

**Date:** 27 August 2026, Thursday
**Session type:** Spec-driven implementation

## Summary

Added the ability to create follow-up tasks directly from a meeting context. Tasks are linked to meetings via a nullable `MeetingId` FK on `[sales].[FollowUpTask]`, enabling traceability from task back to the originating meeting.

## What Was Built

### Database
- **Migration 178** — Added `[MeetingId] INT NULL` to `[sales].[FollowUpTask]` with FK to `[sales].[Meeting]` and a filtered nonclustered index.

### Backend (C#)
- `FollowUpTask.cs` — Added `MeetingId` property and `Meeting` navigation.
- `Meeting.cs` — Added `ICollection<FollowUpTask> Tasks` navigation.
- `PortalDbContext.cs` — Added FK configuration with `DeleteBehavior.NoAction`.
- `FollowUpTaskDtos.cs` — Added `MeetingId` to `CreateFollowUpTaskRequest`; added `MeetingId` + `MeetingSubject` to `FollowUpTaskDto`.
- `MeetingDtos.cs` — Created `MeetingTaskBriefDto`; added `Tasks` list to `MeetingDetailDto`; added `TaskCount`/`PendingTaskCount` to `MeetingPagedListDto`.
- `FollowUpTaskRepository.cs` — Updated `InsertAsync` and all 4 existing SELECT queries to include `[MeetingId]`. Added `GetByMeetingIdAsync` and `GetTaskCountsByMeetingIdsAsync`.
- `MeetingRepository.cs` — Added `GetSubjectsByIdsAsync` for batch meeting subject lookup.
- `FollowUpTaskService.cs` — Injected `MeetingRepository`. Updated `CreateTaskAsync`, `MapToDto`, `GetTasksPagedAsync`, `GetTodaysActionsAsync`, and `GetByLeadIdAsync` to batch-fetch meeting subjects.
- `MeetingService.cs` — Injected `FollowUpTaskRepository`. Updated `GetByIdAsync` to include linked tasks. Updated `GetMeetingsPagedAsync` to include task counts. Added `GetSubjectAsync` for lightweight subject lookup.
- `IMeetingService.cs` — Added `GetSubjectAsync`.
- `SalesController.cs` — Updated `AxPostCreateTask` to use `GetSubjectAsync` for activity feed descriptions referencing the meeting subject.

### Frontend
- `Meetings.cshtml` — Added Meeting Tasks section to Edit Meeting modal (heading, inline creation form, scrollable task list container). Widened modal to 600px. Bumped JS version.
- `meetings.js` — Added task rendering in modal (pending with overdue visual, completed with muted style), inline task creation (`submitMeetingTask`), task completion (`completeMeetingTask`), task count badges on meeting rows, dirty flag for table refresh on modal close.
- `follow-up-tasks.js` — Added "from: {MeetingSubject}" reference label in Today's Actions panel.
- `Tasks.cshtml` — Added meeting reference label below task title in the Tasks list table.

## Review Fixes Applied

After initial implementation, a review pass identified and resolved:

1. **Double TryGetValue** in `MeetingService.GetMeetingsPagedAsync` — extracted to single lookup.
2. **Heavyweight subject lookup** in controller — added lightweight `GetSubjectAsync` to `IMeetingService`.
3. **Stale task count badge** — added `_meetingTasksDirty` flag; `closeEditMeetingModal` reloads the table when tasks were modified.
4. **Swal timer too short** — bumped from 1500ms to 2000ms on task creation success.
5. **No scroll on task list** — added `max-height:200px; overflow-y:auto` to the task list container.
6. **No overdue visual** — pending tasks past due date now show a red dot and "Overdue:" prefix.

## Spec Location

`.kiro/specs/meeting-tasks/` — requirements.md, design.md, tasks.md

## Testing Scenarios

`.kiro/docs/scenarios/meeting-tasks-testing.md` — 16 scenarios + database verification queries
