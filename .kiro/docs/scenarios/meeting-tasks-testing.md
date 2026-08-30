# Testing Scenarios: Meeting Tasks

## Prerequisites

- Run migration `178_AddMeetingIdToFollowUpTask.sql` against the Portal database
- Have at least one contact and one lead with an existing meeting in the system
- Have at least one standalone meeting (not linked to a lead) for comparison testing
- Log in as a user with Sales module access

---

## Scenario 1: Meeting Tasks Section Visibility in Edit Modal

1. Navigate to `/Sales/Meetings`
2. Find any meeting in the table, click the pencil icon to edit
3. **Expected:** The Edit Meeting modal opens with all standard fields (Subject, Type, Date, Duration, Location, Notes, Outcome)
4. **Expected:** Below the Outcome field, a "Meeting Tasks" section appears with heading "Meeting Tasks (0)" and an "+ Add Task" button
5. **Expected:** The task list area shows "No tasks yet." placeholder text
6. **Expected:** The inline task creation form is hidden by default

---

## Scenario 2: Create a Task from Meeting Context

1. Open the Edit Meeting modal for a meeting that is linked to a lead and contact
2. Click "+ Add Task"
3. **Expected:** An inline form appears with: Title input, Type dropdown (defaulted to "Follow-up"), Due Date input, Notes textarea, Cancel and Create Task buttons
4. Leave Title empty, click "Create Task"
5. **Expected:** SweetAlert warning: "Title and due date are required." — form does not submit
6. Fill in Title: "Send demo account credentials", set Due Date to tomorrow, leave Type as "Follow-up"
7. Click "Create Task"
8. **Expected:** BlockUI shows "Creating task...", then success SweetAlert (auto-dismiss after 2 seconds), the form hides, and the task appears in the task list
9. **Expected:** The heading updates to "Meeting Tasks (1)"
10. **Expected:** The task shows: checkmark button, title, type badge, due date
11. **Expected:** The inline form fields are cleared and hidden

---

## Scenario 3: Auto-Population of Contact and Lead

1. After creating a task in Scenario 2, query the database:
   ```sql
   SELECT [Id], [MeetingId], [ContactId], [LeadRequestId], [Title]
   FROM [sales].[FollowUpTask]
   WHERE [MeetingId] IS NOT NULL
   ORDER BY [CreatedAtUtc] DESC
   ```
2. **Expected:** The task has `MeetingId` set to the meeting's ID
3. **Expected:** The task has `ContactId` matching the meeting's contact
4. **Expected:** The task has `LeadRequestId` matching the meeting's lead (if the meeting was linked to a lead)

---

## Scenario 4: Create Multiple Tasks for One Meeting

1. Open the Edit Meeting modal for the same meeting from Scenario 2
2. **Expected:** The previously created task is visible in the list
3. Click "+ Add Task", create a second task: "Schedule technical deep-dive", Type: "Follow-up", Due Date: next week
4. Click "+ Add Task", create a third task: "Email product brochure", Type: "Email", Due Date: today
5. **Expected:** All three tasks appear in the list, heading shows "Meeting Tasks (3)"
6. **Expected:** Tasks are ordered by due date ascending (today's task first, then tomorrow's, then next week's)

---

## Scenario 5: Complete a Task from Meeting Modal

1. In the Edit Meeting modal with 3 tasks from Scenario 4, click the green checkmark button on the first pending task
2. **Expected:** BlockUI shows briefly, the task moves to the bottom of the list with strikethrough title and muted opacity
3. **Expected:** The completed task shows "Completed: {date}" instead of "Due: {date}"
4. **Expected:** No SweetAlert appears (quick operation — the visual update is sufficient feedback)
5. **Expected:** Heading still shows "Meeting Tasks (3)" (total count, not just pending)

---

## Scenario 6: Overdue Task Visual in Meeting Modal

1. Create a task with a due date in the past (e.g., yesterday) via the inline form
2. Close and reopen the Edit Meeting modal
3. **Expected:** The overdue task shows a red dot indicator next to the due date
4. **Expected:** The label reads "Overdue: {date}" in red instead of "Due: {date}" in grey
5. **Expected:** Non-overdue pending tasks show normal grey "Due: {date}" without a red dot

---

## Scenario 7: Task Count Badge on Meeting Rows

1. Close the Edit Meeting modal (click Cancel or the X)
2. **Expected:** The meetings table reloads automatically (because tasks were modified)
3. Find the meeting you've been working with in the table
4. **Expected:** A blue badge appears next to the meeting subject showing "[N tasks]" (e.g., "[3 tasks]") — blue because there are pending tasks
5. Find a meeting with no tasks
6. **Expected:** No badge appears next to its subject

---

## Scenario 8: Task Count Badge — All Completed

1. Open the meeting with tasks, complete all remaining pending tasks via the checkmark buttons
2. Close the modal
3. **Expected:** The table reloads and the badge changes to a green/muted style showing "[3 ✓]" (all tasks completed)

---

## Scenario 9: Task Count Badge — No Reload When No Changes

1. Open a meeting's Edit Meeting modal
2. Don't create or complete any tasks
3. Close the modal
4. **Expected:** The meetings table does NOT reload (no unnecessary network request — the dirty flag was not set)

---

## Scenario 10: Meeting Reference on Tasks List Page

1. Navigate to `/Sales/Tasks`
2. Find the tasks that were created from the meeting context
3. **Expected:** Below each task's title, a subtle grey label reads "from: {Meeting Subject}" (e.g., "from: Guardian Platform Demo")
4. **Expected:** Tasks NOT linked to a meeting show no "from:" label
5. **Expected:** The label is styled in small font (11px), muted colour (#8a9bab)

---

## Scenario 11: Meeting Reference on Today's Actions Panel

1. Navigate to `/Sales/Pipeline`
2. If any meeting-linked tasks are due today or overdue, find them in the Today's Actions panel
3. **Expected:** Below the urgency/assigned-to line, a "from: {Meeting Subject}" label appears
4. **Expected:** Non-meeting tasks show no such label

---

## Scenario 12: Activity Feed Integration

1. Create a new task from a meeting that is linked to a lead (via the Edit Meeting modal)
2. Navigate to the lead's detail page (`/Sales/LeadDetail/{id}`)
3. Check the activity feed section
4. **Expected:** An entry reads "Follow-up task created from meeting: {Meeting Subject}"
5. Create a standalone task (from the Tasks page, not from a meeting)
6. **Expected:** The activity entry reads "Follow-up task created: {Task Title}" (no meeting reference)

---

## Scenario 13: Backward Compatibility — Existing Tasks Unaffected

1. Navigate to `/Sales/Tasks` and find tasks that existed before the migration
2. **Expected:** All existing tasks display normally — no errors, no missing data
3. **Expected:** Existing tasks show no "from:" label (MeetingId is null)
4. Open a task's edit modal, make a change, save
5. **Expected:** Update works without issues — the null MeetingId is preserved
6. Complete or snooze an existing task
7. **Expected:** All operations work identically to pre-migration behaviour

---

## Scenario 14: Standalone Meeting (No Lead) — Task Creation

1. Create a meeting that is NOT linked to any lead (standalone meeting with just a contact)
2. Open the Edit Meeting modal, create a task from it
3. **Expected:** Task is created with `MeetingId` set and `ContactId` set
4. **Expected:** `LeadRequestId` is null (inherited from the meeting, which has no lead)
5. **Expected:** No activity feed entry is recorded (activity feed only fires when `LeadRequestId` is present)

---

## Scenario 15: Task List Scrolling in Modal

1. Create 8+ tasks for a single meeting (use the inline form repeatedly)
2. **Expected:** The task list container scrolls vertically when tasks exceed the visible area
3. **Expected:** The task list does not push the modal buttons off-screen
4. **Expected:** The "+ Add Task" button and the inline form remain above the scrollable list

---

## Scenario 16: Lead Detail Page — Meeting Tasks Visible

1. Navigate to the Lead Detail page for a lead that has a meeting with tasks
2. Check the "Follow-Up Tasks" section
3. **Expected:** Meeting-linked tasks appear in the task list alongside standalone tasks
4. **Expected:** Meeting-linked tasks show the "from: {Meeting Subject}" reference label
5. **Expected:** Complete/snooze/edit operations work normally on meeting-linked tasks

---

## Database Verification Queries

After running through the scenarios above, use these queries to verify data integrity:

```sql
-- Verify MeetingId is set on meeting-created tasks
SELECT FollowUpTask.[Id], FollowUpTask.[Title], FollowUpTask.[MeetingId],
       FollowUpTask.[ContactId], FollowUpTask.[LeadRequestId],
       Meeting.[Subject] AS MeetingSubject
FROM [sales].[FollowUpTask]
INNER JOIN [sales].[Meeting] ON FollowUpTask.[MeetingId] = Meeting.[Id]
ORDER BY FollowUpTask.[CreatedAtUtc] DESC

-- Verify existing tasks still have NULL MeetingId
SELECT COUNT(*) AS TasksWithoutMeeting
FROM [sales].[FollowUpTask]
WHERE [MeetingId] IS NULL

-- Verify task counts match badge expectations
SELECT Meeting.[Id], Meeting.[Subject],
       COUNT(*) AS TotalTasks,
       SUM(CASE WHEN FollowUpTask.[IsCompleted] = 0 THEN 1 ELSE 0 END) AS PendingTasks
FROM [sales].[Meeting]
INNER JOIN [sales].[FollowUpTask] ON Meeting.[Id] = FollowUpTask.[MeetingId]
GROUP BY Meeting.[Id], Meeting.[Subject]
ORDER BY Meeting.[Id]
```
