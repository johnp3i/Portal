# Portal Upscale Roadmap

## Purpose

This document outlines strategic feature additions that would elevate the Portal from a document management tool to a comprehensive business management platform. Each feature builds on existing infrastructure and data already captured in the system.

---

## Priority Tier 1 — High Impact, Leverages Existing Data (Phase 1)

### 1. Recurring Invoices

> ❌ **SKIPPED** — Not relevant for target market (hospitality, retail, wholesale). These industries bill per transaction/delivery, not on recurring schedules. This feature would only benefit subscription-based service businesses.

**What:** Auto-generate invoices on a configurable schedule (weekly, monthly, quarterly) for repeat billing scenarios — retainers, subscriptions, maintenance contracts.

**Why it matters:** Eliminates repetitive manual work. Businesses with 10+ recurring clients spend hours each month creating the same invoices. This is table-stakes for any serious invoicing platform.

**Builds on:** Existing invoice creation, customer registry, line item catalog, payment tracking.

**Key capabilities:**
- Define a recurring template (customer, line items, frequency, start date, end date or indefinite)
- Auto-generate draft or issued invoices on schedule
- Email notification to business owner when generated
- Optional auto-send to customer
- Dashboard widget showing upcoming recurring invoices
- Pause/resume/cancel recurring schedules

**Data model additions:**
- `RecurringInvoiceTemplate` table (frequency, next run date, template line items, customer, status)
- Background job or scheduled task to generate invoices

---

### 2. Automated Payment Reminders

**What:** Configurable email reminders sent to customers when invoices approach or pass their due date. Escalating tone and frequency.

**Why it matters:** Late payments are the #1 cash flow killer for SMEs. Automating the chase saves time and improves collection rates without damaging relationships.

**Builds on:** Existing invoice tracking, due dates, financial status, email service, customer email addresses.

**Key capabilities:**
- Configurable reminder schedule per business (e.g., 7 days before, on due date, 7 days after, 14 days after, 30 days after)
- Email templates with escalating tone (friendly → firm → formal)
- Skip reminders for invoices with pending payments or disputes
- Activity log of all reminders sent
- Per-customer opt-out (for customers who have arranged payment plans)
- Dashboard summary: "12 reminders sent this week, 3 payments received after reminder"

**Data model additions:**
- `PaymentReminderSchedule` (business-level configuration)
- `PaymentReminderLog` (tracks each email sent per invoice)
- Background job to evaluate and send reminders daily

---

### 3. Cash Flow Forecasting

**What:** A forward-looking 30/60/90-day projection showing expected cash inflows (from outstanding invoices by due date) and outflows (from known recurring expenses), with a running balance line.

**Why it matters:** This is the single most requested feature by SME owners. Knowing "will I have enough cash next month?" prevents crises. No competitor at this tier offers it well.

**Builds on:** Existing invoice data (amounts, due dates, financial status), purchase history, recurring patterns.

**Key capabilities:**
- 30/60/90-day forward projection chart
- Expected inflows: outstanding invoices grouped by due date (with confidence weighting based on customer payment history)
- Expected outflows: average monthly expenses by category (derived from purchase history)
- Running balance line (starting from current bank position — manual input initially)
- Scenario modelling: "what if Invoice X doesn't pay on time?"
- Alerts when projected balance drops below a configurable threshold

**Data model additions:**
- `CashFlowSettings` (starting balance, alert threshold)
- Computed views (no new transaction tables — derives from existing data)

---

### 4b. Payment Requests (Invoice Payment Links)

**What:** Business owners connect their own Stripe account (or JCC) to the Portal. When an invoice is issued, a payment link is generated that customers can click to pay instantly. Funds go directly to the business owner's bank account — 3 Inventors never touches the money.

**Why it matters:** Eliminates the friction of manual bank transfers. Customers can pay with one click from the invoice email. Combined with Payment Reminders (#2), this creates a complete "get paid faster" pipeline: remind → click → pay → auto-record.

**Builds on:** Existing invoices, email service, revenue/payment tracking. Uses Stripe Connect (Standard accounts) so each business owns their payment processing.

**Key capabilities:**
- One-time Stripe account connection via OAuth (Settings → Payment Gateway)
- Auto-generate payment links when invoices are issued
- Payment link included in invoice emails and shared invoice pages
- Stripe Checkout handles card entry (PCI compliant, mobile-friendly)
- Webhook auto-records payment against the invoice in real-time
- No funds flow through 3 Inventors (zero regulatory/banking burden)
- Optional platform application fee for revenue (introduced later)
- Provider-agnostic design supports JCC and future gateways

**Cost to business owner:** Standard Stripe processing fees only (1.5% + €0.25 for EU cards). No additional Portal charges at launch.

**Data model additions:**
- `BusinessPaymentGateway` table (provider, account ID, connection status)
- `InvoicePaymentLink` table (invoice, session ID, URL, status, amounts)

**Reference:** See `.kiro/docs/Stripe_Connect_Integration.md` for full technical and business details.

---

## Priority Tier 2 — Significant Value, Moderate Effort

### 4. Profit & Loss Summary

**What:** A financial summary view showing Revenue (from paid invoices), Cost of Goods Sold (from stock purchases), Gross Margin, Operating Expenses (from expense purchases), and Net Profit — by month, quarter, or year.

**Why it matters:** Business owners currently need to export data to Excel or use separate accounting software to see profitability. This closes the loop within the platform.

**Builds on:** Existing invoice payments (revenue), purchase records with expense categories and purchase types (Asset/Stock/Expense).

**Key capabilities:**
- Period selector (month, quarter, year, custom range)
- Revenue = sum of payments received in period
- COGS = sum of Stock-type purchases in period
- Operating expenses = sum of Expense-type purchases in period
- Gross margin = Revenue - COGS
- Net profit = Gross margin - Operating expenses
- Trend comparison (vs previous period, vs same period last year)
- Export to PDF

**Data model additions:**
- None — purely computed from existing invoice payments and purchases
- Optional: `ProfitLossSnapshot` table for caching monthly summaries

---

### 5. Expense Categorisation Insights

**What:** A visual breakdown of spending by expense category with trend lines, budget thresholds, and anomaly detection.

**Why it matters:** Businesses track purchases for VAT compliance, but rarely analyse where their money goes. Showing "you spent 40% more on Marketing this quarter" is actionable intelligence.

**Builds on:** Existing purchases with expense categories, supplier records, VAT periods.

**Key capabilities:**
- Pie/bar chart: spend by category (current period)
- Trend line: category spend over last 6–12 months
- Budget limits per category (configurable) with alerts when exceeded
- Top suppliers per category
- Month-over-month variance highlighting
- Export breakdown to CSV

**Data model additions:**
- `ExpenseCategoryBudget` table (category, monthly limit, alert threshold)

---

### 6. Client Portal / Self-Service

**What:** A lightweight customer-facing area where clients can view their invoices, outstanding balance, payment history, and download statements — without needing a platform login.

**Why it matters:** Reduces email back-and-forth ("can you resend my invoice?", "what's my balance?"). Positions the platform as professional and modern.

**Builds on:** Existing invoice sharing (magic links), customer statements, proposal acceptance flow.

**Key capabilities:**
- Magic link access per customer (similar to demo invitations pattern)
- View: list of invoices with status and amounts
- View: outstanding balance and payment history
- Download: individual invoice PDFs
- Download: statement PDF for any period
- Optional: acknowledge receipt / confirm payment intent
- Branded with the business's logo and colours

**Data model additions:**
- `CustomerPortalToken` table (customer, token, expiry)
- Reuses existing invoice/payment/statement data (read-only)

---

### 7. Document Attachments

**What:** Allow file uploads (PDF, images) attached to purchases, invoices, and quotations. The actual supplier invoice scan, a signed contract, supporting documentation.

**Why it matters:** Makes the platform the single source of truth. Currently, businesses store the Portal record AND the PDF separately (email, Google Drive, filesystem). Especially critical for VAT audits where the original document must be retained.

**Builds on:** Existing logo upload infrastructure (file upload, storage, display).

**Key capabilities:**
- Attach files to: Purchases (supplier invoice scan), Invoices (signed copy), Quotations (supporting docs)
- Supported types: PDF, PNG, JPG, WEBP (max 5MB per file, configurable)
- View/download attachments from detail pages
- Thumbnail preview for images
- Multiple attachments per record (up to 5)
- Storage: Azure Blob Storage or local filesystem (configurable)

**Data model additions:**
- `DocumentAttachment` table (entity type, entity ID, file name, content type, storage path, size, uploaded by, CreatedAtUtc)

---

## Priority Tier 3 — Strategic Differentiators, Higher Effort

### 8. Activity Timeline / Notifications

**What:** A per-business activity feed showing key events (invoice issued, payment received, quotation accepted, customer created, VAT period submitted) with optional email/push notifications.

**Why it matters:** Gives business owners and their teams awareness of what's happening without checking each module. Essential for multi-user businesses.

**Builds on:** Existing audit log infrastructure, SignalR capability (already in the stack).

**Key capabilities:**
- Centralised timeline view (filterable by module, date, user)
- Real-time updates via SignalR for logged-in users
- Email digest: daily or weekly summary of activity
- Notification preferences per user (which events to be notified about)
- Mobile-friendly card layout (already designed in mobile policies)

**Data model additions:**
- `ActivityEvent` table (event type, entity type, entity ID, description, user ID, timestamp)
- `NotificationPreference` table (user, event type, channel)
- Leverage existing MassTransit infrastructure for event publishing

---

### 9. Multi-Currency Support

**What:** Support for invoicing and purchasing in multiple currencies, with exchange rate tracking and reporting currency conversion.

**Why it matters:** Businesses trading across borders (EU reverse charge purchases are already tracked) need to invoice in the customer's currency while reporting in their base currency. Opens up international market.

**Builds on:** Existing currency symbol in business profile, EU/Non-EU purchase origin types.

**Key capabilities:**
- Define base currency per business (existing)
- Allow alternative currencies on invoices and quotations
- Exchange rate lookup (manual entry initially, API integration later)
- Automatic conversion to base currency for reporting (P&L, VAT, dashboards)
- Currency symbol display per document
- Gains/losses tracking on payments received in foreign currency

**Data model additions:**
- `Currency` reference table
- `ExchangeRate` table (from, to, rate, date)
- Add `CurrencyId` to Invoice, Quotation, Purchase tables
- Conversion logic in reporting queries

---

### 10. API / Integrations Layer

**What:** A REST API exposing core platform operations (invoices, payments, customers, purchases) for external integrations — bank feeds, payment gateways (Stripe, PayPal), accounting software sync.

**Why it matters:** No business tool operates in isolation. The ability to connect to banks (auto-reconcile payments), accept online payments (Stripe checkout links on invoices), or sync with existing accounting software removes the biggest objection to adoption.

**Builds on:** Existing service layer (clean separation), MassTransit message bus, entity models.

**Key capabilities:**
- RESTful API with API key authentication (per-business)
- Endpoints: Invoices (CRUD + issue + record payment), Customers (CRUD), Purchases (CRUD), Payments (list)
- Webhook support: notify external systems on key events (invoice issued, payment received)
- Stripe integration: generate payment links on invoices, auto-record payments via webhook
- Bank feed import: CSV/OFX upload to auto-match payments against invoices
- Rate limiting and usage tracking per API key

**Data model additions:**
- `ApiKey` table (business, key hash, permissions, rate limit, created by)
- `WebhookSubscription` table (business, event type, URL, secret)
- `ApiRequestLog` table (key, endpoint, timestamp, response code)

---

## Implementation Priority Matrix

| # | Feature | Impact | Effort | Priority | Notes |
|---|---------|--------|--------|----------|-------|
| ~~1~~ | ~~Recurring Invoices~~ | — | — | ❌ Skipped | Not relevant for target market (hospitality, retail, wholesale). Industry-specific to subscription-based services. |
| 2 | Automated Payment Reminders | High | Medium | 🟢 Phase 1 | Core need — late payments affect all industries |
| 3 | Cash Flow Forecasting | High | Medium | 🟢 Phase 1 | Differentiator — uses existing data |
| 4 | Profit & Loss Summary | High | Low | 🟢 Phase 1 | Quick win — purely computed from existing records |
| 4b | Payment Requests (Stripe Connect) | Very High | Medium | 🟢 Phase 1 | Complements Payment Reminders — remind → pay → auto-record |
| 5 | Expense Categorisation Insights | Medium | Low | � Phase 1 | Quick win — builds on existing purchase categories |
| 6 | Client Portal / Self-Service | High | High | 🟡 Phase 2 | High value but significant build effort |
| 7 | Document Attachments | Medium | Medium | 🟡 Phase 2 | Operational necessity for VAT audits |
| 8 | Activity Timeline / Notifications | Medium | High | � Phase 2 | Multi-user awareness and engagement |
| 9 | Multi-Currency | High | Very High | 🟠 Phase 3 | Strategic — opens international market |
| 10 | API / Integrations | Very High | Very High | 🟠 Phase 3 | Platform play — Stripe, bank feeds, webhooks |

### Phase 1 — Core Financial Intelligence (Features 2–5)

These four features transform the Portal from "track documents" to "understand your business finances":

- **Payment Reminders** — automates the most tedious operational task (chasing payments)
- **Payment Requests** — enables one-click payment from invoice emails via Stripe Connect (funds go directly to business owner)
- **Cash Flow Forecasting** — answers the #1 question every business owner has ("will I have enough money next month?")
- **P&L Summary** — closes the financial loop without needing external accounting software
- **Expense Insights** — turns compliance data (purchases for VAT) into actionable spending intelligence

All four leverage data already being captured. No new user input required to see immediate value.

### Phase 2 — Operational Completeness (Features 6–8)

These features make the platform a complete operational hub:

- **Client Portal** — reduces customer communication overhead
- **Document Attachments** — makes the platform the single source of truth
- **Activity Timeline** — essential for growing teams with multiple users

### Phase 3 — Market Expansion (Features 9–10)

These are strategic investments that expand the addressable market:

- **Multi-Currency** — enables international trading
- **API Layer** — positions the platform for integrations and partnerships

---

## Guiding Principles

1. **Each feature must use data already captured** — avoid requiring users to enter new data before seeing value
2. **Automation over manual work** — if the system can do it, the user shouldn't have to
3. **Visibility over complexity** — show the business owner what matters, hide implementation details
4. **Progressive disclosure** — basic version first, advanced configuration for power users
5. **No feature islands** — every new module connects to existing data and appears on the dashboard

---

## Relationship to Existing Modules

```
┌─────────────────────────────────────────────────────┐
│                   DASHBOARD                          │
│  (Cash Flow Forecast, P&L Summary, Activity Feed)   │
├──────────┬──────────┬──────────┬────────────────────┤
│ Quotation│ Invoice  │ Revenue  │ Purchase            │
│          │          │          │                     │
│          │ +Recurring│ +Reminders│ +Attachments       │
│          │          │ +Client  │ +Category Insights  │
│          │          │  Portal  │                     │
├──────────┴──────────┴──────────┴────────────────────┤
│              API / Integrations Layer                 │
│         (Stripe, Bank Feeds, Webhooks)               │
└─────────────────────────────────────────────────────┘
```
