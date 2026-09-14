# Implementation Plan: Task & Meeting Reminder Assistant (Group 4)

## Overview

Two parts, sequenced: **Part 1** wires up *who owns* tasks & meetings (assignee + attendee mapping +
team-member email), then **Part 2** builds the daily per-team-member **fan-out** agenda on the
existing scheduled engine. Part 2 depends on Part 1's assignment data.

Reuse-first: outbox spine, dispatcher, `ScheduledDigestRunner`, `DigestComposerBase`,
`IOwnerEmailResolver`, plan gating, timezone resolution, the existing Sales task/meeting services +
create/edit endpoints.

New pieces: `[sales].[MeetingTeamMember]` table + attendee wiring; task assignee UI/validation/
auto-assign (the request+persist already exist); `IFanOutDigestComposer` + runner ctor change;
`TaskMeetingReminderComposer`; `IPortalUserEmailResolver`; `DigestCycleKey.RecipientDaily`;
`BusinessAssistantSetting.TaskMeetingLookAheadDays`; AssistantType seed (Id 7); email builder; UI card;
**shared** UNIQUE cycle-index hardening.

Design in `design.md`; requirements in `requirements.md`. Migrations `USE [Portal]`;
guarded/idempotent DDL; `CreatedAtUtc NOT NULL DEFAULT GETUTCDATE()` on the new table (audit steering).

> **Decisions locked:** one morning agenda per assignee (daily digest, not per-item countdown);
> separate from the always-on Daily Brief; recipient = assigned team member (fan-out N/business),
> fallback `TeamMember.Email` → linked portal-user email → owner; include overdue (bounded look-back,
> default 14d) + per-business look-ahead (default 2); NO backfill (unassigned/legacy → owner);
> team-member email is the reminder address (form note; column stays nullable). No new plan key.
>
> **Review fixes baked in:** T5 — task assignee is **already persisted** (`CreateFollowUpTaskRequest.
> TeamMemberId` + `CreateTaskAsync`); only UI/validation/auto-assign remain; and `CreateMeetingAsync`/
> `UpdateMeetingAsync` are **not transactional**, so attendee writes need an explicit transaction.
> T2/T3 — bucket + cycle key are keyed on the **normalised resolved email** (owner pre-mapped to its
> `TeamMember` by `UserId`), not `tm-{id}`/`owner`, so no double-email. T4 — overdue is bounded. T1 —
> `DigestEnqueuer` check-then-insert + non-unique index; shared UNIQUE-index migration + violation
> catch. T6 — runner **constructor** gains `IEnumerable<IFanOutDigestComposer>`. T7 — gating key is
> `digital_assistants` (verified).

## Status

**Implementation complete (tasks 1-10); builds clean (Infrastructure + Web, 0 errors).** Task 11's
build half is done; its manual verification against a running instance remains for the user (use
`.kiro/docs/scenarios/digital-assistants-task-meeting-reminder-testing.md` as the checklist).

**Migration numbers as built:** 205 = MeetingTeamMember table; 206 = seed AssistantType Id 7;
207 = TaskMeetingLookAheadDays column. The shared UNIQUE cycle index (task 5.4) was already created
by the VAT spec as **migration 204** + the `DigestEnqueuer` unique-violation catch — so it was a
no-op here (idempotent), as designed.

## Tasks

### Part 1 — Sales assignment model

- [x] 1. Task assignee wiring (Portal.Infrastructure / Portal.Web)
  - [x] 1.1 Create already persisted `TeamMemberId`; added it to `UpdateFollowUpTaskRequest` + threaded through `UpdateTaskAsync` (interface+impl) + `FollowUpTaskRepository.UpdateAsync` + `SalesController.AxPostUpdateTask`. Added active+in-business validation via `ResolveAssigneeAsync` (create) and inline check (edit)
    - _Requirements: 1.1, 1.4, 1.5_
  - [x] 1.2 Server-side sole-member auto-assign on create (in `ResolveAssigneeAsync`)
    - _Requirements: 1.2_
  - [x] 1.3 Task create/edit UI: `#taskTeamMemberId` / `#editTaskTeamMemberId` selects in `follow-up-tasks.js` (populate from `AxGetLookups.teamMembers`; auto-select sole on create; preselect on edit); payloads carry `teamMemberId`
    - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Meeting attendees — mapping table + wiring (Portal.Database / Infrastructure / Web)
  - [x] 2.1 Migration `205_CreateMeetingTeamMemberTable.sql` — `[sales].[MeetingTeamMember]` (Id PK, MeetingId FK, TeamMemberId FK, `CreatedAtUtc DEFAULT GETUTCDATE()`), unique index `(MeetingId, TeamMemberId)`; guarded; `USE [Portal]`
    - _Requirements: 2.1, 8.2_
  - [x] 2.2 `MeetingTeamMember` entity + DbSet + `Meeting.TeamMembers` nav + `ConfigureMeetingTeamMember`; `MeetingTeamMemberRepository` (`ReplaceAttendeesAsync`, `GetAttendeeIdsByMeetingIdAsync`, `GetAttendeeIdsByMeetingIdsAsync`)
    - _Requirements: 2.1_
  - [x] 2.3 `MeetingService` create/edit accept attendee ids; replace-on-edit; validate via `ResolveAttendeesAsync`. Wrapped meeting write + `ReplaceAttendeesAsync` in `BeginTransactionAsync` (create+update were NOT transactional)
    - _Requirements: 2.2, 2.4, 2.6_
  - [x] 2.4 Server-side sole-member auto-add on create (in `ResolveAttendeesAsync`)
    - _Requirements: 2.3_
  - [x] 2.5 Meeting create/edit UI: `#meetingAttendees` / `#editMeetingAttendees` multi-selects + `fetchTeamMembersOnce`/`populateAttendeeSelect`/`getSelectedAttendeeIds`; payloads carry `attendeeTeamMemberIds`; edit preselects
    - _Requirements: 2.2, 2.3_

- [x] 3. Team-member email note (Portal.Web)
  - [x] 3.1 Note added under the Email field in `Team.cshtml`; column stays nullable
    - _Requirements: 3.1, 3.2_

- [x] 4. Checkpoint — Part 1 build + verify
  - Infra 0 errors; Web 0 error CS (only the VS DLL-copy lock, env not code).
  - _Requirements: 9.4, 9.5_

### Part 2 — Reminder assistant

- [x] 5. Database — seed + settings + options + shared hardening
  - [x] 5.1 Migration `206_SeedTaskMeetingReminderAssistantType.sql` — AssistantType **Id 7** `task_meeting_reminder`, idempotent MERGE + verify PRINT
    - _Requirements: 8.1, 8.3_
  - [x] 5.2 Migration `207_AddTaskMeetingLookAheadDaysToAssistantSetting.sql` — additive nullable column
    - _Requirements: 7.1, 8.2_
  - [x] 5.3 `NotificationOptions.TaskMeetingDefaultLookAheadDays = 2` + `TaskMeetingOverdueLookBackDays = 14`; `DigestAssistantKeys.TaskMeetingReminder`
    - _Requirements: 7.1, 8.1_
  - [x] 5.4 Shared UNIQUE cycle-index hardening + `DigestEnqueuer` unique-violation catch — **already done by the VAT spec as migration 204**; this spec relies on it (no-op)
    - _Requirements: 6.1, 6.2, 6.3, 9.2_

- [x] 6. Settings entity + cycle key (Portal.Infrastructure)
  - [x] 6.1 `int? TaskMeetingLookAheadDays` on `BusinessAssistantSetting` + threaded through the repository (SELECTs + MERGE)
    - _Requirements: 7.1_
  - [x] 6.2 `DigestCycleKey.RecipientDaily(...)` → `"{key}:{yyyy-MM-dd}:{token}"`; token = `email-{sha1(normEmail)[..12]}` (built in the composer)
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 7. Fan-out composer contract (Portal.Infrastructure)
  - [x] 7.1 `IFanOutDigestComposer` (`ComposeManyAsync` → `IReadOnlyList<OutboxMessage>`)
    - _Requirements: 4.1_
  - [x] 7.2 `ScheduledDigestRunner` ctor gains `IEnumerable<IFanOutDigestComposer>` + `_fanOutComposersByKey`; `ProcessBusinessAssistantAsync` branches to the fan-out path. Single-message path unchanged
    - _Requirements: 4.1, 6.2_

- [x] 8. TaskMeetingReminderComposer + recipient resolution (Portal.Infrastructure)
  - [x] 8.1 `IPortalUserEmailResolver.ResolveByUserIdAsync` + registered in DI; member resolution `Email` → portal-user email → null; normalise = trim+lowercase
    - _Requirements: 5.1, 5.2_
  - [x] 8.2 `TaskMeetingReminderComposer : IFanOutDigestComposer` — bounded overdue + look-ahead; bucket by normalised resolved email (unassigned/no-email → owner); de-dupe by `(type,id)`; per-recipient cycle key + `ExistsForCycleAsync` short-circuit; fail-safe per business
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 9.1, 9.3_
  - [x] 8.3 `DigestEmailBuilder.TaskMeetingReminderSubject` + `BuildTaskMeetingReminderHtml` — internal style, no footer; Overdue/Today/Coming-up sections
    - _Requirements: 4.6, 4.7_
  - [x] 8.4 Registered composer as `IFanOutDigestComposer` in Program.cs; added key to runner `digestKeys` + `DailyCadenceKeys`
    - _Requirements: 1.1(engine), 4.1_

- [x] 9. Checkpoint — backend build
  - Infrastructure 0 errors.
  - _Requirements: 9.2, 9.4_

- [x] 10. UI — Task & Meeting Reminder card (Portal.Web)
  - [x] 10.1 `AssistantsController.Index`: `IsTaskMeetingReminder` + `TaskMeetingLookAheadDays` (default = global); `isScheduled = true`
    - _Requirements: 7.4_
  - [x] 10.2 `Assistants/Index.cshtml`: card branch — send-time + look-ahead-days (1..30) + fan-out note (no recipient override); no day-of-week/figures; payload `taskMeetingLookAheadDays`
    - _Requirements: 7.2, 7.4_
  - [x] 10.3 `AxPostSaveAssistantSettings`: task-meeting branch — null `SendDayOfWeek`; validate look-ahead (1..30); preserve-forward (incl. new column in toggle path)
    - _Requirements: 7.1, 7.2, 7.3_

- [ ] 11. Final checkpoint — build + manual verification
  - Build 0 errors: **done** (Infrastructure + Web).
  - **Manual verification (remaining, user):** a member with a task today + meeting tomorrow → one agenda (correct sections); 3 members with items → 3 separate emails, each only their own; unassigned task + attendee-less meeting → owner agenda; active member with no email → items go to owner + info log; overdue task/meeting included (bounded); member with nothing → no email; re-poll same day → no duplicate per recipient; Failed → retriable; per-business look-ahead overrides default; owner-who-is-also-a-member sees no duplicate item; disabled / non-Professional → nothing; other assistants + Daily Brief unchanged; Tasks/Meetings create-edit still work; existing attendee-less meetings don't error the scan.
  - Scenarios doc added: `.kiro/docs/scenarios/digital-assistants-task-meeting-reminder-testing.md`.
  - _Requirements: 9.2, 9.3, 9.4, 9.5_

## Notes

- **Fan-out** is the defining trait: N emails per business, keyed/de-duped by **normalised resolved
  email** (bucket identity == cycle-key token) so the owner-who-is-a-member never double-emails.
- Tasks = 1-to-1 (`FollowUpTask.TeamMemberId`, already persisted — UI/validation/auto-assign only);
  meetings = 1-to-many (`MeetingTeamMember`, new; attendee writes in an explicit transaction). Both
  auto-assign the sole active member. No backfill (unassigned → owner).
- Recipient fallback: `TeamMember.Email` → linked portal-user email (`IPortalUserEmailResolver`) →
  owner. Team-member email strongly encouraged (form note); column stays nullable.
- Overdue is **bounded** (`TaskMeetingOverdueLookBackDays`, default 14) so day-one isn't noisy (T4).
- **Enqueuer race (T1):** shared UNIQUE cycle-index migration (5.4) + `EnqueueAsync` violation catch
  — closes the check-then-insert window that fan-out amplifies. Protects all scheduled assistants.
- Separate from the Daily Brief (both opt-in); the brief keeps its own task line.
- `catch (Exception ex)` everywhere; repositories rethrow; composer per-recipient fail-safe.
- **Migration ranges (X1):** this spec owns **204–206**; VAT owns 202–203; the shared unique-index
  migration (5.4) is co-owned (idempotent — first to build creates it). `200/201` latest seen —
  **reconfirm the next FREE number before creating.** `USE [Portal]`; guarded/idempotent; audit
  timestamp on the new table.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1","1.2","2.1","2.2"] },
    { "id": 1, "tasks": ["1.3","2.3","2.4","3.1"] },
    { "id": 2, "tasks": ["2.5","4"] },
    { "id": 3, "tasks": ["5.1","5.2","5.3","5.4","6.1","6.2","7.1"] },
    { "id": 4, "tasks": ["7.2","8.1","8.2","8.3","8.4"] },
    { "id": 5, "tasks": ["9"] },
    { "id": 6, "tasks": ["10.1","10.2","10.3"] },
    { "id": 7, "tasks": ["11"] }
  ]
}
```
