# Requirements: Task & Meeting Reminder Assistant (Group 4)

## Introduction

The **Task & Meeting Reminder** is a scheduled (Category B) Digital Assistant that emails each
**assigned team member** one morning agenda of their **upcoming tasks and meetings** (plus anything
overdue). Its purpose: people who don't regularly open the portal — including team members who
aren't portal users at all — still get told what's on their plate, and whoever created a task/meeting
for someone else can trust that person will be notified.

This assistant is only as good as the **assignment data** behind it, so the spec has **two parts**:

- **Part 1 — Sales assignment model.** Wire up *who is responsible* for tasks and meetings:
  - Tasks already have `FollowUpTask.TeamMemberId` (1-to-1) but it is never set — add assignee
    selection on task create/edit, auto-assigning the sole active team member when there's only one.
  - Meetings have **no** assignee — add a **many-to-many** mapping (`[sales].[MeetingTeamMember]`)
    plus attendee selection on meeting create/edit (a meeting is 1-to-many).
  - Team-member email becomes the reminder address; the new-member form explains why.

- **Part 2 — The reminder assistant.** A daily, per-team-member **fan-out** email built on the
  existing scheduled engine, notification spine, plan gating, and timezone resolution.

**Product decisions locked (from scoping):**
- One email each **morning** (daily agenda digest), NOT per-item countdown reminders.
- **Separate** from the Daily Brief; each is independently enable/disable. The Daily Brief keeps its
  own "tasks due" line — this assistant is the richer, assignee-routed agenda.
- **Recipient = the assigned team member** (fan-out: N emails per business, one per member who has
  something upcoming). Fallback order: `TeamMember.Email` → the linked portal user's email
  (`TeamMember.UserId`) → the **business owner** (for unassigned/legacy items).
- **Horizon:** include **overdue** items + a **configurable per-business look-ahead** window
  (default 2 days). Members with nothing in range get **no** email.
- **Backfill:** none. Existing tasks (null `TeamMemberId`) and all existing meetings (no attendees)
  are treated as **unassigned → routed to the owner** in the reminder. Assignee selection is
  required going forward.

**Plan gating:** same `digital_assistants` module (Professional+). No new plan key.

---

## Glossary

- **Team member** — `[sales].[TeamMember]` (business-scoped; `FirstName`, `LastName?`, `Email?`,
  `UserId?` (optional portal-user link), `IsActive`). The reminder recipient.
- **Task** — `[sales].[FollowUpTask]` with `TeamMemberId?` (single assignee), `DueAtUtc` (date) +
  `ScheduledTimeUtc?` (time-of-day), `IsCompleted`.
- **Meeting** — `[sales].[Meeting]` with `ScheduledAtUtc` (datetime), `IsCancelled`, `IsActive`,
  `Outcome`. No assignee today.
- **Assignee/attendee** — the team member(s) responsible: one per task, many per meeting.
- **Look-ahead window** — `[today .. today + lookAheadDays]` for upcoming items.
- **Overdue** — an incomplete task with `DueAtUtc < today`, or a non-cancelled past meeting with no
  `Outcome`.

---

## Part 1 — Sales Assignment Model

### Requirement 1 — Task assignee selection

**User Story:** As a user creating a follow-up task, I want to choose which team member it's
assigned to, so the right person is reminded.

#### Acceptance Criteria
1. The task create and edit forms SHALL let the user select an **active** team member as the
   assignee (`FollowUpTask.TeamMemberId`).
2. WHEN the business has exactly **one** active team member THEN the system SHALL auto-assign that
   member and MAY hide/disable the selector.
3. WHEN the business has multiple active team members THEN assignee selection SHALL be presented;
   if left unset, the task is **unassigned** (allowed — routed to the owner in the reminder).
4. The task create/edit endpoints SHALL persist `TeamMemberId` (nullable) and validate that any
   chosen member is active and belongs to the business.
5. Existing tasks with null `TeamMemberId` SHALL remain valid (no backfill) and be treated as
   unassigned.

### Requirement 2 — Meeting attendee mapping (many-to-many)

**User Story:** As a user scheduling a meeting, I want to assign one or more team members to it, so
each of them is reminded.

#### Acceptance Criteria
1. A new mapping table `[sales].[MeetingTeamMember]` SHALL relate meetings to team members
   (many-to-many; columns at least `MeetingId`, `TeamMemberId`, plus audit `CreatedAtUtc`).
2. The meeting create and edit forms SHALL let the user select **one or more** active team members
   as attendees.
3. WHEN the business has exactly **one** active team member THEN that member SHALL be auto-added as
   an attendee.
4. The meeting create/edit endpoints SHALL persist the attendee set (replace-on-edit), validating
   each member is active and belongs to the business.
5. Existing meetings SHALL remain valid with **no** attendees (no backfill) and be treated as
   unassigned.
6. Deleting/cancelling a meeting or deactivating a member SHALL not orphan the mapping in a way that
   breaks the reminder scan (the scan filters to active members and non-cancelled meetings).

### Requirement 3 — Team-member email as the reminder address

**User Story:** As a business owner, I want to understand that a team member's email is where their
reminders go, so I provide it.

#### Acceptance Criteria
1. The team-member add/edit form SHALL show an explanatory note near the Email field, stating the
   address is where that member's task/meeting reminders are sent.
2. `TeamMember.Email` SHALL remain nullable at the database level (no breaking change to existing
   rows), but the UI SHALL strongly encourage it (e.g. mark it as recommended/required-for-reminders).
3. WHEN a team member has no resolvable email (neither `Email` nor a linked portal user's email)
   THEN their tasks/meetings SHALL fall back to the **owner** in the reminder (never silently
   dropped), and this SHALL be logged at information level.

---

## Part 2 — Reminder Assistant

### Requirement 4 — Daily per-member agenda (fan-out)

**User Story:** As a team member, I want one morning email listing my upcoming tasks and meetings,
so I know my day without opening the portal.

#### Acceptance Criteria
1. On a **daily** cadence, per plan+module-entitled business, the system SHALL build one agenda per
   **recipient** and send a separate email to each (fan-out).
2. A recipient's agenda SHALL contain: their **overdue** items, their items **due/scheduled today**,
   and their items within the **look-ahead window**, for **both** tasks and meetings.
3. Task assignment routes by `FollowUpTask.TeamMemberId`; meeting assignment routes by
   `[sales].[MeetingTeamMember]`. **Unassigned** tasks/meetings SHALL be routed to the **owner**.
4. WHEN a recipient has **no** items in range THEN the system SHALL send them **no** email
   (per-recipient quiet skip).
5. Completed tasks and cancelled meetings SHALL be excluded.
6. The email SHALL group/sort sensibly (e.g. overdue first, then today, then upcoming; meetings show
   time-of-day, tasks show time when `ScheduledTimeUtc` set else "all day").
7. The email SHALL be internal/owner-facing style: **no** customer branding footer.

### Requirement 5 — Recipient resolution & fan-out identity

#### Acceptance Criteria
1. For an assigned item, the recipient email SHALL resolve as: `TeamMember.Email` → linked portal
   user email via `TeamMember.UserId` → (only for unassigned items) the business owner.
2. The system SHALL only fan out to **active** team members.
3. Multiple assigned members of the same meeting SHALL each receive it in their own agenda.
4. The owner's agenda (if the owner is also a team member, or as the unassigned-catch-all) SHALL not
   duplicate an item the owner would otherwise receive as an assignee (de-dupe per recipient).

### Requirement 6 — Once-per-recipient-per-day idempotency

#### Acceptance Criteria
1. The system SHALL use a **recipient-scoped daily cycle key** so each recipient gets at most one
   agenda per business-day. The recipient token SHALL derive from the **normalised resolved email**
   (trim + lowercase, hashed short) — NOT the team-member id — so a person reachable as both an
   assignee and the owner catch-all resolves to a single identity/key and is never double-emailed
   (e.g. `task_meeting_reminder:{yyyy-MM-dd}:email-{hash}`).
2. WHEN a non-Failed outbox row already exists for a recipient's daily key THEN the system SHALL NOT
   send a second agenda to them that day.
3. A previously **Failed** agenda SHALL be eligible to re-produce (consistent with
   `ExistsForCycleAsync` excluding Failed).

### Requirement 7 — Per-business configuration (look-ahead, time, enable)

#### Acceptance Criteria
1. The assistant SHALL support a per-business **look-ahead days** setting (default 2; validated
   1..30).
2. The assistant SHALL send at the business-local **send time** (reuse `SendTimeLocal`; default
   08:00); day-of-week SHALL be irrelevant (`SendDayOfWeek` nulled, like the Daily Brief).
3. WHEN disabled for a business THEN no agendas SHALL be produced for it.
4. The `/Assistants` card SHALL expose: on/off, send-time, look-ahead-days, and an activity-log
   expander. Recipient is intrinsically the assignees (no single-recipient override), though the
   card MAY note the fan-out behaviour.
5. Plan-gated identically to the other assistants.

### Requirement 8 — Seeding & config

#### Acceptance Criteria
1. A migration SHALL seed AssistantType **Id 7**, key `task_meeting_reminder`, name "Task & Meeting
   Reminder", `RecipientKind = 'PortalUser'` (see design note — the recipient is really a team
   member, but the existing enum has no 'TeamMember' kind; design resolves this), `IsCustomerFacing
   = 0`, via idempotent explicit-Id MERGE + verification PRINT (matching 198/201).
2. The `MeetingTeamMember` table and any new settings column (`TaskMeetingLookAheadDays`) SHALL be
   added via guarded, additive migrations.
3. No new plan module key (rides `digital_assistants`).

### Requirement 9 — Reliability & consistency

#### Acceptance Criteria
1. New data access SHALL follow repository conventions (async, typed, `try/catch (Exception ex)`
   rethrow) and be tenant-less/`businessId`-explicit (the scan runs without tenant context).
2. The reminder SHALL reuse the existing dispatcher for send/retry/failure — no new send path.
3. Per-recipient composition failure SHALL be isolated (one member's failure must not block others).
4. Both backend projects SHALL build with 0 errors; the change SHALL not regress the other
   assistants, the Daily Brief's task line, or the existing Tasks/Meetings screens.
5. SHALL not break existing task/meeting create/edit flows (assignee/attendee are additive).
