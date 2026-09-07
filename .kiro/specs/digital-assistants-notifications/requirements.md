# Requirements: Digital Assistants — Notification & Outbox Spine (Phase 1)

## Introduction

Small businesses (1–3 people) lose time on manual, easily-forgotten follow-through:
thanking a customer for a payment, chasing an unanswered quotation, remembering a
lead task. This feature introduces **Digital Assistants** — opt-in automations that a
business enables, that run on the business's behalf, and whose activity is visible in a
log. Each assistant is framed as a helper with a job ("the Thank-You Assistant sends a
courteous payment confirmation the moment a customer pays"), not as infrastructure.

Under the hood, all assistants share one reliable delivery backbone: a **transactional
outbox** plus an **in-process background dispatcher**. When a business event occurs and
the relevant assistant is enabled, the producer writes an outbox message inside the same
database transaction as the event (guaranteeing the notification is never lost even if
email delivery later fails). A hosted dispatcher polls the outbox on an interval, sends
each due message via the existing email sender, records success/failure, retries up to a
limit, and marks persistently-failing messages as failed while alerting administrators.

**Phase 1 scope:** the shared spine + the **Thank-You Assistant** (payment-received
confirmation email) as the first producer. The existing Payment Reminders module is left
untouched; migrating it onto the spine is explicitly out of scope for Phase 1.

**Planned assistants (design-captured, phased after Phase 1):** Quotation Follow-Up and
Lead-Task Reminder (notification assistants), and the **Recurring Invoices Assistant** — a
*scheduled document-generation* assistant that generates (and optionally auto-issues)
invoices on a cadence, optionally attaches a payment link, and notifies the owner. Recurring
Invoices is design-only in this spec (see design.md "Phase 2 — Recurring Invoices
Assistant"); it reuses the notification spine for its owner alert but adds its own schedule
model and generation engine. Its confirmed product decisions: per-schedule auto-issue vs
draft; optional payment link per schedule; composes with the existing auto-payment-link and
reminder pipeline rather than reimplementing it.

> **Full assistant roadmap:** the complete catalog of planned assistants — grouped for
> delivery, and split into the two structural categories (event/state-driven vs. scheduled
> scan/digest) — lives in
> [`.kiro/docs/features/digital-assistants-catalog.md`](../../docs/features/digital-assistants-catalog.md).
> That document also captures the reusable **scheduled-scan / digest engine** that Category B
> assistants (the weekly owner digests, Recurring Invoices, payment-reminder migration, and
> VAT/compliance reminders) all depend on.

This document also covers three supporting capabilities the spine requires: a seeded
**time-zone reference table** (for per-business working-hours scheduling), a configurable
**administrator notification recipients** list (managed by a SuperAdmin), and a small
extension to the email sender (reply-to support + a branded footer on customer-facing
assistant emails).

## Glossary

- **Assistant:** A named, opt-in automation (e.g. Thank-You Assistant). Has a persona, a per-business enable toggle, optional working hours, and a visible activity log.
- **Producer:** Code that reacts to a business event (e.g. payment recorded) and, if the relevant assistant is enabled, writes an outbox message.
- **Outbox message:** A durable row representing one pending notification (recipient, subject/body or a template reference, channel, status, retry count, scheduled-for time).
- **Dispatcher:** The in-process hosted background service that polls the outbox and sends due messages.
- **Working hours:** A per-business window (in the business's time zone) during which customer-facing messages may be sent. Messages generated outside the window are scheduled for the next valid time.
- **Administrator:** A platform SuperAdmin who receives failure alerts and configures recipient lists.

## Requirements

### Requirement 1 — Transactional outbox

**User Story:** As the platform, I want notifications written durably at the moment their triggering event occurs, so that a later email failure never loses a notification and never corrupts the business event.

#### Acceptance Criteria

1. THE system SHALL provide an outbox table in a dedicated `[notification]` schema storing, per message: an id, the owning `BusinessId`, the assistant/service type, recipient email, the resolved subject and HTML body (or a template reference + payload), status, `RetryCount`, `MaxRetries`, `ScheduledForUtc`, `LastError`, `CreatedAtUtc`, and timestamps for sent/failed.
2. WHEN a producer writes an outbox message THEN it SHALL do so within the same database transaction as the business event that triggered it, so the two commit or roll back together.
3. THE outbox message status SHALL be one of: `Pending`, `Sent`, `Failed`.
4. Every outbox table SHALL include `CreatedAtUtc DATETIME NOT NULL DEFAULT GETUTCDATE()` and be tenant-scoped by `BusinessId`.

### Requirement 2 — Background dispatcher

**User Story:** As the platform, I want a background process that reliably delivers pending notifications, so that automation runs without any user being logged in.

#### Acceptance Criteria

1. THE system SHALL run a single in-process hosted background service (a `BackgroundService`) that polls the outbox on a configurable interval (default e.g. 60 seconds).
2. THE dispatcher SHALL select messages where `Status = Pending` AND `ScheduledForUtc <= now`, ordered oldest-first, in bounded batches.
3. FOR each selected message THE dispatcher SHALL attempt delivery via the existing `IEmailSender`, and on success set `Status = Sent` and the sent timestamp.
4. WHEN delivery fails THEN THE dispatcher SHALL increment `RetryCount`, store `LastError`, and leave the message `Pending` for a later attempt — UNLESS `RetryCount >= MaxRetries`, in which case it SHALL set `Status = Failed`.
5. THE dispatcher SHALL process each message in isolation so that one failing message does not stop the batch, following the resilient per-item try/catch convention of the existing `PaymentReminderBackgroundService`.
6. THE dispatcher SHALL be operable across all businesses (it drains the global outbox); per-business gating happens at write time (Requirement 4).
7. THE dispatcher's polling interval and enable/disable SHALL be configurable (matching the existing `PaymentReminders` config pattern).

### Requirement 3 — Administrator failure alerts

**User Story:** As an administrator, I want to be alerted when notifications are persistently failing, so that I can fix a delivery problem before many messages are lost.

#### Acceptance Criteria

1. WHEN outbox messages reach `Failed` status beyond a configurable threshold within a rolling window (e.g. N failures in 24h) THEN THE system SHALL alert administrators.
2. Administrator alerts SHALL be delivered BOTH in-app (real-time via the existing SignalR infrastructure) AND by email.
3. THE administrator email recipient list SHALL be configurable at runtime (stored in the existing `PlatformConfig`), support MORE THAN ONE address, and require no redeploy to change.
4. THE alert SHALL be throttled so a delivery outage produces a summary alert rather than one email per failed message.

### Requirement 4 — Per-business assistant configuration

**User Story:** As a business owner, I want to enable or disable each Digital Assistant and set when it may act, so that automation matches how I want to run my business.

#### Acceptance Criteria

1. THE system SHALL store, per business and per assistant, an enabled/disabled flag (`Is`-prefixed bit) and optional working-hours settings.
2. WHEN a producer runs AND the relevant assistant is disabled for that business THEN NO outbox message SHALL be written.
3. WHEN an assistant is enabled with working hours AND the triggering event occurs outside those hours THEN the outbox message's `ScheduledForUtc` SHALL be set to the next valid time within working hours (computed in the business's time zone); otherwise `ScheduledForUtc` SHALL be now.
4. THE enabled check and the working-hours-to-`ScheduledForUtc` computation SHALL both occur at write time, so the dispatcher remains business-agnostic and time-zone-agnostic.

### Requirement 5 — Time-zone reference data

**User Story:** As the platform, I want each business to have a real time zone, so that working-hours scheduling reflects the business's local time rather than UTC.

#### Acceptance Criteria

1. THE system SHALL provide a seeded reference table (e.g. `[reference].[TimeZone]` or `[notification].[TimeZone]`) of supported time zones (id, display name, IANA/Windows identifier).
2. THE `Business` (or an automation-settings table) SHALL carry a nullable `TimeZoneId` foreign key referencing the time-zone table.
3. WHEN a business has no `TimeZoneId` set THEN working-hours computation SHALL fall back to a sensible default (UTC, or a configured platform default) without error.
4. The reference table SHALL be exempt from the mandatory `CreatedAtUtc` audit column only if it is static seed data per the SQL schema steering; otherwise it SHALL include it.

### Requirement 6 — Email sender extension (reply-to + branded footer)

**User Story:** As the platform, I want customer-facing assistant emails sent from a reputable platform address with an optional reply-to and a subtle brand footer, so that deliverability is protected and satisfied customers can discover the Portal.

#### Acceptance Criteria

1. THE email sender SHALL support an optional reply-to address in addition to the department-based from address (extending the current single-from behaviour).
2. Customer-facing assistant emails SHALL be sent from a dedicated platform email account (a new `EmailDepartmentEnum` value + `EmailAccounts` entry), NOT from the individual business's mailbox, to preserve sender-domain reputation.
3. Customer-facing assistant email templates SHALL include a subtle "Powered by 3 Inventors Business Portal" footer with a link.
4. THE branded footer SHALL be controlled by a per-business setting (default configurable) so a business can suppress it if desired.
5. New email credentials SHALL follow the User-Secrets pattern and SHALL NOT be added as plaintext to `appsettings.json`.

### Requirement 7 — Thank-You Assistant (first producer)

**User Story:** As a business owner, when a customer pays an invoice, I want them to automatically receive a courteous thank-you email, so that I never forget to acknowledge payment and my customers feel looked after.

#### Acceptance Criteria

1. WHEN a payment is recorded against an invoice (manual recording path) AND the Thank-You Assistant is enabled for that business AND the customer has an email address THEN the system SHALL write a Thank-You outbox message within the payment's database transaction.
2. THE thank-you email SHALL address the customer by name and reference the payment amount and the invoice number (e.g. "Thank you for your payment of €420.00 for invoice INV-00089").
3. IF the customer has opted out of communications (reuse the existing opt-out semantics where applicable) OR has no email THEN NO thank-you message SHALL be written.
4. THE Thank-You Assistant SHALL be disabled by default OR enabled by default per a product decision recorded in the design; either way the per-business toggle (Requirement 4) SHALL govern it.
5. Sending SHALL occur through the shared dispatcher/outbox (Requirements 1–2), NOT inline in the payment request.
6. Stripe-webhook-initiated payments MAY be covered in a later phase; Phase 1 MUST at minimum cover the manual payment-recording path, and the design SHALL note whether the Stripe path is included.

### Requirement 8 — Assistant activity log & management UI

**User Story:** As a business owner, I want to see what each assistant has done, so that I trust the automation is working and can review what was sent.

#### Acceptance Criteria

1. THE system SHALL record a log of assistant activity (which assistant, recipient, subject, status, timestamps) — the outbox table MAY serve as this log, or a projection of it.
2. THE business SHALL have a "Digital Assistants" management page listing each assistant with its persona/description, an enable/disable toggle, working-hours settings, and access to its activity log.
3. THE management page SHALL follow the app's UI standards (BlockUI + SweetAlert2 for actions; the design system's cards/toggles).
4. Toggling an assistant SHALL be a quick operation (BlockUI → AJAX → reload/refresh) and SHALL persist the per-business setting.

### Requirement 9 — SuperAdmin administration UI

**User Story:** As a SuperAdmin, I want to configure who receives failure alerts, so that operational problems reach the right people.

#### Acceptance Criteria

1. THE system SHALL provide a SuperAdmin page to view and edit the administrator notification recipient list (stored in `PlatformConfig`).
2. THE page SHALL accept multiple email addresses and validate their format before saving.
3. Changes SHALL take effect without redeploying the application.

### Requirement 10 — Conventions, safety, and non-functional

#### Acceptance Criteria

1. All new AJAX endpoints SHALL use the `AxGet`/`AxPost` naming convention and return `Json(new { success, ... })`.
2. All new tables SHALL use `[schema].[Table]` naming, `<TableName>Id` foreign keys, `Is`/`Has` bit prefixes, and `CreatedAtUtc DATETIME NOT NULL DEFAULT GETUTCDATE()` per the SQL schema steering.
3. All catch blocks SHALL use `catch (Exception ex)`; repositories SHALL rethrow; the dispatcher SHALL fail safe per message.
4. Tenant-scoped tables SHALL apply the global `BusinessId` query filter, consistent with existing entities.
5. THE design SHALL explicitly document the deliberate deviation from the steering's MassTransit/RabbitMQ + transactional-outbox invariant, recording that a DB-polling in-process dispatcher was chosen for the platform's early-adoption stage, and that the outbox boundary keeps a future extraction to a standalone worker or message bus low-cost.
6. New SQL SHALL be delivered as sequentially-numbered migration scripts with guarded `CREATE SCHEMA`, avoiding the known duplicate-number collisions in the migrations folder.
```