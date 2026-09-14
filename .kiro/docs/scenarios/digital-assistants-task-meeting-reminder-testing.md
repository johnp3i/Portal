# Testing Scenarios: Digital Assistants — Task & Meeting Reminder (Group 4)

The **Task & Meeting Reminder** is a scheduled (Category B) **fan-out** assistant: each morning it
emails **each assigned team member** their own agenda of upcoming and overdue **tasks and meetings**.
People who don't open the portal still learn what's assigned to them.

This assistant has two parts:
- **Part 1 — Sales assignment model:** task assignee (`FollowUpTask.TeamMemberId`, 1-to-1) and
  meeting attendees (`[sales].[MeetingTeamMember]`, 1-to-many), plus a team-member email note.
- **Part 2 — the reminder:** a daily per-recipient fan-out on the scheduled engine.

## Key mechanics

- **Fan-out:** N emails per business — one per team member who has items, plus an **owner catch-all**
  for unassigned items and members with no resolvable email.
- **Recipient resolution:** `TeamMember.Email` → linked portal-user email (via `TeamMember.UserId`)
  → the business **owner**.
- **Identity:** buckets AND cycle keys are keyed on the **normalised resolved email** (trim +
  lowercase, hashed short), so an owner who is also a team member gets **one** merged agenda, never
  two. Items are de-duped per recipient by `(type, id)`.
- **Window:** overdue (bounded by `TaskMeetingOverdueLookBackDays`, default 14) + upcoming within a
  per-business **look-ahead** (default `TaskMeetingDefaultLookAheadDays` = 2). Completed tasks and
  cancelled meetings are excluded; overdue meetings are only those with no `Outcome`.
- **Once per recipient per day:** recipient-scoped cycle key
  `task_meeting_reminder:{yyyy-MM-dd}:email-{hash}`, de-duped via the UNIQUE cycle index +
  `DigestEnqueuer`. Failed rows may re-enqueue.

## Prerequisites

- Run migrations through **207**: `205` creates `[sales].[MeetingTeamMember]`; `206` seeds
  AssistantType id 7 `task_meeting_reminder`; `207` adds
  `BusinessAssistantSetting.TaskMeetingLookAheadDays`. (Migration `204` — the UNIQUE cycle index —
  must already be applied from the VAT reminder.)
- `Notifications` SMTP password set in User Secrets.
- Business on **Professional or Enterprise** (`digital_assistants`), `TimeZoneId` set, scheduler on.
- At least two **active team members with emails** (Sales → Team).

---

## Part 1 — assignment model

### Scenario 1: Assign a task
1. Sales → Tasks → New task. **Expected:** an "Assign to" dropdown lists active team members.
2. With multiple members, pick one, save → the task persists that assignee. Edit the task → the
   assignee is preselected and can be changed or cleared (Unassigned).
3. With exactly **one** active member, the assignee auto-selects that member on create.

### Scenario 2: Assign meeting attendees
1. Sales → Meetings → Schedule Meeting. **Expected:** an "Attendees" multi-select of active members.
2. Select two members, save → both persist in `[sales].[MeetingTeamMember]`. Edit → the current
   attendees are preselected; changing the selection replaces them.
3. Sole active member → auto-added as attendee on create.

### Scenario 3: Team-member email note
1. Sales → Team → Add/Edit member. **Expected:** a note under the Email field explaining the address
   receives that member's task & meeting reminders.

### Scenario 4: Existing data unaffected
1. Tasks created before this change (null assignee) and meetings with no attendees still open, edit,
   and save without error. In the reminder, their items route to the owner.

---

## Part 2 — the reminder

### Scenario 5: One agenda per member (fan-out, happy path)
1. Enable the Task & Meeting Reminder; set send time a minute ahead; look-ahead = 2.
2. Give **Alice** a task due today; give **Bob** a meeting scheduled tomorrow.
3. On the next poll after send time, **Expected:** two separate outbox rows (`AssistantTypeId = 7`),
   one to Alice's email, one to Bob's — each listing only their own item, in the right section
   (Alice "Today", Bob "Coming up"). No customer footer.

### Scenario 6: Unassigned → owner
1. Create a task with **no** assignee and a meeting with **no** attendees, both due in-window.
2. **Expected:** both appear in the **owner's** agenda (the catch-all), not dropped.

### Scenario 7: No-email member → owner
1. Give an active member with a **blank email** (and no linked portal user) an assigned task in-window.
2. **Expected:** the item lands in the owner's agenda + an information-level log line; the member
   gets no email.

### Scenario 8: Owner-who-is-a-member — no duplicate
1. Make the owner also a team member (same email), and assign them a task.
2. **Expected:** the owner receives **one** agenda containing the item — not one "as member" and one
   "as owner catch-all". (Bucket + cycle key are keyed on the normalised email.)

### Scenario 9: Overdue included but bounded
1. Give a member an incomplete task due 5 days ago and another due 60 days ago (with default 14-day
   look-back).
2. **Expected:** only the 5-day-old task appears in "Overdue"; the 60-day-old one is excluded.
3. A past meeting with **no outcome** within the look-back shows as overdue; one **with** an outcome
   does not.

### Scenario 10: Quiet recipient → no email
1. A member with nothing overdue and nothing within the look-ahead.
2. **Expected:** that member gets no email (they were never bucketed). Other members still do.

### Scenario 11: Once per recipient per day
1. After agendas send (Scenario 5), let the scheduler poll again the same business-day.
2. **Expected:** no duplicate agendas — each recipient's daily cycle key already exists. A member
   whose send **Failed** may re-enqueue on a later poll.

### Scenario 12: Per-business look-ahead
1. Set look-ahead = 1 (save; validated 1–30). A meeting 2 days out → excluded today, included once
   it is within 1 day. Clear the field → falls back to the global default (2).

### Scenario 13: Disabled / non-entitled plan
1. Toggle the assistant off → no agendas. Non-Professional plan → nothing evaluated or sent.

### Scenario 14: Regression
1. The other assistants (weekly digests, Daily Brief, VAT reminder, Thank-You, New Payment) are
   unchanged. The Daily Brief keeps its own "tasks due" line (this assistant is separate and
   assignee-routed). Tasks/Meetings create+edit still work; a meeting with no attendees does not
   error the scan.

---

## Notes

- **This is the first fan-out assistant.** The runner branches on `IFanOutDigestComposer`
  (`ComposeManyAsync` → N messages) vs. the single-message `IDigestComposer`. A key maps to exactly
  one interface.
- Deadline/window arithmetic uses the business-local date passed by the runner; send timing is
  timezone-aware via the runner's `IsDue`.
- Card is owner-configured but has **no single recipient override** — it fans out to assignees; the
  card shows a note instead of a "Send to" field.
- `catch (Exception ex)` throughout; the composer is fail-safe per business (a failure returns an
  empty list and the runner guards per business/assistant).
