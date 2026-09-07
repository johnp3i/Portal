# Testing Scenarios: Scheduled Owner Digests (Group 3)

Scheduled digests are owner-facing Digital Assistants that email a weekly summary on a
cadence. Group 3 adds the reusable **scheduled-scan engine** plus two digests:

- **Weekly Outstanding Balance Digest** — receivables (what customers owe) + upcoming supplier
  payments coming due.
- **Weekly Financial Snapshot** — this-week collected / expenses / net + to-date outstanding.

Both are `PortalUser`-facing (no customer branded footer), reuse the Phase 1 outbox spine for
delivery, and are gated behind the `digital_assistants` module (Professional+).

## Prerequisites

- Run migrations **198–200** against the Portal database:
  - `198` seeds the two digest `AssistantType` rows (ids 2, 3).
  - `199` adds the schedule/recipient/figure columns to `BusinessAssistantSetting`.
  - `200` adds `CycleKey` + the `IX_OutboxMessage_Cycle` index to `OutboxMessage`.
- The `Notifications` email account password must be configured in **User Secrets** (same as
  Phase 1 — the dispatcher sends these emails).
- `Notifications:EnableScheduler = true` (default). `SchedulerPollIntervalMinutes` defaults to
  15; lower it in `appsettings.Development.json` to speed up manual testing.
- The business must be on **Professional or Enterprise** (`digital_assistants` gated) and have
  a business owner with an email (`UserBusiness.IsOwner && IsActive`).
- The business should have a `TimeZoneId` set to test timezone-correct scheduling.

> The business page is at `/Assistants`. The digests appear as cards alongside Thank-You, each
> with a **send day + time**, a **recipient** field, an **also-send-to-owner** toggle, and (for
> the Snapshot) an **included figures** checklist — instead of the customer-facing working-hours
> control.

---

## Scenario 1: Digest cards render and default on

1. As a Professional/Enterprise user, open `/Assistants`.
2. **Expected:** Two new cards — "Weekly Outstanding Balance Digest" and "Weekly Financial
   Snapshot" — each **on** by default, showing a send-day dropdown (default Monday), a send-time
   input (default 08:00), a recipient field (placeholder "Business owner (default)"), and an
   "Also send to owner" toggle. The Snapshot card additionally shows the included-figures
   checklist (Collected / Expenses / Net / Total outstanding checked by default).

---

## Scenario 2: Weekly Outstanding Balance Digest (happy path)

1. Ensure the digest is enabled. Set its send day/time to **one minute from now** in the
   business's local time, and Save (BlockUI → SweetAlert2 "Saved").
2. Have at least one issued invoice with an outstanding balance, and one non-cancelled purchase
   with a `TargetPaymentDate` within 14 days.
3. Wait for the scheduler poll (or restart with a short interval).
4. **Expected (DB):** exactly one `[notification].[OutboxMessage]` row with the Outstanding
   digest `AssistantTypeId`, `Status = 'Pending'`, `CycleKey = "weekly_outstanding_digest:<ISO
   year>-W<week>"`, `RecipientEmail` = the owner's email, and a subject like "Your weekly
   outstanding balance — {business}".
5. Within one dispatcher poll, **Expected:** the email sends, the row flips to `Sent`, and the
   owner receives a digest showing total outstanding, overdue, the top outstanding invoices, and
   a "supplier payments coming due" section — plus a "this week at a glance" line.

---

## Scenario 3: Weekly Financial Snapshot

1. Enable the Snapshot, set day/time to just ahead, Save.
2. Record a payment and issue an invoice this week so there is activity.
3. **Expected:** one outbox row with the Snapshot `CycleKey`; the email lists the selected
   figures (collected / expenses / net / outstanding) for the last 7 days plus the glance line.
4. Uncheck some figures, Save, and (next cycle / after clearing the row) confirm only the
   selected figures appear.

---

## Scenario 4: Always-send heartbeat / all-clear

1. Use a business with **no** outstanding invoices and **no** upcoming payables.
2. Trigger the Outstanding digest cycle.
3. **Expected:** the digest is **still sent** (not suppressed), rendered as a positive
   "all clear — nothing outstanding this week" message, still carrying the "this week at a
   glance" line. This proves the delivery chain is healthy even on a quiet week.

---

## Scenario 5: Idempotency (at most one per cycle)

1. After a digest has been enqueued for the current week, let the scheduler poll again (or
   trigger a second run) within the same ISO week.
2. **Expected:** **no** second row is created — the exact `CycleKey` dedup (`ExistsForCycleAsync`)
   blocks it. The `IX_OutboxMessage_Cycle` index backs this lookup.
3. If a cycle's message previously **failed** (exhausted retries → `Failed`), a re-run **is**
   allowed to enqueue again (failed rows do not block).

---

## Scenario 6: Disabled digest → nothing enqueued

1. Toggle a digest **off** on `/Assistants`.
2. Trigger the cycle.
3. **Expected:** no outbox row for that digest/business.

---

## Scenario 7: Configurable recipient (accounting dept)

1. In the digest's **Send to** field, enter an accounting address (e.g. `accounts@acme.test`).
   Leave "Also send to owner" on.
2. Save, then trigger the cycle.
3. **Expected:** the digest is sent to the accounting address (primary), with the owner CC'd on
   the same message (one outbox row per cycle). Turning "Also send to owner" off sends to the
   override only.
4. Enter an invalid email → Save is rejected with a SweetAlert2 validation error.

---

## Scenario 8: Timezone-correct scheduling

1. Set the business `TimeZoneId` (e.g. Cyprus / GTB Standard Time).
2. Set the digest send day/time to a moment that has **not yet** passed in the business's local
   time but **has** passed in UTC (or vice-versa).
3. **Expected:** the digest is due based on **business-local** time, not the server's UTC clock.
   A newly-enabled digest whose send moment already passed this week is **not** back-filled — it
   begins from the next due cycle.

---

## Scenario 9: Plan gating

1. Downgrade a business below Professional (or use one without `digital_assistants`).
2. Trigger the scheduler.
3. **Expected:** nothing is enqueued for that business — the scheduler's tenant-less plan check
   (`IPlanCheckService.IsModuleInPlanAsync(businessId, "digital_assistants")`) excludes it.
   (There is no `HttpContext` in the background service; gating uses the businessId overload.)

---

## Scenario 10: Preserve-on-partial-write (regression)

1. Configure a digest's send day, time, recipient, and figures. Save.
2. Toggle the digest **off**, then back **on** (the quick toggle path).
3. **Expected:** the send day/time/recipient/figures are **preserved** — the toggle does not
   reset them to defaults or NULL. (Both the toggle and the save path read the existing row and
   carry forward every column.)

---

## Scenario 11: Thank-You regression

1. With Thank-You still enabled, record a payment (manual or Stripe).
2. **Expected:** Thank-You enqueues and sends exactly as before; its outbox row has
   `CycleKey = NULL` and dedups via `RelatedEntityType/RelatedEntityId` — unchanged by Group 3.

---

## Notes

- The scheduler (`DigitalAssistantSchedulerBackgroundService`) decides *what/when* to enqueue;
  the existing dispatcher drains the outbox and sends. Delivery failures/retries and the admin
  failure-threshold alert are handled entirely by the Phase 1 dispatcher — no new alerting.
- The digest body is rendered and stored on the outbox row at write time (self-contained), so
  the activity log shows exactly what was sent.
- **Heartbeat blind spot:** a received digest proves the dispatcher + SMTP path works, but does
  NOT self-monitor the scheduler. If the scheduler is down, nothing is enqueued and nothing
  alerts. Scheduler-liveness monitoring is a documented future enhancement (out of scope).
- Payables are labelled "coming due," never "unpaid" — `Purchase` has no paid-state (only
  `IsCancelled`), so the section shows non-cancelled purchases whose effective due date
  (`COALESCE(TargetPaymentDate, SupplierDueDate)`) falls within the window.
