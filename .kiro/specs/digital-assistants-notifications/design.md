# Design: Digital Assistants — Notification & Outbox Spine (Phase 1)

## Overview

A shared, reliable notification backbone plus the first assistant (Thank-You on payment
received). Producers (in `Portal.Infrastructure`) write durable **outbox** rows when a
business event occurs and the relevant assistant is enabled; an in-process **dispatcher**
(a `BackgroundService` in `Portal.Web`, mirroring `PaymentReminderBackgroundService`)
polls the outbox on an interval and sends each due message via the existing `IEmailSender`,
recording success/failure with bounded retries and administrator alerting on persistent
failure.

Phase 1 delivers: the `[notification]` schema (outbox + per-business assistant settings +
a timezone reference table), the dispatcher, the Thank-You producer hooked into the manual
payment-recording path, a business-facing "Digital Assistants" page, and a SuperAdmin page
for failure-alert recipients. The existing Payment Reminders module is untouched.

## Architecture

```
Business event (payment recorded)  [Portal.Infrastructure: PaymentService]
   │  if Thank-You Assistant enabled for business AND customer has email:
   │     resolve ScheduledForUtc from working hours (business timezone)
   ▼
[notification].[OutboxMessage]  ← durable row (Pending)
   ▲                                   │  polled every N seconds
   │                                   ▼
Producers                     NotificationDispatcher  [Portal.Web: BackgroundService]
(Thank-You now;                 │  select Pending where ScheduledForUtc <= now (batch)
 Quotation/Lead later)          │  render body (if template ref) → IEmailSender.SendEmailAsync(reply-to)
                                │  success → Sent ; fail → RetryCount++/LastError ; >= Max → Failed
                                ▼
                         Admin failure alert (threshold) → SignalR (in-app) + email
                                                          recipients from PlatformConfig
```

**Layer boundary (deliberate):** producers live in Infrastructure and can only *write*
outbox rows (they cannot reach `IEmailSender`, which lives in Web). The dispatcher lives
in Web and does the sending. The outbox table is the seam — exactly the decoupling that
keeps a future extraction to a standalone worker cheap.

## Deliberate deviation from steering (documented)

The project steering lists a **MassTransit + RabbitMQ transactional outbox** as an
architectural invariant. That infrastructure is **not wired** in code today (verified:
no packages, no `AddMassTransit`, no consumers, no outbox table). This design uses a
**database-polling in-process dispatcher** instead, because:

- at early-adoption volume a message bus adds operational weight (separate broker,
  deployment, monitoring) without earning its complexity;
- the durability guarantee the bus would provide is delivered by the outbox table;
- the outbox boundary makes a later switch to a hosted worker **or** MassTransit low-cost
  (producers and consumers don't change — only what drains the table does).

An internal doc (`.kiro/docs/upcoming/Sales_Module_Brief.md`) already prescribes exactly
this approach (`IHostedService` + app-level `OutboxMessage` table) for future automation,
so this is consistent with intended direction. This deviation is recorded here per
requirement 10.5.

## Data model — `[notification]` schema

### `[notification].[OutboxMessage]`

The durable message + its own audit/log (serves Requirement 8.1 as the activity log).

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT IDENTITY PK | |
| `BusinessId` | INT NOT NULL FK → `[portal].[Business]` | tenant scope |
| `AssistantTypeId` | INT NOT NULL FK → `[notification].[AssistantType]` | which assistant produced it |
| `RecipientEmail` | NVARCHAR(320) NOT NULL | |
| `RecipientName` | NVARCHAR(200) NULL | for greeting |
| `ReplyToEmail` | NVARCHAR(320) NULL | optional reply-to |
| `Subject` | NVARCHAR(300) NOT NULL | resolved at write time |
| `BodyHtml` | NVARCHAR(MAX) NOT NULL | resolved at write time (self-contained; no runtime template dependency) |
| `Status` | NVARCHAR(20) NOT NULL DEFAULT 'Pending' | `Pending` / `Sent` / `Failed` |
| `RetryCount` | INT NOT NULL DEFAULT 0 | |
| `MaxRetries` | INT NOT NULL DEFAULT 5 | |
| `ScheduledForUtc` | DATETIME NOT NULL | dispatcher picks up when `<= now` |
| `LastError` | NVARCHAR(MAX) NULL | |
| `SentAtUtc` | DATETIME NULL | |
| `FailedAtUtc` | DATETIME NULL | |
| `RelatedEntityType` | NVARCHAR(50) NULL | e.g. "Invoice" (for the log/traceability) |
| `RelatedEntityId` | INT NULL | e.g. invoice id |
| `CreatedAtUtc` | DATETIME NOT NULL DEFAULT GETUTCDATE() | |

Indexes: `IX_OutboxMessage_Status_ScheduledForUtc` (the dispatcher's hot query),
`IX_OutboxMessage_BusinessId_AssistantTypeId_CreatedAtUtc` (log views),
`IX_OutboxMessage_Status_FailedAtUtc` (admin failure threshold query).

**Decision — self-contained body:** the body HTML is rendered and stored at write time
(not a template-ref resolved later). Rationale: keeps the dispatcher dumb, makes the log
show exactly what was sent, and avoids the dispatcher needing template/tenant context.
Cost: larger rows. Acceptable at this volume.

### `[notification].[AssistantType]` (seeded reference)

Static registry of assistants. Seeded; exempt from `CreatedAtUtc` per the lookup-table
exception in the SQL steering.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT PK (explicit, `IDENTITY_INSERT`) | |
| `Key` | NVARCHAR(50) NOT NULL UNIQUE | e.g. `thank_you` |
| `Name` | NVARCHAR(100) NOT NULL | "Thank-You Assistant" |
| `Description` | NVARCHAR(300) NOT NULL | persona/job line |
| `RecipientKind` | NVARCHAR(20) NOT NULL | `Customer` / `PortalUser` (drives future recipients) |
| `IsCustomerFacing` | BIT NOT NULL | governs branded footer + platform-from-account |

Seed row 1: `thank_you` / "Thank-You Assistant" / "Sends a courteous payment confirmation
the moment a customer pays." / `Customer` / 1.

### `[notification].[BusinessAssistantSetting]`

Per-business config for each assistant (Requirement 4). One row per (business, assistant).

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT IDENTITY PK | |
| `BusinessId` | INT NOT NULL FK | |
| `AssistantTypeId` | INT NOT NULL FK | |
| `IsEnabled` | BIT NOT NULL DEFAULT 1 | Thank-You defaults ON (see decision below) |
| `IsBrandingFooterEnabled` | BIT NOT NULL DEFAULT 1 | per-business footer toggle (Req 6.4) |
| `WorkingHoursStart` | TIME NULL | null = no restriction (send immediately) |
| `WorkingHoursEnd` | TIME NULL | |
| `CreatedAtUtc` / `UpdatedAtUtc` | DATETIME | |

Unique constraint `(BusinessId, AssistantTypeId)`. If no row exists for a business, fall
back to the assistant's default (enabled, footer on, no working-hours restriction).

**Decision — Thank-You default ON.** Given early adoption and the growth-footer angle,
the assistant is enabled by default; businesses can disable per the toggle. Recorded per
Requirement 7.4.

### `[notification].[TimeZone]` (seeded reference)

Requirement 5. Static seed; exempt from `CreatedAtUtc`.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT PK (explicit) | |
| `DisplayName` | NVARCHAR(100) NOT NULL | "Cyprus (EET/EEST)" |
| `WindowsId` | NVARCHAR(100) NOT NULL | `TimeZoneInfo.FindSystemTimeZoneById` key on the host OS |
| `IanaId` | NVARCHAR(100) NOT NULL | `Europe/Nicosia` (portability / future Linux hosting) |

Seed a practical set (Cyprus, UK, Ireland, CET, UTC, etc.). Working-hours math uses
`TimeZoneInfo` with the `WindowsId` on Windows hosting (matches current deployment).

### `[portal].[Business]` — add `TimeZoneId`

Add nullable `TimeZoneId INT NULL FK → [notification].[TimeZone]`. Null → platform default
(UTC or a configured default). Added via migration; new EF property + config.

### `[notification].[AssistantOptOut]` — per-service, per-recipient opt-out

**Decision (updated):** opt-out is **individual per assistant**, not a shared reminder flag.
A customer may want payment reminders off but thank-you emails on. This table records an
opt-out for a specific recipient against a specific assistant.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT IDENTITY PK | |
| `BusinessId` | INT NOT NULL FK | tenant scope |
| `AssistantTypeId` | INT NOT NULL FK | which assistant they opted out of |
| `CustomerId` | INT NULL FK → `[customer].[Customer]` | set for customer-recipient assistants |
| `RecipientEmail` | NVARCHAR(320) NOT NULL | opt-out is also keyed by email (covers non-customer recipients / robustness) |
| `OptedOutAtUtc` | DATETIME NOT NULL | |
| `CreatedAtUtc` | DATETIME NOT NULL DEFAULT GETUTCDATE() | |

Unique constraint `(BusinessId, AssistantTypeId, RecipientEmail)`. The producer checks this
table for the (assistant, recipient) pair before enqueuing. The existing
`Customer.IsReminderOptedOut` is left as-is for the Payment Reminders module (untouched in
Phase 1); the new per-service model governs assistants only. An unsubscribe link in
assistant emails can write an opt-out row (fast-follow; the table supports it now).

### `[dbo].[PlatformConfig]` — new key (no schema change)

Admin failure-alert recipients stored under key `AdminNotificationRecipients` as a
semicolon-delimited list of emails. Uses existing `IPlatformConfigService.GetValueAsync` /
`SetValueAsync`.

## Components

### 1. Entities + repository (Portal.Infrastructure)

- Entities: `OutboxMessage`, `AssistantType`, `BusinessAssistantSetting`, `NotificationTimeZone` (class name avoids clashing with `System.TimeZone`).
- `NotificationOutboxRepository` (raw-SQL, per repository steering): `InsertAsync(OutboxMessage)`, `GetDuePendingAsync(int batchSize, DateTime nowUtc)`, `MarkSentAsync(id)`, `MarkRetryAsync(id, error)`, `MarkFailedAsync(id, error)`, `CountFailedSinceAsync(DateTime sinceUtc)`, and log queries `GetByBusinessAndAssistantPagedAsync(...)`.
- `BusinessAssistantSettingRepository`: `GetAsync(businessId, assistantTypeId)`, `UpsertAsync(...)`, `GetAllForBusinessAsync(businessId)`.
- EF config in `PortalDbContext`: `ToTable(..., "notification")`, tenant `HasQueryFilter` on `BusinessId` for OutboxMessage + BusinessAssistantSetting; `AssistantType`/`NotificationTimeZone` are global reference (no tenant filter). `CreatedAtUtc` default `GETUTCDATE()`.

### 2. Producer service (Portal.Infrastructure)

`INotificationProducer` / `NotificationProducer`:

```csharp
Task EnqueueThankYouAsync(int businessId, int invoiceId, int paymentId, string customerName,
                          string customerEmail, decimal amount, string invoiceNumber,
                          string? replyToEmail);
```

Logic:
1. Load the business's setting for `thank_you` (or default). If not enabled → return (no row).
2. Resolve `ScheduledForUtc`: if working hours set and now is outside them (in the business's timezone via `NotificationTimeZone` + `TimeZoneInfo`), compute next in-window UTC time; else `now`.
3. Render subject + self-contained HTML body (via a template builder; include branded footer if `IsBrandingFooterEnabled`).
4. `InsertAsync` an `OutboxMessage` (Pending, MaxRetries from config, RelatedEntity = Invoice/invoiceId).

The producer is the single place the "enabled?" and "working hours?" decisions happen
(Requirement 4.4).

### 3. Thank-You producer hooks — manual AND Stripe (Portal.Infrastructure)

**Decision (updated):** Phase 1 covers **both** payment paths. The producer is called from:
- **Manual recording** — `PaymentService.RecordPaymentAsync` (see transaction note below).
- **Stripe card payments** — the webhook handler that records a payment on
  `checkout.session.completed` (in the Stripe webhook/processing service). The same
  `INotificationProducer.EnqueueThankYouAsync` is called after the payment row is created,
  within the same transactional scope used to record that payment. Reply-to for card
  payments follows the same platform-from rule.

Both paths share the single producer method, so the thank-you content and gating are
identical regardless of how the payment arrived.

#### PaymentService hook

**Transaction decision (important).** `PaymentService.RecordPaymentAsync` currently runs
sequential awaits with **no explicit DB transaction**; receipt auto-generation is a
best-effort call after the payment insert. The requirement asks the outbox row to be
written in the same transaction as the payment. Two options:

- **A. Wrap payment insert + outbox write in an explicit `IDbContextTransaction`.** True to
  the outbox promise (atomic), but changes the method's transactional shape and must also
  consider the existing schedule-match / status-recalc / receipt calls.
- **B. Best-effort enqueue after the payment commit**, mirroring the existing
  `TryAutoGenerateReceiptAsync` pattern (wrapped in try/catch, never fails the payment).

**Chosen: A, scoped narrowly.** Wrap the **payment insert and the outbox insert** in a
single transaction so the notification is atomic with the payment (the whole point of an
outbox). The subsequent schedule-match, status recalc, and receipt generation remain as
today (post-commit, best-effort) — they are not part of the outbox guarantee. This keeps
the atomicity where it matters (payment ↔ notification) without reworking the unrelated
downstream steps. The producer call is injected via a new optional
`INotificationProducer?` dependency on `PaymentService` (same nullable-injection pattern
already used for `IPaymentReceiptService`), so existing `PaymentService` unit tests that
don't supply it continue to compile and pass.

Guard conditions before enqueue: customer has an email; the (assistant, recipient) pair has
no row in `[notification].[AssistantOptOut]`; payment amount > 0 (already validated).

### 4. Dispatcher (Portal.Web — BackgroundService)

`NotificationDispatcherBackgroundService : BackgroundService`, registered with
`AddHostedService`. Mirrors `PaymentReminderBackgroundService`:

- Reads `Notifications:EnableDispatcher` (default true) and `Notifications:PollIntervalSeconds` (default 60).
- Loop: `Task.Delay(interval)` → open a scope → `GetDuePendingAsync(batchSize, DateTime.UtcNow)` → for each message: try `IEmailSender.SendEmailAsync(RecipientEmail, Subject, BodyHtml, <platform dept>, replyTo: ReplyToEmail)` → `MarkSentAsync`; on exception → if `RetryCount + 1 >= MaxRetries` `MarkFailedAsync` else `MarkRetryAsync`. Per-message try/catch; batch continues on individual failure. Fatal wrapper try/catch logs and never crashes the host.
- After each batch, run the failure-threshold check (Requirement 3) — throttled so it alerts at most once per window.

### 5. Email sender extension (Portal.Web)

Extend `IEmailSender` with an optional `replyTo`:

```csharp
Task SendEmailAsync(string email, string subject, string message,
                    EmailDepartmentEnum department, string? replyTo = null);
```

Implementation adds `emailMessage.ReplyTo.Add(new MailboxAddress(replyTo, replyTo))` when
provided. Existing callers are unaffected (optional param). Add a new `EmailDepartmentEnum`
value **`Notifications`** and a matching `EmailAccounts` entry (credentials via User
Secrets, per Requirement 6.5) so customer-facing assistant mail sends from a dedicated
platform address (e.g. `notifications@3inventors.com`), not the business mailbox.

### 6. Thank-You email template + branded footer

A template builder (Web-side, alongside existing `PortalEmailService` builders) produces
the self-contained HTML: greeting by name, "Thank you for your payment of {currency}{amount}
for invoice {invoiceNumber}", business name, and — when `IsBrandingFooterEnabled` — a
subtle footer: *"Sent via 3 Inventors Business Portal — like this? Discover it →"* linking
to a landing/lead-capture URL. The rendered HTML is what the producer stores in the outbox
row. (Wiring footer-link clicks into the Sales lead pipeline is a documented fast-follow,
not Phase 1.)

### 7. Admin failure alerting (Portal.Web)

A small `INotificationAdminAlertService`:
- `CountFailedSinceAsync(window)` via the repository; if over the configured threshold
  (`Notifications:FailureAlertThreshold`, `Notifications:FailureAlertWindowHours`) AND not
  already alerted this window (throttle state in memory or a `PlatformConfig` timestamp
  key) → send.
- In-app: push via existing SignalR (a SuperAdmin alerts hub/message).
- Email: to each address in `PlatformConfig["AdminNotificationRecipients"]`, via
  `IEmailSender` (department `Notifications` or `ResponseToAdmin`).

### 8. UI — business-facing "Digital Assistants" page

Route e.g. `GET /Assistants`. Lists each assistant as a card: icon/persona, name,
description, an enable/disable toggle, optional working-hours inputs, footer toggle, and a
link/expander to its activity log (from OutboxMessage). Actions:
- Toggle → `AxPostToggleAssistant(assistantKey, enabled)` → BlockUI → reload (quick op).
- Working hours / footer save → `AxPostSaveAssistantSettings(...)` → BlockUI → SweetAlert2.
- Log → `AxGetAssistantLog(assistantKey, page)` → renders recipient/subject/status/time.

Follows the design system and the AJAX BlockUI + SweetAlert2 standards. **Plan gating
(updated):** add a **new module key** `digital_assistants` to `PortalModules` (and the
`CK_DemoInvitationPermission_Module` check constraint + plan-feature seed), Professional+.
The Assistants controller is gated with `[ModuleAccess(PortalModules.DigitalAssistants)]`
and mapped in `ModuleControllerMap`.

### 9. UI — SuperAdmin failure-recipient page

Route under the existing Admin area. Loads `AdminNotificationRecipients` from
`PlatformConfig`, textarea/repeater of emails, validates format, `AxPostSaveAdminRecipients`
→ `SetValueAsync`. Effective immediately (no redeploy).

## Configuration (appsettings + User Secrets)

```json
"Notifications": {
  "EnableDispatcher": true,
  "PollIntervalSeconds": 60,
  "BatchSize": 50,
  "DefaultMaxRetries": 5,
  "FailureAlertThreshold": 20,
  "FailureAlertWindowHours": 24,
  "DefaultTimeZoneWindowsId": "GTB Standard Time",
  "BrandingFooterUrl": "https://www.3inventors.com/portal"
}
```
`EmailAccounts` gains a `Notifications` department entry; its password lives in **User
Secrets**, not appsettings.

## Error Handling

| Scenario | Handling |
|----------|----------|
| Email send throws | Per-message catch → RetryCount++/LastError; batch continues |
| Retries exhausted | Status=Failed, FailedAtUtc set; contributes to threshold alert |
| Producer enqueue fails inside payment txn | Transaction rolls back → payment not recorded (atomic); surfaced as a normal payment failure |
| Business has no timezone | Fall back to `DefaultTimeZoneWindowsId` / UTC |
| PlatformConfig recipients empty/malformed | Skip email alert, still push in-app; log a warning |
| Dispatcher disabled by config | Service returns without polling (like PaymentReminders) |
| Duplicate enqueue (same payment retried) | RelatedEntityType+Id lets us dedupe if needed; Phase 1 relies on the single enqueue point inside the txn |

## Testing Strategy

- **Producer:** enabled/disabled gating writes/skips a row; working-hours math yields correct `ScheduledForUtc` across timezone boundaries; opted-out / no-email → no row.
- **Dispatcher:** due-selection query respects `ScheduledForUtc`; success → Sent; failure → retry then Failed at Max; one poison message doesn't stop the batch.
- **Transaction atomicity:** if the outbox insert throws, the payment insert rolls back (integration test against a real/localdb transaction).
- **Email sender:** reply-to header set when provided; existing calls unaffected.
- **Admin alert:** threshold triggers once per window (throttle); recipients parsed from PlatformConfig.
- Manual scenarios doc to follow (like the VAT checklist).

## Files (indicative)

**New (Infrastructure):** `Entities/OutboxMessage.cs`, `AssistantType.cs`,
`BusinessAssistantSetting.cs`, `NotificationTimeZone.cs`;
`Repositories/NotificationOutboxRepository.cs`, `BusinessAssistantSettingRepository.cs`;
`Services/INotificationProducer.cs` + `NotificationProducer.cs`.
**New (Web):** `BackgroundServices/NotificationDispatcherBackgroundService.cs`;
`Services/Notifications/INotificationAdminAlertService.cs` + impl;
`Services/Email/AssistantEmailBuilder.cs`; `Controllers/AssistantsController.cs`;
`Controllers/AdminNotificationController.cs`; views for both.
**Modified:** `IEmailSender`/`EmailSender` (reply-to), `EmailDepartmentEnum` (+Notifications),
`PaymentService` (txn + producer hook), `PortalDbContext` (DbSets + config + `TimeZoneId`),
`Business` entity (+TimeZoneId), `Program.cs` (DI + `AddHostedService`), `appsettings` +
User Secrets.
**Migrations:** `NNN_CreateNotificationSchema.sql`, `NNN_CreateOutboxAndAssistantTables.sql`,
`NNN_SeedAssistantTypes.sql`, `NNN_CreateTimeZoneTableAndSeed.sql`,
`NNN_AddBusinessTimeZoneId.sql` (number carefully — the folder has known duplicate numbers;
pick the next free sequential numbers).

## Phase 2 — Recurring Invoices Assistant (design-only for now; not implemented in Phase 1)

> **See also — full assistant roadmap:** the complete catalog of planned assistants (Quotation
> Follow-Up, the weekly owner digests, payment-reminder migration, VAT/compliance reminders,
> Recurring Invoices, etc.), organised into delivery groups and split into the two structural
> categories (event/state-driven vs. scheduled scan/digest), is documented in
> [`.kiro/docs/features/digital-assistants-catalog.md`](../../docs/features/digital-assistants-catalog.md).
> Recurring Invoices below is Group 6 in that catalog and depends on the reusable
> scheduled-scan / digest engine described there.

Recurring Invoices is a **scheduled document-generation assistant**, distinct from the
notification assistants above. Its scheduled run does not primarily send an email — it
**creates a real invoice** (header, line items, sequence number, VAT, totals) on a cadence,
optionally issues it, optionally attaches a payment link, and then writes an outbox
**notification** to the owner ("N recurring invoices generated for {month}"). It therefore
reuses the *notification* half of the spine (dispatcher + outbox) but adds its own schedule
model and a generation engine that reuses existing invoice creation/issue/number-generation
services.

This section is captured now for a unified product story; **implementation is deferred**
until the Thank-You assistant has proven the spine in production.

### Product decisions (confirmed)

- **Per-schedule mode: Auto-issue OR Draft.** Each recurring definition chooses. Auto-issue
  is the headline case (e.g. a kindergarten monthly subscription that should go out
  untouched); draft-for-review is the safe option for schedules whose amounts vary.
- **The automation chain is the value:** `recurring → (auto-issue) → optional auto-payment-link
  → auto-remind → paid → auto-record`. It composes with the *existing* Professional
  automation (Stripe Connect, auto payment links, Payment Reminders) rather than
  reimplementing any of it — the recurring assistant only produces the invoice and hands off.
- **Payment link is optional per schedule.** When enabled and the business has Stripe
  Connect, the generated/issued invoice gets an auto payment link (existing mechanism);
  the reminder pipeline then applies as normal once issued.

### Data model (Phase 2)

`[notification].[RecurringInvoiceSchedule]` (or a dedicated `[recurring]` schema — decide at
Phase 2 spec time):

| Column | Type | Notes |
|--------|------|-------|
| `Id` | INT IDENTITY PK | |
| `BusinessId` | INT NOT NULL FK | tenant scope |
| `CustomerId` | INT NOT NULL FK | who is billed |
| `Frequency` | NVARCHAR(20) NOT NULL | `Monthly` / `Quarterly` / `Annually` |
| `DayOfMonth` | INT NULL | e.g. issue on the 1st |
| `NextRunDate` | DATE NOT NULL | when the next invoice is due to be generated |
| `IsAutoIssue` | BIT NOT NULL | true = issue immediately; false = create draft |
| `IsAutoPaymentLinkEnabled` | BIT NOT NULL | attach a payment link on issue (requires Stripe Connect) |
| `PaymentTermsDays` | INT NOT NULL | due date = issue date + terms |
| `IsActive` | BIT NOT NULL | pause/resume |
| `Notes` | NVARCHAR(500) NULL | |
| `CreatedByUserId` / `CreatedAtUtc` / `UpdatedAtUtc` | | |

`[notification].[RecurringInvoiceScheduleLine]` — the template line items copied onto each
generated invoice (Description, Qty, UnitPrice, VatRate, Discount, SortOrder).

`[notification].[RecurringInvoiceRun]` — an audit row per generation (ScheduleId, generated
InvoiceId, RunAtUtc, Outcome, Error) for the assistant's activity log and idempotency.

### Generation service (Phase 2)

A `RecurringInvoiceGenerationService` invoked by a scheduled run (either the shared
dispatcher extended with a daily "due schedules" pass, or a sibling hosted service):
1. Select `RecurringInvoiceSchedule` rows where `IsActive` AND `NextRunDate <= today`.
2. For each: reuse the existing invoice creation + `IInvoiceNumberGenerator` + VAT-period
   assignment + line copying to build the invoice; if `IsAutoIssue` issue it (existing
   issue path), else leave draft; if `IsAutoPaymentLinkEnabled` attach a link (existing
   mechanism).
3. Compute the next `NextRunDate` from `Frequency`.
4. Write a `RecurringInvoiceRun` audit row, and enqueue an outbox **owner notification**
   summarising what was generated (reusing the spine).
5. Idempotency: guard on (ScheduleId, period) so a re-run doesn't double-generate.

### Assistant registration

Seed a `recurring_invoices` `AssistantType` (RecipientKind = `PortalUser` for the summary
notification; the invoice itself goes to the customer through the normal issue/email path).
It appears on the Digital Assistants page as a card, and its per-business enable lives in
`BusinessAssistantSetting` like the others. Plan gate: `digital_assistants` (Professional+),
same as the spine.

### Deferred / open for the Phase 2 spec

- Whether the recurring schedule lives in `[notification]` or its own `[recurring]` schema.
- Draft lifecycle UI (list/edit/skip-once/pause) and the "3 generated — review" inbox.
- Exact handoff points into auto-payment-link and reminders (confirm no double-send).
- Prorating / mid-cycle changes / end dates.

## Resolved decisions (previously open)

1. **Opt-out semantics** — RESOLVED: per-service, per-recipient opt-out via the new
   `[notification].[AssistantOptOut]` table. The reminder module's `IsReminderOptedOut` is
   not reused; assistants have independent consent.
2. **Plan gating** — RESOLVED: add a new module key `digital_assistants` (Professional+).
3. **Stripe-webhook payments** — RESOLVED: included in Phase 1. Thank-You fires for both
   manual recording and Stripe card payments via the shared producer.
4. **Transaction atomicity** — CONFIRMED: payment insert + outbox insert commit atomically
   (both manual and Stripe paths), so a satisfied thank-you can never exist without its
   payment and vice versa.
```