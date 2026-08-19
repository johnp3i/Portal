# Requirements Document

## Introduction

The Sales Tasks & Meetings Enhancements module extends the existing Sales/Opportunities pipeline with four targeted improvements to task management, scheduling precision, and daily operational visibility. These enhancements build on the existing `[sales].[FollowUpTask]` and `[sales].[Meeting]` tables, the FollowUpTaskService, MeetingService, and the established Today's Actions panel on the Pipeline page.

The four enhancements are:
1. **Task "Unprocessed" Status** — Introduce an alternative task closure outcome ("Unprocessed") for tasks where the intended action could not be completed (e.g., customer unreachable, no response received). The task is closed but explicitly marked as not successfully actioned.
2. **Optional Time on Tasks** — Allow an optional time-of-day component on follow-up tasks, enabling users to schedule tasks at specific times while preserving the existing date-only behaviour as default.
3. **Meetings Brief on Pipeline Page** — Add a collapsible "Upcoming Meetings" panel to the Pipeline page showing meetings scheduled for the next few days, mirroring the existing Today's Actions pattern.
4. **Dashboard Brief for Tasks + Meetings (Home Page)** — Add a "Today's Brief" section to the Home/Index dashboard providing a concise, actionable summary of tasks due today/tomorrow and upcoming meetings, including preparation reminders.

## Glossary

- **FollowUpTask_Service**: The service responsible for FollowUpTask CRUD operations, task completion, snoozing, reopening, and status management including the new "Unprocessed" outcome
- **Meeting_Service**: The service responsible for Meeting CRUD, ICS file generation, and meeting queries including upcoming meeting retrieval for dashboard and pipeline panels
- **Sales_Controller**: The ASP.NET Core MVC controller responsible for the Pipeline page, Today's Actions panel, the new Upcoming Meetings panel, and task action endpoints
- **Home_Controller**: The ASP.NET Core MVC controller responsible for the main Dashboard (Home/Index) page including the new Today's Brief section
- **FollowUpTask**: A lightweight follow-up reminder attached to a lead or contact, stored in `[sales].[FollowUpTask]`, now extended with an outcome status and optional scheduled time
- **Meeting**: A scheduled meeting related to a lead, stored in `[sales].[Meeting]`, with type, duration, and outcome recording
- **TaskOutcomeType**: A classification of how a task was closed: "Completed" (successfully actioned) or "Unprocessed" (closed without successful action)
- **Today_Actions_Panel**: The existing collapsible panel on the Pipeline page that displays tasks due today, overdue, and due tomorrow
- **Upcoming_Meetings_Panel**: A new collapsible panel on the Pipeline page that displays meetings scheduled for today and the next few days
- **Todays_Brief**: A new section on the Home/Index dashboard that summarises tasks and meetings due today and tomorrow with preparation reminders
- **Scheduled_Time**: An optional time-of-day component on a FollowUpTask, allowing users to schedule tasks at a specific hour and minute rather than treating the task as an all-day item
- **All_Day_Task**: A follow-up task with no specific Scheduled_Time set, displayed with only a due date
- **Page_Size**: The number of records displayed per page in list views, fixed at 15

## Requirements

### Requirement 1: Task "Unprocessed" Status — Data Model Extension

**User Story:** As a salesperson, I want to mark a follow-up task as "Unprocessed" when I could not successfully action it (e.g., customer not reached, no response), so that the task is closed from my to-do list but the outcome is tracked separately from successfully completed tasks.

#### Acceptance Criteria

1. THE Portal_Database SHALL add a nullable TaskOutcome column (nvarchar(20), nullable) to the existing `[sales].[FollowUpTask]` table, storing the closure outcome classification
2. WHEN a complete task request is submitted, THE FollowUpTask_Service SHALL set the TaskOutcome value to "Completed" on the FollowUpTask record in addition to setting IsCompleted to true and CompletedAtUtc to the current UTC time
3. WHEN an unprocessed task request is submitted for an active task, THE FollowUpTask_Service SHALL set IsCompleted to true, CompletedAtUtc to the current UTC time, and TaskOutcome to "Unprocessed" on the FollowUpTask record
4. IF an unprocessed task request is submitted for a task that is already completed, THEN THE FollowUpTask_Service SHALL return an error indicating the task is already closed
5. WHEN a reopen task request is submitted, THE FollowUpTask_Service SHALL set IsCompleted to false, CompletedAtUtc to null, and TaskOutcome to null on the FollowUpTask record
6. THE FollowUpTask_Service SHALL include the TaskOutcome value in the FollowUpTaskDto returned for all task queries

### Requirement 2: Task "Unprocessed" Status — UI Presentation

**User Story:** As a salesperson, I want an "Unprocessed" button displayed alongside the existing "Complete" button on task cards, so that I can quickly close a task with the appropriate outcome without extra navigation.

#### Acceptance Criteria

1. THE Today_Actions_Panel SHALL display an "Unprocessed" action button alongside the existing "Complete" button on each active task card
2. WHEN the user clicks the "Unprocessed" button, THE Sales_Controller SHALL submit an AJAX request to mark the task as unprocessed using BlockUI during processing and refreshing the task list upon success
3. THE Sales_Controller SHALL expose an AxPostMarkTaskUnprocessed action that accepts a task ID, invokes the FollowUpTask_Service unprocessed operation, and returns a JSON result
4. WHEN a task is displayed in a completed state, THE Today_Actions_Panel SHALL show the TaskOutcome label ("Completed" or "Unprocessed") as a badge indicating how the task was closed
5. THE Pipeline task list view SHALL support filtering tasks by TaskOutcome (All, Completed, Unprocessed) in the status filter options

### Requirement 3: Optional Time on Tasks — Data Model Extension

**User Story:** As a salesperson, I want to optionally specify a time of day for a follow-up task, so that I can schedule time-sensitive tasks (e.g., "Call at 14:00") while keeping less specific tasks as all-day items.

#### Acceptance Criteria

1. THE Portal_Database SHALL add a nullable ScheduledTimeUtc column (time(0), nullable) to the existing `[sales].[FollowUpTask]` table, representing the optional time-of-day for the task
2. WHEN a create task request includes a ScheduledTimeUtc value, THE FollowUpTask_Service SHALL store the provided time value in the ScheduledTimeUtc column
3. WHEN a create task request does not include a ScheduledTimeUtc value, THE FollowUpTask_Service SHALL leave the ScheduledTimeUtc column as null, indicating an all-day task
4. WHEN an update task request is submitted with a ScheduledTimeUtc value, THE FollowUpTask_Service SHALL update the ScheduledTimeUtc column with the new value
5. WHEN an update task request is submitted with a null ScheduledTimeUtc value, THE FollowUpTask_Service SHALL clear the ScheduledTimeUtc column to null, converting the task to an all-day task
6. THE FollowUpTask_Service SHALL include the ScheduledTimeUtc value in the FollowUpTaskDto returned for all task queries

### Requirement 4: Optional Time on Tasks — UI Presentation

**User Story:** As a salesperson, I want to see the scheduled time displayed on task cards and in the Today's Actions panel when a time is set, so that I know exactly when to action time-specific tasks.

#### Acceptance Criteria

1. THE Today_Actions_Panel SHALL display the ScheduledTimeUtc formatted as "HH:mm" next to the task title when a ScheduledTimeUtc value is set on the task
2. WHEN a task has no ScheduledTimeUtc set (all-day task), THE Today_Actions_Panel SHALL display only the due date without a time indicator
3. THE task creation form SHALL include an optional time picker field labelled "Scheduled Time" that allows the user to select a time of day or leave it blank
4. THE task edit form SHALL include the same optional time picker field, pre-populated with the existing ScheduledTimeUtc value when set
5. WHEN tasks are displayed in the Today's Actions panel, THE Sales_Controller SHALL order tasks with a ScheduledTimeUtc before all-day tasks within the same urgency group, sorted by ScheduledTimeUtc ascending

### Requirement 5: Meetings Brief on Pipeline Page — Data Retrieval

**User Story:** As a salesperson, I want to see upcoming meetings on the Pipeline page, so that I can plan my day around scheduled appointments without navigating away from the pipeline view.

#### Acceptance Criteria

1. THE Meeting_Service SHALL expose a GetUpcomingMeetingsBriefAsync method that returns active, non-cancelled meetings scheduled from the current UTC date through the next 3 calendar days (today + 3 days ahead), ordered by ScheduledAtUtc ascending, for the authenticated Business
2. THE Meeting_Service SHALL return each meeting in the brief with: Id, Subject, ContactName (resolved from the associated Contact), MeetingTypeName (resolved from MeetingType), ScheduledAtUtc, DurationMinutes, and Location
3. THE Meeting_Service SHALL filter meetings by the authenticated user's BusinessId and WHERE IsCancelled equals false and IsActive equals true
4. THE Meeting_Service SHALL limit the brief result to a maximum of 10 meetings to keep the panel concise

### Requirement 6: Meetings Brief on Pipeline Page — UI Panel

**User Story:** As a salesperson, I want a collapsible "Upcoming Meetings" panel on the Pipeline page that shows subject, contact, meeting type, and scheduled time, so that I have a quick-glance view of my upcoming schedule.

#### Acceptance Criteria

1. THE Sales_Controller Pipeline action SHALL include the upcoming meetings brief data in the Pipeline view model
2. THE Pipeline page SHALL display an "Upcoming Meetings" panel below the existing Today's Actions panel, using the same glass card-pad styling and collapsible behaviour
3. THE Upcoming_Meetings_Panel SHALL display each meeting as a card or row showing: Subject, Contact name, Meeting type badge, Scheduled date and time (formatted as "dd MMM HH:mm"), and Duration
4. WHEN no upcoming meetings exist, THE Upcoming_Meetings_Panel SHALL display a message "No upcoming meetings scheduled"
5. THE Upcoming_Meetings_Panel SHALL include a collapse/expand toggle (chevron icon) that remembers its state within the current session, mirroring the Today's Actions panel behaviour
6. WHEN the user clicks a meeting entry in the Upcoming_Meetings_Panel, THE Sales_Controller SHALL navigate to the lead detail page for the associated LeadRequestId, or to the contact detail if no LeadRequestId is linked

### Requirement 7: Dashboard Today's Brief — Data Retrieval

**User Story:** As a platform user, I want the Home dashboard to aggregate my tasks and meetings due today and tomorrow into a single briefing, so that I start each day with a clear picture of what needs attention.

#### Acceptance Criteria

1. THE FollowUpTask_Service SHALL expose a GetDashboardBriefAsync method that returns active, incomplete tasks due today or tomorrow (based on DueAtUtc date), ordered by DueAtUtc ascending then ScheduledTimeUtc ascending (nulls last), for the authenticated Business
2. THE Meeting_Service SHALL expose a GetDashboardMeetingsBriefAsync method that returns active, non-cancelled meetings scheduled for today or tomorrow (based on ScheduledAtUtc date), ordered by ScheduledAtUtc ascending, for the authenticated Business
3. THE FollowUpTask_Service SHALL return each task in the dashboard brief with: Id, Title, TaskType, DueAtUtc, ScheduledTimeUtc, ContactName, and Urgency classification (today or tomorrow)
4. THE Meeting_Service SHALL return each meeting in the dashboard brief with: Id, Subject, ContactName, MeetingTypeName, ScheduledAtUtc, and DurationMinutes

### Requirement 8: Dashboard Today's Brief — UI Panel

**User Story:** As a platform user, I want a "Today's Brief" section on the Home/Index page that shows tasks due today/tomorrow, upcoming meetings, and preparation reminders, so that I have a concise, actionable planning summary each morning.

#### Acceptance Criteria

1. THE Home_Controller Index action SHALL include the dashboard tasks brief and meetings brief data in the Home page view model
2. THE Home/Index page SHALL display a "Today's Brief" section in a glass card-pad container with a clear heading
3. THE Todays_Brief section SHALL display a "Tasks" subsection listing all tasks due today and tomorrow, showing: Title, TaskType badge, due indicator ("Today" or "Tomorrow"), and ScheduledTimeUtc (formatted as "HH:mm") when set
4. THE Todays_Brief section SHALL display a "Meetings" subsection listing all meetings scheduled for today and tomorrow, showing: Subject, Contact name, Meeting type badge, and scheduled time (formatted as "HH:mm")
5. WHEN a meeting is scheduled for tomorrow, THE Todays_Brief section SHALL display a preparation reminder text: "Prepare for tomorrow's meeting with {ContactName} at {Time}" for each tomorrow meeting
6. WHEN no tasks or meetings exist for the brief period, THE Todays_Brief section SHALL display a message "All clear — no tasks or meetings for today and tomorrow"
7. THE Todays_Brief section SHALL visually separate today's items from tomorrow's items using date group headers ("Today" and "Tomorrow")
