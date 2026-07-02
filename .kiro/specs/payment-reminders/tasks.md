# Implementation Plan: Payment Reminders

## Overview

This plan implements the Payment Reminders feature across database, service, background job, controller, and UI layers. Tasks are ordered so each step builds on the previous, with property tests placed close to the service implementations they validate. The feature adds automated and manual email reminder capabilities for unpaid/overdue invoices with configurable escalation schedules (Friendly → Firm → Formal), plan-gated access, and a dashboard widget.

## Tasks

- [x] 1. Database migrations and schema setup
  - [x] 1.1 Create [reminder] schema and PaymentReminderSchedule table
    - Create migration file `XXX_CreateReminderSchemaAndScheduleTable.sql`
    - Create `[reminder]` schema
    - Create `[reminder].[PaymentReminderSchedule]` table with columns: Id, BusinessId (FK), EscalationTier (varchar(20)), DaysOffset (int), MaxRemindersPerTier (int, default 1), MinIntervalDays (int, default 3), PartialPaymentSuppressionDays (int, default 7), IsEnabled (bit, default 1), CreatedAtUtc, UpdatedAtUtc
    - Add CHECK constraint for EscalationTier IN ('Friendly', 'Firm', 'Formal')
    - Add index on BusinessId
    - _Requirements: 14.1, 14.2_

  - [x] 1.2 Create PaymentReminderLog table
    - Create migration file `XXX_CreatePaymentReminderLogTable.sql`
    - Create `[reminder].[PaymentReminderLog]` with columns: Id, BusinessId (FK), InvoiceId (FK), CustomerId (FK), RecipientEmail (nvarchar(200)), EscalationTier (varchar(20)), IsSentSuccessfully (bit), ErrorMessage (nvarchar(1000) nullable), IsManualTrigger (bit, default 0), SentAtUtc, CreatedAtUtc
    - Add CHECK constraint for EscalationTier
    - Add composite index on (BusinessId, InvoiceId)
    - Add composite index on (BusinessId, SentAtUtc)
    - _Requirements: 14.3, 14.4, 14.5_

  - [x] 1.3 Add IsReminderOptedOut to Customer and IsDisputed to Invoice
    - Create migration file `XXX_AddReminderOptOutAndDisputedColumns.sql`
    - Add `[IsReminderOptedOut] BIT NOT NULL DEFAULT 0` to `[customer].[Customer]`
    - Add `[IsDisputed] BIT NOT NULL DEFAULT 0` to `[invoice].[Invoice]`
    - _Requirements: 14.6, 14.7_

- [x] 2. Entity classes and DbContext configuration
  - [x] 2.1 Create PaymentReminderSchedule and PaymentReminderLog entity classes
    - Create `Portal.Infrastructure/Entities/PaymentReminderSchedule.cs` with all properties and navigation to Business
    - Create `Portal.Infrastructure/Entities/PaymentReminderLog.cs` with all properties and navigations to Business, Invoice, Customer
    - Add `IsReminderOptedOut` property to existing Customer entity
    - Add `IsDisputed` property to existing Invoice entity
    - _Requirements: 14.2, 14.3, 14.6, 14.7_

  - [x] 2.2 Add DbSets and Fluent API configuration to PortalDbContext
    - Add `DbSet<PaymentReminderSchedule>` and `DbSet<PaymentReminderLog>` properties
    - Add `ConfigurePaymentReminderSchedule` and `ConfigurePaymentReminderLog` Fluent API methods
    - Configure table mappings to `[reminder]` schema, keys, indexes, relationships, and defaults per design
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5_

- [x] 3. DTOs, models, and constants
  - [x] 3.1 Create DTOs and result models
    - Create `PaymentReminderScheduleDto` (tier, offset, max, interval, suppression, enabled)
    - Create `SaveReminderScheduleRequest` (same shape, used for input)
    - Create `PaymentReminderLogDto` (tier, email, sentAt, isManual, isSuccess, error)
    - Create `ReminderDashboardWidgetDto` (totalSent, paymentsReceived, amountReceived)
    - Create `ReminderEvaluationResult` (invoicesEvaluated, remindersSent, remindersFailed)
    - Create `ManualReminderResult` (success, errorMessage, customerOptedOut)
    - _Requirements: 1.1, 4.1, 4.4, 10.1_

  - [x] 3.2 Add module constants for plan gating
    - Add `PaymentReminderManual` and `PaymentReminderAuto` to `PortalModules.cs`
    - Add `EmailDepartmentEnum.PaymentReminder` value to the existing email department enum
    - _Requirements: 9.1, 9.2, 3.2_

- [x] 4. Service interfaces
  - [x] 4.1 Create IPaymentReminderScheduleService and IPaymentReminderService interfaces
    - Create `Portal.Infrastructure/Services/IPaymentReminderScheduleService.cs` with methods: GetScheduleAsync, SaveScheduleAsync, ValidateSchedule
    - Create `Portal.Infrastructure/Services/IPaymentReminderService.cs` with methods: EvaluateAndSendAsync, SendManualReminderAsync, GetHistoryByInvoiceAsync, GetDashboardWidgetDataAsync, GetEligibleBusinessIdsAsync
    - _Requirements: 1.1, 2.1, 4.3, 4.4, 5.1, 10.1_

- [x] 5. Implement PaymentReminderScheduleService
  - [x] 5.1 Implement PaymentReminderScheduleService
    - Implement `GetScheduleAsync` — return configured schedule or system defaults (Friendly -3 enabled, Firm +7 disabled, Formal +21 disabled)
    - Implement `SaveScheduleAsync` — upsert all 3 tiers for a business, set UpdatedAtUtc
    - Implement `ValidateSchedule` — validate integer offsets, enforce Friendly < Firm < Formal ordering, validate max/interval/suppression ranges
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7_

  - [ ]* 5.2 Write property test: Schedule Persistence Round-Trip
    - **Property 1: Schedule Persistence Round-Trip**
    - Generate random tier configs (offset ∈ [-30, 90], max ∈ [1,5], interval ∈ [1,30], suppression ∈ [1,30], enabled ∈ {true, false})
    - Save and retrieve must produce identical configuration
    - **Validates: Requirements 1.1, 1.2, 1.5, 1.6, 12.1**

  - [ ]* 5.3 Write property test: Schedule Validation Enforces Tier Ordering
    - **Property 2: Schedule Validation Enforces Tier Ordering**
    - Generate random int triplets, verify accept/reject matches Friendly < Firm < Formal predicate
    - **Validates: Requirements 1.4**

- [x] 6. Implement PaymentReminderService (evaluation logic)
  - [x] 6.1 Implement EvaluateAndSendAsync — core evaluation engine
    - Load schedule for business (or defaults)
    - Query eligible invoices (status IN {1,2,4}, not deleted)
    - For each invoice × enabled tier: check if evaluationDate matches DueDate + DaysOffset
    - Apply exclusion rules: opt-out, disputed, no email, recent partial payment, max reminders reached, min interval not elapsed
    - Implement idempotency check (existing successful log for same invoice/tier/date)
    - Send email via PortalEmailService and create PaymentReminderLog
    - Enforce tenant isolation (all queries filtered by businessId)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8, 2.9, 2.10, 3.7, 4.1, 4.2, 8.4_

  - [x] 6.2 Implement SendManualReminderAsync
    - Validate invoice status is eligible (Unpaid, PartiallyPaid, Overdue)
    - Check customer has email — return error if not
    - Check customer opt-out — return warning with `CustomerOptedOut = true`
    - Check invoice not disputed — return error if disputed
    - Send email and create log with `IsManualTrigger = true`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [x] 6.3 Implement GetHistoryByInvoiceAsync and GetDashboardWidgetDataAsync
    - `GetHistoryByInvoiceAsync` — query logs by BusinessId + InvoiceId, order descending by SentAtUtc, map to PaymentReminderLogDto
    - `GetDashboardWidgetDataAsync` — calculate total reminders this week, payments received within 7 days of a reminder for the same invoice
    - `GetEligibleBusinessIdsAsync` — query businesses with `payment_reminder_auto` module permission
    - _Requirements: 4.3, 4.4, 6.1, 6.4, 10.1, 10.4, 8.2_

  - [ ]* 6.4 Write property test: Evaluation Trigger Matching
    - **Property 3: Evaluation Trigger Matching**
    - Generate invoices with random DueDates, schedule with random offsets, random evaluation dates
    - Verify evaluation returns exactly the invoices where (evaluationDate - DueDate).Days equals a configured DaysOffset for an enabled tier
    - **Validates: Requirements 2.1, 2.10**

  - [ ]* 6.5 Write property test: Financial Status Filter
    - **Property 4: Financial Status Filter**
    - Generate invoices with random InvoiceFinancialStatusTypeId ∈ [1..5]
    - Verify only statuses {1, 2, 4} are included in candidate consideration
    - **Validates: Requirements 2.2, 5.6**

  - [ ]* 6.6 Write property test: Opt-Out Exclusion
    - **Property 5: Opt-Out Exclusion**
    - Generate customers with random IsReminderOptedOut, verify opted-out customers are always excluded
    - **Validates: Requirements 2.3, 7.2**

  - [ ]* 6.7 Write property test: Disputed Invoice Exclusion
    - **Property 6: Disputed Invoice Exclusion**
    - Generate invoices with random IsDisputed, verify disputed invoices are always excluded
    - **Validates: Requirements 2.4, 11.2**

  - [ ]* 6.8 Write property test: Partial Payment Suppression Window
    - **Property 7: Partial Payment Suppression Window**
    - Generate payments with random dates relative to evaluation date
    - Verify invoices with payment within N days are suppressed, those outside N days are not
    - **Validates: Requirements 2.5, 12.2, 12.3**

  - [ ]* 6.9 Write property test: Max Reminders Per Tier Cap
    - **Property 8: Max Reminders Per Tier Cap**
    - Generate varying counts of existing logs per tier
    - Verify invoices at or above max are excluded for that tier
    - **Validates: Requirements 2.7**

  - [ ]* 6.10 Write property test: Minimum Interval Enforcement
    - **Property 9: Minimum Interval Enforcement**
    - Generate last-reminder dates at varying distances from today
    - Verify invoices within min interval are excluded for that tier
    - **Validates: Requirements 2.8**

  - [ ]* 6.11 Write property test: Tenant Isolation
    - **Property 10: Tenant Isolation**
    - Generate data for 2 businesses, verify evaluation for Business A never returns data from Business B
    - **Validates: Requirements 2.9, 4.5, 7.4**

  - [ ]* 6.12 Write property test: Evaluation Idempotency
    - **Property 12: Evaluation Idempotency**
    - Generate a state, run evaluation twice for the same date, compare log counts — second run must not create duplicates
    - **Validates: Requirements 8.4**

  - [ ]* 6.13 Write property test: Dashboard Widget Calculation
    - **Property 13: Dashboard Widget Calculation**
    - Generate random logs and payments, verify "payments received after reminder" calculation matches spec (Payment within 0–7 days of most recent reminder for same invoice)
    - **Validates: Requirements 10.1, 10.4**

- [x] 7. Checkpoint - Core services complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Email template builder methods
  - [x] 8.1 Implement SendPaymentReminderEmailAsync and BuildPaymentReminderHtml
    - Add `SendPaymentReminderEmailAsync` to PortalEmailService with tier-specific subject lines
    - Implement `BuildPaymentReminderHtml` with tier-specific styling per locked mockups:
      - Friendly: Blue accent (#0D5EA6), "Payment Reminder" badge, "View Invoice" CTA
      - Firm: Amber accent (#C8912E), "Payment Overdue" badge, "Pay Now" CTA
      - Formal: Red accent (#C24A4A), "Final Notice" badge, "Settle Invoice" CTA
    - Include invoice number, outstanding amount, due date, business name in all templates
    - Footer: Business name + "Powered by 3 Inventors"
    - Use existing IEmailSender infrastructure with EmailDepartmentEnum.PaymentReminder
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [ ]* 8.2 Write property test: Email Content Completeness
    - **Property 11: Email Content Completeness**
    - Generate random invoice/customer/business data
    - Verify rendered HTML contains invoice number, outstanding amount, due date, and business name for all tiers
    - **Validates: Requirements 3.3**

- [x] 9. Background job
  - [x] 9.1 Implement PaymentReminderBackgroundService
    - Create `Portal.Web/BackgroundServices/PaymentReminderBackgroundService.cs`
    - Inherit from `BackgroundService` (Microsoft.Extensions.Hosting)
    - Implement daily timer with configurable scheduled time (default 06:00 UTC) from `appsettings.json`
    - Query eligible business IDs via `GetEligibleBusinessIdsAsync`
    - Process businesses sequentially with scoped services
    - Wrap each business in try/catch — log error and continue on failure
    - Add `PaymentReminders` configuration section to `appsettings.json`
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

- [x] 10. PaymentReminderController
  - [x] 10.1 Create PaymentReminderController with all endpoints
    - Create `Portal.Web/Controllers/PaymentReminderController.cs`
    - Class-level `[ModuleAccess(PortalModules.PaymentReminderManual)]`
    - `Settings()` action with `[ModuleAccess(PortalModules.PaymentReminderAuto)]` — returns schedule config page
    - `AxPostSaveSchedule` — validate and save schedule, return JSON success/error
    - `AxPostSendManualReminder(int invoiceId, string tier)` — send manual reminder, return JSON result
    - `AxGetReminderHistory(int invoiceId)` — return reminder log history as JSON
    - `AxGetDashboardWidget()` — return dashboard widget data as JSON
    - Follow BlockUI + SweetAlert2 pattern for all AJAX responses
    - _Requirements: 5.1, 5.2, 9.1, 9.2, 9.4_

- [x] 11. Views and UI
  - [x] 11.1 Create Settings page (Views/PaymentReminder/Settings.cshtml)
    - Schedule table with per-tier toggles (Friendly ON default, Firm/Formal OFF default)
    - Disabled tiers shown greyed out (opacity 0.5) but still configurable
    - Inputs for days offset, max reminders, min interval per tier
    - Suppression rules card with partial payment days input and system-wide toggle
    - Email preview card with tabbed interface (Friendly/Firm/Formal)
    - Save button using BlockUI + SweetAlert2 pattern
    - Match locked mockup design exactly
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6, 12.1_

  - [x] 11.2 Create reminder history partial (_ReminderHistoryPanel.cshtml)
    - Display all PaymentReminderLog records for an invoice
    - Show escalation tier, recipient email, date/time, manual/automated flag, success/failure
    - Order by date descending (most recent first)
    - Empty state message when no reminders sent
    - Wire to AxGetReminderHistory endpoint
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 11.3 Create dashboard widget partial (_ReminderDashboardWidget.cshtml)
    - Show total reminders sent this week and payments received after reminder
    - Zero-state with brief explanation when no reminders sent
    - Gate behind `payment_reminder_manual` module permission
    - Wire to AxGetDashboardWidget endpoint
    - _Requirements: 10.1, 10.2, 10.3_

  - [x] 11.4 Create teaser card partial (_ReminderTeaserCard.cshtml)
    - Soft-gate teaser for Starter plan users on Revenue Dashboard
    - Describe benefit of automated reminders
    - Include CTA to upgrade
    - Replace with functional widget when business has Professional plan
    - _Requirements: 13.1, 13.2, 13.3_

- [x] 12. JavaScript for Settings page
  - [x] 12.1 Implement settings page JavaScript
    - `saveSchedule()` function: collect tier data, BlockUI → fetch POST → unblock → SweetAlert2
    - Per-tier toggle handlers: greyed-out disabled state (opacity 0.5)
    - Email preview tab switching (Friendly/Firm/Formal)
    - Form validation before submit (offset ordering, required fields)
    - Include antiforgery token in POST requests
    - _Requirements: 1.2, 1.4_

- [x] 13. DI registration, navigation, and plan gating
  - [x] 13.1 Register services and background job in Program.cs
    - Register `IPaymentReminderScheduleService` → `PaymentReminderScheduleService` (scoped)
    - Register `IPaymentReminderService` → `PaymentReminderService` (scoped)
    - Register `PaymentReminderBackgroundService` as hosted service
    - _Requirements: 8.1_

  - [x] 13.2 Add navigation entry and plan gating seed data
    - Add sidebar navigation entry for Payment Reminders (under Revenue or as own section)
    - Ensure `[ModuleAccess]` attributes correctly gate: manual for Starter, auto for Professional
    - Verify soft-gate teaser displays when Starter user navigates to Settings
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

- [x] 14. Final checkpoint
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (13 properties total)
- Unit tests validate specific examples and edge cases
- The design uses C# (ASP.NET Core MVC 8) — all implementation follows existing Portal patterns
- Email templates must match locked mockup designs exactly (`.kiro/docs/mockups/LOCKED.md`)
- All AJAX calls must follow BlockUI → fetch → unblock → SweetAlert2 pattern per UI steering rules
- All SQL scripts must include `USE [Portal]` header per SQL schema design steering rules

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.2", "3.1", "3.2"] },
    { "id": 2, "tasks": ["4.1"] },
    { "id": 3, "tasks": ["5.1", "6.1", "6.2", "6.3"] },
    { "id": 4, "tasks": ["5.2", "5.3", "6.4", "6.5", "6.6", "6.7", "6.8", "6.9", "6.10", "6.11", "6.12", "6.13"] },
    { "id": 5, "tasks": ["8.1"] },
    { "id": 6, "tasks": ["8.2", "9.1"] },
    { "id": 7, "tasks": ["10.1"] },
    { "id": 8, "tasks": ["11.1", "11.2", "11.3", "11.4", "12.1"] },
    { "id": 9, "tasks": ["13.1", "13.2"] }
  ]
}
```
