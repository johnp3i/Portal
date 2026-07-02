# Implementation Plan: Payment Reminder Enhancements

## Overview

This plan implements three enhancements to the existing Payment Reminders module: Open Tracking (pixel embedding + event recording), Test Reminder Sending, and Upcoming Reminders Preview. Implementation follows a bottom-up approach — database first, then entity/service layer, then controllers and views — ensuring each step builds on the previous one with no orphaned code.

## Tasks

- [x] 1. Database migration and entity updates
  - [x] 1.1 Create SQL migration to add tracking and test send columns to PaymentReminderLog
    - Create migration file `Portal.Database/Migrations/099_AddOpenTrackingAndTestSendToPaymentReminderLog.sql`
    - Add columns: `TrackingToken` (NVARCHAR(64) NULL), `IsOpened` (BIT NOT NULL DEFAULT 0), `OpenedAtUtc` (DATETIME NULL), `OpenCount` (INT NOT NULL DEFAULT 0), `LastOpenedAtUtc` (DATETIME NULL), `IsTestSend` (BIT NOT NULL DEFAULT 0)
    - Create unique filtered index `UX_PaymentReminderLog_TrackingToken` on `TrackingToken` WHERE `TrackingToken IS NOT NULL`
    - Create filtered index `IX_PaymentReminderLog_BusinessId_IsTestSend` on `(BusinessId, InvoiceId, EscalationTier)` WHERE `IsTestSend = 0`
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 1.2 Update PaymentReminderLog entity class with new properties
    - Add `TrackingToken`, `IsOpened`, `OpenedAtUtc`, `OpenCount`, `LastOpenedAtUtc`, `IsTestSend` properties to `Portal.Infrastructure/Entities/PaymentReminderLog.cs`
    - _Requirements: 6.4_

  - [x] 1.3 Update EF Core DbContext configuration for new columns
    - Add property mappings with correct types, max lengths, defaults, and index definitions in the PortalDbContext configuration
    - Map `TrackingToken` with `HasMaxLength(64)`, unique filtered index
    - Map `IsOpened` and `IsTestSend` with `HasDefaultValue(false)`
    - Map `OpenCount` with `HasDefaultValue(0)`
    - Add filtered composite index for `(BusinessId, InvoiceId, EscalationTier)` WHERE `IsTestSend = 0`
    - _Requirements: 6.5_

- [x] 2. Utilities and constants
  - [x] 2.1 Create TrackingTokenGenerator static utility class
    - Create `Portal.Infrastructure/Services/TrackingTokenGenerator.cs`
    - Implement `Generate()` method using `RandomNumberGenerator.GetBytes(32)` with URL-safe Base64 encoding (replace `+` with `-`, `/` with `_`, trim `=`)
    - _Requirements: 1.4, 7.1_

  - [x] 2.2 Create TransparentPixel static constant class
    - Create `Portal.Web/Constants/TransparentPixel.cs`
    - Store pre-computed 1×1 transparent PNG as a static `byte[]` from Base64
    - _Requirements: 2.4_

- [x] 3. Checkpoint
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Email service and open tracking
  - [x] 4.1 Update PortalEmailService to inject tracking pixel and support test subject prefix
    - Update `SendPaymentReminderEmailAsync` signature to accept optional `trackingToken` and `isTestSend` parameters
    - When `trackingToken` is not null, append `<img src="{baseUrl}/PaymentReminder/Track/{trackingToken}" width="1" height="1" style="display:block" alt="" />` before closing `</body>`
    - When `isTestSend` is true, prefix the email subject with `[TEST] `
    - Update the `IEmailService` interface accordingly
    - _Requirements: 1.1, 1.2, 1.3, 4.6_

  - [x] 4.2 Implement RecordOpenEventAsync in PaymentReminderService
    - Add `RecordOpenEventAsync(string trackingToken)` to `IPaymentReminderService` interface
    - Implement: look up `PaymentReminderLog` by `TrackingToken`
    - If found and `IsOpened == false`: set `IsOpened = true`, `OpenedAtUtc = DateTime.UtcNow`, `OpenCount = 1`
    - If found and `IsOpened == true`: increment `OpenCount`, set `LastOpenedAtUtc = DateTime.UtcNow`
    - If not found: return silently (no error exposure)
    - Save changes
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 4.3 Add Track endpoint to PaymentReminderController
    - Add `[HttpGet][AllowAnonymous][ResponseCache(NoStore = true, Duration = 0)]` action `Track(string token)`
    - Call `RecordOpenEventAsync(token)` wrapped in try/catch (silently ignore errors)
    - Always return `File(TransparentPixel.Bytes, "image/png")` regardless of token validity
    - _Requirements: 2.4, 2.5, 2.6, 7.3_

- [x] 5. Update existing evaluation logic to exclude test sends
  - [x] 5.1 Add IsTestSend exclusion filters to EvaluateAndSendAsync
    - Add `&& !l.IsTestSend` to the idempotency check query (existing logs for today)
    - Add `&& !l.IsTestSend` to the max-reminders-per-tier count query
    - Add `&& !l.IsTestSend` to the min-interval last-reminder query
    - _Requirements: 4.4_

  - [x] 5.2 Add IsTestSend exclusion to dashboard widget calculations
    - Add `&& !l.IsTestSend` to the `GetDashboardWidgetDataAsync` queries for sent count and recent reminders
    - _Requirements: 4.5_

  - [x] 5.3 Update log creation in EvaluateAndSendAsync and SendManualReminderAsync to generate tracking tokens
    - Before creating each `PaymentReminderLog` entry (success or failure), generate a tracking token via `TrackingTokenGenerator.Generate()` and set `TrackingToken` property
    - Set `IsTestSend = false` explicitly on all real/manual sends
    - _Requirements: 1.4_

- [x] 6. Checkpoint
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Test reminder sending
  - [x] 7.1 Create TestReminderResult DTO
    - Create `Portal.Infrastructure/Models/PaymentReminders/TestReminderResult.cs` with `Success` (bool) and `Message` (string?) properties
    - _Requirements: 4.1_

  - [x] 7.2 Implement SendTestReminderAsync in PaymentReminderService
    - Add `SendTestReminderAsync(int businessId, int invoiceId, string escalationTier, string testRecipientEmail)` to `IPaymentReminderService`
    - Validate email format (contains `@`, non-empty local and domain parts with at least one `.`)
    - Validate invoice belongs to current business (tenant isolation)
    - Send email using `_emailService.SendPaymentReminderEmailAsync` with `isTestSend: true` and `trackingToken` generated
    - Create `PaymentReminderLog` with `IsTestSend = true`, `RecipientEmail = testRecipientEmail`
    - Return `TestReminderResult` with success/failure
    - _Requirements: 4.1, 4.2, 4.3, 4.6, 4.8, 7.4_

  - [x] 7.3 Add AxPostSendTestReminder endpoint to PaymentReminderController
    - Add `[HttpPost][ValidateAntiForgeryToken]` action accepting `invoiceId`, `escalationTier`, `testRecipientEmail`
    - Call `SendTestReminderAsync` and return `Json(new { success, message })`
    - Require `PaymentReminderManual` module access
    - _Requirements: 4.1, 4.7, 7.5_

  - [x] 7.4 Create _TestSendModal partial view and JavaScript
    - Create `Portal.Web/Views/Shared/_TestSendModal.cshtml`
    - Modal with: invoice readonly field, escalation tier select (Friendly/Firm/Formal), email input with "Send to my email" quick-link
    - Info note about [TEST] prefix and no count toward limits
    - Cancel + Send Test buttons
    - JavaScript: BlockUI.show → fetch POST to `/PaymentReminder/AxPostSendTestReminder` with antiforgery token → BlockUI.hide → Swal.fire result
    - _Requirements: 4.1, 4.6_

- [x] 8. Upcoming reminders preview
  - [x] 8.1 Create UpcomingReminderDto model
    - Create `Portal.Infrastructure/Models/PaymentReminders/UpcomingReminderDto.cs`
    - Properties: `ScheduledDate` (DateOnly), `InvoiceNumber` (string), `CustomerName` (string), `EscalationTier` (string), `OutstandingAmount` (decimal), `DueDate` (DateOnly)
    - _Requirements: 5.3_

  - [x] 8.2 Implement GetUpcomingRemindersAsync with EvaluateForDateRange refactor
    - Extract shared `EvaluateForDateRange(businessId, startDate, endDate, dryRun, tierFilter)` private method from `EvaluateAndSendAsync`
    - When `dryRun = true`: collect projections without sending emails or creating log entries
    - Apply same exclusion rules: opt-out, disputed, partial payment suppression, max cap (with `!IsTestSend` filter), min interval
    - Public method `GetUpcomingRemindersAsync(int businessId, int daysAhead = 14, string? tierFilter = null)` wraps `EvaluateForDateRange`
    - Return results ordered by `ScheduledDate` then `EscalationTier`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.9_

  - [x] 8.3 Add AxGetUpcomingReminders endpoint to PaymentReminderController
    - Add `[HttpGet][ModuleAccess(PortalModules.PaymentReminderAuto)]` action accepting `daysAhead` (default 14) and `tier` (optional)
    - Call `GetUpcomingRemindersAsync` and return `Json(new { success = true, data })`
    - _Requirements: 5.7, 5.8_

  - [x] 8.4 Create Upcoming.cshtml view page
    - Create `Portal.Web/Views/PaymentReminder/Upcoming.cshtml`
    - Add `[HttpGet][ModuleAccess(PortalModules.PaymentReminderAuto)]` page action `Upcoming()` returning the view
    - Topbar: eyebrow "Payment Reminders", heading "Upcoming Reminders", subtitle
    - Filter card (glass card-pad, margin-bottom:22px): Period dropdown (7/14/30 days), Tier dropdown (All/Friendly/Firm/Formal), Filter + Clear buttons
    - Data table card (glass card-pad): Columns — Date, Customer, Invoice, Amount Due, Tier (badge), Due Date
    - Summary text: "{N} reminders projected in the next {period} days"
    - Empty state: "No upcoming reminders projected."
    - JavaScript: on page load and filter change → BlockUI.show → fetch GET `/PaymentReminder/AxGetUpcomingReminders` → BlockUI.hide → render table or empty state
    - Match locked mockup layout exactly (`.kiro/docs/mockups/payment-reminder-enhancements.html` Section 1)
    - _Requirements: 5.7, 5.8_

- [x] 9. Checkpoint
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. History panel and navigation updates
  - [x] 10.1 Update PaymentReminderLogDto with open tracking and test send fields
    - Add `IsOpened`, `OpenedAtUtc`, `OpenCount`, `LastOpenedAtUtc`, `IsTestSend` to `PaymentReminderLogDto`
    - _Requirements: 3.1_

  - [x] 10.2 Update GetHistoryByInvoiceAsync to map new DTO fields
    - Update the `Select` projection in `GetHistoryByInvoiceAsync` to include `IsOpened`, `OpenedAtUtc`, `OpenCount`, `LastOpenedAtUtc`, `IsTestSend`
    - _Requirements: 3.1_

  - [x] 10.3 Update _ReminderHistoryPanel partial view with Open Tracking and Test badge columns
    - Add "Opened" column header to the table
    - Update `buildRow` function to render:
      - "Test" purple badge when `isTestSend === true` in the Method column
      - "Opened (Nx)" green badge with first-opened timestamp when `isOpened === true` and `openCount > 1`
      - "Opened" green badge with timestamp when `isOpened === true` and `openCount <= 1`
      - "Not opened" italic muted text when `isOpened === false` and `isSentSuccessfully === true`
      - Em-dash for failed sends
    - Match locked mockup Section 2 exactly
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 10.4 Add sidebar navigation link for Upcoming Reminders page
    - Add navigation entry in the sidebar partial for `/PaymentReminder/Upcoming`
    - Conditionally show based on `PaymentReminderAuto` module access
    - _Requirements: 5.7_

- [x] 11. Final checkpoint
  - Ensure all tests pass, ask the user if questions arise.

- [ ]* 12. Property-based tests (optional)
  - [ ]* 12.1 Write property test for tracking token entropy and uniqueness
    - **Property 2: Tracking Token Entropy and Uniqueness**
    - Generate 100+ tokens, verify each decodes to ≥32 bytes, verify no duplicates
    - **Validates: Requirements 1.4, 7.1**

  - [ ]* 12.2 Write property test for open event state machine transitions
    - **Property 3: Open Event State Machine**
    - Test first-open sets IsOpened=true/OpenedAtUtc/OpenCount=1; subsequent open increments OpenCount and updates LastOpenedAtUtc without changing OpenedAtUtc
    - **Validates: Requirements 2.2, 2.3**

  - [ ]* 12.3 Write property test for test send exclusion from caps and metrics
    - **Property 5: Test Send Exclusion from Caps and Metrics**
    - Generate random log sets with mixed IsTestSend values; verify cap/interval/idempotency queries only count IsTestSend=false entries
    - **Validates: Requirements 3.5, 4.4, 4.5**

  - [ ]* 12.4 Write property test for test email subject prefix
    - **Property 6: Test Email Subject Prefix**
    - For random invoice data, verify test render subject = "[TEST] " + real render subject; body content identical
    - **Validates: Requirements 4.6**

  - [ ]* 12.5 Write property test for email validation logic
    - **Property 7: Test Recipient Email Validation**
    - Generate random strings; verify only well-formed emails (one @, non-empty local, domain with .) pass validation
    - **Validates: Requirements 4.2**

  - [ ]* 12.6 Write property test for upcoming preview window bounds
    - **Property 8: Upcoming Preview Window Bounds**
    - For random daysAhead values and schedules, verify all returned ScheduledDate values fall within [today, today+N]
    - **Validates: Requirements 5.1**

  - [ ]* 12.7 Write property test for upcoming preview side-effect freedom
    - **Property 10: Upcoming Preview Side-Effect Freedom**
    - Verify PaymentReminderLog row count is identical before and after calling GetUpcomingRemindersAsync
    - **Validates: Requirements 5.5**

  - [ ]* 12.8 Write property test for test send tenant isolation
    - **Property 12: Test Send Tenant Isolation**
    - Verify SendTestReminderAsync from Business B for an invoice belonging to Business A returns error and creates no log entry
    - **Validates: Requirements 4.8**

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The locked mockup (`.kiro/docs/mockups/payment-reminder-enhancements.html`) is the visual source of truth for all UI work
- Existing infrastructure (`PaymentReminderService`, `PaymentReminderController`, `_ReminderHistoryPanel`) is extended, not replaced
- All AJAX calls must follow BlockUI → fetch → BlockUI.hide → Swal.fire pattern per project standards
- Use `TrackingTokenGenerator.Generate()` everywhere a new log is created (both real and test sends)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["2.1", "2.2", "7.1", "8.1"] },
    { "id": 3, "tasks": ["4.1", "5.1", "5.2", "5.3"] },
    { "id": 4, "tasks": ["4.2", "4.3"] },
    { "id": 5, "tasks": ["7.2", "10.1"] },
    { "id": 6, "tasks": ["7.3", "7.4", "8.2", "10.2"] },
    { "id": 7, "tasks": ["8.3", "8.4", "10.3", "10.4"] },
    { "id": 8, "tasks": ["12.1", "12.2", "12.3", "12.4", "12.5", "12.6", "12.7", "12.8"] }
  ]
}
```
