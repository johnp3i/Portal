# Design Document: Dashboard Briefing

## Overview

The Dashboard Briefing renders a narrative summary at the top of the main dashboard. It queries business data, assembles prioritized insights from sentence templates, and renders a concise, human-readable card that tells the user what matters right now.

The system is entirely template-driven (no AI/LLM dependency). Each "signal" is a data query paired with a conditional sentence template. Signals are evaluated in priority order, and the top 6 are rendered.

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Server-rendered (not AJAX) | Avoids layout shift; data queries are fast (counts/sums) |
| Template-driven (no LLM) | Deterministic, fast, no external API cost, no latency |
| Max 6 insights | Keeps the card concise — more would overwhelm |
| Priority ordering | Most urgent items surface first regardless of data availability |
| Permission-aware | Only show signals the user can act on |
| Flowing paragraph style | Feels like a human speaking, not a bullet list report |

---

## Architecture

```
HomeController.Index()
    → DashboardBriefingService.GenerateBriefingAsync(businessId, userId, permissions)
        → Signal evaluators (one per data source)
        → Sentence assembly
        → Return BriefingViewModel
    → ViewBag.Briefing = result
    → _DashboardBriefing.cshtml partial
```

### Components

**DashboardBriefingService** — orchestrates signal evaluation and sentence assembly.

```csharp
public interface IDashboardBriefingService
{
    Task<BriefingViewModel> GenerateBriefingAsync(int businessId, string userId, Dictionary<string, string> permissions);
}
```

**BriefingViewModel** — the output model for the partial view.

```csharp
public class BriefingViewModel
{
    public string Greeting { get; set; }           // "Good morning"
    public List<BriefingInsight> Insights { get; set; }
    public bool HasInsights => Insights.Count > 0;
}

public class BriefingInsight
{
    public int Priority { get; set; }
    public string Html { get; set; }               // Pre-rendered HTML with <strong> and <a> tags
    public string? LinkUrl { get; set; }           // Optional primary action URL
}
```

**Signal Evaluators** — private methods within the service, one per signal type:

| Signal | Method | Priority | Module Required |
|--------|--------|----------|-----------------|
| Overdue invoices | `EvaluateOverdueInvoices` | 1 | revenue |
| Draft invoices (stale) | `EvaluateDraftInvoices` | 7 | invoice |
| Pending proposals | `EvaluatePendingProposals` | 3 | quotation |
| Unassigned purchases | `EvaluateUnassignedPurchases` | 4 | purchase |
| Upcoming instalments | `EvaluateUpcomingInstalments` | 5 | schedule_payments |
| Reminders due today | `EvaluateRemindersToday` | 6 | payment_reminder_auto |
| Cash flow outlook | `EvaluateCashFlowOutlook` | 8 | cashflow |
| Recent payment received | `EvaluateRecentPayment` | 9 | revenue |
| All clear | (fallback) | 10 | — |

---

## Data Queries

Each signal evaluator runs a lightweight query (count + optional aggregate):

```sql
-- Overdue invoices
SELECT COUNT(*), SUM(TotalAmount - PaidAmount)
FROM [invoice].[Invoice]
WHERE BusinessId = @BusinessId AND InvoiceStatusTypeId = 2
  AND DueDate < GETDATE() AND InvoiceFinancialStatusTypeId IN (1, 2, 4)

-- Pending proposals
SELECT COUNT(*), SUM(TotalAmount)
FROM [quotation].[Quotation]
WHERE BusinessId = @BusinessId AND QuotationStatusTypeId = 2

-- Unassigned purchases
SELECT COUNT(*)
FROM [purchase].[Purchase]
WHERE BusinessId = @BusinessId AND VatSubmissionPeriodId IS NULL AND IsCancelled = 0

-- Draft invoices older than 3 days
SELECT COUNT(*)
FROM [invoice].[Invoice]
WHERE BusinessId = @BusinessId AND InvoiceStatusTypeId = 1
  AND CreatedAtUtc < DATEADD(day, -3, GETUTCDATE())

-- Upcoming instalments (next 7 days)
SELECT COUNT(*), SUM(Amount)
FROM [revenue].[PaymentScheduleInstalment]
WHERE BusinessId = @BusinessId AND DueDate BETWEEN GETDATE() AND DATEADD(day, 7, GETDATE())
  AND StatusTypeId = 1

-- Reminders scheduled today
SELECT COUNT(*)
FROM [dbo].[PaymentReminderSchedule]
WHERE BusinessId = @BusinessId AND IsActive = 1 AND NextReminderDateUtc <= GETUTCDATE()
```

All queries use existing indexed columns and are expected to execute in < 10ms each.

---

## Sentence Templates

```csharp
// Overdue invoices
$"You have <strong>{count} overdue invoice{s}</strong> totalling <strong>{currency}{total:N2}</strong> — the oldest is {days} days past due ({customerName}). <a href=\"/Revenue/Receivables\">View receivables →</a>"

// Pending proposals
$"<strong>{count} proposal{s}</strong> worth <strong>{currency}{total:N2}</strong> are awaiting client acceptance. <a href=\"/Quotation?status=2\">Follow up →</a>"

// Unassigned purchases
$"<strong>{count} purchase{s}</strong> are not yet assigned to a VAT period. <a href=\"/Purchase?vatPeriodId=0\">Assign now →</a>"

// Draft invoices
$"<strong>{count} invoice{s}</strong> have been sitting in Draft for more than 3 days. <a href=\"/Invoice?status=1\">Review drafts →</a>"

// Upcoming instalments
$"<strong>{count} payment instalment{s}</strong> totalling <strong>{currency}{total:N2}</strong> are due this week."

// Reminders today
$"<strong>{count} payment reminder{s}</strong> are scheduled to send today. <a href=\"/PaymentReminder/Upcoming\">View upcoming →</a>"

// Cash flow positive
$"Cash flow looks healthy for the next 30 days."

// Cash flow negative
$"Your 30-day cash flow projection is <strong>negative ({currency}{amount:N2})</strong>. <a href=\"/CashFlow\">Review forecast →</a>"

// Recent payment
$"A payment of <strong>{currency}{amount:N2}</strong> was received from <strong>{customerName}</strong>."

// All clear
$"Everything looks good — no items need your attention right now."
```

---

## UI Design

The briefing card sits between the topbar and the KPI cards:

```html
<section class="glass card-pad" style="border-left:3px solid #0D5EA6;margin-bottom:22px;">
    <div style="font-size:14px;line-height:1.7;color:#334155;">
        <strong style="color:#0B1B28;">Good morning.</strong>
        You have <strong>3 overdue invoices</strong> totalling <strong>€4,200.00</strong> — 
        the oldest is 12 days past due (Le Paris Roasting). 
        <a href="/Revenue/Receivables" style="color:#0D5EA6;font-weight:600;">View receivables →</a>
        <br/>
        <strong>2 proposals</strong> worth <strong>€8,500.00</strong> are awaiting client acceptance.
        <a href="/Quotation?status=2" style="color:#0D5EA6;font-weight:600;">Follow up →</a>
        <br/>
        Cash flow looks healthy for the next 30 days.
    </div>
</section>
```

- Font size: 14px (body text, readable)
- Line height: 1.7 (breathing room between insights)
- Key values in `<strong>` (darker, bolder)
- Links styled as blue, bold, with arrow suffix
- Each insight on its own line (via `<br/>`)
- Left border accent matches the brand

---

## Error Handling

| Scenario | Behaviour |
|----------|-----------|
| A signal query fails | Log the error, skip that signal, continue with others |
| All signal queries fail | Show the "all clear" fallback (graceful degradation) |
| No permissions for any module | Show greeting + "No dashboard data available" |
| Business has no data yet (new account) | Show: "Welcome! Start by creating your first quotation or recording a purchase." |

---

## Performance

- All queries are simple COUNT/SUM with indexed WHERE clauses
- Expected total execution: < 50ms for all signals combined
- No external API calls, no LLM, no caching needed
- Runs once per page load (server-rendered)

---

## Testing Strategy

### Unit Tests
- Each signal evaluator: verify sentence output for known data scenarios
- Priority ordering: verify signals are sorted correctly
- Permission filtering: verify omitted signals for restricted users
- Greeting logic: verify time-of-day mapping
- Edge cases: zero counts, null amounts, empty business

### Integration Tests
- Full briefing generation with in-memory database
- Verify max 6 insights limit
- Verify graceful degradation when queries fail
