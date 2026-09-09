# Requirements: Digital Assistants — Owner Alerts, Phase 4a (Daily Brief + New Payment Received)

## Introduction

Group 4 of the Digital Assistants catalog is "owner attention & alerts" — a set of owner-facing
assistants. It is split into two phases. **Phase 4a** (this spec) delivers the two lowest-risk,
highest-reuse assistants:

1. **Daily Brief** — a once-a-day email to the business owner summarising what needs attention
   today (overdue invoices, oldest unpaid, supplier payments due, quotations awaiting a
   response, an approaching VAT deadline). It is a *scheduled* assistant that reuses the Group 3
   scheduled engine and the attention-item logic already built for the Weekly Financial
   Snapshot — differing mainly in cadence (daily) and framing (today-focused).

2. **New Payment Received** — an *event* assistant that emails the business owner when a payment
   is recorded, mirroring the existing Thank-You producer but owner-facing.

**Phase 4b** (separate spec, later) covers the remaining Group 4 assistants — VAT period due
reminder, Compliance filing due reminder, and Lead-Task reminder — which need product decisions
(avoiding double-notification with the digests) and data investigation first.

**Reference:** the catalog and category model are in
`.kiro/docs/features/digital-assistants-catalog.md`. The spine is
`.kiro/specs/digital-assistants-notifications/`; the scheduled engine + digests are
`.kiro/specs/digital-assistants-scheduled-digests/`.

### Product decisions (confirmed with the product owner)

1. **Daily Brief skips empty days.** Unlike the weekly digests (which always send as a
   heartbeat), a *daily* email must NOT send on days with nothing to report — daily all-clear
   emails become noise. When there is nothing worth flagging, no email is sent that day.
2. **Daily Brief content** uses the **same** attention-item set as the Weekly Financial Snapshot
   (overdue, oldest unpaid, supplier payments due, quotes awaiting, VAT deadline), via a shared
   builder — the same items, delivered daily. Keeping the two emails consistent is preferred over
   a bespoke daily filter.
3. **New Payment Received fires for all recorded payments** (manual, Stripe card, and global/
   unallocated), owner-facing — owners generally want to know money landed. No minimum-amount
   threshold in v1.
4. **Both are owner-facing** (`RecipientKind = PortalUser`), reuse the notification outbox spine
   for delivery, and are gated behind the `digital_assistants` module (Professional+).
5. **Both default ON** for eligible businesses (consistent with the existing assistants).

### Known reuse & generalization points (verified against code)

- The scheduled engine (`ScheduledDigestRunner`) currently supports only a **weekly** cadence
  (`IsDue` uses `SendDayOfWeek`; `DigestCycleKey` has only `Weekly`). A **daily cadence** must
  be added: a `DigestCycleKey.Daily` variant and a daily branch in the due calculation.
- The attention-item assembly is currently **private** to `FinancialSnapshotComposer`
  (`BuildAttentionItemsAsync`). It must be **extracted into a shared service** so the Daily
  Brief reuses it rather than duplicating.
- There are **three** payment-recording paths: `PaymentService.RecordPaymentAsync` (manual),
  `StripeConnectService.HandleCheckoutCompletedAsync` (Stripe card), and
  `PaymentService.RecordGlobalPaymentAsync` (global/unallocated — has **no** notification hook
  today). All three must enqueue the owner alert.
- The owner email is resolved from the Membership DB via `UserBusiness.IsOwner && IsActive`
  (the same query `DigestRecipientResolver` uses). The producer must read the Membership DB for
  this — the payment paths currently touch only the Portal DB.
- Composer returning `null` already means "skip, no enqueue" in the runner — this is how the
  Daily Brief expresses "nothing to report today."

## Glossary

- **Daily Brief:** the once-a-day owner email of today's attention items. Scheduled (Category B).
- **New Payment Received:** the event-driven owner email fired when a payment is recorded
  (Category A).
- **Attention item:** one plain "needs your attention" line (overdue, oldest unpaid, payables
  due, quotes awaiting, VAT deadline), assembled by the shared attention-item builder.
- **Cycle key:** the per-occurrence idempotency key stored on the outbox row. Weekly digests use
  an ISO-week key; the Daily Brief uses a per-day key.

## Requirements

### Requirement 1 — Daily cadence in the scheduled engine

**User Story:** As the platform, I want the scheduled engine to support a daily cadence
alongside the weekly one, so that a daily assistant runs once per business per day.

#### Acceptance Criteria

1. THE scheduled engine SHALL support a **daily** cadence: an assistant configured as daily is
   due once per business per calendar day (business-local), at the business's configured send
   time, independent of day-of-week.
2. THE system SHALL derive a **daily cycle key** (e.g. `{assistantKey}:{yyyy-MM-dd}` in
   business-local date) so the existing outbox cycle-dedup guarantees at most one send per
   business per day.
3. THE daily due calculation SHALL use the business's `SendTimeLocal` (default 08:00) and ignore
   `SendDayOfWeek`; it SHALL fire once the business-local time is at or past the send time for
   the current day, with no back-fill of missed days.
4. THE existing weekly digests SHALL be unaffected by the addition of the daily cadence.
5. THE daily assistant SHALL be resilient per-business (a failure for one business SHALL NOT stop
   others), consistent with the existing runner.

### Requirement 2 — Shared attention-item builder

**User Story:** As a developer, I want the "needs your attention" assembly logic in one place,
so that the Daily Brief and the Weekly Financial Snapshot produce consistent items without
duplication.

#### Acceptance Criteria

1. THE attention-item assembly currently private to the Financial Snapshot SHALL be extracted
   into a shared, injectable service (tenant-less, keyed by explicit `businessId`).
2. THE Financial Snapshot SHALL be refactored to use the shared builder, producing the same
   output it does today (no behavioural change to the weekly email).
3. THE shared builder SHALL expose the individual attention items (overdue, oldest unpaid,
   supplier payments due, quotations awaiting, VAT deadline) so callers can use all or a subset.

### Requirement 3 — Daily Brief content & "skip when empty"

**User Story:** As a business owner, I want a short daily email of what needs my attention
today, and no email on quiet days, so that it stays useful and never becomes noise.

#### Acceptance Criteria

1. THE Daily Brief SHALL include the attention items from the shared builder — the same set as
   the Weekly Financial Snapshot: overdue invoices (count + amount), the oldest unpaid invoice,
   supplier payments coming due, quotations awaiting a response, and an approaching VAT deadline.
2. WHEN there are **no** attention items to report for a business on a given day THEN the Daily
   Brief SHALL **not** be sent that day (the composer returns null → the runner skips).
3. THE Daily Brief SHALL be owner-facing: no customer branded footer; sent to the resolved
   owner/recipient.
4. THE Daily Brief SHALL be scoped to the business and currency-formatted, consistent with the
   digests.
5. Urgent items (e.g. overdue, imminent VAT deadline) SHALL be visually distinguished, as in the
   Snapshot's attention section.

### Requirement 4 — Daily Brief configuration, enablement, recipient

**User Story:** As a business owner, I want to control the Daily Brief like the other assistants
— on/off, send time, recipient — so that it fits how I work.

#### Acceptance Criteria

1. THE Daily Brief SHALL be a seeded `AssistantType` (owner-facing, not customer-facing) and
   appear as a card on the `/Assistants` page.
2. THE Daily Brief SHALL be **enabled by default** for eligible businesses (absence of a setting
   row = enabled).
3. THE business SHALL be able to configure the Daily Brief's **send time** (business-local) and
   **recipient** (default owner, optional override with owner CC), reusing the existing
   per-business assistant-setting fields; the day-of-week control SHALL be hidden/ignored for a
   daily assistant.
4. THE Daily Brief SHALL be gated behind the `digital_assistants` module (Professional+) and
   only enqueue for entitled businesses (tenant-less plan check).
5. THE Daily Brief's activity log SHALL be available via the existing outbox-backed log.

### Requirement 5 — New Payment Received: owner alert on every recorded payment

**User Story:** As a business owner, I want an email when a payment is recorded, so that I know
money has landed without checking the app.

#### Acceptance Criteria

1. WHEN a payment is recorded via the **manual** path (`RecordPaymentAsync`) THEN the system
   SHALL enqueue an owner-facing "payment received" notification, inside the same transaction as
   the payment (transactional outbox), mirroring the Thank-You pattern.
2. WHEN a payment is recorded via the **Stripe card** path
   (`HandleCheckoutCompletedAsync`) THEN the system SHALL enqueue the same owner alert, in the
   same transaction.
3. WHEN a payment is recorded via the **global/unallocated** path (`RecordGlobalPaymentAsync`)
   THEN the system SHALL enqueue the same owner alert (this path has no notification hook today
   and one SHALL be added).
4. THE alert SHALL include the payment amount and, where available, the invoice number and/or
   customer name; the global path may have no invoice, and the alert SHALL still be sent with
   the available details.
5. THE alert SHALL be sent to the business **owner** (resolved via `UserBusiness.IsOwner &&
   IsActive`); WHEN no owner email can be resolved THEN no alert SHALL be enqueued (fail-safe).

### Requirement 6 — New Payment Received: gating, dedup, configuration

**User Story:** As a business owner, I want the payment alert to respect my settings and never
duplicate, so that it is controllable and trustworthy.

#### Acceptance Criteria

1. THE New Payment Received alert SHALL be a seeded `AssistantType` (owner-facing) with a card on
   `/Assistants`, enabled by default, gated behind `digital_assistants`.
2. WHEN the assistant is disabled for a business THEN no alert SHALL be enqueued for that
   business.
3. A single recorded payment SHALL produce at most one alert. This SHALL be achieved by
   **inheriting the existing upstream idempotency** rather than a new entity-dedup: the Stripe
   webhook path is already guarded (webhook event-id check + `checkoutSession.Status ==
   "completed"`, both **before** any payment is inserted), so a retried webhook never reaches the
   payment insert and therefore never enqueues a second alert; the manual and global paths are
   deliberate, non-retried user actions. The producer SHALL therefore set no related-entity
   dedup key. (Rationale: a `paymentId`-keyed dedup would be a no-op — a fresh in-transaction id
   cannot collide — and would not catch the retried-webhook case, which creates a new id.)
4. WHEN a payment is recorded via the **global/unallocated** path (which inserts one parent
   payment plus N child allocation rows) THEN exactly **one** alert SHALL be enqueued, for the
   parent payment (`dto.Amount`), never one per allocation.
5. THE alert enqueue SHALL be resolved/gated **before** the payment transaction and inserted
   **inside** it, keeping the hot-path transaction short (consistent with Thank-You).
6. A failure to enqueue or resolve the alert SHALL NOT roll back or block the payment recording
   (the alert is best-effort relative to the payment; if it cannot be prepared, the payment
   still commits).

### Requirement 7 — Tenancy, delivery, and consistency

#### Acceptance Criteria

1. All new queries SHALL be tenant-less (explicit `businessId`); no assistant SHALL include
   another business's data.
2. Both assistants SHALL deliver through the existing outbox + dispatcher; delivery failures,
   retries, and admin failure-alerting are handled by the existing dispatcher (no new alerting).
3. Both assistants' emails SHALL be owner-facing (no customer branded footer) and rendered
   self-contained on the outbox row at enqueue time.
4. The Daily Brief's attention items SHALL be consistent with the Weekly Snapshot's (same shared
   builder, same overdue definition, same VAT-deadline derivation/config).

## Out of scope (Phase 4b / later)

- VAT period due reminder, Compliance filing due reminder, Lead-Task reminder (Phase 4b).
- A minimum-amount threshold or digest-batching for New Payment Received (v1 sends per payment).
- Any change to the existing Payment Reminders module (Group 5).
- Real-time in-app (SignalR) surfacing of these alerts.
