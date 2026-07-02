# Design Document: Payment Reminder Enhancements

## Overview

This design extends the existing Payment Reminders module with three new capabilities:

1. **Open Tracking** — Embeds a 1×1 transparent pixel in each reminder email; when the recipient's email client loads the image, the system records the open event (timestamp, count).
2. **Test Reminder Sending** — Allows business owners to send a preview reminder to an alternate email (their own), marked as `[TEST]`, excluded from all caps/metrics.
3. **Upcoming Reminders Preview** — A read-only projection showing which reminders will fire in the coming N days, reusing the same evaluation logic in "dry run" mode.

All three enhancements build on the existing `PaymentReminderService`, `PaymentReminderLog` table, and `PaymentReminderController` without modifying the core evaluation algorithm's behaviour for real sends.

**Locked Mockup Reference:** `.kiro/docs/mockups/payment-reminder-enhancements.html`

## Architecture

```mermaid
graph TB
    subgraph "Web Layer (Portal.Web)"
        RC[PaymentReminderController]
        TP[Track Endpoint<br/>/PaymentReminder/Track/{token}]
        UP[Upcoming Page<br/>/PaymentReminder/Upcoming]
        HP[Enhanced _ReminderHistoryPanel]
        TSM[Test Send Modal]
    end

    subgraph "Service Layer (Portal.Infrastructure)"
        PRS[IPaymentReminderService<br/>+ SendTestReminderAsync<br/>+ GetUpcomingRemindersAsync<br/>+ RecordOpenEventAsync]
        TG[TrackingTokenGenerator]
    end

    subgraph "Email Rendering"
        PES[PortalEmailService<br/>+ tracking pixel injection]
    end

    subgraph "Data Layer"
        DB[(SQL Server)]
        PRL_T["[reminder].PaymentReminderLog<br/>+ TrackingToken<br/>+ IsOpened / OpenCount<br/>+ IsTestSend"]
    end

    RC --> PRS
    TP -->|Anonymous| PRS
    UP --> PRS
    HP -->|AJAX| RC
    TSM -->|AJAX| RC
    PRS --> PES
    PRS --> TG
    PRS --> DB
    DB --> PRL_T
```

### Key Architectural Decisions

1. **Tracking pixel as anonymous endpoint** — The `/PaymentReminder/Track/{token}` endpoint must be anonymous because email clients load images without authentication. A cryptographically random 32-byte token prevents enumeration.

2. **Dry-run projection reuses evaluation logic** — Rather than duplicating the exclusion rules, `GetUpcomingRemindersAsync` calls a shared private method `EvaluateInvoicesForDate` with a `dryRun: true` parameter that skips the send step and log creation.

3. **IsTestSend flag on existing table** — Instead of a separate table, a single BIT column cleanly separates test sends from real sends. All existing queries that calculate caps/metrics gain a `WHERE IsTestSend = 0` filter.

4. **Token stored on log row** — The `TrackingToken` is generated at log creation time and stored on the same row. A unique filtered index enables O(1) lookup for the tracking endpoint.

5. **No caching on upcoming preview** — Projections are computed fresh on each request to reflect schedule changes immediately.

## Components and Interfaces

### 1. Updated IPaymentReminderService

New methods added to the existing interface:

```csharp
namespace Portal.Infrastructure.Services;

public interface IPaymentReminderService
{
    // --- Existing methods (unchanged) ---
    Task<ReminderEvaluationResult> EvaluateAndSendAsync(int businessId, DateOnly evaluationDate);
    Task<ManualReminderResult> SendManualReminderAsync(int businessId, int invoiceId, string escalationTier);
    Task<List<PaymentReminderLogDto>> GetHistoryByInvoiceAsync(int businessId, int invoiceId);
    Task<ReminderDashboardWidgetDto> GetDashboardWidgetDataAsync(int businessId);
    Task<List<int>> GetEligibleBusinessIdsAsync();

    // --- New methods ---

    /// <summary>
    /// Sends a test reminder to an alternate email address.
    /// Creates a log entry with IsTestSend = true. Excluded from caps/metrics.
    /// </summary>
    Task<TestReminderResult> SendTestReminderAsync(
        int businessId, int invoiceId, string escalationTier, string testRecipientEmail);

    /// <summary>
    /// Projects upcoming reminders for the next N days using the same evaluation
    /// logic as EvaluateAndSendAsync but in dry-run mode (no sends, no log creation).
    /// </summary>
    Task<List<UpcomingReminderDto>> GetUpcomingRemindersAsync(
        int businessId, int daysAhead = 14, string? tierFilter = null);

    /// <summary>
    /// Records an email open event for a tracking token.
    /// If first open: sets IsOpened=true, OpenedAtUtc.
    /// If subsequent: increments OpenCount, updates LastOpenedAtUtc.
    /// </summary>
    Task RecordOpenEventAsync(string trackingToken);
}
```

### 2. TrackingTokenGenerator (New Utility)

```csharp
namespace Portal.Infrastructure.Services;

/// <summary>
/// Generates cryptographically secure tracking tokens for email open tracking.
/// Produces URL-safe Base64-encoded tokens from 32 bytes of entropy.
/// </summary>
public static class TrackingTokenGenerator
{
    private const int TokenByteLength = 32;

    /// <summary>
    /// Generates a new URL-safe Base64-encoded tracking token (32 bytes of entropy).
    /// </summary>
    public static string Generate()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(TokenByteLength);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
```

### 3. Updated PaymentReminderController

```csharp
namespace Portal.Web.Controllers;

[Authorize]
[ModuleAccess(PortalModules.PaymentReminderManual)]
public class PaymentReminderController : Controller
{
    // --- Existing endpoints unchanged ---

    // --- New Page Actions ---

    /// <summary>
    /// Upcoming reminders preview page — requires PaymentReminderAuto permission.
    /// </summary>
    [HttpGet]
    [ModuleAccess(PortalModules.PaymentReminderAuto)]
    public IActionResult Upcoming()
    {
        return View();
    }

    // --- New AJAX Endpoints ---

    /// <summary>
    /// Returns projected upcoming reminders for the next N days.
    /// </summary>
    [HttpGet]
    [ModuleAccess(PortalModules.PaymentReminderAuto)]
    public async Task<IActionResult> AxGetUpcomingReminders(int daysAhead = 14, string? tier = null)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var data = await _reminderService.GetUpcomingRemindersAsync(businessId, daysAhead, tier);
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to load upcoming reminders." });
        }
    }

    /// <summary>
    /// Sends a test reminder to an alternate email address.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AxPostSendTestReminder(
        int invoiceId, string escalationTier, string testRecipientEmail)
    {
        try
        {
            var businessId = _currentTenantService.CurrentBusinessId;
            var result = await _reminderService.SendTestReminderAsync(
                businessId, invoiceId, escalationTier, testRecipientEmail);
            return Json(new { success = result.Success, message = result.Message });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "Failed to send test reminder." });
        }
    }

    /// <summary>
    /// Tracking pixel endpoint — anonymous, returns 1x1 transparent PNG.
    /// Records email open event for the given tracking token.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Duration = 0)]
    public async Task<IActionResult> Track(string token)
    {
        try
        {
            if (!string.IsNullOrEmpty(token))
            {
                await _reminderService.RecordOpenEventAsync(token);
            }
        }
        catch (Exception ex)
        {
            // Silently fail — never expose tracking errors to recipient
        }

        // Always return the same 1x1 transparent PNG
        return File(TransparentPixel.Bytes, "image/png");
    }
}
```

### 4. TransparentPixel (Static Resource)

```csharp
namespace Portal.Web.Constants;

/// <summary>
/// Static 1x1 transparent PNG bytes used for the tracking pixel response.
/// Pre-computed to avoid repeated file I/O.
/// </summary>
public static class TransparentPixel
{
    // Minimal valid 1x1 transparent PNG (67 bytes)
    public static readonly byte[] Bytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVQI12NgAAIABQAB" +
        "Nl7pcQAAAABJRU5ErkJggg==");
}
```

### 5. Updated PortalEmailService (Tracking Pixel Injection)

The `SendPaymentReminderEmailAsync` method gains a new parameter `trackingToken` to inject the pixel:

```csharp
// Updated signature
Task SendPaymentReminderEmailAsync(
    string toEmail, string customerName, string invoiceNumber,
    decimal outstandingAmount, DateOnly dueDate, string businessName,
    string escalationTier, string? invoiceShareToken, string baseUrl,
    string? trackingToken = null, bool isTestSend = false);
```

**Changes in implementation:**
- If `trackingToken` is not null, append a tracking pixel `<img>` tag before the closing `</body>` tag:
  ```html
  <img src="{baseUrl}/PaymentReminder/Track/{trackingToken}" width="1" height="1" style="display:block" alt="" />
  ```
- If `isTestSend` is true, prefix the subject line with `[TEST] `.

### 6. Updated Evaluation Logic (Exclusion of Test Sends)

All existing queries in `PaymentReminderService` that count logs for cap/interval/idempotency checks gain `&& !l.IsTestSend`:

```csharp
// Idempotency check — exclude test sends
var existingLogs = await _dbContext.PaymentReminderLogs
    .Where(l => l.BusinessId == businessId
               && !l.IsTestSend  // NEW
               && l.SentAtUtc >= evaluationDateStart
               && l.SentAtUtc < evaluationDateEnd)
    .ToListAsync();

// Max reminders per tier — exclude test sends
var tierLogCount = await _dbContext.PaymentReminderLogs
    .CountAsync(l => l.InvoiceId == invoice.Id
                   && l.EscalationTier == tier.EscalationTier
                   && l.IsSentSuccessfully
                   && !l.IsTestSend);  // NEW

// Min interval check — exclude test sends
var lastSameTypeReminder = await _dbContext.PaymentReminderLogs
    .Where(l => l.InvoiceId == invoice.Id
               && l.EscalationTier == tier.EscalationTier
               && l.IsSentSuccessfully
               && !l.IsTestSend)  // NEW
    .OrderByDescending(l => l.SentAtUtc)
    .FirstOrDefaultAsync();
```

### 7. Upcoming Reminders — Dry-Run Projection

The projection logic is extracted into a shared method:

```csharp
/// <summary>
/// Core evaluation logic shared between EvaluateAndSendAsync and GetUpcomingRemindersAsync.
/// When dryRun=true, returns projections without sending or logging.
/// </summary>
private async Task<List<ReminderProjection>> EvaluateForDateRange(
    int businessId, DateOnly startDate, DateOnly endDate, bool dryRun, string? tierFilter = null)
{
    var projections = new List<ReminderProjection>();

    var schedule = await _scheduleService.GetScheduleAsync(businessId);
    var enabledTiers = schedule
        .Where(t => t.IsEnabled)
        .Where(t => tierFilter == null || t.EscalationTier == tierFilter)
        .ToList();

    if (!enabledTiers.Any()) return projections;

    // Load eligible invoices (same filter as EvaluateAndSendAsync)
    var invoices = await _dbContext.Invoices
        .Include(i => i.Customer)
        .Where(i => i.BusinessId == businessId
                    && !i.IsDeleted
                    && !i.IsDisputed
                    && EligibleFinancialStatuses.Contains(i.InvoiceFinancialStatusTypeId))
        .ToListAsync();

    var suppressionDays = enabledTiers.First().PartialPaymentSuppressionDays;

    foreach (var invoice in invoices)
    {
        if (string.IsNullOrEmpty(invoice.Customer?.Email)) continue;
        if (invoice.Customer.IsReminderOptedOut) continue;

        foreach (var tier in enabledTiers)
        {
            var triggerDate = DateOnly.FromDateTime(invoice.DueDate.AddDays(tier.DaysOffset));

            if (triggerDate < startDate || triggerDate > endDate) continue;

            // Apply same exclusion rules (suppression, max cap, min interval)
            // ... (same logic as existing EvaluateAndSendAsync)

            projections.Add(new ReminderProjection
            {
                ScheduledDate = triggerDate,
                InvoiceNumber = invoice.InvoiceNumber,
                CustomerName = invoice.Customer.Name,
                EscalationTier = tier.EscalationTier,
                OutstandingAmount = /* calculated */,
                DueDate = DateOnly.FromDateTime(invoice.DueDate)
            });
        }
    }

    return projections.OrderBy(p => p.ScheduledDate).ThenBy(p => p.EscalationTier).ToList();
}
```

The public method wraps this:

```csharp
public async Task<List<UpcomingReminderDto>> GetUpcomingRemindersAsync(
    int businessId, int daysAhead = 14, string? tierFilter = null)
{
    try
    {
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var endDate = startDate.AddDays(daysAhead);

        var projections = await EvaluateForDateRange(
            businessId, startDate, endDate, dryRun: true, tierFilter);

        return projections.Select(p => new UpcomingReminderDto
        {
            ScheduledDate = p.ScheduledDate,
            InvoiceNumber = p.InvoiceNumber,
            CustomerName = p.CustomerName,
            EscalationTier = p.EscalationTier,
            OutstandingAmount = p.OutstandingAmount,
            DueDate = p.DueDate
        }).ToList();
    }
    catch (Exception ex)
    {
        throw;
    }
}
```

### 8. View Structure (Referencing Locked Mockup)

| View / Partial | Route | Purpose | Mockup Section |
|----------------|-------|---------|----------------|
| `Views/PaymentReminder/Upcoming.cshtml` | `/PaymentReminder/Upcoming` | Read-only projection of upcoming reminders | Section 1: Upcoming Reminders Page |
| Updated `_ReminderHistoryPanel.cshtml` | Partial on Invoice Detail | Enhanced with Open Tracking + Test badge columns | Section 2: Enhanced History Panel |
| `_TestSendModal.cshtml` (new partial) | Modal on Invoice Detail | Test send form (tier picker + email input) | Section 3: Test Send Modal |

#### Upcoming Page Structure (from mockup)

- **Topbar**: Eyebrow "Payment Reminders", heading "Upcoming Reminders", subtitle
- **Filter card** (`glass card-pad`, `margin-bottom:22px`): Period dropdown (7/14/30 days), Tier dropdown (All/Friendly/Firm/Formal), Filter + Clear buttons
- **Data table card** (`glass card-pad`): Columns — Date, Customer, Invoice, Amount Due, Tier (badge), Due Date
- **Summary text**: "{N} reminders projected in the next {period} days"

#### Enhanced History Panel (from mockup)

New columns added to the existing table:
- **Method**: `Auto` (muted badge), `Manual` (blue badge), or `Test` (purple badge)
- **Opened**: `Opened (Nx)` (green badge + timestamp), or `Not opened` (italic muted), or `—` for failed sends

#### Test Send Modal (from mockup)

- **Heading**: "Send Test Reminder"
- **Subtitle**: "Send a preview to verify email content before sending to the customer."
- **Fields**: Invoice (readonly), Escalation Tier (select), Send to email (input + quick link "Send to my email")
- **Info note**: "Test reminders are marked as [TEST] in the subject line and don't count toward reminder limits."
- **Buttons**: Cancel (secondary), Send Test (primary)

## Data Models

### Database Migration: ALTER PaymentReminderLog (Add Tracking & Test Columns)

```sql
-- ============================================================
-- Add open tracking and test send columns to PaymentReminderLog
-- ============================================================

USE [Portal]
GO

-- Add tracking token column (URL-safe Base64, max 64 chars for 32 bytes)
ALTER TABLE [reminder].[PaymentReminderLog]
    ADD [TrackingToken] NVARCHAR(64) NULL
GO

-- Add open tracking columns
ALTER TABLE [reminder].[PaymentReminderLog]
    ADD [IsOpened] BIT NOT NULL CONSTRAINT [DF_PaymentReminderLog_IsOpened] DEFAULT 0
GO

ALTER TABLE [reminder].[PaymentReminderLog]
    ADD [OpenedAtUtc] DATETIME NULL
GO

ALTER TABLE [reminder].[PaymentReminderLog]
    ADD [OpenCount] INT NOT NULL CONSTRAINT [DF_PaymentReminderLog_OpenCount] DEFAULT 0
GO

ALTER TABLE [reminder].[PaymentReminderLog]
    ADD [LastOpenedAtUtc] DATETIME NULL
GO

-- Add test send flag
ALTER TABLE [reminder].[PaymentReminderLog]
    ADD [IsTestSend] BIT NOT NULL CONSTRAINT [DF_PaymentReminderLog_IsTestSend] DEFAULT 0
GO

-- Unique filtered index for fast token lookup (only non-null tokens)
CREATE UNIQUE NONCLUSTERED INDEX [UX_PaymentReminderLog_TrackingToken]
    ON [reminder].[PaymentReminderLog]([TrackingToken])
    WHERE [TrackingToken] IS NOT NULL
GO

-- Filtered index for efficient queries excluding test sends
CREATE NONCLUSTERED INDEX [IX_PaymentReminderLog_BusinessId_IsTestSend]
    ON [reminder].[PaymentReminderLog]([BusinessId], [InvoiceId], [EscalationTier])
    WHERE [IsTestSend] = 0
GO
```

### Updated Entity Class: PaymentReminderLog

```csharp
namespace Portal.Infrastructure.Entities;

/// <summary>
/// Audit record for each reminder email sent (or failed).
/// Schema: [reminder].PaymentReminderLog
/// </summary>
public class PaymentReminderLog
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int InvoiceId { get; set; }
    public int CustomerId { get; set; }
    public string RecipientEmail { get; set; } = null!;
    public string EscalationTier { get; set; } = null!;
    public bool IsSentSuccessfully { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsManualTrigger { get; set; }
    public DateTime SentAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // --- Open Tracking (NEW) ---
    public string? TrackingToken { get; set; }
    public bool IsOpened { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public int OpenCount { get; set; }
    public DateTime? LastOpenedAtUtc { get; set; }

    // --- Test Send Flag (NEW) ---
    public bool IsTestSend { get; set; }

    // Navigation properties
    public Business Business { get; set; } = null!;
    public Invoice Invoice { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}
```

### Updated EF Core Configuration

Add to the existing `ConfigurePaymentReminderLog` method:

```csharp
// Open Tracking columns
entity.Property(e => e.TrackingToken)
    .HasMaxLength(64);

entity.HasIndex(e => e.TrackingToken)
    .IsUnique()
    .HasFilter("[TrackingToken] IS NOT NULL")
    .HasDatabaseName("UX_PaymentReminderLog_TrackingToken");

entity.Property(e => e.IsOpened)
    .IsRequired()
    .HasDefaultValue(false);

entity.Property(e => e.OpenedAtUtc);

entity.Property(e => e.OpenCount)
    .IsRequired()
    .HasDefaultValue(0);

entity.Property(e => e.LastOpenedAtUtc);

// Test Send flag
entity.Property(e => e.IsTestSend)
    .IsRequired()
    .HasDefaultValue(false);

// Filtered index for queries excluding test sends
entity.HasIndex(e => new { e.BusinessId, e.InvoiceId, e.EscalationTier })
    .HasFilter("[IsTestSend] = 0")
    .HasDatabaseName("IX_PaymentReminderLog_BusinessId_IsTestSend");
```

### New DTOs

```csharp
namespace Portal.Infrastructure.Models.PaymentReminders;

/// <summary>
/// Result of sending a test reminder.
/// </summary>
public class TestReminderResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Projected upcoming reminder (read-only, not yet sent).
/// </summary>
public class UpcomingReminderDto
{
    public DateOnly ScheduledDate { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public string CustomerName { get; set; } = null!;
    public string EscalationTier { get; set; } = null!;
    public decimal OutstandingAmount { get; set; }
    public DateOnly DueDate { get; set; }
}
```

### Updated PaymentReminderLogDto

```csharp
namespace Portal.Infrastructure.Models.PaymentReminders;

public class PaymentReminderLogDto
{
    public string EscalationTier { get; set; } = null!;
    public string RecipientEmail { get; set; } = null!;
    public DateTime SentAtUtc { get; set; }
    public bool IsManualTrigger { get; set; }
    public bool IsSentSuccessfully { get; set; }
    public string? ErrorMessage { get; set; }

    // --- New fields ---
    public bool IsOpened { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public int OpenCount { get; set; }
    public DateTime? LastOpenedAtUtc { get; set; }
    public bool IsTestSend { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Tracking Pixel Embedding

*For any* payment reminder email rendered with a non-null tracking token, the resulting HTML SHALL contain exactly one `<img>` tag whose `src` attribute matches the pattern `/PaymentReminder/Track/{trackingToken}` with `width="1"`, `height="1"`, and `style="display:block"` attributes.

**Validates: Requirements 1.1, 1.2, 1.3**

### Property 2: Tracking Token Entropy and Uniqueness

*For any* set of N generated tracking tokens (where N ≥ 100), each token SHALL decode to at least 32 bytes of data, and no two tokens in the set SHALL be equal.

**Validates: Requirements 1.4, 7.1**

### Property 3: Open Event State Machine

*For any* `PaymentReminderLog` record with a valid `TrackingToken`:
- If `IsOpened` is false before an open event, then after the event `IsOpened` SHALL be true, `OpenedAtUtc` SHALL be set to the current UTC time, and `OpenCount` SHALL be 1.
- If `IsOpened` is true with `OpenCount = N` before an open event, then after the event `OpenCount` SHALL be N+1 and `LastOpenedAtUtc` SHALL be updated to the current UTC time, while `OpenedAtUtc` SHALL remain unchanged.

**Validates: Requirements 2.2, 2.3**

### Property 4: History DTO Field Completeness

*For any* `PaymentReminderLog` record, when retrieved via `GetHistoryByInvoiceAsync`, the resulting `PaymentReminderLogDto` SHALL include the `IsOpened`, `OpenedAtUtc`, `OpenCount`, `LastOpenedAtUtc`, and `IsTestSend` fields mapped correctly from the entity.

**Validates: Requirements 3.1**

### Property 5: Test Send Exclusion from Caps and Metrics

*For any* set of `PaymentReminderLog` entries for an invoice where some have `IsTestSend = true`:
- The evaluation engine's max-reminders-per-tier count SHALL only include entries where `IsTestSend = false`.
- The min-interval check SHALL only consider entries where `IsTestSend = false`.
- The idempotency check SHALL only consider entries where `IsTestSend = false`.
- The dashboard widget metrics SHALL only include entries where `IsTestSend = false`.

**Validates: Requirements 3.5, 4.4, 4.5**

### Property 6: Test Email Subject Prefix

*For any* invoice data, customer name, and escalation tier, when a test reminder is rendered the subject line SHALL be identical to a real reminder's subject except prefixed with `[TEST] `. The HTML body content SHALL be identical.

**Validates: Requirements 4.6**

### Property 7: Test Recipient Email Validation

*For any* string input as `testRecipientEmail`, the `SendTestReminderAsync` method SHALL reject strings that do not match a well-formed email pattern (containing exactly one `@`, a non-empty local part, and a non-empty domain with at least one `.`).

**Validates: Requirements 4.2**

### Property 8: Upcoming Preview Window Bounds

*For any* request to `GetUpcomingRemindersAsync` with `daysAhead = N`, all returned `UpcomingReminderDto` records SHALL have a `ScheduledDate` between today (inclusive) and today + N days (inclusive). No record outside this range SHALL be returned.

**Validates: Requirements 5.1**

### Property 9: Upcoming Preview Logic Equivalence

*For any* business with a configured schedule and eligible invoices, for each date D within the preview window, the set of (InvoiceId, EscalationTier) pairs projected by `GetUpcomingRemindersAsync` SHALL match exactly the set that `EvaluateAndSendAsync(businessId, D)` would send to (assuming no sends have occurred between the preview request and D).

**Validates: Requirements 5.2**

### Property 10: Upcoming Preview Side-Effect Freedom

*For any* call to `GetUpcomingRemindersAsync`, the count of rows in `[reminder].[PaymentReminderLog]` SHALL be identical before and after the call, and no email send operations SHALL be invoked.

**Validates: Requirements 5.5**

### Property 11: Schedule Change Reactivity

*For any* schedule modification followed by a call to `GetUpcomingRemindersAsync`, the returned projections SHALL reflect the updated schedule configuration. Specifically, disabling a tier SHALL remove all projections for that tier, and changing a `DaysOffset` SHALL shift the projected dates accordingly.

**Validates: Requirements 5.9**

### Property 12: Test Send Tenant Isolation

*For any* invoice belonging to Business A, a call to `SendTestReminderAsync` from Business B SHALL return a validation error and SHALL NOT create any `PaymentReminderLog` entry.

**Validates: Requirements 4.8**

## Error Handling

| Scenario | Handling | User Impact |
|----------|----------|-------------|
| Tracking pixel request with invalid/missing token | Return 1×1 PNG silently (no error exposed) | None — recipient sees nothing different |
| Tracking pixel request when DB write fails | Catch exception, log warning, still return PNG | None — open event lost but email not disrupted |
| Rate limit exceeded on tracking endpoint | Return 429 after 100 requests/token/hour | None — tracking pixel still returns image on next window |
| Test send with invalid email format | Return `{ success: false, message: "..." }` | SweetAlert2 warning shown in modal |
| Test send with non-existent invoice | Return `{ success: false, message: "Invoice not found." }` | SweetAlert2 error in modal |
| Test send for invoice from different business | Return `{ success: false, message: "Invoice not found." }` (same message to avoid info leak) | SweetAlert2 error in modal |
| Test send email delivery failure | Log with `IsSentSuccessfully = false`, return error | SweetAlert2 error: "Failed to send test reminder." |
| Upcoming preview with no qualifying invoices | Return `{ success: true, data: [] }` | Empty state shown: "No upcoming reminders projected." |
| Upcoming preview service error | Catch exception, return `{ success: false, message: "..." }` | SweetAlert2 error shown |
| RecordOpenEventAsync concurrent writes | Use optimistic concurrency (EF Core row version or retry) | None — worst case: one open event lost |
| Email rendering fails to inject pixel | Log error, send email without pixel (degrade gracefully) | Email delivered but open tracking unavailable for that send |

## Testing Strategy

### Property-Based Tests (using FsCheck / xUnit)

Property-based testing applies well to this feature because the core logic involves:
- Token generation with entropy guarantees (randomness, uniqueness)
- State machine transitions (open event recording)
- Filter/projection logic over variable input sets (upcoming preview, test exclusion)
- Validation functions over arbitrary string inputs (email validation)

**Library:** FsCheck (with FsCheck.Xunit) — already the standard PBT library for .NET in this repository.

**Configuration:** Minimum 100 iterations per property test.

**Tag format:** `Feature: payment-reminder-enhancements, Property {number}: {property_text}`

| Property # | Test Target | What Varies |
|------------|-------------|-------------|
| 1 | Email HTML rendering | Random tracking tokens, invoice data |
| 2 | TrackingTokenGenerator.Generate() | N generations |
| 3 | RecordOpenEventAsync | Random initial states (opened/not, various OpenCounts) |
| 4 | GetHistoryByInvoiceAsync DTO mapping | Random log entries with various field combinations |
| 5 | Evaluation queries | Random log sets with mixed IsTestSend values |
| 6 | SendPaymentReminderEmailAsync | Random invoice data, both test/real mode |
| 7 | Email validation logic | Random strings (valid/invalid formats) |
| 8 | GetUpcomingRemindersAsync | Random schedules, invoices, daysAhead values |
| 9 | EvaluateForDateRange equivalence | Random business data, date ranges |
| 10 | GetUpcomingRemindersAsync side effects | Random inputs, verify no DB writes |
| 11 | Schedule change → preview change | Before/after schedule modifications |
| 12 | SendTestReminderAsync tenant check | Cross-business invoice references |

### Unit Tests (Example-Based)

| Area | Test Cases |
|------|-----------|
| Track endpoint response | Returns correct content-type, cache headers, and 67-byte PNG body |
| Track endpoint with invalid token | Returns same PNG (no error leakage) |
| Track endpoint anonymous access | Accessible without `[Authorize]` |
| History panel "Opened" badge | Renders "Opened (2×)" when OpenCount > 1 |
| History panel "Not opened" | Renders italic muted text |
| History panel "Test" badge | Renders purple Test badge for IsTestSend entries |
| Test send modal form | Accepts invoiceId, tier, and email; calls endpoint correctly |
| Antiforgery on test send | POST without token returns 400 |

### Integration Tests

| Area | Test Cases |
|------|-----------|
| Full tracking flow | Send email → extract pixel URL → GET pixel → verify DB updated |
| Test send end-to-end | POST test send → verify log created with IsTestSend=true → verify email received |
| Upcoming preview accuracy | Seed data → call preview → compare with manual evaluation for each date |
| Rate limiting | 101 requests to same token → verify 429 on 101st |
