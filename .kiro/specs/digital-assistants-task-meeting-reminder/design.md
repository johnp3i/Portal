# Design: Task & Meeting Reminder Assistant (Group 4)

## Overview

Two-part design:

- **Part 1 — Sales assignment model:** task assignee wiring (`FollowUpTask.TeamMemberId`, already
  present but unset), a new `[sales].[MeetingTeamMember]` many-to-many table + attendee UI, and a
  team-member-email note. This is a **Sales module change**, not just a notification feature.
- **Part 2 — Reminder assistant:** a daily, per-team-member **fan-out** agenda built on the Group 3
  engine.

The single most important architectural fact (verified in code): `IDigestComposer.ComposeAsync`
returns **one** `OutboxMessage?`, and `ScheduledDigestRunner.ProcessBusinessAssistantAsync`
enqueues that one message. This assistant must emit **N messages per business** (one per team
member with items, plus an owner catch-all). The existing single-message contract does not fit, so
this design introduces a **fan-out composer contract** rather than bending the runner awkwardly.

Grounded in code (verified this review): `ScheduledDigestRunner`, `IDigestComposer`/
`DigestComposerBase`, `IDigestEnqueuer`/`DigestEnqueuer`, `DigestCycleKey`,
`IDigestRecipientResolver`/`IOwnerEmailResolver`, `NotificationOutboxRepository.ExistsForCycleAsync`
(excludes Failed), `FollowUpTask`/`Meeting`/`TeamMember` entities, `FollowUpTaskService.CreateTaskAsync`
+ `MeetingService.CreateMeetingAsync`/`UpdateMeetingAsync`, the `/Assistants` card machinery, and the
198/199/200/201 migration patterns.

**Verified facts that corrected this design (see the numbered notes below):**
- `CreateFollowUpTaskRequest` **already has `TeamMemberId`** and `CreateTaskAsync` **already
  persists it** (`TeamMemberId = request.TeamMemberId`). So the task side needs only UI +
  sole-member auto-assign, not request/service plumbing (T5).
- `CreateMeetingAsync`/`UpdateMeetingAsync` are **NOT transactional** — a plain `InsertAsync`/
  `UpdateAsync` followed by unrelated calls. So attendee writes are a **second call**, not "inside
  the existing transaction"; if atomicity matters, this design must open one explicitly (T5).
- `DigestEnqueuer.EnqueueAsync` is **check-then-insert** (two statements) and `IX_OutboxMessage_Cycle`
  (migration 200) is **NON-unique** — so the "enqueuer closes the race" claim is only partly true;
  fan-out amplifies the exposure (T1).
- `PortalModules.DigitalAssistants = "digital_assistants"` confirmed as the gating key (T7).

---

## Part 1 — Sales Assignment Model

### 1.1 Task assignee (1-to-1) — mostly already wired (T5 correction)

`FollowUpTask.TeamMemberId (int?)` exists **and is already threaded end-to-end**:
`CreateFollowUpTaskRequest.TeamMemberId` exists and `CreateTaskAsync` already does
`TeamMemberId = request.TeamMemberId`. **Verify the edit path** (`UpdateTaskAsync` /
`UpdateFollowUpTaskRequest`) carries it too — if not, add it there. So the remaining work is small:
- **Validation:** on create/edit, if `TeamMemberId` is set, validate the member is **active** and in
  the business (currently persisted without that check).
- **Auto-assign sole member:** when the business has exactly one active `TeamMember` and the request
  leaves `TeamMemberId` null, default it server-side (so it holds even if the UI omits it).
- **UI:** an assignee `<select>` (active members) on the task create/edit forms; hidden/prefilled
  when only one member.
- No backfill; existing null rows stay valid (→ owner in the reminder).

### 1.2 Meeting attendees (1-to-many) — new mapping table

New table `[sales].[MeetingTeamMember]`:

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT IDENTITY PK | |
| `MeetingId` | INT NOT NULL | FK → `[sales].[Meeting](Id)` |
| `TeamMemberId` | INT NOT NULL | FK → `[sales].[TeamMember](Id)` |
| `CreatedAtUtc` | DATETIME NOT NULL DEFAULT GETUTCDATE() | audit (per steering) |

Unique index on `(MeetingId, TeamMemberId)`. Migration guarded (`IF NOT EXISTS`), `USE [Portal]`.

- **Entity** `MeetingTeamMember` + `Meeting.TeamMembers` navigation, plus a small repository
  (`ReplaceAttendeesAsync(meetingId, businessId, memberIds)`, `GetAttendeeIdsByMeetingIdsAsync(...)`).
- **Create/Edit in `MeetingService`:** accept a list of team-member ids; on create, insert the
  attendee rows **after** the meeting insert; on edit, **replace** (delete existing for the meeting,
  insert the new set). Validate each member active + in business.
  - **Transactionality (T5 correction):** `CreateMeetingAsync`/`UpdateMeetingAsync` are **not**
    transactional today (plain insert/update + unrelated follow-ups). So either (a) wrap the meeting
    write + attendee write in a new explicit transaction (`_context.Database.BeginTransactionAsync`)
    for atomicity, or (b) accept best-effort attendee writes (meeting persists even if attendee
    insert fails) consistent with the current non-transactional style. **Recommend (a)** — attendees
    are core to this feature and a half-saved meeting/attendee split would silently break reminders.
    Confirm the repository writes can enlist in an ambient transaction (they use the shared context).
- **Auto-add sole member** on create when only one active member exists.
- **UI:** a multi-select of active members on the meeting create/edit forms.
- No backfill; existing meetings have no rows (→ owner in the reminder).

### 1.3 Team-member email note

- `TeamMember.Email` stays **nullable** (no breaking migration).
- Add an explanatory note under the Email field on the team-member add/edit form: this address is
  where the member's task & meeting reminders are sent; strongly recommended.

> **Naming/convention checks:** `MeetingTeamMember` follows the `<TableName>Id` FK convention
> (`MeetingId`, `TeamMemberId` → tables `Meeting`, `TeamMember` exist). `CreatedAtUtc NOT NULL
> DEFAULT GETUTCDATE()` satisfies the mandatory audit-timestamp steering rule. Full table names in
> any hand-written SQL (no short aliases).

---

## Part 2 — Reminder Assistant

### 2.1 The fan-out problem & chosen contract

`IDigestComposer.ComposeAsync → OutboxMessage?` is single-message. Two options:

- **Option A — new fan-out interface.** Introduce `IFanOutDigestComposer` with
  `Task<IReadOnlyList<OutboxMessage>> ComposeManyAsync(businessId, assistantTypeId, setting,
  businessLocalNow, ct)`. The runner, when a composer implements it, iterates the returned messages
  and enqueues each (the enqueuer already de-dupes per cycle key). Keeps `IDigestComposer`
  untouched for the existing single-message assistants.
- **Option B — loop outside the composer.** Put the per-member iteration in the runner. Rejected:
  leaks assistant-specific logic into the generic runner.

**Decision: Option A.** Add `IFanOutDigestComposer` (separate from `IDigestComposer`). In
`ScheduledDigestRunner.ProcessBusinessAssistantAsync`, after resolving the composer by key, branch:
if it's a fan-out composer, call `ComposeManyAsync` and enqueue each message (each carries its own
recipient-scoped cycle key); else the existing single-message path. The runner still owns
enabled?/IsDue?/timezone. This is a **`ScheduledDigestRunner` constructor change** — it must also
inject `IEnumerable<IFanOutDigestComposer>` (a second keyed dictionary) alongside the existing
`IEnumerable<IDigestComposer>` (T6). A composer implements **exactly one** of the two interfaces
(never both) so a key resolves to a single path.

> The runner's generic single pre-check (`ExistsForCycleAsync` before compose) is skipped for
> fan-out composers (there's no single key to pre-check); dedup is per-message.

> **⚠️ Enqueuer race (T1, verified — matters MORE for fan-out):** `DigestEnqueuer.EnqueueAsync` is
> **check-then-insert** — `ExistsForCycleAsync` then `InsertAsync` as two separate statements — and
> `IX_OutboxMessage_Cycle` (migration 200) is **NON-unique**. So two overlapping scheduler passes
> can both pass the check and double-insert. For a single-message assistant that's one rare
> duplicate; for **fan-out** it multiplies across every recipient. The design must NOT claim the
> enqueuer "closes" the race — it only narrows it.
>
> **Required hardening (shared, do as part of this spec):** make the cycle index **UNIQUE** filtered
> — `CREATE UNIQUE INDEX [UX_OutboxMessage_Cycle] ON [notification].[OutboxMessage]
> ([BusinessId],[AssistantTypeId],[CycleKey]) WHERE [CycleKey] IS NOT NULL` — and have
> `EnqueueAsync` catch the unique-key violation (SQL error 2601/2627) and treat it as
> "already enqueued → return false" (same outcome as the existing exists-check, now race-safe). This
> also protects the existing weekly digests + Daily Brief. NOTE: this replaces the non-unique index
> from migration 200, so the migration drops-then-recreates it (guarded), and a one-off dedup of any
> existing duplicate `(BusinessId,AssistantTypeId,CycleKey)` rows may be needed before the unique
> index can be created — the migration should delete duplicate non-Failed rows (keep the earliest)
> first. This shared migration is co-owned with the VAT spec (idempotent; first to build creates it).

### 2.2 `TaskMeetingReminderComposer` (new, fan-out)

Implements `IFanOutDigestComposer`; `AssistantKey = DigestAssistantKeys.TaskMeetingReminder`. Deps:
base `PortalDbContext`, `IOwnerEmailResolver`, `IPortalUserEmailResolver` (new — member→email by
`UserId`, §2.3), `NotificationOutboxRepository`, `NotificationOptions`, `ILogger`, and read access to
tasks/meetings/members/attendees (via repositories or the context).

`ComposeManyAsync(businessId, assistantTypeId, setting, businessLocalNow, ct)`:

1. `lookAhead = setting?.TaskMeetingLookAheadDays ?? _options.TaskMeetingDefaultLookAheadDays` (2).
   `today = DateOnly.FromDateTime(businessLocalNow)`; `windowEnd = today.AddDays(lookAhead)`.
2. Load active `TeamMember`s for the business (id → member).
   Also `overdueFloor = today.AddDays(-overdueLookBackDays)` (T4 — bound the overdue look-back so a
   business with a long tail of un-actioned items doesn't get a wall of "overdue" on day one).
   `overdueLookBackDays` = `_options.TaskMeetingOverdueLookBackDays` (default **14**); global for now
   (a per-business control can come later).
3. Load candidate **tasks**: `BusinessId`, `!IsCompleted`, and (`DueAtUtc.Date` in
   `[overdueFloor .. today)` [overdue, bounded] OR `DueAtUtc.Date` in `[today .. windowEnd]`).
   Project id, title, type, due, time, assignee.
4. Load candidate **meetings**: `BusinessId`, `!IsCancelled`, `IsActive`, and
   (`ScheduledAtUtc.Date` in `[overdueFloor .. today)` with no `Outcome` [overdue, bounded] OR
   `ScheduledAtUtc.Date` in `[today .. windowEnd]`), with their attendee member ids via
   `MeetingTeamMember`.
5. **Resolve the owner up front** to a `(TeamMember? ownerMember, string ownerEmail)`:
   - `ownerEmail = IOwnerEmailResolver.ResolveAsync(businessId)`.
   - `ownerMember` = the active `TeamMember` whose `UserId` equals the owner's portal user id, if one
     exists (so the owner-who-is-also-a-member is a **single identity**, not two — T2 fix).

6. **Bucket by a single normalised recipient identity (T2/T3 fix).** The bucket key is the
   **normalised resolved email** (trim + lowercase), NOT the team-member id and NOT a separate
   "owner" concept. This guarantees the owner and a member row that resolve to the same address
   collapse into one agenda, and the cycle key (built from the same normalised email) can't diverge:
   - **Assigned task** → resolve its assignee member's email (§2.3). Add to that email's bucket.
     Unassigned task → the **owner email** bucket.
   - **Meeting** → for each active attendee, resolve their email and add to that bucket. Meeting with
     no attendees → the **owner email** bucket.
   - A member with **no resolvable email** → their items go to the **owner email** bucket + info log
     (never dropped).
   - Because the owner catch-all uses `ownerEmail`, and an owner-who-is-a-member also resolves to
     `ownerEmail`, both naturally merge — no item appears twice, one email.
   - **Within a bucket, de-dupe items** (a meeting with two attendees who resolve to the same email;
     or an item reached via both assignment and the owner catch-all) by `(type, id)`.

7. For each **bucket (recipient email)** with ≥1 item:
   - `recipientToken` = a stable token derived from the **normalised email** (e.g.
     `email-{sha1(normalisedEmail)[..12]}`), so the cycle key is per-identity and matches the
     bucket key exactly (T3 fix — no `tm-{id}`-vs-`owner` divergence).
   - Cycle key: `DigestCycleKey.RecipientDaily(AssistantKey, businessLocalNow, recipientToken)`.
   - Short-circuit: `if (await _outbox.ExistsForCycleAsync(businessId, assistantTypeId, cycleKey)) continue;`
   - Build subject + HTML agenda (overdue → today → upcoming; meetings show time, tasks show time or
     "all day"). Set `RecipientName` to a friendly name when known (the member's, or the owner).
   - Add an `OutboxMessage` (Pending, `RecipientEmail = bucketEmail`, `ScheduledForUtc = now`,
     `CycleKey = cycleKey`, `MaxRetries = DefaultMaxRetries`).

8. Return the list (empty → runner enqueues nothing; buckets with no items were never created →
   per-recipient quiet skip).

Each recipient's compose is wrapped so one failure doesn't abort the others (collect, log, continue).

### 2.3 Recipient resolution & identity normalisation

`ResolveMemberEmail(TeamMember m)`:
1. `m.Email` if non-blank;
2. else the portal user email for `m.UserId` (Membership lookup by `UserId` — a small resolver
   sibling to `IOwnerEmailResolver`, e.g. `IPortalUserEmailResolver.ResolveByUserIdAsync(userId)`);
3. else null → items fall to the **owner** bucket + info log.

`NormaliseEmail(e)` = `e.Trim().ToLowerInvariant()` — the **single identity key** used for both the
bucket dictionary and the cycle-key token, so an owner-who-is-a-member, two member rows sharing an
address, or an item reached via both assignment and the owner catch-all all collapse to one agenda /
one cycle key (T2/T3). Only **active** members are recipients.

### 2.4 Cycle key

Add to `DigestCycleKey`:
```csharp
/// Recipient-scoped daily key: at most one agenda per recipient per business-day.
/// recipientToken is derived from the NORMALISED recipient email (not team-member id / not "owner"),
/// so it matches the bucket identity exactly — two identities resolving to the same address share
/// one key (T3). CycleKey column is NVARCHAR(80): keep the token short (hash prefix), e.g.
/// $"email-{Convert.ToHexString(SHA1(normalisedEmail))[..12].ToLower()}".
public static string RecipientDaily(string assistantKey, DateTime businessLocalNow, string recipientToken)
    => $"{assistantKey}:{businessLocalNow:yyyy-MM-dd}:{recipientToken}";
```
Once-per-recipient-per-day; Failed rows remain retriable. **Length check:** `CycleKey` is
`NVARCHAR(80)` (migration 200). `"task_meeting_reminder:2026-02-04:email-xxxxxxxxxxxx"` ≈ 55 chars —
within budget. (The raw email is NOT used in the key to avoid the 80-char limit and PII in the key.)

### 2.5 Email builder

`DigestEmailBuilder.TaskMeetingReminderSubject(...)` + `BuildTaskMeetingReminderHtml(recipientName,
today, overdue[], todayItems[], upcoming[])` — owner/internal style, **no** customer footer. Each
item line: for a task, title + type + (time or "all day") + optional contact/lead; for a meeting,
subject + time + location/contact. Sections: **Overdue** (red), **Today**, **Next N days**.

### 2.6 Settings, seed, runner registration

- **Setting column:** `BusinessAssistantSetting.TaskMeetingLookAheadDays (int?)` — additive nullable
  migration (mirror 199). Threaded through the repository upsert/selects and the controller
  preserve-forward logic. Null → global default (2).
- **NotificationOptions:** add `TaskMeetingDefaultLookAheadDays = 2` and
  `TaskMeetingOverdueLookBackDays = 14` (T4 — bounds the overdue tail).
- **Seed:** AssistantType **Id 7** `task_meeting_reminder`, `RecipientKind = 'PortalUser'`
  (free-text; no enum for 'TeamMember' and adding one is out of scope — the recipient is internal,
  which 'PortalUser' adequately signals), `IsCustomerFacing = 0`. Idempotent MERGE + verify PRINT.
- **Key const:** `DigestAssistantKeys.TaskMeetingReminder = "task_meeting_reminder"`.
- **Runner:** add the key to the `digestKeys` array AND to `DailyCadenceKeys`; branch to the fan-out
  path when the resolved key maps to an `IFanOutDigestComposer`. **Runner constructor change (T6):**
  inject `IEnumerable<IFanOutDigestComposer>` and build a second `_fanOutComposersByKey` dictionary
  alongside `_composersByKey`. A given key must map to **exactly one** of the two dictionaries.
- **Shared unique-index hardening (T1):** the migration that makes `IX_OutboxMessage_Cycle` UNIQUE
  filtered (see §2.1) + `EnqueueAsync` catching the unique violation. Co-owned with the VAT spec;
  idempotent (`IF NOT EXISTS` / guarded drop-recreate); dedup existing duplicate non-Failed rows
  before creating the unique index.
- **DI:** register the composer as `IFanOutDigestComposer` (not `IDigestComposer`).

### 2.7 UI — new "team agenda" card

- `AssistantsController.Index`: `IsTaskMeetingReminder` classification; surface
  `TaskMeetingLookAheadDays` (default shown = global). `isScheduled = true`.
- Card (`Assistants/Index.cshtml`): on/off, **send-time**, **look-ahead-days** (1..30), activity-log
  expander, and a short note explaining it emails each assigned team member (fan-out; no single
  recipient override). No day-of-week, no figures.
- `AxPostSaveAssistantSettings`: task-meeting branch — null `SendDayOfWeek`; parse+validate
  look-ahead (1..30; empty → null → global); preserve-forward everything (incl. the new column in the
  toggle path).

---

## Data Model Summary

| Change | Type | Migration (reserved) |
|--------|------|-----------|
| `[sales].[MeetingTeamMember]` (Id, MeetingId, TeamMemberId, CreatedAtUtc) + unique(MeetingId,TeamMemberId) | new table | `204_CreateMeetingTeamMember.sql` |
| `AssistantType` Id 7 `task_meeting_reminder` | seed row | `205_SeedTaskMeetingReminderAssistantType.sql` |
| `BusinessAssistantSetting.TaskMeetingLookAheadDays` | `INT NULL` | `206_AddTaskMeetingLookAheadDays.sql` |
| `IX_OutboxMessage_Cycle` → **UNIQUE** filtered (shared T1 hardening; dedup existing dupes first) | index change | shared `20x_MakeOutboxCycleIndexUnique.sql` |

> **Migration ranges (X1):** VAT owns **202–203**; this spec owns **204–206**. The shared
> unique-index hardening is a single migration co-owned by both — whichever spec builds first creates
> it (guarded/idempotent), the other reuses it. **Reconfirm the next FREE numbers before creating**
> — this session did not re-scan the migrations folder; 200/201 are the latest seen.

`FollowUpTask.TeamMemberId` already exists **and is already persisted** — no schema change, only UI
+ validation + auto-assign. `TeamMember.Email` stays nullable.

---

## Error Handling & Reliability

- Composer + per-recipient build wrapped in `try/catch (Exception ex)`; one recipient's failure logs
  and is skipped, others proceed (Req 9.3). Whole-composer failure → log + return empty list.
- Reuses the existing dispatcher (send/retry/failure). No new send path.
- Per-recipient once-a-day via recipient-scoped cycle key + `ExistsForCycleAsync` (Failed excluded →
  retriable).
- Repositories: async, typed, `try/catch (Exception ex)` rethrow, explicit `businessId` (tenant-less
  scan). Part 1 endpoints reuse the tenant-scoped Sales services.

---

## Testing Strategy

Part 1 (assignment):
1. Task create/edit persists `TeamMemberId`; validates active + in-business; sole-member auto-assign.
2. Meeting create/edit persists attendee set (replace-on-edit); sole-member auto-add; validation.
3. Existing null-assignee task & attendee-less meeting stay valid.

Part 2 (reminder):
4. Member with a task today + a meeting tomorrow → one agenda listing both, correct sections.
5. **Fan-out:** 3 members each with items → 3 separate emails, each only their own items.
6. **Unassigned → owner:** null-assignee task + attendee-less meeting → appear in the owner agenda.
7. **No-email member → owner:** active member without `Email`/portal-user email → their items go to
   owner + info log.
8. **Overdue included:** incomplete past-due task + past meeting with no outcome → in the overdue
   section.
9. **Quiet skip:** member with nothing in range → no email.
10. **Once per recipient/day:** re-poll same day → no duplicate per recipient (recipient cycle key);
    Failed → retriable.
11. **Look-ahead:** per-business value overrides the global default (2); item just outside window
    excluded, just inside included.
12. **De-dupe:** owner who is also a team member doesn't get an item twice.
13. Disabled / non-entitled plan → nothing.
14. **Regression:** other assistants + Daily Brief task line unchanged; Tasks/Meetings screens still
    create/edit fine; existing meetings without attendees don't error the scan.

Manual: seed 2 members with emails + assorted assigned tasks/meetings; confirm each gets their own
agenda at send-time; confirm no duplicate next day; leave one member email blank → owner catches
their items.

---

## Review Revisions Summary

- **Fan-out contract = new `IFanOutDigestComposer`** (`ComposeManyAsync` → list); runner branches on
  it via a second keyed dictionary — a **runner constructor change** (T6). A composer implements
  exactly one of the two interfaces.
- **Meeting attendees = new `MeetingTeamMember` join table** (1-to-many); tasks reuse the existing
  `TeamMemberId` (1-to-1). Both auto-assign the sole active member; no backfill (unassigned → owner).
- **T5 (verified):** `CreateFollowUpTaskRequest.TeamMemberId` + `CreateTaskAsync` **already persist**
  the assignee — task-side work is only UI + validation + auto-assign, not plumbing. And
  `CreateMeetingAsync`/`UpdateMeetingAsync` are **not transactional**, so attendee writes need an
  explicit transaction (recommended) rather than "the existing transaction" (which doesn't exist).
- **T2/T3 (identity collapse):** bucket + cycle key are both keyed on the **normalised resolved
  email** (not `tm-{id}`/`owner`). The owner is pre-resolved to its `TeamMember` (by `UserId`) so an
  owner-who-is-a-member is one identity; items de-duped by `(type,id)` within a bucket. Prevents the
  double-email the earlier `tm-{id}`-vs-`owner` split allowed.
- **T4:** overdue is **bounded** by `TaskMeetingOverdueLookBackDays` (default 14) so day-one isn't a
  wall of stale items.
- **T1 (enqueuer race, verified):** `DigestEnqueuer` is check-then-insert and the cycle index is
  non-unique — fan-out amplifies the double-send window. **Shared hardening:** UNIQUE filtered cycle
  index + `EnqueueAsync` catching the unique violation (dedup existing dupes first). Co-owned with
  the VAT spec.
- **T7 (verified):** `PortalModules.DigitalAssistants = "digital_assistants"` is the correct gating
  key; Professional+ per the Phase 4a precedent.
- **Recipient resolution:** `TeamMember.Email` → linked portal-user email (new
  `IPortalUserEmailResolver` by `UserId`) → owner.
- **Separate from the Daily Brief** (both opt-in); the brief keeps its own task line.
- **`RecipientKind = 'PortalUser'`** reused (free-text; no 'TeamMember' enum value — out of scope).
- **Team-member email note** on the member form; column stays nullable.
- **CycleKey length:** token is a short hash prefix of the normalised email, keeping the key within
  the `NVARCHAR(80)` column and out of PII.
- **Scope note:** Part 1 (Sales assignment) must land + verify before Part 2 relies on it.
