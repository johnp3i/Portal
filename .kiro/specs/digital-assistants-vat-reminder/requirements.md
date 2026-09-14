# Requirements: VAT Period Due Reminder Assistant (Group 4)

## Introduction

The **VAT Period Due Reminder** is an owner-facing, scheduled (Category B) Digital Assistant. It
emails the business owner as a VAT filing deadline approaches, so a return is never missed, and —
critically — it includes the **approximate net VAT payable** for that period so the owner knows
roughly what they'll owe (or be refunded), not just that a deadline is near.

It reuses the existing scheduled-scan engine (`ScheduledDigestRunner`), the notification spine
(outbox + dispatcher), per-business settings, plan gating, timezone resolution, and the existing
VAT deadline-derivation logic already present in `AttentionItemBuilder`. It introduces one new
composer, one new AssistantType seed (Id 6), a **period-scoped cycle key** (so it fires **once per
period**, not daily), a small **public per-period VAT figure accessor**, and a **per-business
notice lead-time** setting.

**Product decisions locked (from scoping):**
- **A — Once per period.** Exactly one reminder per VAT period per business (no escalating
  multi-fire). Avoids nagging.
- **B — Per-business lead time.** The "remind me N days before the deadline" window is a
  per-business setting, defaulting to the global `NotificationOptions.VatDeadlineNoticeDays` (21).

**Plan gating:** same `digital_assistants` module (Professional+). No new plan key.

---

## Glossary

- **Period** — a `[vat].[VatSubmissionPeriod]` row (`PeriodStartDate`, `PeriodEndDate`,
  `PeriodLabel`). No stored filing deadline exists.
- **Derived deadline** — `PeriodEndDate + VatFilingOffsetDays` (global, default 40; Cyprus-tuned).
- **Unsubmitted** — the period has **no** `VatSubmission` row, **or** one with `IsSubmitted = 0`.
- **Net VAT payable** — `TotalOutputVat − TotalInputVat` (credit-note-aware), from the authoritative
  VAT figure calculation.
- **Notice window** — `[0 .. leadDays]` days before the derived deadline, during which the reminder
  becomes eligible.

---

## Requirements

### Requirement 1 — Scheduled evaluation on the existing engine

**User Story:** As the platform, I want the VAT reminder evaluated on the existing scheduled
runner, so no new scheduling infrastructure is added.

#### Acceptance Criteria
1. WHEN the scheduled runner polls THEN the system SHALL evaluate the VAT reminder for each
   plan-entitled, module-entitled business, exactly as it does the other scheduled assistants.
2. The system SHALL add the VAT reminder's AssistantKey to the runner's evaluated-assistant set
   (the runner's key list is hard-coded — composer registration alone is insufficient).
3. The reminder SHALL be evaluated on a **daily** cadence (checked each day), but SHALL SEND at
   most **once per period** (Requirement 4), not once per day.
4. WHEN a business is not entitled to the `digital_assistants` module THEN the system SHALL NOT
   evaluate or send the reminder.
5. Evaluation and send timing SHALL respect the business's time zone (reuse the runner's existing
   business-local-now resolution).

### Requirement 2 — Deadline detection (reuse existing logic)

**User Story:** As an owner, I want to be reminded only when a real filing deadline is
approaching, not months out and not after I've already filed.

#### Acceptance Criteria
1. The system SHALL identify the relevant period as an **unsubmitted** period whose **derived
   deadline** falls within the notice window: `0 ≤ (deadline − today) ≤ leadDays`.
2. The derived deadline SHALL be `PeriodEndDate + VatFilingOffsetDays`, consistent with the
   existing `AttentionItemBuilder` VAT line (no divergent deadline math).
3. WHEN the covering-or-recent unsubmitted period is already submitted (a `VatSubmission` with
   `IsSubmitted = 1`) THEN the system SHALL NOT send a reminder for it.
4. WHEN no unsubmitted period is within the notice window today THEN the composer SHALL return null
   (an expected skip; logged at information level, not a warning).
5. The scan SHALL catch a period whose window has **ended** but whose derived deadline is still
   approaching (i.e. it must not only look at the period covering *today* — it must consider
   recently-ended unsubmitted periods whose deadline is imminent).

### Requirement 3 — Approximate net VAT payable in the email

**User Story:** As an owner, I want the reminder to tell me roughly how much VAT I'll owe, so I can
plan the payment — not just that a deadline exists.

#### Acceptance Criteria
1. The email SHALL include the **approximate net VAT payable** for the reminded period.
2. The figure SHALL be computed credit-note-aware, consistent with the authoritative VAT
   calculation (`VatSubmissionService`'s figure logic), NOT a divergent formula.
3. WHEN an unsubmitted `VatSubmission` row exists for the period THEN the system SHALL use its
   persisted `NetVatPayable`; ELSE the system SHALL compute the figure in-memory. (Persisted-then-
   compute, mirroring `GetPreSubmissionChecklistAsync`.)
4. The figure SHALL be labelled as an **estimate** (e.g. "approximately, as it stands today"),
   because late invoices/purchases/credit notes can still move it before filing.
5. The wording SHALL follow the existing sign convention: `> 0` → "tax owed"; `< 0` → "refund due"
   (absolute value); `= 0` → "no payment expected".
6. The email SHALL also state the period label and the derived deadline date, and how many days
   remain.

### Requirement 4 — Once per period (idempotency)

**User Story:** As an owner, I want at most one VAT reminder per period, so I'm not pinged daily
throughout the notice window.

#### Acceptance Criteria
1. The system SHALL use a **period-scoped cycle key** (e.g. `vat_period_due_reminder:period-{periodId}`),
   NOT the daily/weekly date-based key, so the reminder de-dupes per period across the whole notice
   window.
2. WHEN a non-Failed outbox row already exists for `(business, assistant, periodCycleKey)` THEN the
   system SHALL NOT enqueue a second reminder for that period.
3. A reminder that previously **Failed** SHALL be eligible to be re-produced (consistent with
   `ExistsForCycleAsync`, which excludes Failed rows).
4. The cycle key SHALL be stable across days for the same period, so re-evaluation the next day
   within the window produces no duplicate.

### Requirement 5 — Per-business notice lead time

**User Story:** As an owner, I want to choose how many days before the deadline I'm reminded.

#### Acceptance Criteria
1. The system SHALL support a per-business notice lead time (days before the deadline).
2. WHEN no per-business value is set THEN the system SHALL fall back to the global
   `NotificationOptions.VatDeadlineNoticeDays` (default 21).
3. The lead time SHALL be validated to a sensible range (e.g. 1..90 days) on save.
4. The lead time SHALL be editable from the assistant's card on `/Assistants`.

### Requirement 6 — Recipient, enable/disable, timing

**User Story:** As an owner, I want to control who receives it, whether it's on, and what time of
day it sends.

#### Acceptance Criteria
1. The reminder SHALL default to the business **owner** (`IOwnerEmailResolver`), with an optional
   recipient override and the shared "also CC the owner" toggle (reuse `IDigestRecipientResolver`).
2. WHEN no owner email resolves and no override is set THEN the composer SHALL return null and log a
   **warning** (distinct from the information-level "nothing due today" skip).
3. The reminder SHALL send at the business-local **send time** (reuse `SendTimeLocal`; default
   08:00). Day-of-week SHALL be irrelevant (`SendDayOfWeek` nulled, like the Daily Brief).
4. WHEN the assistant is disabled for a business THEN no reminder SHALL be produced.
5. The email SHALL be owner-facing: **no** branded customer footer.

### Requirement 7 — Configuration UI (new card shape)

**User Story:** As an owner, I want a clear card to configure the VAT reminder.

#### Acceptance Criteria
1. The `/Assistants` page SHALL render a VAT reminder card with: on/off toggle, recipient control
   (owner + optional override + owner-CC toggle), a **send-time** input, a **notice lead-time
   (days)** input, and an activity-log expander.
2. The card SHALL NOT show a day-of-week selector or a figures selector.
3. `AxPostSaveAssistantSettings` SHALL persist the lead time and send time, validate the lead time
   range and the recipient list, and preserve all untouched settings (the upsert writes all
   columns).
4. Plan-gated identically to the other assistants (soft-gate for non-entitled plans).

### Requirement 8 — Seeding & plan gating

#### Acceptance Criteria
1. A migration SHALL seed AssistantType **Id 6**, key `vat_period_due_reminder`, name "VAT Period
   Due Reminder", `RecipientKind = 'PortalUser'`, `IsCustomerFacing = 0`, via idempotent explicit-Id
   MERGE with a post-merge verification PRINT (matching migrations 198/201).
2. Any new per-business column (notice lead time) SHALL be added via an additive, nullable/defaulted
   migration (matching migration 199).
3. No new plan module key SHALL be introduced (rides `digital_assistants`).

### Requirement 9 — Reliability & consistency

#### Acceptance Criteria
1. All new data access SHALL follow repository conventions (async, typed, `try/catch (Exception ex)`
   rethrow) and be tenant-less/`businessId`-explicit (the scan runs without tenant context).
2. The reminder SHALL reuse the existing dispatcher for send/retry/failure — no new send path.
3. The VAT figure accessor added for the composer SHALL NOT alter existing VAT screen behaviour
   (the current private calc and its callers stay behaviourally unchanged).
4. Both backend projects SHALL build with 0 errors; the change SHALL not regress the other
   scheduled assistants (weekly digests + Daily Brief) or the existing `AttentionItemBuilder` VAT
   line.
