# Requirements: Digital Assistants — Scheduled-Scan Engine + Weekly Owner Digests (Group 3)

## Introduction

Phase 1 delivered the Digital Assistants **notification spine** (transactional outbox + an
in-process dispatcher) and the first assistant, **Thank-You**, which is *event-driven*: a
business event (a payment) fires a producer that enqueues an outbox row in the same
transaction. That pattern cannot express assistants whose trigger is a **clock**, not an
event — e.g. "every Monday morning, email the owner a summary of what they're owed."

This spec (Group 3 in the Digital Assistants catalog) introduces the second structural
category of assistant — **scheduled scan / digest** — by building a reusable
**scheduled-scan engine** and shipping the first two assistants on top of it:

1. **Weekly Outstanding Balance Digest** — an owner-facing weekly email summarising
   receivables (what customers owe) **and** upcoming payables (supplier payments coming due,
   by their target payment date). A cash-flow-at-a-glance email.
2. **Weekly Financial Snapshot** — an owner-facing weekly email summarising the business's
   financial position (collected, outstanding, expenses, net) over a configurable set of
   figures.

Both are **owner-facing** (RecipientKind = `PortalUser`), reuse the Phase 1 outbox spine for
delivery, and are the foundation the later scheduled assistants (Quotation Follow-Up, daily
brief, VAT/compliance reminders, Recurring Invoices, Payroll) all depend on.

**Reference:** the full assistant catalog and the two-category model are documented in
`.kiro/docs/features/digital-assistants-catalog.md`. The Phase 1 spine is in
`.kiro/specs/digital-assistants-notifications/`.

### Product decisions (confirmed with the product owner)

1. **Separate scheduler service** — a sibling hosted service, NOT an extension of the existing
   dispatcher. The dispatcher's job is to *drain* the outbox; the scheduler's job is to
   *decide on a cadence what to enqueue*. Keep them separate.
2. **Per-business schedule** — each business configures its own send day + time, interpreted
   in the **business's time zone** (`Business.TimeZoneId`, already present).
3. **Always send** — both digests send **every** cycle, even on a quiet week. The email is
   also a **heartbeat** that proves the delivery chain (SMTP → dispatcher → templates) is
   healthy; a missing Monday email is itself a signal. Quiet weeks get a warm, positive
   "all clear" variant rather than an empty table, and always carry a small "this week at a
   glance" line so the email always has value.
4. **Configurable recipient** — per business, the digest may be sent to someone other than the
   owner (e.g. the accounting department). Defaults to the business owner's email.
5. **Financial Snapshot content is configurable** — the owner chooses which figures appear
   (with a sensible default set).
6. **Default ON** — both digests default enabled for eligible (Professional+) businesses.
7. **Plan gating** — same `digital_assistants` module (Professional+) as Thank-You.
8. **New AssistantType seeds** — `weekly_outstanding_digest` and `weekly_financial_snapshot`,
   both RecipientKind = `PortalUser`, IsCustomerFacing = 0, surfaced as cards on the existing
   `/Assistants` page with a day/time + recipient config (instead of working-hours).

### Known data constraints (verified against the codebase)

- **Payables have no "paid" flag.** `Purchase` tracks only `IsCancelled` (plus
  `TargetPaymentDate` / `SupplierDueDate`). There is no payment-status on a purchase. So the
  payables section shows **"supplier payments coming due"** (non-cancelled purchases whose
  effective due date `COALESCE(TargetPaymentDate, SupplierDueDate)` is within a window),
  **not** "unpaid payables." The email wording must reflect this honestly.
- **No per-business recipient store today.** The existing semicolon-delimited recipient config
  (`AdminNotificationRecipients`) is platform-wide `PlatformConfig`. A per-business digest
  recipient is new (added to the per-business assistant setting).
- **No "this week" P&L period type.** `PnlService.GetSummaryAsync` supports `Custom` with
  explicit dates — the snapshot uses a 7-day custom range.
- **Owner email** is resolved from the Membership DB via `UserBusiness` where `IsOwner` and
  `IsActive` (the pattern used by `InvoiceEmailService`).

## Glossary

- **Scheduler:** the new in-process hosted background service that, on an interval, finds
  businesses whose digest is due (per their configured day/time in their time zone) and
  enqueues the composed digest into the notification outbox.
- **Digest:** a composed, owner-facing summary email (Outstanding Balance or Financial
  Snapshot) rendered at enqueue time and stored self-contained on the outbox row.
- **Cycle / period key:** the identifier of one scheduled occurrence (e.g. ISO week + year for
  a weekly digest) used for idempotency so a digest is enqueued at most once per cycle.
- **Effective due date (payables):** `COALESCE(Purchase.TargetPaymentDate,
  Purchase.SupplierDueDate)` — the established convention for when a purchase is due to be paid.
- **Recipient:** the configured destination address for a business's digest — defaults to the
  business owner, optionally overridden (e.g. accounting).

## Requirements

### Requirement 1 — Scheduled-scan engine (sibling hosted service)

**User Story:** As the platform, I want a background scheduler that wakes on an interval and
enqueues due digests, so that owner-facing summaries are produced on a cadence without any
user action.

#### Acceptance Criteria

1. THE system SHALL provide a hosted background service, separate from the Phase 1
   notification dispatcher, that runs on a configurable poll interval and does not block
   application startup.
2. WHEN the scheduler runs THEN it SHALL, per enabled business, determine whether a digest is
   due for the current cycle based on the business's configured send day + time interpreted in
   the business's time zone.
3. WHEN a digest is due for a business THEN the scheduler SHALL compose the digest and enqueue
   exactly one outbox message for it, reusing the Phase 1 outbox/producer mechanism.
4. THE scheduler SHALL be resilient: a failure composing or enqueuing one business's digest
   SHALL NOT prevent other businesses' digests from being processed in the same run (per-item
   try/catch), and a fatal error SHALL be caught so the service continues on the next interval.
5. THE scheduler SHALL be disable-able via configuration (mirroring the dispatcher's
   enable/disable flag), returning without work when disabled.
6. THE scheduler SHALL scope every read and enqueue by explicit `BusinessId`.

### Requirement 2 — Per-business schedule (day, time, time zone)

**User Story:** As a business owner, I want to choose which day and time my weekly digest
arrives, in my own time zone, so that it lands when it is useful to me.

#### Acceptance Criteria

1. THE system SHALL store, per business per digest assistant, a send **day-of-week** and a send
   **time-of-day**.
2. THE scheduler SHALL interpret the configured time in the business's time zone
   (`Business.TimeZoneId`), falling back to a configured default time zone when the business
   has none (consistent with the Phase 1 schedule resolver).
3. WHEN a business has not configured a day/time THEN the system SHALL apply a sensible default
   (Monday, 08:00 business-local).
4. THE system SHALL enqueue a digest for a given cycle **at most once** — a re-run of the
   scheduler within the same cycle SHALL NOT produce a duplicate (see Requirement 6).
5. WHEN the configured send time for the current cycle has already passed at the moment the
   business first becomes eligible (e.g. newly enabled mid-week) THEN the system SHALL NOT
   back-fill missed cycles; it SHALL begin from the next due cycle.

### Requirement 3 — Weekly Outstanding Balance Digest (receivables + payables)

**User Story:** As a business owner, I want a weekly email showing what I am owed and what
supplier payments are coming due, so that I have a cash-flow picture without opening the app.

#### Acceptance Criteria

1. THE digest SHALL include a **receivables** section summarising outstanding customer
   invoices: total outstanding, count of outstanding invoices, total overdue vs not-yet-due,
   and a list of the top N outstanding invoices (by amount and/or age) with customer name,
   invoice number, due date, and outstanding balance.
2. Outstanding balance SHALL be computed as `Invoice.TotalAmount − SUM(valid Payments)` over
   issued (`InvoiceStatusTypeId = 2`), non-deleted invoices, reusing the existing receivables
   computation.
3. THE digest SHALL include a **payables** section summarising upcoming supplier payments:
   non-cancelled purchases whose effective due date `COALESCE(TargetPaymentDate,
   SupplierDueDate)` falls within a configured window, showing supplier name, description,
   amount, and effective due date, with an overdue / due-soon / upcoming indicator.
4. THE payables section SHALL be labelled as "supplier payments coming due" (or equivalent) and
   SHALL NOT claim to represent "unpaid" payables, because purchase paid-state is not tracked.
5. WHEN there are no outstanding receivables AND no upcoming payables THEN the digest SHALL
   still be sent as an **"all clear"** positive-tone message (Requirement 5), not suppressed.
6. All figures SHALL be scoped to the business and formatted using the business's currency
   symbol (`BusinessProfile.CurrencySymbol`).

### Requirement 4 — Weekly Financial Snapshot (configurable figures)

**User Story:** As a business owner, I want a weekly at-a-glance summary of my finances, with
control over which figures appear, so that the email matches what I care about.

#### Acceptance Criteria

1. THE snapshot SHALL support a set of figures including (at minimum): amount collected in the
   period, amount invoiced/new-outstanding in the period, total outstanding to date, and
   period expenses / net position.
2. Period figures over a rolling window SHALL reuse the existing P&L computation
   (`PnlService.GetSummaryAsync`) with an explicit `Custom` date range; to-date aggregates
   (e.g. total outstanding) SHALL reuse the existing dashboard KPI computation.
3. THE business SHALL be able to configure **which** figures are included in its snapshot; the
   system SHALL provide a sensible default selection when the business has not customised it.
4. THE snapshot SHALL be sent **every** cycle regardless of activity (Requirement 5), including
   quiet weeks, as a delivery heartbeat.
5. All figures SHALL be scoped to the business and currency-formatted.

### Requirement 5 — Always-send heartbeat + all-clear variant

**User Story:** As a business owner, I want to receive my digest every week even when there is
nothing notable, so that I stay engaged and know the system is working.

#### Acceptance Criteria

1. THE system SHALL send each enabled digest on **every** due cycle, including cycles with no
   data to report; it SHALL NOT suppress a digest for being empty.
2. WHEN a digest has no notable data THEN the system SHALL render a positive-tone "all clear"
   variant that acknowledges the quiet period and still includes a short "this week at a
   glance" line (e.g. invoices issued, payments received) so the email always carries value.
3. THE always-send behaviour SHALL serve as a delivery heartbeat: the absence of a scheduled
   digest is a signal that the delivery chain may be unhealthy. *(Limitation: a received digest
   proves the dispatcher + SMTP path works, but does NOT self-monitor the scheduler — if the
   scheduler is down, nothing is enqueued and nothing alerts. Scheduler-liveness monitoring is
   out of scope for this spec.)*

### Requirement 6 — Idempotency (at most one digest per business per cycle)

**User Story:** As the platform, I want each digest enqueued at most once per cycle, so that a
scheduler re-run, overlap, or restart never emails a business twice for the same period.

#### Acceptance Criteria

1. THE system SHALL derive a deterministic **cycle key** per digest occurrence (e.g.
   `{assistantKey}:{ISOYear}-W{ISOWeek}` for weekly digests) and store it verbatim on the
   enqueued message. *(An exact stored cycle key is required — NOT a hash of the key into an
   integer id, which is not collision-safe at the per-business/per-assistant scope and could
   silently drop a weekly digest. See design "Outbox schema change — CycleKey".)*
2. WHEN the scheduler attempts to enqueue a digest THEN it SHALL guard against a prior enqueue
   for the same (business, assistant, cycle key) by an **exact cycle-key match**, enqueuing only
   if no non-failed message exists.
3. THE idempotency guard SHALL be evaluated in a way that is safe against concurrent scheduler
   passes (the final check occurs at insert time, mirroring the Phase 1 producer's
   insert-time re-check).
4. A previously **failed** digest for a cycle SHALL NOT permanently block a re-enqueue for that
   cycle (consistent with the Phase 1 dedup semantics that failed messages do not block).

### Requirement 7 — Configurable recipient (default owner, optional accounting)

**User Story:** As a business owner, I want to choose who receives the digest, so that (for
example) my accountant gets it instead of or in addition to me.

#### Acceptance Criteria

1. THE system SHALL resolve the digest recipient per business, defaulting to the business
   owner's email (`UserBusiness.IsOwner AND IsActive`, via the Membership DB).
2. THE business SHALL be able to configure an **override recipient** per digest assistant. When
   an override is set, the digest SHALL be sent to the override's **primary address**; when the
   business also elects "include owner," the owner's address SHALL be added as a CC on the
   **same** digest message (one message per cycle — see Requirement 6). *(v1 scope: a single
   primary recipient plus an optional owner CC on one outbox row. True fan-out — a separate
   message per address — is explicitly deferred; if added later, the per-cycle idempotency key
   must incorporate a recipient discriminator.)*
3. WHEN no recipient can be resolved (no owner email and no override) THEN the system SHALL NOT
   enqueue a digest and SHALL log a warning (fail-safe, no crash).
4. Configured recipient addresses SHALL be validated for basic email format before use; the
   override field SHALL accept the existing delimited-list convention, and v1 SHALL use the
   first valid address as the primary recipient.

### Requirement 8 — Per-business enablement + defaults

**User Story:** As a business owner, I want each digest to be on by default but easy to turn
off, so that I get value immediately without configuration but retain control.

#### Acceptance Criteria

1. Each digest assistant SHALL be **enabled by default** for eligible businesses (absence of an
   explicit setting = enabled), consistent with the Phase 1 default-enabled pattern.
2. THE business SHALL be able to disable a digest assistant; WHEN disabled the scheduler SHALL
   NOT enqueue that digest for that business.
3. THE per-business settings for a digest SHALL include: enabled, send day, send time, included
   figures (snapshot), and recipient override.
4. WHEN any single setting is changed (e.g. a quick enable/disable toggle) THEN the system SHALL
   preserve all other configured settings for that (business, assistant) row; a partial update
   SHALL NOT overwrite unrelated columns (day, time, recipient, figures) with defaults or NULL.
   *(Rationale: the shared upsert writes all mapped columns, so every writer must read the
   existing row and carry forward the fields it is not changing — otherwise toggling a digest
   off/on would silently erase its schedule and recipient. Data-loss guard.)*

### Requirement 9 — AssistantType registration + Digital Assistants UI

**User Story:** As a business owner, I want the digests to appear on my Digital Assistants page
alongside Thank-You, so that I manage all assistants in one place.

#### Acceptance Criteria

1. THE system SHALL seed two new `[notification].[AssistantType]` rows —
   `weekly_outstanding_digest` and `weekly_financial_snapshot` — each with RecipientKind =
   `PortalUser` and IsCustomerFacing = 0, using the explicit-Id idempotent MERGE seed pattern.
2. THE `/Assistants` page SHALL render a card for each digest with its persona, an on/off
   toggle, a **send day + time** control (instead of the working-hours control used by
   customer-facing assistants), a **recipient** field, and (for the snapshot) the **included
   figures** selection, plus the existing activity-log expander.
3. Saving a digest's settings SHALL follow the established AJAX pattern (BlockUI → request →
   BlockUI hide → SweetAlert2 result), and toggling SHALL follow the quick-op pattern.
4. THE digest activity log SHALL show, per cycle, the recipient, subject, status, and time,
   reusing the existing outbox-backed activity-log query.

### Requirement 10 — Plan gating + tenancy

**User Story:** As the platform, I want the digests gated to the correct plan and strictly
tenant-scoped, so that only entitled businesses receive them and no data crosses tenants.

#### Acceptance Criteria

1. THE digest assistants SHALL be gated behind the `digital_assistants` module (Professional+),
   consistent with Thank-You.
2. THE scheduler SHALL only enqueue digests for businesses whose plan includes
   `digital_assistants`. *(The scheduler runs with no HTTP request context, so this check SHALL
   use a tenant-less plan lookup that takes an explicit `businessId` — not the existing
   `HttpContext`/current-tenant-based entitlement path, which cannot run in a background
   service.)*
3. Every query composing a digest SHALL be scoped by explicit `BusinessId`; no digest SHALL
   contain any other business's data.
4. THE digest emails SHALL be sent from the platform notifications account, consistent with the
   Phase 1 email-sender configuration, and SHALL NOT include a customer-facing branded footer
   (these are internal owner emails).

## Out of scope (for this spec)

- Quotation Follow-Up, the daily brief, VAT/compliance reminders, payment-reminder migration,
  Recurring Invoices, and the Payroll Assistant — all later groups that will **consume** the
  scheduled-scan engine built here.
- Aging-bucket (30/60/90) receivables analytics beyond a simple overdue/not-yet-due split
  (no such service exists today; a single overdue cut is available).
- A dedicated per-business configuration store beyond the fields added to the assistant
  setting.
- Any change to the Phase 1 dispatcher, Thank-You assistant, or the existing Payment Reminders
  module.
