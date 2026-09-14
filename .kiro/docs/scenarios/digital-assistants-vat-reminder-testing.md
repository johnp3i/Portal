# Testing Scenarios: Digital Assistants — VAT Period Due Reminder (Group 4)

The **VAT Period Due Reminder** is an owner-facing, scheduled (Category B) assistant on the existing
scheduled engine. As a VAT filing deadline approaches, it emails the business owner **once per VAT
period**, including the **approximate net VAT payable**. It reuses the notification spine,
dispatcher, per-business settings, plan gating, timezone resolution, and the derived-deadline logic
already used by the Daily Brief / Weekly Financial Snapshot attention line.

## Key mechanics

- **Deadline is derived, not stored:** `deadline = VatSubmissionPeriod.PeriodEndDate +
  NotificationOptions.VatFilingOffsetDays` (default 40; Cyprus-tuned). Shared with
  `AttentionItemBuilder` via `VatDeadline.For`.
- **Fires when** an **unsubmitted** period's derived deadline is within the notice window:
  `0 ≤ (deadline − today) ≤ leadDays`, where `leadDays` = the per-business `VatNoticeLeadDays` or
  the global `VatDeadlineNoticeDays` (default 21).
- **Unsubmitted** = the period has no `VatSubmission` row, or one with `IsSubmitted = 0`.
- **Once per period:** the outbox row uses a **period-scoped** cycle key
  (`vat_period_due_reminder:period-{periodId}`), so the reminder de-dupes across the whole notice
  window — not once per day. A previously **Failed** send may re-enqueue.
- **Approximate net VAT:** `IVatSubmissionService.GetApproxNetVatPayableAsync(businessId, period)` —
  prefers a persisted (unsubmitted) `VatSubmission.NetVatPayable`, else computes in-memory
  (credit-note-aware). Labelled an estimate.

## Prerequisites

- Run migrations through **204**: `202` seeds AssistantType id 6 `vat_period_due_reminder`
  (`RecipientKind = 'PortalUser'`, `IsCustomerFacing = 0`); `203` adds
  `BusinessAssistantSetting.VatNoticeLeadDays`; `204` makes the outbox cycle index UNIQUE
  (`UX_OutboxMessage_Cycle`) after de-duplicating any existing cycle rows.
- The `Notifications` email account password is set in User Secrets (same as Phase 1).
- Business on **Professional or Enterprise** (`digital_assistants` module).
- Business has an **owner** (`UserBusiness.IsOwner && IsActive`) with an email.
- Business `TimeZoneId` set; the scheduled runner enabled.
- At least one `VatSubmissionPeriod` for the business (created via the VAT UI / period generation).

> Business page: `/Assistants`. SuperAdmin page: `/Admin/Notifications`.

---

## Scenario 1: Card renders as a VAT reminder (send-time + lead-days, no day-of-week)

1. As a Professional/Enterprise user, open `/Assistants`.
2. **Expected:** the **VAT Period Due Reminder** card shows an on/off toggle, a **send time** input,
   a **"Remind me before"** number input (days; shows the global default 21 when unset), a recipient
   control (owner + optional override + owner-CC toggle), and a "View activity log" expander — but
   **no** day-of-week selector and **no** figures selector.

## Scenario 2: Reminder fires in the notice window (happy path — tax owed)

1. Enable the assistant. Set its send time a few minutes ahead (business local).
2. Arrange an **unsubmitted** period whose derived deadline (`PeriodEndDate + 40d`) is, say, 10 days
   out, with recorded invoices so output VAT > input VAT (net payable positive).
3. When the send time passes, on the next runner poll **Expected (DB):** one
   `[notification].[OutboxMessage]` row, `Status = Pending`, `AssistantTypeId` = the
   `vat_period_due_reminder` id, owner email, subject like "VAT for {period} is due soon", `CycleKey
   = vat_period_due_reminder:period-{id}`.
4. Dispatcher sends it; the owner receives an owner-styled email (no customer footer) stating the
   period, the deadline date, days remaining, and **"Estimated VAT to pay: {sym}{amount}
   (approximate, as it stands today)."**
5. Activity log shows the send as "Sent".

## Scenario 3: Estimate sign — refund and none

1. Arrange a period where input VAT > output VAT (net negative) → **Expected:** "Estimated VAT
   refund: {sym}{abs amount} (approximate…)".
2. Arrange a period where output = input (net zero) → **Expected:** "No payment expected
   (approximate…)".

## Scenario 4: Once per period — no daily repeat

1. After a reminder has sent for a period (Scenario 2), let the runner poll again the **next day**
   while the deadline is still inside the notice window.
2. **Expected:** **no** second email — the period cycle key already has a non-Failed row, so the
   scan skips that period. The owner is reminded once per period, not daily.

## Scenario 5: Submitted period → silence

1. For a period that would otherwise be in-window, mark its `VatSubmission` as submitted
   (`IsSubmitted = 1`) via the VAT UI.
2. **Expected:** no reminder — the unsubmitted-period scan excludes it.

## Scenario 6: Outside the window → nothing

1. A period whose derived deadline is **beyond** the lead window (e.g. 30 days out with the default
   21-day lead) → **Expected:** no reminder (info-level "no un-reminded period in window" skip, not
   a warning).
2. A period whose deadline has **passed** (deadline < today) → **Expected:** no reminder.

## Scenario 7: Ended-but-imminent period is caught

1. Arrange an **unsubmitted** period whose window has already **ended** (PeriodEndDate in the past)
   but whose derived deadline (`+40d`) is still, say, 5 days out.
2. **Expected:** the reminder fires — the scan uses `GetUnsubmittedPeriodsFromAsync` + per-period
   derived deadline, so it does not only look at the period covering *today*.

## Scenario 8: Per-business lead time overrides the global default

1. Set the card's "Remind me before" to **5** days and Save.
2. A period 10 days from its deadline → **Expected:** no reminder yet (10 > 5).
3. When it reaches 5 days out → **Expected:** reminder fires.
4. Clear the field (empty) → **Expected:** falls back to the global `VatDeadlineNoticeDays` (21).
5. Saving a value outside 1..90 → **Expected:** validation error, nothing saved.

## Scenario 9: No owner email → warning, no send

1. Temporarily clear the owner's email (or the owner `UserBusiness` row) so no recipient resolves,
   with an in-window period present.
2. **Expected:** no email; a **warning**-level log ("no resolvable recipient"), distinct from the
   information-level quiet skip.

## Scenario 10: Disabled / non-entitled plan

1. Toggle the assistant **off** → across the send time, no reminder regardless of due periods.
2. On a plan without `digital_assistants` → the `/Assistants` soft-gate applies; nothing is
   evaluated or sent.

## Scenario 11: Two in-window periods — no starvation

1. Arrange two unsubmitted periods both inside the notice window (unusual, but possible around
   period generation).
2. **Expected:** one poll reminds the **most urgent** (earliest deadline); a subsequent poll reminds
   the **second** (it has no cycle row yet). The earlier period's existing cycle row does not shadow
   the second — each period gets exactly one reminder.

## Scenario 12: Concurrency / retry hardening

1. (Hard to force manually.) Two overlapping scheduler passes for the same period → **Expected:**
   exactly one outbox row; the losing insert hits the UNIQUE `UX_OutboxMessage_Cycle` and
   `DigestEnqueuer` treats it as already-enqueued (returns false, logged at information level).
2. A reminder that reached `Failed` (SMTP misconfigured) → after fixing SMTP, a later poll may
   re-enqueue it (the exists-check excludes Failed).

---

## Notes

- Deadline offset (`VatFilingOffsetDays`) and the fallback notice window
  (`VatDeadlineNoticeDays`) are global config; only the per-business notice lead time is
  overridable. Multi-jurisdiction deadline rules are out of scope.
- The estimate can move before filing (late invoices/purchases/credit notes); the email says so.
- The private `VatSubmissionService.ComputeSubmissionFiguresAsync` is unchanged — the new accessor
  is an additive read-only wrapper, so existing VAT screens are unaffected.
- Send timing respects the business time zone (runner `IsDue`); the deadline-day arithmetic uses the
  UTC date (whole-day granularity), so a few hours of skew never changes `daysUntil`.
