# Implementation Plan: Digital Assistants — Notification Spine + Thank-You (Phase 1)

## Overview

Bottom-up build of the notification spine and the first assistant. Database + reference
data first, then entities/EF, repositories, the producer (Infrastructure), the dispatcher
(Web), the email-sender extension, the Thank-You template, admin failure alerting, the two
UIs, and finally the payment-path hooks (manual + Stripe) that enqueue the thank-you.
Recurring Invoices and the other assistants are NOT in this plan (design-only).

## Tasks

- [ ] 1. Database — schema, tables, seed, and Business.TimeZoneId
  - [ ] 1.1 Create `[notification]` schema (guarded `CREATE SCHEMA`, next free migration number)
    - _Requirements: 1.1, 10.6_
  - [ ] 1.2 Create `[notification].[OutboxMessage]` table
    - Columns per design (Id, BusinessId, AssistantTypeId, RecipientEmail/Name, ReplyToEmail, Subject, BodyHtml, Status default 'Pending', RetryCount, MaxRetries, ScheduledForUtc, LastError, SentAtUtc, FailedAtUtc, RelatedEntityType/Id, CreatedAtUtc default GETUTCDATE())
    - Indexes: (Status, ScheduledForUtc); (BusinessId, AssistantTypeId, CreatedAtUtc); (Status, FailedAtUtc)
    - _Requirements: 1.1, 1.3, 1.4, 10.2_
  - [ ] 1.3 Create `[notification].[AssistantType]` reference table + seed `thank_you`
    - Explicit-Id seed (IDENTITY_INSERT): thank_you / "Thank-You Assistant" / persona / RecipientKind='Customer' / IsCustomerFacing=1
    - _Requirements: 7, 8_
  - [ ] 1.4 Create `[notification].[BusinessAssistantSetting]` table
    - IsEnabled (default 1), IsBrandingFooterEnabled (default 1), WorkingHoursStart/End (TIME NULL), audit cols; UNIQUE (BusinessId, AssistantTypeId)
    - _Requirements: 4.1, 6.4_
  - [ ] 1.5 Create `[notification].[AssistantOptOut]` table
    - BusinessId, AssistantTypeId, CustomerId NULL, RecipientEmail, OptedOutAtUtc, CreatedAtUtc; UNIQUE (BusinessId, AssistantTypeId, RecipientEmail)
    - _Requirements: 4 (per-service opt-out)_
  - [ ] 1.6 Create `[notification].[TimeZone]` reference table + seed
    - Id, DisplayName, WindowsId, IanaId; seed Cyprus, UK, Ireland, CET, UTC
    - _Requirements: 5.1_
  - [ ] 1.7 Add `TimeZoneId INT NULL FK` to `[portal].[Business]`
    - _Requirements: 5.2_
  - [ ] 1.8 Add `digital_assistants` to the `CK_DemoInvitationPermission_Module` check constraint + seed `PlanModulePermission` for Professional & Enterprise
    - _Requirements: (plan gating)_

- [ ] 2. Entities + EF Core configuration (Portal.Infrastructure)
  - [ ] 2.1 Add entities: `OutboxMessage`, `AssistantType`, `BusinessAssistantSetting`, `AssistantOptOut`, `NotificationTimeZone`
    - _Requirements: 1, 4, 5_
  - [ ] 2.2 Add `TimeZoneId` property to `Business` entity
    - _Requirements: 5.2_
  - [ ] 2.3 EF config in `PortalDbContext`: DbSets + `Configure*` methods, `ToTable(..., "notification")`, indexes, `CreatedAtUtc` default; tenant `HasQueryFilter` on OutboxMessage/BusinessAssistantSetting/AssistantOptOut; no tenant filter on AssistantType/NotificationTimeZone
    - _Requirements: 1.4, 10.4_
  - [ ] 2.4 Add `PortalModules.DigitalAssistants` constant + include in `PortalModules.All`
    - _Requirements: (plan gating)_

- [ ] 3. Repositories (Portal.Infrastructure)
  - [ ] 3.1 `NotificationOutboxRepository` — InsertAsync, GetDuePendingAsync(batch, nowUtc), MarkSentAsync, MarkRetryAsync(id, error), MarkFailedAsync(id, error), CountFailedSinceAsync(sinceUtc), GetByBusinessAndAssistantPagedAsync
    - Raw SQL, full table names, null-safe params, `catch (Exception ex)` rethrow
    - _Requirements: 1, 2, 3.1, 8.1_
  - [ ] 3.2 `BusinessAssistantSettingRepository` — GetAsync(businessId, assistantTypeId), GetAllForBusinessAsync, UpsertAsync
    - _Requirements: 4.1_
  - [ ] 3.3 `AssistantOptOutRepository` — ExistsAsync(businessId, assistantTypeId, email), InsertAsync
    - _Requirements: 4 (opt-out check)_
  - [ ] 3.4 Register all three in Program.cs (factory-lambda convention)

- [ ] 4. Working-hours / scheduling helper (Portal.Infrastructure)
  - [ ] 4.1 `IScheduleResolver` / `ScheduleResolver` — ResolveScheduledForUtc(setting, businessTimeZone, nowUtc): now if no working hours, else next in-window UTC using `TimeZoneInfo`
    - Fallback to configured default timezone when Business.TimeZoneId is null
    - _Requirements: 4.3, 5.3_

- [ ] 5. Producer (Portal.Infrastructure)
  - [ ] 5.1 `INotificationProducer` / `NotificationProducer` with `EnqueueThankYouAsync(...)`
    - Load setting (or default); if disabled → return. Check opt-out; if opted out or no email → return. Resolve ScheduledForUtc (task 4). Render subject+HTML body (task 7 builder — see note). Insert OutboxMessage (Pending, MaxRetries from config, RelatedEntity=Invoice).
    - _Requirements: 4.2, 4.4, 7.1, 7.2, 7.3_
  - [ ] 5.2 Register `INotificationProducer` in Program.cs

- [ ] 6. Email sender extension (Portal.Web)
  - [ ] 6.1 Add optional `string? replyTo = null` to `IEmailSender.SendEmailAsync` (+ attachment overload if needed) and set `ReplyTo` in `EmailSender` when provided
    - Existing callers unaffected (optional param)
    - _Requirements: 6.1_
  - [ ] 6.2 Add `EmailDepartmentEnum.Notifications` + an `EmailAccounts` entry (credentials via User Secrets, NOT plaintext appsettings)
    - _Requirements: 6.2, 6.5_

- [ ] 7. Thank-You email template (Portal.Web)
  - [ ] 7.1 `AssistantEmailBuilder.BuildThankYouHtml(customerName, amount, invoiceNumber, businessName, currencySymbol, includeFooter, footerUrl)` — self-contained HTML matching the design system; conditional "Powered by 3 Inventors — like this? Discover it" footer
    - NOTE: the producer (Infrastructure) needs the rendered HTML but the builder is Web-side. Resolve by placing the builder where the producer can reach it (Infrastructure) OR passing a rendered body into the producer from a Web-side caller. **Decision at task time:** put a pure static HTML builder in Infrastructure (no Web dependency) so the producer stays self-contained.
    - _Requirements: 6.3, 7.2_

- [ ] 8. Dispatcher (Portal.Web — BackgroundService)
  - [ ] 8.1 `NotificationDispatcherBackgroundService : BackgroundService`, registered via `AddHostedService`
    - Config: `Notifications:EnableDispatcher` (default true), `PollIntervalSeconds` (60), `BatchSize` (50)
    - Loop: Task.Delay → scope → GetDuePendingAsync → per message: IEmailSender.SendEmailAsync(reply-to) → MarkSent; on exception → MarkRetry or MarkFailed at Max. Per-message try/catch; fatal wrapper try/catch. Mirrors PaymentReminderBackgroundService.
    - _Requirements: 2.1–2.7_
  - [ ] 8.2 After each batch, invoke the admin failure-threshold check (task 9)
    - _Requirements: 3.1_

- [ ] 9. Admin failure alerting (Portal.Web)
  - [ ] 9.1 `INotificationAdminAlertService` — CheckAndAlertAsync(): CountFailedSinceAsync(window) > threshold AND not already alerted this window → alert; throttle via in-memory flag or PlatformConfig timestamp key
    - Config: `Notifications:FailureAlertThreshold`, `FailureAlertWindowHours`
    - _Requirements: 3.1, 3.4_
  - [ ] 9.2 In-app alert via existing SignalR (SuperAdmin channel)
    - _Requirements: 3.2_
  - [ ] 9.3 Email alert to each address in `PlatformConfig["AdminNotificationRecipients"]` (semicolon-delimited)
    - _Requirements: 3.2, 3.3_

- [ ] 10. Checkpoint — backend build
  - Build solution; ensure schema entities, repos, producer, dispatcher, email extension, alerting compile.

- [ ] 11. Payment-path hooks (Portal.Infrastructure)
  - [ ] 11.1 Inject optional `INotificationProducer?` into `PaymentService` (nullable, mirroring `IPaymentReceiptService`)
  - [ ] 11.2 In `RecordPaymentAsync`: wrap the payment insert + thank-you outbox enqueue in a single `IDbContextTransaction` (atomic); load customer name/email + invoice number for the enqueue; leave schedule-match/status-recalc/receipt as post-commit best-effort
    - _Requirements: 7.1, 7.5, (transaction) 7 confirmed decision_
  - [ ] 11.3 Add the same enqueue (within the payment-recording transaction) to the Stripe webhook payment path (`checkout.session.completed` handler)
    - _Requirements: 7.6 (Stripe included)_

- [ ] 12. Business-facing "Digital Assistants" UI (Portal.Web)
  - [ ] 12.1 `AssistantsController` (`[ModuleAccess(PortalModules.DigitalAssistants)]`) + add to `ModuleControllerMap`
    - _Requirements: 8.2, (plan gating)_
  - [ ] 12.2 Index view: assistant cards (persona, toggle, working-hours inputs, footer toggle, log expander) matching the mockup + design system
    - _Requirements: 8.2, 8.3_
  - [ ] 12.3 `AxPostToggleAssistant(assistantKey, enabled)` — BlockUI → reload (quick op)
    - _Requirements: 8.4_
  - [ ] 12.4 `AxPostSaveAssistantSettings(...)` (working hours + footer) — BlockUI → SweetAlert2
    - _Requirements: 8.3_
  - [ ] 12.5 `AxGetAssistantLog(assistantKey, page)` — recipient/subject/status/time from OutboxMessage
    - _Requirements: 8.1_

- [ ] 13. SuperAdmin recipients UI (Portal.Web)
  - [ ] 13.1 Admin page to view/edit `AdminNotificationRecipients` (PlatformConfig); validate email format
    - _Requirements: 9.1, 9.2, 9.3_
  - [ ] 13.2 `AxPostSaveAdminRecipients` → `SetValueAsync`
    - _Requirements: 9.3_

- [ ] 14. Configuration + DI wiring
  - [ ] 14.1 Add `Notifications` config section (appsettings) + `Notifications` EmailAccounts entry (User Secrets for password)
  - [ ] 14.2 Register services + `AddHostedService<NotificationDispatcherBackgroundService>()` under a `// --- Digital Assistants ---` block
  - [ ] 14.3 Add `Notifications:DefaultTimeZoneWindowsId` and `BrandingFooterUrl`

- [ ] 15. Final checkpoint — build + manual verification
  - Build, verify 0 errors.
  - Verify: enabling Thank-You + recording a manual payment writes an outbox row in the payment txn; dispatcher sends it and marks Sent; disabling produces no row; opt-out suppresses; working-hours schedules to next window in the business timezone; Stripe payment also enqueues; retries → Failed at Max; failure threshold alerts admins (in-app + email); Digital Assistants page toggles/logs; SuperAdmin recipients save and take effect.
  - Produce a manual test-scenarios doc (like the VAT checklist).

## Notes

- Reuse the `PaymentReminderBackgroundService` pattern for the dispatcher (BackgroundService + Task.Delay + IServiceScopeFactory scope-per-iteration + resilient try/catch).
- Reuse `IEmailSender` (MailKit) for sending; `IPlatformConfigService` for admin recipients; existing SignalR for in-app alerts.
- `catch (Exception ex)` everywhere; repositories rethrow; dispatcher fails safe per message.
- Self-contained rendered body stored on the outbox row (dispatcher stays dumb; log shows exactly what was sent).
- Document the MassTransit/Outbox steering deviation in the design (done) — no code impact.
- Migrations: pick the next FREE sequential numbers (folder has known duplicate-number collisions); guarded `CREATE SCHEMA`; `USE [Portal]` header per steering.
- Thank-You default: ENABLED (per design decision) via the BusinessAssistantSetting default / fallback.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1","1.2","1.3","1.4","1.5","1.6","1.7","1.8"] },
    { "id": 1, "tasks": ["2.1","2.2","2.3","2.4"] },
    { "id": 2, "tasks": ["3.1","3.2","3.3","3.4","4.1","6.1","6.2","7.1"] },
    { "id": 3, "tasks": ["5.1","5.2","8.1","8.2","9.1","9.2","9.3"] },
    { "id": 4, "tasks": ["10"] },
    { "id": 5, "tasks": ["11.1","11.2","11.3"] },
    { "id": 6, "tasks": ["12.1","12.2","12.3","12.4","12.5","13.1","13.2","14.1","14.2","14.3"] },
    { "id": 7, "tasks": ["15"] }
  ]
}
```
