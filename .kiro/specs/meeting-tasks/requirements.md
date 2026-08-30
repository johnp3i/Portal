# Requirements Document: Meeting Tasks

## Introduction

After a meeting, sales users frequently need to create actionable follow-ups — "send a demo account", "email revised pricing", "schedule a technical review". Currently, follow-up tasks and meetings are completely independent entities: a task can link to a lead or contact, but there is no way to trace a task back to the specific meeting that generated it.

Meeting Tasks bridge this gap by adding an optional link from FollowUpTask to Meeting. This allows users to create tasks directly from a meeting context (with contact and lead auto-populated), view all tasks spawned by a meeting, and trace any task back to its originating meeting.

## Glossary

- **Meeting_Task**: A FollowUpTask that has a non-null MeetingId, linking it to the meeting that spawned it
- **Meeting_Tasks_Section**: A UI panel within the Edit Meeting modal showing tasks linked to that meeting, with inline creation
- **Task_Badge**: A small count indicator on a meeting row showing the number of linked tasks
- **Meeting_Reference**: A visual label on a task indicating which meeting it originated from
- **Inline_Task_Form**: A compact form within the Meeting Tasks Section for quick task creation without leaving the meeting context

## Requirements

### Requirement 1: MeetingId on FollowUpTask

**User Story:** As a developer, I want FollowUpTask to optionally reference a Meeting, so that tasks can be traced back to the meeting that generated them.

#### Acceptance Criteria

1. THE `[sales].[FollowUpTask]` table SHALL have a nullable `[MeetingId]` column of type INT with a foreign key to `[sales].[Meeting]([Id])`
2. THE `FollowUpTask` entity class SHALL include a nullable `MeetingId` property of type `int?`
3. THE `FollowUpTask` entity class SHALL include a `Meeting?` navigation property
4. THE `Meeting` entity class SHALL include an `ICollection<FollowUpTask> Tasks` navigation property
5. THE `CreateFollowUpTaskRequest` DTO SHALL include a nullable `MeetingId` property of type `int?`
6. THE existing task creation, update, and querying logic SHALL continue to work unchanged for tasks without a MeetingId (backward compatible)
7. THE `[MeetingId]` column SHALL have a nonclustered index for efficient meeting-scoped lookups

### Requirement 2: Create Task from Meeting Context

**User Story:** As a sales user, I want to create a follow-up task directly from a meeting, so that I can capture action items while the meeting context is fresh.

#### Acceptance Criteria

1. THE Meeting_Tasks_Section SHALL be visible within the Edit Meeting modal, below the Outcome field
2. THE Meeting_Tasks_Section SHALL display an "Add Task" button that reveals the Inline_Task_Form
3. THE Inline_Task_Form SHALL include: Title (text input, required), Task Type (dropdown: Call, Email, Follow-up, Meeting Prep, Other), Due Date (date input, required), Notes (textarea, optional)
4. WHEN a task is created from the Inline_Task_Form, THE system SHALL automatically set MeetingId to the current meeting's Id
5. WHEN the meeting has a ContactId, THE system SHALL automatically set the task's ContactId to the meeting's ContactId
6. WHEN the meeting has a LeadRequestId, THE system SHALL automatically set the task's LeadRequestId to the meeting's LeadRequestId
7. WHEN the Inline_Task_Form is submitted with valid data, THE task SHALL appear in the Meeting_Tasks_Section list immediately without closing the Edit Meeting modal
8. WHEN the Inline_Task_Form is submitted with an empty Title or missing Due Date, THE system SHALL show a SweetAlert2 warning and prevent submission

### Requirement 3: View Tasks Linked to a Meeting

**User Story:** As a sales user, I want to see all tasks associated with a meeting, so that I can track what was agreed and what's still pending.

#### Acceptance Criteria

1. THE Meeting_Tasks_Section SHALL display a list of all tasks linked to the current meeting (MeetingId = current meeting Id)
2. EACH task in the list SHALL display: Title, Task Type, Due Date, and completion status
3. PENDING tasks SHALL appear above completed tasks in the list
4. COMPLETED tasks SHALL be visually muted (reduced opacity or strikethrough)
5. THE system SHALL provide an AJAX endpoint to fetch tasks by MeetingId
6. WHEN no tasks are linked to the meeting, THE Meeting_Tasks_Section SHALL display "No tasks yet." placeholder text

### Requirement 4: Complete Task from Meeting Context

**User Story:** As a sales user, I want to mark a meeting task as complete without leaving the meeting modal, so that I can manage tasks efficiently.

#### Acceptance Criteria

1. EACH pending task in the Meeting_Tasks_Section SHALL display a "Complete" button
2. WHEN the Complete button is clicked, THE system SHALL mark the task as completed (IsCompleted = true, CompletedAtUtc = now, TaskOutcome = "Completed")
3. AFTER completion, THE task SHALL move to the completed section of the list and appear visually muted
4. THE completion action SHALL use BlockUI during the request and show no SweetAlert2 on success (quick operation — UI update is sufficient feedback)

### Requirement 5: Task Count Badge on Meeting Row

**User Story:** As a sales user, I want to see at a glance how many tasks are linked to each meeting, so that I can identify meetings with outstanding action items.

#### Acceptance Criteria

1. THE Meetings_Table SHALL display a Task_Badge in the meeting row showing the count of linked tasks
2. THE Task_Badge SHALL only appear when the meeting has one or more linked tasks (count > 0)
3. THE Task_Badge SHALL show the total task count and visually distinguish when there are pending (incomplete) tasks
4. WHEN all linked tasks are completed, THE Task_Badge SHALL appear in a muted style (green or grey)
5. WHEN there are pending tasks, THE Task_Badge SHALL appear in the primary style (blue)

### Requirement 6: Meeting Reference on Task Views

**User Story:** As a sales user, I want to see which meeting a task came from when viewing it in the Tasks list or Today's Actions, so that I have full context.

#### Acceptance Criteria

1. THE `FollowUpTaskDto` SHALL include a nullable `MeetingSubject` property (string?) populated when the task has a MeetingId
2. WHEN a task has a MeetingSubject, THE Tasks list page and Today's Actions panel SHALL display the meeting subject as a reference label below the task title
3. THE Meeting_Reference label SHALL be styled as a subtle tag (small font, muted colour) to avoid visual clutter

### Requirement 7: Meeting Detail DTO Extension

**User Story:** As a developer, I want the meeting detail endpoint to include linked tasks, so that the Edit Meeting modal can render the Meeting Tasks Section.

#### Acceptance Criteria

1. THE `MeetingDetailDto` SHALL include a `List<MeetingTaskBriefDto> Tasks` property
2. THE `MeetingTaskBriefDto` SHALL contain: Id, Title, TaskType, DueAtUtc, IsCompleted, CompletedAtUtc, TaskOutcome
3. THE `AxGetMeetingDetail` endpoint SHALL return the Tasks collection populated with all tasks where MeetingId matches the meeting
4. THE Tasks collection SHALL be ordered: pending tasks first (by DueAtUtc ascending), then completed tasks (by CompletedAtUtc descending)

### Requirement 8: Activity Feed Integration

**User Story:** As a sales user, I want meeting-originated tasks to appear in the lead's activity feed, so that the full history is captured.

#### Acceptance Criteria

1. WHEN a task is created from a meeting context with a LeadRequestId, THE system SHALL record an activity feed entry: "Follow-up task created from meeting: {MeetingSubject}"
2. THE activity description SHALL reference the meeting subject for traceability
