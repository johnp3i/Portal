# Implementation Plan: Follow-Up Tasks

## Overview

Adds a lightweight follow-up task system to the Sales Pipeline. Tasks are quick reminders (Call, Email, Follow-up) tied to leads/contacts with due dates, quick presets, complete/snooze actions, and a "Today's Actions" panel on the Pipeline page.

## Tasks

- [ ] 1. Database migration
  - [ ] 1.1 Create migration `128_CreateFollowUpTaskTable.sql`
    - Create `[sales].[FollowUpTask]` with all columns, FKs, indexes
  - [ ] 1.2 Seed `[sales].[FollowUpTaskType]` lookup if needed (or use NVARCHAR for simplicity)

- [ ] 2. Entity and DbContext
  - [ ] 2.1 Create `FollowUpTask` entity
  - [ ] 2.2 Register in PortalDbContext with table/schema configuration
  - [ ] 2.3 Add global query filter on BusinessId

- [ ] 3. Repository
  - [ ] 3.1 Create `FollowUpTaskRepository`
    - InsertAsync, UpdateAsync, CompleteAsync, SnoozeAsync
    - GetByLeadIdAsync, GetTodaysActionsAsync(businessId, teamMemberId?)
    - GetPagedAsync(filters), GetOverdueCountAsync

- [ ] 4. Service layer
  - [ ] 4.1 Create `IFollowUpTaskService` / `FollowUpTaskService`
    - CreateAsync(request), CompleteAsync(id), SnoozeAsync(id, newDate)
    - GetTodaysActionsAsync(businessId, userId) — overdue + today + tomorrow
    - GetByLeadIdAsync(leadId), GetPagedAsync(filters)
    - GetOverdueCountAsync(businessId, userId) — for dashboard briefing

- [ ] 5. Controller endpoints
  - [ ] 5.1 Add AJAX endpoints to SalesController (or new FollowUpTaskController)
    - AxPostCreateTask, AxPostCompleteTask, AxPostSnoozeTask
    - AxGetTodaysActions, AxGetTasksByLead, AxGetTasksPaged

- [ ] 6. Today's Actions panel (Pipeline page)
  - [ ] 6.1 Create `_TodaysActionsPanel.cshtml` partial or inline section on Pipeline
  - [ ] 6.2 Load tasks via AJAX on page load
  - [ ] 6.3 Render: overdue (red), today (amber), tomorrow (grey)
  - [ ] 6.4 Complete button → AJAX → remove from list
  - [ ] 6.5 Snooze dropdown → AJAX → update due date
  - [ ] 6.6 Collapsible panel with badge count

- [ ] 7. Quick creation from Lead Detail
  - [ ] 7.1 Add "Schedule Follow-up" button to Lead Detail page
  - [ ] 7.2 Create compact modal: title (pre-filled), type dropdown, quick preset buttons, assign dropdown
  - [ ] 7.3 Wire AJAX creation → success → refresh task list on lead

- [ ] 8. Lead Detail tasks section
  - [ ] 8.1 Add "Follow-Up Tasks" section to Lead Detail page
  - [ ] 8.2 Show pending tasks (with complete/snooze actions) above completed (muted)

- [ ] 9. Tasks list page
  - [ ] 9.1 Create `Sales/Tasks` view (or sub-nav item)
  - [ ] 9.2 Table with filters: status, type, team member, date range
  - [ ] 9.3 Add "Tasks" to Opportunities navigation section

- [ ] 10. Dashboard briefing integration
  - [ ] 10.1 Update DashboardBriefingService to include overdue follow-up count
  - [ ] 10.2 Add signal: "You have X overdue follow-ups" to the briefing card

- [ ] 11. DI registration
  - [ ] 11.1 Register repository and service in Program.cs

- [ ] 12. Verification
  - [ ] 12.1 Create task from lead → verify appears in Today's Actions
  - [ ] 12.2 Complete task → verify removed from panel
  - [ ] 12.3 Snooze task → verify due date updated
  - [ ] 12.4 Verify overdue tasks show red indicator
  - [ ] 12.5 Verify tenant isolation

## Notes

- Task types are stored as NVARCHAR (not a lookup table) to keep it simple — only 5 fixed values
- The "Today's Actions" panel queries: WHERE DueAtUtc <= tomorrow AND IsCompleted = 0
- Snooze increments SnoozedCount — visual warning after 3 snoozes ("this keeps slipping")
- No email notifications in v1 — just the pipeline panel. Email reminders can be added later.
- Foundation feature — no plan gating (available to all users with sales module access)
- Team member assignment uses existing TeamMember table from the sales module

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["3.1"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["5.1", "11.1"] },
    { "id": 5, "tasks": ["6.1", "6.2", "6.3", "6.4", "6.5", "6.6"] },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3", "8.1", "8.2"] },
    { "id": 7, "tasks": ["9.1", "9.2", "9.3"] },
    { "id": 8, "tasks": ["10.1", "10.2"] },
    { "id": 9, "tasks": ["12.1", "12.2", "12.3", "12.4", "12.5"] }
  ]
}
```
