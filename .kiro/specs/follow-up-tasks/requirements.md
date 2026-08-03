# Requirements Document: Follow-Up Tasks

## Introduction

Sales teams lose deals when follow-ups slip through the cracks. Currently, the only way to schedule a follow-up is to create a "Meeting" — which adds friction, clutters the calendar, and doesn't distinguish between a 5-second "send an email" reminder and an actual client meeting.

Follow-Up Tasks are lightweight action items attached to leads or contacts. They answer one question: **"Who should I contact today, and about what?"** They live on the Pipeline page, surface overdue actions, and can be created in seconds with quick presets.

## Glossary

- **Follow-Up Task**: A lightweight reminder to perform an action (call, email, follow-up) on a specific date
- **Today's Actions**: A dashboard panel on the Pipeline page showing tasks due today + overdue
- **Snooze**: Pushing a task's due date forward (1 day, 3 days, or custom)
- **Quick Preset**: One-click due date options: Tomorrow, In 3 days, Next week, Custom

## Requirements

### Requirement 1: Follow-Up Task Data Model

**User Story:** As a developer, I want a dedicated table for follow-up tasks, so that they're distinct from meetings and can be managed independently.

#### Acceptance Criteria

1. THE database SHALL contain a `[sales].[FollowUpTask]` table with columns: Id (INT IDENTITY PK), BusinessId (INT NOT NULL FK), LeadRequestId (INT NULL FK — optional, task can exist without a lead), ContactId (INT NULL FK — optional link to a contact), TeamMemberId (INT NULL FK — who owns the task), Title (NVARCHAR(200) NOT NULL), TaskType (NVARCHAR(50) NOT NULL — 'Call', 'Email', 'Follow-up', 'Meeting Prep', 'Other'), DueAtUtc (DATETIME NOT NULL), Notes (NVARCHAR(500) NULL), IsCompleted (BIT NOT NULL DEFAULT 0), CompletedAtUtc (DATETIME NULL), SnoozedCount (INT NOT NULL DEFAULT 0), CreatedByUserId (NVARCHAR(450) NOT NULL), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE()).
2. THE table SHALL have indexes on (BusinessId, DueAtUtc) for efficient "today's actions" queries.
3. THE table SHALL have an index on (LeadRequestId) for lead-scoped task lookups.

### Requirement 2: Quick Task Creation

**User Story:** As a sales team member, I want to create a follow-up task in seconds from the lead page, so that I don't lose momentum switching to a separate form.

#### Acceptance Criteria

1. THE Lead Detail page SHALL include a "Schedule Follow-up" button that opens a compact form.
2. THE form SHALL include: Title (pre-filled with "Follow up — {Contact Name}"), Task Type (dropdown: Call, Email, Follow-up, Meeting Prep, Other), Due Date (with quick presets).
3. THE quick presets SHALL be buttons: "Tomorrow", "In 3 days", "Next week", "Custom" (date picker).
4. CLICKING a preset SHALL immediately set the due date and allow saving with one click.
5. THE task SHALL auto-associate with the current lead and its contact.
6. THE task SHALL default to the currently logged-in user as the owner (TeamMemberId).

### Requirement 3: Today's Actions Panel (Pipeline Page)

**User Story:** As a sales team member, I want to see all my pending follow-ups when I open the Pipeline, so that I know exactly who to contact today.

#### Acceptance Criteria

1. THE Pipeline page SHALL display a "Today's Actions" panel above the Kanban board.
2. THE panel SHALL show: overdue tasks (past due, not completed), today's tasks, and optionally tomorrow's tasks.
3. EACH task row SHALL display: colour indicator (red=overdue, amber=today, grey=tomorrow), Title, Contact name, Task type icon, Due date/time, Quick actions (Complete, Snooze).
4. THE panel SHALL show a count badge: "3 due today, 2 overdue".
5. OVERDUE tasks SHALL always appear first, sorted by how many days overdue.
6. THE panel SHALL be collapsible (user can minimize it if they want to focus on the board).

### Requirement 4: Complete and Snooze

**User Story:** As a sales team member, I want to mark tasks as done or push them forward, so that my action list stays current.

#### Acceptance Criteria

1. CLICKING "Complete" SHALL set IsCompleted = 1 and CompletedAtUtc = now.
2. CLICKING "Snooze" SHALL show options: "+1 day", "+3 days", "Next Monday", "Custom date".
3. SNOOZING SHALL update DueAtUtc to the new date and increment SnoozedCount.
4. A task that has been snoozed 3+ times SHALL show a warning indicator (this follow-up keeps getting pushed).
5. COMPLETED tasks SHALL disappear from the Today's Actions panel immediately.

### Requirement 5: Task List View

**User Story:** As a sales manager, I want to see all follow-up tasks across the team, so that I can monitor activity and spot missed follow-ups.

#### Acceptance Criteria

1. THE system SHALL provide a "Tasks" page (or sub-tab under Opportunities) listing all tasks.
2. THE list SHALL support filtering by: status (pending/completed/overdue), team member, task type, date range.
3. THE list SHALL show: Title, Contact, Type, Due Date, Status (overdue/today/upcoming/completed), Assigned To.
4. THE list SHALL be sorted by DueAtUtc ascending by default (most urgent first).

### Requirement 6: Lead Detail Integration

**User Story:** As a user viewing a lead, I want to see all follow-up tasks associated with it, so that I have the full activity context.

#### Acceptance Criteria

1. THE Lead Detail page SHALL include a "Follow-Up Tasks" section showing all tasks for that lead.
2. EACH task SHALL show: title, type, due date, status, quick complete/snooze actions.
3. COMPLETED tasks SHALL appear below pending ones, visually muted.
4. THE section SHALL include the "Schedule Follow-up" button (from Requirement 2).

### Requirement 7: Notifications & Urgency

**User Story:** As a user who might forget, I want overdue tasks surfaced prominently, so that I never miss a critical follow-up.

#### Acceptance Criteria

1. THE main Dashboard briefing card SHALL mention overdue follow-up tasks (if any exist).
2. THE Pipeline page's Today's Actions panel SHALL show a red badge count for overdue items.
3. IF a task is overdue by 3+ days, IT SHALL be highlighted with a stronger visual indicator.

### Requirement 8: Team Assignment

**User Story:** As a sales manager, I want to assign follow-up tasks to team members, so that responsibilities are clear.

#### Acceptance Criteria

1. THE task creation form SHALL include an "Assign to" dropdown listing active team members.
2. THE default assignment SHALL be the current user (self-assign).
3. EACH team member's Today's Actions SHALL only show their own assigned tasks.
4. A manager view SHALL show all team tasks (with filtering by team member).
