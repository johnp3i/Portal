# Implementation Plan: VAT Period Due Reminder Assistant (Group 4)

## Overview

One owner-facing, scheduled (Category B) assistant on the existing engine. It reminds the owner
**once per VAT period** as the derived filing deadline approaches, including the **approximate net
VAT payable**. Per-business notice lead time; owner recipient with override.

Reuse-first: the outbox spine, dispatcher, `ScheduledDigestRunner`, `DigestComposerBase`,
`IDigestRecipientResolver`/`IOwnerEmailResolver`, plan gating, timezone resolution, the
`GetUnsubmittedPeriodsFromAsync` query, and the derived-deadline math from `AttentionItemBuilder`.

New pieces: AssistantType seed (Id 6) + key const; a `VatNoticeLeadDays` settings column;
`DigestCycleKey.Period`; a public tenant-less `GetApproxNetVatPayableAsync`; a `VatReminderComposer`;
`DigestEmailBuilder` VAT subject/body; runner key-list + cadence registration; a new UI card shape.

Design in `design.md`; requirements in `requirements.md`. Migrations `USE [Portal]` header;
idempotent guarded DDL.

> **Decisions locked:** (A) once per period - period-scoped cycle key. (B) per-business lead time -
> new `INT?` column, falls back to global `VatDeadlineNoticeDays` (21). Deadline stays **derived**
> (`PeriodEndDate + VatFilingOffsetDays`, global, via shared `VatDeadline.For`). No new plan key.
> `today` for deadline-day math = UTC date; send-time-of-day stays timezone-aware via the runner.
> **NO runner change** - the composer's scan does the per-period `ExistsForCycleAsync` dedup and owns
> its key; the runner's pre-check is a harmless no-op for this assistant.
>
> **Review fixes baked in:** V1 - the VAT accessor takes the `VatSubmissionPeriod` the composer
> already found (no re-load). V2 - the scan emits for the most-urgent **not-yet-reminded** in-window
> period (no starvation of a second period). Enqueuer race - `DigestEnqueuer` is check-then-insert
> and `IX_OutboxMessage_Cycle` is non-unique; a shared migration makes it UNIQUE + `EnqueueAsync`
> catches the violation (low-risk here, but done once for all scheduled assistants).

## Status

**Implementation complete (tasks 1-8); builds clean (Infrastructure + Web, 0 errors).** Task 9's
build half is done; its manual verification against a running instance remains for the user (use
`.kiro/docs/scenarios/digital-assistants-vat-reminder-testing.md` as the checklist).

## Tasks

- [x] 1. Database - seed + settings column + shared hardening
  - [x] 1.1 Migration `202_SeedVatReminderAssistantType.sql` - seed AssistantType **Id 6** `vat_period_due_reminder` (`RecipientKind='PortalUser'`, `IsCustomerFacing=0`) via idempotent explicit-Id MERGE + post-merge verification PRINT (mirror 201).
    - _Requirements: 8.1, 8.3_
  - [x] 1.2 Migration `203_AddVatNoticeLeadDaysToAssistantSetting.sql` - `ALTER TABLE [notification].[BusinessAssistantSetting] ADD [VatNoticeLeadDays] INT NULL;` (additive, nullable; mirror 200).
    - _Requirements: 5.1, 5.2, 8.2_
  - [x] 1.3 **Shared** migration `204_MakeOutboxCycleIndexUnique.sql` - dedup existing duplicate `(BusinessId,AssistantTypeId,CycleKey)` rows (keep earliest), drop `IX_OutboxMessage_Cycle`, recreate as UNIQUE filtered `UX_OutboxMessage_Cycle WHERE [CycleKey] IS NOT NULL`. Guarded/idempotent; co-owned with the Task/Meeting spec. Closes the check-then-insert race for ALL scheduled assistants.
    - _Requirements: 4.1, 4.4_

- [x] 2. Constants + cycle key (Portal.Infrastructure)
  - [x] 2.1 `DigestAssistantKeys.VatPeriodDueReminder = "vat_period_due_reminder"`
    - _Requirements: 8.1_
  - [x] 2.2 `DigestCycleKey.Period(assistantKey, periodId)` -> `"{assistantKey}:period-{periodId}"` (date-independent -> once-per-period across the notice window; Failed rows remain retriable via `ExistsForCycleAsync`)
    - _Requirements: 4.1, 4.3, 4.4_

- [x] 3. Settings entity + persistence (Portal.Infrastructure)
  - [x] 3.1 Add `int? VatNoticeLeadDays` to `BusinessAssistantSetting`
    - _Requirements: 5.1_
  - [x] 3.2 Thread `VatNoticeLeadDays` through `BusinessAssistantSettingRepository` (UpsertAsync columns + all SELECT projections), mirroring the other nullable columns
    - _Requirements: 5.1, 5.2_

- [x] 4. Public per-period VAT figure accessor (Portal.Infrastructure)
  - [x] 4.1 Add `Task<decimal> GetApproxNetVatPayableAsync(int businessId, VatSubmissionPeriod period)` to `IVatSubmissionService` + `VatSubmissionService` - takes the period the composer already holds (V1, no re-load); tenant-less, side-effect-free: if an **unsubmitted** `VatSubmission` exists for the period -> return its `NetVatPayable`; else return `ComputeSubmissionFiguresAsync(businessId, period).NetVatPayable`. **Left `ComputeSubmissionFiguresAsync` private and unchanged** (no regression to VAT screens).
    - _Requirements: 3.1, 3.2, 3.3, 9.3_
  - [x] 4.2 Extract `VatDeadline.For(period, options)` = `PeriodEndDate.AddDays(VatFilingOffsetDays)` and use it in BOTH `AttentionItemBuilder` and the new composer (single definition of the derived deadline; no drift).
    - _Requirements: 2.2_

- [x] 5. VatReminderComposer + email (Portal.Infrastructure)
  - [x] 5.1 `VatReminderComposer : DigestComposerBase, IDigestComposer` - `AssistantKey = VatPeriodDueReminder`. ComposeAsync: (a) scan `GetUnsubmittedPeriodsFromAsync`, compute each derived deadline via `VatDeadline.For`, filter to the lead window `0..leadDays` (leadDays = `setting?.VatNoticeLeadDays ?? _options.VatDeadlineNoticeDays`), order most-urgent-first, and return the **first period with NO existing `Period(...)` cycle row** (V2 - the per-period `ExistsForCycleAsync` here IS the once-per-period dedup and prevents starving a second in-window period); catches ended-but-imminent periods (Req 2.5). None -> **info** log + null. (b) resolve recipient; none -> **warning** log + null. (c) `GetApproxNetVatPayableAsync(businessId, due.p)`. (d) build subject+body; return OutboxMessage (Pending, `ScheduledForUtc=now`, `CycleKey = Period(due.p.Id)`, MaxRetries = DefaultMaxRetries). `today = DateOnly.FromDateTime(DateTime.UtcNow)`. Deps: base `PortalDbContext`, `IDigestRecipientResolver`, `VatSubmissionPeriodRepository`, `IVatSubmissionService`, `NotificationOutboxRepository`, `NotificationOptions`, `ILogger`.
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.3, 4.1, 4.2, 5.2, 6.1, 6.2, 6.4, 9.1_
  - [x] 5.2 `DigestEmailBuilder.VatReminderSubject(...)` + `BuildVatReminderHtml(businessName, currencySymbol, periodLabel, deadline, daysUntil, netVat)` - owner-facing, **no** customer footer; states period label, deadline date, days remaining; **estimate** line per sign convention (`>0` tax owed / `<0` refund due (abs) / `=0` no payment expected), all labelled "approximate, as it stands today"
    - _Requirements: 3.1, 3.4, 3.5, 3.6, 6.5_

- [x] 6. Runner registration (Portal.Infrastructure / Portal.Web)
  - [x] 6.1 Added `DigestAssistantKeys.VatPeriodDueReminder` to the `digestKeys` array in `ScheduledDigestRunner.RunAsync` AND to `DailyCadenceKeys` (evaluated daily; period cycle key gives once-per-period)
    - _Requirements: 1.1, 1.2, 1.3_
  - [x] 6.2 Registered `AddScoped<IDigestComposer, VatReminderComposer>()` in Program.cs (with the other composers)
    - _Requirements: 1.1_

- [x] 7. Checkpoint - backend build
  - Built Infrastructure + Web; 0 errors. No regression to the existing composers, the runner, or the `AttentionItemBuilder` VAT line.
  - _Requirements: 9.4_

- [x] 8. UI - VAT reminder card (Portal.Web)
  - [x] 8.1 `AssistantsController.Index`: added `IsVatReminder` classification + surfaced `VatNoticeLeadDays` on the card VM (display default = global `VatDeadlineNoticeDays` when null). `isScheduled = true` for it. Injected `NotificationOptions` into the controller.
    - _Requirements: 7.1, 7.2_
  - [x] 8.2 `Assistants/Index.cshtml`: VAT card branch - on/off toggle, recipient (owner + override + owner-CC), **send-time** input, **"remind me N days before deadline"** number input, activity-log expander. No day-of-week, no figures. Client payload includes `vatNoticeLeadDays`.
    - _Requirements: 7.1, 7.2_
  - [x] 8.3 `AxPostSaveAssistantSettings`: VAT branch - null `SendDayOfWeek`; parse+validate `VatNoticeLeadDays` (1..90; empty -> null -> global default); recipient-list validation (shared); preserve-forward all untouched settings (incl. `VatNoticeLeadDays` in the toggle path)
    - _Requirements: 5.3, 5.4, 7.3, 7.4_

- [ ] 9. Final checkpoint - build + manual verification
  - Build 0 errors: **done** (Infrastructure + Web).
  - **Manual verification (remaining, user):** an unsubmitted period with a deadline inside the (per-business) lead window sends **one** reminder with the correct deadline + approximate net VAT (test each sign: owed / refund / none); a second poll the next day -> **no** duplicate (period cycle key); marking the period submitted -> silence; a period outside the window -> nothing; a period whose window has **ended** but deadline is imminent -> still reminded; per-business lead time overrides the global default; no owner email -> warning + no send; disabled / non-Professional plan -> nothing; weekly digests + Daily Brief + the `AttentionItemBuilder` VAT line unchanged; existing VAT screens unchanged.
  - Scenarios doc added: `.kiro/docs/scenarios/digital-assistants-vat-reminder-testing.md`.
  - _Requirements: 9.2, 9.3, 9.4_

## Notes

- Deadline is **derived** (`PeriodEndDate + VatFilingOffsetDays`), never stored - single global
  offset (jurisdiction-tuned). Multi-jurisdiction accuracy is out of scope (would need per-business
  offset/rules).
- Once-per-period = period-scoped cycle key; the daily date key would re-send every day in the
  window. The scan skips already-reminded periods (no starvation of a second in-window period, V2).
- Net VAT via the new tenant-less accessor taking the period (persisted-then-compute); the private
  `ComputeSubmissionFiguresAsync` stays untouched.
- **No runner change** - dedup is the composer scan's `ExistsForCycleAsync` + the (now-UNIQUE)
  cycle index; the runner's pre-check is a harmless no-op for a period-scoped key.
- `catch (Exception ex)` everywhere; repositories rethrow; composer + runner fail safe per business.
- **Migrations used:** 202 (seed), 203 (VatNoticeLeadDays column), 204 (UNIQUE cycle index). The
  204 hardening is co-owned with the Task/Meeting spec (idempotent - first to build creates it).

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1","1.2","1.3"] },
    { "id": 1, "tasks": ["2.1","2.2","3.1","3.2","4.1","4.2"] },
    { "id": 2, "tasks": ["5.1","5.2"] },
    { "id": 3, "tasks": ["6.1","6.2"] },
    { "id": 4, "tasks": ["7"] },
    { "id": 5, "tasks": ["8.1","8.2","8.3"] },
    { "id": 6, "tasks": ["9"] }
  ]
}
```
