# Testing Scenarios: Digital Assistants — Owner Alerts (Phase 4a)

Phase 4a adds two **owner-facing** assistants on top of the existing notification spine and the
Group 3 scheduled-scan engine:

- **New Payment Received** (Category A — event producer): emails the business **owner** the
  moment a payment is recorded, from any of the three payment paths (manual, Stripe card,
  global/unallocated).
- **Daily Brief** (Category B — scheduled, **daily** cadence): emails the owner a short daily
  summary of what needs attention (overdue invoices, quotations awaiting a response, tasks
  due) — and sends **nothing on a quiet day**.

Both go to the owner (`PortalUser`), so there is **no branded footer** and **no customer
suppression** concern. The attention items in the Daily Brief come from the same shared
`IAttentionItemBuilder` the Weekly Financial Snapshot uses, so the two never drift.

## Prerequisites

- Run migrations through **201** against the Portal database (migration 201 seeds the two
  Phase 4a `[notification].[AssistantType]` rows: id 4 `new_payment_received`, id 5
  `daily_brief` — both `RecipientKind = 'PortalUser'`, `IsCustomerFacing = 0`). The migration
  prints a `2/2 verified` line; if it prints a WARNING about a count other than 2, an id
  collided — reassign ids 4/5 before continuing.
- The `Notifications` email account password must be set in **User Secrets** (same requirement
  as Phase 1 — an unconfigured account makes every send fail and eventually raises the admin
  alert).
- The business is on **Professional or Enterprise** (both assistants are gated by the same
  `digital_assistants` module — no new plan key).
- The business has an **owner** (`UserBusiness.IsOwner = 1 && IsActive = 1`) whose `User.Email`
  is set — this is the default recipient for both assistants.
- `Notifications:EnableDispatcher = true`; the scheduled runner is enabled. The business's
  `TimeZoneId` is set so daily send-time resolves correctly.

> Business page: `/Assistants`. SuperAdmin page: `/Admin/Notifications`.

---

## Part A — New Payment Received (event alert)

### Scenario A1: Card renders as an event alert (no schedule)

1. As a Professional/Enterprise user, open `/Assistants`.
2. **Expected:** the **New Payment Received** card shows an on/off toggle, a recipient control
   (owner by default, with an optional override), and a "View activity log" expander — but
   **no** send day/time and **no** working-hours inputs (it is an event alert, not scheduled).

### Scenario A2: Alert on a manual payment (happy path)

1. Ensure New Payment Received is **enabled** and the owner email resolves.
2. Record a manual payment against an issued invoice (Revenue → record payment).
3. **Expected (DB):** an `[notification].[OutboxMessage]` row with `Status = 'Pending'`,
   `AssistantTypeId` = the `new_payment_received` id, the **owner's** email as recipient, a
   subject naming the amount (and invoice number when allocated), and `ScheduledForUtc` ≈ now
   (event alerts intentionally **bypass** working-hours deferral — the owner wants to know
   immediately).
4. **Expected (atomicity):** the payment row and the outbox row committed together.
5. Within one poll (~60s): the dispatcher sends it; the row flips to `Sent`; the owner receives
   the alert with **no** branded footer.
6. On `/Assistants`, the New Payment activity log shows the send as "Sent".

### Scenario A3: Alert on a Stripe card payment (retried webhook → no duplicate)

1. With Stripe Connect active and the assistant enabled, have a customer pay an invoice by card.
2. When `checkout.session.completed` records the payment, **Expected:** one owner alert is
   written alongside the Thank-You, then sent.
3. **Replay the same webhook** (or let Stripe retry it). **Expected:** **no** second payment and
   therefore **no** second alert — the Stripe path short-circuits before the payment insert
   (verified idempotent upstream via `WebhookProcessingService.ExistsByEventIdAsync` +
   `checkoutSession.Status == "completed"`). This is why New Payment carries **no** entity-dedup
   of its own.

### Scenario A4: Global / unallocated payment → exactly ONE alert for the parent

1. Enable the assistant. Record a **global payment** (`RecordGlobalPaymentAsync`) for a customer
   that spreads across **multiple** invoices (parent payment + several allocation children).
2. **Expected:** exactly **one** owner alert, for the **parent** payment — `invoiceNumber` is
   null (it is not tied to a single invoice), `customerName` is the paying customer, `amount` is
   the total `dto.Amount`.
3. **Expected:** **no** per-allocation alerts — the hook fires once, after the parent insert,
   never inside the per-child allocation loop.

### Scenario A5: Disabled → no alert

1. Toggle New Payment Received **off** on `/Assistants`.
2. Record a manual payment.
3. **Expected:** no outbox row for the owner alert (the Thank-You, if enabled, still sends —
   they are independent assistants).
4. Toggle back **on** → the next payment produces an alert again.

### Scenario A6: No owner email → no alert, payment unaffected

1. Temporarily clear the owner's email (or deactivate the owner `UserBusiness` row) so the owner
   email does not resolve.
2. Record a payment.
3. **Expected:** the producer returns null → **no** outbox row, **and the payment still succeeds
   and commits normally**. A prepare/insert failure in the alert path must **never** roll back
   the payment (best-effort).

---

## Part B — Daily Brief (scheduled, daily cadence)

### Scenario B1: Card renders as a daily brief (time only, no day-of-week)

1. Open `/Assistants`.
2. **Expected:** the **Daily Brief** card shows an on/off toggle, a **send time** input (with a
   hint like "Sent daily, your business time zone"), a recipient control, an owner-CC toggle,
   and an activity-log expander — but **no** day-of-week selector (unlike the weekly digests)
   and **no** figures/glance block.

### Scenario B2: Sends once on a day with attention items

1. Enable the Daily Brief. Set its send time to a few minutes from now (business local time).
2. Arrange at least one attention item — e.g. an **issued invoice past its due date with an
   outstanding balance** (credit-note-aware — a balance fully settled by a credit note does
   **not** count, per the derive-always overdue convention), and/or a quotation awaiting a
   response, and/or a task due.
3. When the send time passes, on the next runner poll **Expected:** one outbox row for the
   owner (`AssistantTypeId` = `daily_brief` id), subject like "Your daily brief — {business}",
   body listing the attention items (urgent items in red), **no** figures/glance block.
4. The dispatcher sends it; the owner receives it; the activity log shows "Sent".

### Scenario B3: Quiet day → NO email (and no false warning)

1. With the Daily Brief enabled, arrange a business that has **no** attention items today
   (nothing overdue, nothing awaiting, nothing due).
2. When the send time passes, **Expected:** **no** email is sent — the composer returns null and
   the runner skips it. This is the key difference from the weekly digests, which always send.
3. **Expected (logs):** an **information**-level line (e.g. "nothing to report today"), **not** a
   warning. A recipient-resolution failure is the only thing that logs a **warning** — a quiet
   day must not look like a problem.

### Scenario B4: Same-day re-poll → no duplicate (daily cycle-key dedup)

1. After a Daily Brief has sent for today (Scenario B2), let the runner poll again the **same
   day** (e.g. wait for the next poll interval).
2. **Expected:** **no** second brief — the daily cycle key `daily_brief:{yyyy-MM-dd}` already has
   a row for today, so the idempotency guard skips it. A new brief is only eligible the next
   day.

### Scenario B5: Before send time → not yet

1. Set the send time to later today and ensure attention items exist.
2. On a poll **before** the send time, **Expected:** nothing is sent yet (daily `IsDue` requires
   business-local time-of-day ≥ send time; day-of-week is ignored, and there is no back-fill for
   a missed earlier time within the day beyond the same-day window).

### Scenario B6: Disabled → nothing

1. Toggle the Daily Brief **off**.
2. Across the send time, **Expected:** no brief, regardless of attention items.

---

## Part C — Shared behaviour & regression

### Scenario C1: Weekly Financial Snapshot unchanged (builder-extraction regression)

1. The attention-item logic was **extracted** from `FinancialSnapshotComposer` into the shared
   `IAttentionItemBuilder`, with the KPI passed in as an optional param so the Snapshot reuses
   its already-loaded KPI (no double-query).
2. Trigger a Weekly Financial Snapshot (its normal weekly path).
3. **Expected:** the email content — including its attention section — is **identical** to
   before the extraction, and no extra KPI query is issued. The Daily Brief and the Snapshot
   now render attention items from the **same** source, so they cannot drift.

### Scenario C2: Thank-You unchanged

1. Record a payment with Thank-You enabled.
2. **Expected:** the customer still receives the Thank-You exactly as in Phase 1 — the New
   Payment owner alert is additive and independent (both can fire for the same payment: one to
   the customer, one to the owner).

### Scenario C3: Plan gating

1. As a Foundation user (no `digital_assistants`), open `/Assistants`.
2. **Expected:** the same soft-gate as other Professional modules — neither Phase 4a assistant
   is available. No new plan key was introduced; both ride the existing `digital_assistants`
   module.

### Scenario C4: Preserve-on-partial-write (new card shapes)

1. On the Daily Brief card (time-only) and the New Payment card (recipient-only), save a change
   that only touches the controls that card exposes.
2. **Expected:** `AxPostSaveAssistantSettings` preserves the untouched settings — e.g. saving the
   Daily Brief's send time does not wipe its recipient, and saving the New Payment recipient does
   not introduce a spurious schedule. Each card sends only the controls it owns.

---

## Notes

- **Recipient:** both assistants default to the business **owner** (`IOwnerEmailResolver`:
  `UserBusiness.IsOwner && IsActive` → `User.Email` via `MembershipDbContext`). This is the same
  resolver the weekly owner digests now use (single source of truth).
- **No entity-dedup on New Payment:** dedup is unnecessary because the only path that could
  double-fire (Stripe webhook retry) is already idempotent before the payment insert. The global
  payment fires exactly once (parent), never per allocation.
- **Daily "skip when empty" = composer returns null**, which the runner already treats as skip.
  A quiet day logs at information level; only a missing recipient logs a warning.
- **Daily cycle key** `daily_brief:{yyyy-MM-dd}` guarantees at most one brief per business per
  day, even across multiple polls.
- **Overdue is derived** (balance > 0 AND due date passed), credit-note-aware — see
  `.kiro/steering/financial-conventions.md`. A quiet-day test must not use an invoice that only
  looks overdue by a stale persisted status or an un-subtracted credit note.
- Event alerts (New Payment) intentionally **bypass working-hours deferral** (`ScheduledForUtc =
  now`); the Daily Brief respects the business time zone for its send time.
