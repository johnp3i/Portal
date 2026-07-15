# Implementation Plan: Dashboard Briefing

## Overview

Server-rendered narrative summary card for the dashboard. A `DashboardBriefingService` evaluates business signals (overdue invoices, pending proposals, etc.), assembles prioritized insights from sentence templates, and renders a concise card above the KPI section.

## Tasks

- [x] 1. Data models and service interface
  - [x] 1.1 Create `BriefingViewModel` and `BriefingInsight` models
    - Create `Portal.Infrastructure/Models/BriefingViewModel.cs`
    - Properties: Greeting (string), Insights (List<BriefingInsight>), HasInsights (bool)
    - BriefingInsight: Priority (int), Html (string), LinkUrl (string?)
  - [x] 1.2 Create `IDashboardBriefingService` interface
    - Create `Portal.Infrastructure/Services/IDashboardBriefingService.cs`
    - Method: `Task<BriefingViewModel> GenerateBriefingAsync(int businessId, string userId, Dictionary<string, string> permissions)`

- [x] 2. DashboardBriefingService implementation
  - [x] 2.1 Create service with signal evaluator methods
    - Create `Portal.Infrastructure/Services/DashboardBriefingService.cs`
    - Inject: PortalDbContext, ICurrentTenantService
    - Implement greeting logic (Good morning/afternoon/evening)
    - Implement max 6 insights limit with priority sorting
  - [x] 2.2 Implement overdue invoices signal (Priority 1)
    - Query: issued invoices where DueDate < today AND outstanding balance > 0
    - Include: count, total amount, oldest invoice's customer + days overdue
    - Template: "You have X overdue invoices totalling €Y — the oldest is Z days past due (Customer)."
    - Link: /Revenue/Receivables
    - Gate: revenue module
  - [x] 2.3 Implement pending proposals signal (Priority 3)
    - Query: quotations where StatusTypeId = 2 (Sent)
    - Include: count, total value
    - Template: "X proposals worth €Y are awaiting client acceptance."
    - Link: /Quotation?status=2
    - Gate: quotation module
  - [x] 2.4 Implement unassigned purchases signal (Priority 4)
    - Query: non-cancelled purchases where VatSubmissionPeriodId IS NULL
    - Include: count
    - Template: "X purchases are not yet assigned to a VAT period."
    - Link: /Purchase?vatPeriodId=0
    - Gate: purchase module
  - [x] 2.5 Implement upcoming instalments signal (Priority 5)
    - Query: payment schedule instalments due within 7 days, status = pending
    - Include: count, total amount
    - Template: "X payment instalments totalling €Y are due this week."
    - Gate: schedule_payments module
  - [x] 2.6 Implement reminders due today signal (Priority 6)
    - Query: active payment reminder schedules with next reminder date <= today
    - Include: count
    - Template: "X payment reminders are scheduled to send today."
    - Link: /PaymentReminder/Upcoming
    - Gate: payment_reminder_auto module
  - [x] 2.7 Implement draft invoices signal (Priority 7)
    - Query: invoices with StatusTypeId = 1 (Draft) created more than 3 days ago
    - Include: count
    - Template: "X invoices have been sitting in Draft for more than 3 days."
    - Link: /Invoice?status=1
    - Gate: invoice module
  - [x] 2.8 Implement cash flow outlook signal (Priority 8)
    - Query: 30-day cash flow projection (use existing CashFlowService or direct query)
    - If negative: "Your 30-day cash flow projection is negative (€X)."
    - If positive: "Cash flow looks healthy for the next 30 days."
    - Link (if negative): /CashFlow
    - Gate: cashflow module
  - [x] 2.9 Implement recent payment received signal (Priority 9)
    - Query: payments received in the last 24 hours (non-voided)
    - Include: most recent amount + customer name
    - Template: "A payment of €X was received from Customer."
    - Gate: revenue module
  - [x] 2.10 Implement fallback / all-clear message
    - When no signals produce insights: "Everything looks good — no items need your attention right now."
    - When new business (no data): "Welcome! Start by creating your first quotation or recording a purchase."

- [x] 3. Checkpoint — Verify service compiles
  - Run `dotnet build`, ensure 0 errors

- [x] 4. Dashboard integration
  - [x] 4.1 Wire DashboardBriefingService in the HomeController
    - Inject IDashboardBriefingService
    - Call GenerateBriefingAsync with businessId, userId, and user permissions
    - Pass result to ViewBag.Briefing
  - [x] 4.2 Create `_DashboardBriefing.cshtml` partial view
    - Create `Portal.Web/Views/Shared/_DashboardBriefing.cshtml`
    - Render: left-border accent card, greeting, flowing text with insights
    - Each insight on its own line (br or div)
    - Strong tags for emphasis, blue links with arrows
    - Fallback message when no insights
  - [x] 4.3 Embed partial on Dashboard (Home/Index.cshtml)
    - Insert between topbar and KPI cards
    - Only render if ViewBag.Briefing is not null

- [x] 5. DI registration
  - [x] 5.1 Register service in Program.cs
    - `builder.Services.AddScoped<IDashboardBriefingService, DashboardBriefingService>()`

- [x] 6. Checkpoint — Full build and verify
  - Ensure all tests pass, ask the user if questions arise

## Notes

- No module gating on the briefing itself — it's always visible on the dashboard
- Individual signals are gated by module permission (omitted if user lacks access)
- The service is read-only — no state changes, no side effects
- Greeting is based on UTC time (can be refined to user timezone later)
- Max 6 insights prevents information overload
- Each signal evaluator is independent — if one fails, others still render
- Currency symbol comes from BusinessProfile.CurrencySymbol

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4", "2.5", "2.6", "2.7", "2.8", "2.9", "2.10"] },
    { "id": 2, "tasks": ["4.1", "4.2", "4.3", "5.1"] }
  ]
}
```
