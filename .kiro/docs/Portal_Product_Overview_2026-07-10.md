# Portal — Business Management Platform
**Product Overview | 11 July 2026**

---

## Platform Identity

- **Product Name:** Portal
- **Developed by:** 3 Inventors (Operational Intelligence Company)
- **Type:** Multi-tenant back-office business management platform
- **Technology:** ASP.NET Core MVC 8, SQL Server, SignalR, MassTransit/RabbitMQ
- **Target Users:** Small-to-medium businesses seeking structured financial operations without the complexity of enterprise ERP systems
- **Access Model:** Invitation-only registration with subscription tiers
- **URL:** portal.3inventors.com

---

## Subscription Tiers

| Tier | Monthly | Annual (2 months free) | Target |
|------|---------|----------------------|--------|
| Foundation | €39/mo | €390/year | Solo operators and micro-businesses — complete business management |
| Professional | €89/mo | €890/year | Growing businesses — full financial intelligence with automated workflows |
| Enterprise | €169/mo | €1,690/year | Teams — collaboration, audit trails, API access (future) |

---

## Core Platform (Available on all plans)

### Customer Management
- Customer database with contact details, addresses, communication history
- Active/inactive status management
- Customer-linked invoicing and quotation history

### Quotation & Proposal System
- Professional quotation creation with line items, sections, and custom pricing
- Multi-section proposals with branded formatting
- Quotation-to-invoice one-click conversion
- Quotation contacts and proposal sharing via unique links
- Customer acceptance tracking with IP/timestamp audit
- Proposal section management (descriptions, notes, column configurations)
- PDF export and print-ready layouts

### Invoicing
- Invoice creation (from quotations or standalone)
- Line items with quantity, unit price, VAT rate, discounts
- Invoice status lifecycle: Draft → Issued → Cancelled
- Financial status tracking: Unpaid → Partially Paid → Paid → Overdue → Written Off
- Invoice sharing via secure links with customer acceptance flow
- PDF download and preview
- Invoice duplication for recurring billing
- Soft-delete with recovery
- Per-invoice payment instructions override (show/hide bank details)
- VAT period assignment

### Revenue Control
- Payment recording with method, date, reference, and notes
- Payment voiding with audit trail
- Outstanding balance tracking per invoice
- Receivables dashboard: overdue invoices, recent payments, collection metrics
- Financial KPI cards: Invoice Total, Total Paid, Outstanding Balance, Due Date
- Payment progress bars with percentage tracking

### Purchase & Expense Management
- Purchase recording with supplier, category, amount, VAT
- Supplier management (active/inactive)
- Expense categories with custom naming
- Purchase origin types: Domestic, EU Reverse Charge, Non-EU, EU Paid
- Purchase types: Asset, Stock, Expense
- Expense category limits (annual and period budgets)

### VAT Compliance
- Automatic VAT period generation based on registration date and period length
- VAT submission period management
- User-controlled purchase-to-period assignment (no auto-assignment)
- Optional VAT period selector on purchase Create, Edit, and Bulk Entry forms
- Unassigned Purchases panel on VAT Detail page with bulk-assign capability
- Submission advisory: warns when unassigned purchases exist before filing
- Assignment locking: purchases locked to their period after submission
- Invoice-to-period assignment
- Output VAT calculation from invoices
- Input VAT calculation from purchases
- Net VAT payable computation
- Submission status tracking (filed/unfiled)

### Product Catalog
- Product/service catalog with codes, descriptions, pricing
- Default selling price, cost price, and VAT rate per product
- Price history tracking (who changed what, when)
- Supplier linkage per product

### Credit Notes
- Credit note issuance against invoices
- Line items with quantities and amounts
- Application to invoices (reduce outstanding balance)
- Void/cancel workflow
- VAT period assignment for credit notes

### Document Sharing
- Secure share links for invoices and proposals
- Customer acceptance flow (terms agreement, IP logging)
- Share link management (active/deactivate)

### Business Configuration
- Business profile: company registration, VAT number, address, contact details
- Multiple bank accounts (payment details with labels)
- Business logo management (upload, set primary)
- Currency symbol configuration
- Payment instructions toggle (global and per-invoice)

---

## Professional Tier Features

### Module 1: Profit & Loss Summary
- Revenue, COGS, and Operating Expense computation
- Period-based calculations: month, quarter, year, custom range
- Trend comparison: vs previous period, vs same period last year
- PDF export of P&L reports
- Visual breakdown tables with category detail

### Module 2: Expense Categorisation Insights
- Spend analysis by category with pie/bar charts
- Trend lines: category spend over 6–12 months
- Budget configuration per category (annual + period limits)
- Budget threshold alerts: exceeded and approaching warnings
- Top suppliers per category breakdown
- Month-over-month variance highlighting
- CSV export for external analysis

### Module 3: Automated Payment Reminders
- Three escalation tiers: Friendly → Firm → Formal
- Configurable day offsets, max reminders per tier, minimum intervals
- Automated daily evaluation (06:00 UTC) of all overdue/approaching invoices
- Smart suppression: respects opt-outs, disputed invoices, recent partial payments
- Email templates: tier-specific tone and colour (blue/amber/red)
- Open tracking via embedded pixel (opens, open count, timestamps)
- Manual send button on Invoice Detail (Foundation plan)
- Test reminder sending with [TEST] prefix
- Upcoming reminders preview: 7/14/30-day projection
- Reminder history page with filters (Tier, Status, Method, Date, Customer)
- Dashboard widget: reminders sent this week, payments received after reminder
- Per-customer opt-out configuration
- Auto-creation of invoice share links when sending reminders

### Module 4: Payment Instructions (Bank Transfer)
- "Pay by Bank Transfer" button on shared invoice pages
- Payment instructions modal: bank name, IBAN, SWIFT/BIC, payee name, outstanding amount
- Copy-to-clipboard for IBAN and transfer reference
- Customer "I've made the payment" declaration flow
- PaymentOnboard financial status for pending verifications
- Business settings toggle for payment instructions visibility
- Per-invoice override: force show / force hide / follow business default
- Rate limiting on declarations (3 per token per hour)
- Audit logging: IP address, timestamp, share token
- Future: Stripe Connect for card payments (documented, not yet implemented)

### Module 5: Cash Flow Forecasting
- Forward-looking cash position projection (30/60/90 days)
- Projected inflows from outstanding invoices with confidence weighting
- Customer confidence scoring based on payment history (DaysLateAverage)
- Projected outflows from 6-month historical expense category averages
- Running balance line chart with alert threshold danger zone
- Scenario modelling: toggle invoices out of projection ("what if" analysis)
- Configurable starting balance and alert threshold
- Dashboard widget: compact 30-day projection summary
- Chart.js visualisation with annotation plugin (threshold line + danger zone)

### Module 7: Payment Schedules (Instalment Plans)
- Create instalment plans per invoice with configurable amounts and due dates
- Auto-suggestion of remaining balance for next instalment
- Real-time balance validation (sum must equal outstanding balance)
- Instalment status tracking: Pending, Due, Overdue, Paid, Partially Paid
- Auto-matching: recorded payments allocated to next eligible instalment (priority: Due → Overdue → Pending)
- Partial payment handling: remainder instalment auto-created for shortfalls
- Schedule modification with full audit history (field changed, old/new values, user, timestamp)
- VAT period warning: alerts when first instalment is after VAT submission deadline
- Smart VAT period derivation (from assigned period, existing rows, or calculated from business config)
- Schedule deletion with protection (blocked when payments are matched)
- Payment Schedules Overview page: KPI cards, monthly timeline with year selector, filterable table
- Nested remainder instalment display (visual hierarchy)
- Payment void → automatic match reversion

### Module 8: Recurring Expense Validation
- Define expected recurring purchase rules per supplier (optionally scoped to expense category)
- Frequency-based validation: monthly, bimonthly, quarterly, or custom intervals
- Amount-anchored validation: verify a specific expected amount is recorded (with configurable tolerance %)
- Grace period: extend lookup window at period boundaries (0–15 days)
- Validation report with pass/warning/fail status per rule, sorted by severity
- VAT submission integration: collapsible advisory panel auto-validates before submission
- Standalone validation view: run checks at any time against any period or custom date range
- Rule management: create, edit, disable/enable, soft-delete rules grouped by supplier
- Multiple rules per supplier (e.g., hosting vs. SSL from the same vendor)
- Non-blocking: validation is advisory — never prevents VAT submission

### Activity Log
- Timeline-style feed of all business activity
- Plain-English summaries with coloured timeline dots (created/edited/deleted/status changed)
- Expandable detail panels: before/after diff for edits
- Quick stats: changes this week, active team members, most active area
- Business-friendly filters: what changed, who, what type, date range
- User name resolution from membership database
- Relative timestamps ("Just now", "2 min ago", "Yesterday at 14:32")
- Entity links: invoice numbers, customer names link to detail pages

---

## Infrastructure & Security

### Permission Gating
- Three-tier subscription model: Foundation / Professional / Enterprise
- Plan-level module access enforcement (global filter)
- User-level permission management (full / readonly / none per module)
- Soft-gate promotional pages for features above current plan
- Upgrade CTA integration ("Go to Billing" buttons)

### Audit & Compliance
- Full audit log of all data changes (create, edit, delete, status change)
- User identity tracking on all mutations
- Timestamp-based audit trail
- SuperAdmin audit viewer (system-wide)
- Business-manager Activity Log (own business only)

### Multi-Tenant Architecture
- Tenant isolation via BusinessId on all entities
- Global EF Core query filters for automatic tenant scoping
- Secure credential storage (ASP.NET Core User Secrets)
- ASP.NET Core Identity for authentication
- Invitation-only registration with SuperAdmin approval

### Technical Foundation
- ASP.NET Core MVC 8 (.NET 8)
- Entity Framework Core (Database-First approach)
- SQL Server with schema-per-domain ([portal], [invoice], [revenue], [purchase], [vat], [credit], [audit], [reminder], [cashflow], [billing])
- SignalR for real-time updates
- MassTransit + RabbitMQ for message bus
- Serilog structured logging
- Background services for scheduled operations (payment reminders)

---

## Design System

- **Visual Identity:** MyChair Design System — calm, operational, structured
- **Colours:** Primary Blue #0D5EA6, Accent Cyan #57B8E8, Success #129867, Warning #C8912E, Danger #C24A4A
- **Typography:** Manrope (headings), Inter (body)
- **Layout:** Sidebar + Topbar + Content grid with glass-morphism cards
- **Mobile:** Responsive at 375px and 810px breakpoints
- **Alerts:** SweetAlert2 for all user-facing dialogs (never native alerts)
- **Loading:** BlockUI for all AJAX operations

---

## What Makes Portal Different

### Portal is NOT an ERP

Enterprise Resource Planning systems (SAP, Oracle, Microsoft Dynamics, Odoo) are built for large organisations with dedicated IT teams. They require months of implementation, consultant-driven customisation, and ongoing maintenance contracts. They solve everything — and in doing so, solve nothing simply.

Portal solves **one domain exceptionally well**: the financial operations lifecycle for small-to-medium businesses that don't have (or want) an ERP.

| | Traditional ERP | Portal |
|--|----------------|--------|
| **Setup time** | 3–12 months | Same day |
| **Requires consultants** | Yes | No |
| **Target user** | IT department configures, staff operates | Business owner operates directly |
| **Complexity** | Hundreds of modules, most unused | Focused feature set — nothing unnecessary |
| **Pricing** | €500–5,000+/month per user | €39–169/month for the entire business |
| **Customisation** | Required to function | Works out of the box |
| **Learning curve** | Weeks of training | Minutes to first invoice |

### Portal vs Accounting Software (Xero, QuickBooks, FreshBooks)

Accounting platforms focus on **compliance** — recording transactions for tax reporting. Portal focuses on **operational control** — giving the business owner visibility into what's happening, what's coming, and what needs attention.

| | Accounting Software | Portal |
|--|---------------------|--------|
| **Primary goal** | Tax compliance | Operational clarity |
| **Quotation → Invoice flow** | Basic or none | Full lifecycle with proposals, acceptance, conversion |
| **Payment scheduling** | Not available | Full instalment plans with auto-matching and VAT warnings |
| **Cash flow forecasting** | Basic projections | Confidence-weighted with scenario modelling |
| **Payment reminders** | Simple email nudges | Three-tier escalation with open tracking and smart suppression |
| **Revenue visibility** | After the fact (reports) | Real-time (dashboards, KPIs, progress bars) |
| **Multi-tenant** | Per-company account | Multi-business from one login |

### Portal vs Invoicing Tools (Stripe Invoicing, PayPal, Wave)

Invoicing tools are **transactional** — create invoice, send, get paid. Portal is **operational** — it tracks the entire revenue lifecycle from quotation through collection, with intelligence at every step.

| | Invoicing Tools | Portal |
|--|-----------------|--------|
| **Quotation system** | None | Professional proposals with sections, acceptance flow |
| **Payment tracking** | Auto (payment gateway) | Manual + auto matching to instalment schedules |
| **Expense management** | None or basic | Full categorisation, budgets, insights |
| **VAT compliance** | Basic rate application | Period management, submission tracking, deadline warnings |
| **Business intelligence** | None | P&L, cash flow forecasting, expense insights |
| **Customer communication** | Receipt emails | Escalating reminders with tracking |

### The Portal Philosophy

1. **Structured, not complex** — Every feature follows a clear workflow. No configuration wizards, no hidden settings buried three menus deep.

2. **Financial awareness, not just recording** — Portal doesn't just record what happened. It tells you what's coming (cash flow), what needs attention (overdue), and what's at risk (VAT deadline warnings).

3. **Operational intelligence readiness** — Every data point is structured for future intelligence. The platform is designed to feed into 3 Inventors' Canonical Operational Model (COM) — where business data across all four platforms converges into unified decision intelligence.

4. **Calm design, serious execution** — The UI is intentionally calm and structured. No gamification, no notification spam, no dark patterns. It respects the operator's time and attention.

5. **Subscription fairness** — The Foundation plan covers everything a solo operator needs. Professional unlocks intelligence and automation. No per-user pricing traps, no feature walls on basic operations.

---

## Platform Card Summary (for 3 Inventors website)

**Suggested card copy:**

> **Portal**
> FINANCIAL OPERATIONS
>
> Quotations, invoicing, payment tracking, cash flow forecasting, and VAT compliance — unified in one calm, structured platform designed for operational clarity.
>
> Tags: Invoicing · Payments · Cash Flow
>
> Visit portal.3inventors.com →

---

## Current Status

- **Phase 1:** Complete (7 modules + infrastructure)
- **Phase 2:** Planned (Client Portal, Document Attachments, Activity Timeline expansion)
- **Subscription Management:** Live (Stripe integration for billing)
- **Production Readiness:** All modules end-to-end tested
