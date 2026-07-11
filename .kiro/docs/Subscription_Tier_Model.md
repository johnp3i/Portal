# Subscription Tier Model
**Last revised: 11 July 2026**

## Philosophy

The Portal exists to help businesses operate with clarity and control. The pricing model reflects this:

- **Foundation gives you everything you need to run your business** — no critical features held hostage. The name says it: this is the foundation every business starts with.
- **Professional gives you automation** — the platform works for you while you focus on selling
- **Enterprise gives you scale** — multi-user, integrations, and advanced analytics for growing teams

The upgrade path is natural: as a business grows, manual processes become bottlenecks. The next tier removes those bottlenecks. No business should feel forced to pay for something they can't afford — they should feel compelled to upgrade because the value is obvious.

---

## Tier Definitions

### Foundation

**For:** Solo operators, micro-businesses, early-stage companies (1–2 users)

**Value proposition:** A complete business management environment. Create quotations, issue invoices, track payments, manage customers, record purchases, handle VAT — all in one place.

**Included modules:**
- Quotation Platform (create, send, share, accept)
- Invoicing (create, issue, share, track)
- Revenue Control (manual payment recording, receivables view)
- Customer Registry (CRUD, statements)
- Purchase Management (record, categorise, VAT assignment)
- Supplier Registry
- VAT Periods & Submissions
- Credit Notes
- Product/Line Item Catalog
- Payment Links (manual generation via Stripe — one at a time)
- Payment Reminders (manual send only — click per invoice, no automation)
- Dashboard (basic KPIs, recent activity)
- Mobile responsive access

**Limitations:**
- 1–2 users (owner + 1 assistant)
- Manual payment link generation (not auto-included in emails)
- Manual reminders only (no scheduled automation)
- No financial intelligence features (P&L, Cash Flow, Expense Insights)
- No document attachments
- No API access

---

### Professional

**For:** Established businesses with regular invoicing volume (2–5 users)

**Value proposition:** Complete hands-off automation. The platform chases payments, generates payment links, forecasts cash flow, and gives you financial intelligence — so you can focus on running the business, not administering it.

**The automation pipeline:**
```
Quotation → Invoice → Auto-Payment Link → Overdue? → Auto-Remind → Customer Pays → Auto-Record
```

**Everything in Foundation, plus:**
- Automated Payment Reminders (configurable schedule, escalating tone)
- Auto-generated Payment Links (included in invoice emails and shared pages)
- Payment Schedules (instalment plans with auto-matching, VAT warnings, remainder tracking)
- Cash Flow Forecasting (30/60/90-day projections)
- Profit & Loss Summary (period-based financial overview)
- Expense Categorisation Insights (spend analysis, budget alerts)
- Recurring Expense Validation (define expected supplier purchases, validate before VAT submission)
- Document Attachments (attach PDFs/images to purchases, invoices, quotations)
- Activity Log (readonly — view history of operations)
- Up to 5 users with granular permissions
- Priority email support

**Upgrade trigger:** Business has 15+ invoices/month and spends time chasing payments manually. The automation pays for the upgrade within the first month.

---

### Enterprise

**For:** Growing businesses with teams, multiple departments, or integration needs (5+ users)

**Value proposition:** Full operational platform with team collaboration, customer self-service, real-time activity awareness, and external system integrations.

**Everything in Professional, plus:**
- Client Portal (customers view invoices, pay, download statements)
- Activity Timeline & Notifications (real-time feed, email digests)
- Activity Log (full — edit, export, and advanced filtering)
- API Access (REST API for external integrations)
- Webhook subscriptions (real-time event notifications to external systems)
- Multi-Currency Support (invoice in customer's currency, report in base)
- Unlimited users with role-based access
- Custom branding on client-facing pages
- Dedicated account support

**Upgrade trigger:** Business has multiple team members who need visibility, or customers asking for self-service access, or integration requirements with accounting/banking systems.

---

## Feature Distribution Matrix

| Feature | Foundation | Professional | Enterprise |
|---------|---------|-------------|-----------|
| **Core Operations** | | | |
| Quotations (create, send, share) | ✅ | ✅ | ✅ |
| Invoicing (create, issue, share) | ✅ | ✅ | ✅ |
| Revenue (manual payment recording) | ✅ | ✅ | ✅ |
| Customer Registry | ✅ | ✅ | ✅ |
| Purchases & Suppliers | ✅ | ✅ | ✅ |
| VAT Periods & Submissions | ✅ | ✅ | ✅ |
| Credit Notes | ✅ | ✅ | ✅ |
| Product Catalog | ✅ | ✅ | ✅ |
| Dashboard (basic) | ✅ | ✅ | ✅ |
| Mobile Access | ✅ | ✅ | ✅ |
| **Payments** | | | |
| Payment Links (manual) | ✅ | ✅ | ✅ |
| Payment Links (auto-generated) | ❌ | ✅ | ✅ |
| Payment Reminders (manual, one-at-a-time) | ✅ | ✅ | ✅ |
| Payment Reminders (automated schedule) | ❌ | ✅ | ✅ |
| **Financial Intelligence** | | | |
| Cash Flow Forecasting | ❌ | ✅ | ✅ |
| Profit & Loss Summary | ❌ | ✅ | ✅ |
| Expense Categorisation Insights | ❌ | ✅ | ✅ |
| Payment Schedules (Instalment Plans) | ❌ | ✅ | ✅ |
| Recurring Expense Validation | ❌ | ✅ | ✅ |
| **Operational Tools** | | | |
| Document Attachments | ❌ | ✅ | ✅ |
| Activity Log | ❌ | ✅ (readonly) | ✅ (full) |
| Client Portal (customer self-service) | ❌ | ❌ | ✅ |
| Activity Timeline & Notifications | ❌ | ❌ | ✅ |
| **Integrations** | | | |
| API Access | ❌ | ❌ | ✅ |
| Webhooks | ❌ | ❌ | ✅ |
| Multi-Currency | ❌ | ❌ | ✅ |
| **Team** | | | |
| Users included | 2 | 5 | Unlimited |
| Granular user permissions | Basic (admin/viewer) | ✅ Full module-level | ✅ Full module-level |
| Custom branding (client-facing) | ❌ | ❌ | ✅ |

---

## Permission Architecture

### Two-Dimensional Access Control

```
Business Plan Permissions (what the subscription allows)
          ×
User Role Permissions (what the owner grants to each user)
```

**Rule:** A user can never access a feature that the business plan doesn't include. User permissions are a subset of plan permissions.

### Permission Enforcement Layers

```
Request arrives
  → Layer 1: Authentication (is the user logged in?)
  → Layer 2: Plan Check (does the business subscription include this module?)
  → Layer 3: User Permission Check (has the owner granted this user access to this module?)
  → Layer 4: Access Level (full / readonly / none for this specific user)
```

### Data Model

#### `[portal].[SubscriptionPlan]`

| Column | Type | Notes |
|--------|------|-------|
| Id | INT IDENTITY PK | |
| Name | NVARCHAR(50) | 'foundation', 'professional', 'enterprise' |
| DisplayName | NVARCHAR(100) | 'Foundation', 'Professional', 'Enterprise' |
| MaxUsers | INT | 2, 5, 9999 |
| MonthlyPrice | DECIMAL(10,2) | |
| AnnualPrice | DECIMAL(10,2) | Annual discount |
| IsActive | BIT DEFAULT 1 | |
| CreatedAtUtc | DATETIME2 DEFAULT GETUTCDATE() | |

#### `[portal].[PlanModulePermission]`

| Column | Type | Notes |
|--------|------|-------|
| Id | INT IDENTITY PK | |
| SubscriptionPlanId | INT FK → SubscriptionPlan | |
| Module | NVARCHAR(50) | e.g., 'payment_reminders_auto', 'cashflow', 'pnl' |
| IsIncluded | BIT | Whether this plan includes the module |
| AccessLevel | NVARCHAR(20) | 'full', 'readonly', 'limited' |
| CreatedAtUtc | DATETIME2 DEFAULT GETUTCDATE() | |

#### `[portal].[BusinessSubscription]`

| Column | Type | Notes |
|--------|------|-------|
| Id | INT IDENTITY PK | |
| BusinessId | INT FK → Business | |
| SubscriptionPlanId | INT FK → SubscriptionPlan | |
| Status | NVARCHAR(20) | 'active', 'trial', 'cancelled', 'expired' |
| StartedAtUtc | DATETIME2 | |
| ExpiresAtUtc | DATETIME2 NULL | NULL = no expiry (active subscription) |
| TrialEndsAtUtc | DATETIME2 NULL | For trial periods |
| CreatedAtUtc | DATETIME2 DEFAULT GETUTCDATE() | |

#### `[portal].[UserModulePermission]`

| Column | Type | Notes |
|--------|------|-------|
| Id | INT IDENTITY PK | |
| UserId | NVARCHAR(450) FK → AspNetUsers | |
| BusinessId | INT FK → Business | |
| Module | NVARCHAR(50) | Same module keys as PlanModulePermission |
| AccessLevel | NVARCHAR(20) | 'full', 'readonly', 'none' |
| GrantedByUserId | NVARCHAR(450) | Who granted this permission |
| CreatedAtUtc | DATETIME2 DEFAULT GETUTCDATE() | |

### Module Keys

| Key | Feature | Available From |
|-----|---------|---------------|
| `quotation` | Quotation Platform | Foundation |
| `invoice` | Invoicing | Foundation |
| `revenue` | Revenue Control | Foundation |
| `customer` | Customer Registry | Foundation |
| `purchase` | Purchase Management | Foundation |
| `vat` | VAT Periods | Foundation |
| `credit` | Credit Notes | Foundation |
| `products` | Product Catalog | Foundation |
| `payment_link_manual` | Manual Payment Links | Foundation |
| `payment_reminder_manual` | Manual Reminders | Foundation |
| `payment_link_auto` | Auto Payment Links | Professional |
| `payment_reminder_auto` | Automated Reminders | Professional |
| `schedule_payments` | Payment Schedules | Professional |
| `cashflow` | Cash Flow Forecasting | Professional |
| `pnl` | Profit & Loss | Professional |
| `expense_insights` | Expense Categorisation | Professional |
| `recurring_expense_validation` | Recurring Expense Validation | Professional |
| `attachments` | Document Attachments | Professional |
| `audit_log` | Activity Log (readonly on Professional, full on Enterprise) | Professional |
| `client_portal` | Client Portal | Enterprise |
| `activity_timeline` | Activity & Notifications | Enterprise |
| `api` | API Access | Enterprise |
| `webhooks` | Webhook Subscriptions | Enterprise |
| `multi_currency` | Multi-Currency | Enterprise |

---

## User Roles Within a Business

| Role | Description | Default Permissions |
|------|-------------|--------------------|
| Owner | Business owner (always has full access to plan features) | All modules at 'full' within plan |
| Admin | Trusted team member with broad access | Configurable — default: all modules at 'full' |
| Staff | Day-to-day operator | Configurable — default: core modules at 'full', financial at 'readonly' |
| Viewer | Read-only access (e.g., external accountant) | Configurable — default: all modules at 'readonly' |
| External | Limited access for specific purpose | Configurable — default: selected modules only |

The Owner cannot have their permissions reduced. All other roles are fully configurable by the Owner.

---

## Upgrade Flow (UI Behaviour)

### Soft Gating (Awareness)

When a Foundation user navigates to a Professional feature area:
- **Don't hide the feature entirely** — show a preview/teaser
- Show the section heading with a lock icon and brief value description
- Example: "Cash Flow Forecasting — See your 30/60/90-day financial outlook. Available on Professional."
- Include a "Learn More" or "Upgrade" button (not aggressive)

### Hard Gating (Enforcement)

When a user attempts to use a gated feature via direct URL or API:
- Return a friendly "Feature not available on your current plan" page
- Show what the feature does and what plan includes it
- No error messages — always position as "here's what you're missing"

### Nudge Triggers (Contextual)

Show upgrade suggestions at natural pain points:
- Revenue Dashboard: "3 invoices are overdue. Automatic reminders could help."
- Invoice Detail: "Payment links are auto-included in emails on Professional."
- Invoice Detail (high value): "Set up payment schedules to track instalment collection. Available on Professional."
- Purchase List: "See spending insights and budget alerts on Professional."

**Never:** Pop-ups, blocking modals, countdown timers, or pressure tactics. The platform respects the business owner's decision.

---

## Pricing Principles

1. **Foundation must be genuinely useful** — a business should be able to operate fully on Foundation for months/years if that's what they need
2. **Professional sells itself** — the automation saves more time than the subscription costs
3. **Enterprise earns trust** — only offered to businesses that genuinely need scale/integrations
4. **Annual discount** — reward commitment (typically 15–20% off monthly price)
5. **No feature crippling** — Foundation features work fully, they're just not automated
6. **Transparent pricing** — published on the website, no "contact sales" for standard tiers
7. **Free trial of Professional** — 14 days, no credit card, full access (shows the automation value)

---

## Trial Strategy

| Plan | Trial | Purpose |
|------|-------|---------|
| Foundation | Free forever (with subscription) | Core platform access — this is what they pay the subscription for |
| Professional | 14-day free trial | Let them experience automation — reminders, cash flow, P&L |
| Enterprise | Demo invitation (existing feature) | Guided by 3 Inventors team for serious prospects |

The existing Demo Invitations system can serve as the Enterprise trial mechanism — SuperAdmin configures exactly what the prospect sees.

---

## Migration Path (Existing Users)

When this system launches, existing users transition as follows:

1. All current active businesses → **Professional** (grandfather for a period)
2. After grace period → businesses choose their tier based on actual usage
3. Businesses using only core features → suggest Foundation (save money)
4. Businesses using automation → stay on Professional
5. No forced downgrades — always the business owner's choice

---

## Relationship to Demo Invitations

The existing Demo Invitations system already implements per-module permission gating. The subscription tier system extends this same pattern:

| System | Controls | Mechanism |
|--------|----------|-----------|
| Demo Invitations | Module access for prospects | DemoPermissionFilter (claim-based) |
| Subscription Tiers | Module access for paying customers | PlanPermissionFilter (plan-based) |
| User Permissions | Module access within a business | UserPermissionFilter (role-based) |

All three share the same module key vocabulary and access level concept ('full', 'readonly', 'none'). The enforcement architecture is consistent — only the source of the permission differs.


---

## Pricing Summary

### Final Pricing

| Plan | Monthly Equivalent | Annual Charge | Positioning |
|------|-------------------|---------------|-------------|
| Foundation | €39/mo | €390/year | The base — complete business management for any business |
| Professional | €89/mo | €890/year | Automation — the platform works for you |
| Enterprise | €169/mo | €1,690/year | Scale — teams, integrations, self-service |

### Annual Discount Model

All tiers use a "pay for 10, get 12" annual billing model:

| Tier | Monthly Rate | Annual Charge | Savings |
|------|-------------|---------------|---------|
| Foundation | €39 | €390 | €78 (2 months free) |
| Professional | €89 | €890 | €178 (2 months free) |
| Enterprise | €169 | €1,690 | €338 (2 months free) |

This model prioritises operational continuity over monthly billing — a business should never lose access to its platform because of a missed payment.

### Pricing Rationale

**Foundation (€39/mo)** — The name is intentional. This tier provides the complete foundation for running a business — quotations, invoicing, revenue control, purchases, suppliers, VAT submissions, credit notes, product catalog, and customer statements. Unlike competitors who cripple basic tiers to force upgrades, Foundation is genuinely complete. Positioned alongside serious business platforms (Xero, QuickBooks), not budget invoicing tools.

**Professional (€89/mo)** — The highest-value tier. A business paying €89/mo for fully automated payment chasing, auto-generated payment links, cash flow forecasting, and P&L summaries is getting capabilities that would cost them €200+/month with a part-time bookkeeper. The automation pipeline alone (remind → pay link → auto-record) eliminates hours of manual work every week.

**Enterprise (€169/mo)** — For businesses running serious operations with multiple team members, customer self-service needs, and integration requirements. At €149/mo, still well below the cost of fragmented tools covering the same scope (separate invoicing + accounting + CRM + payment platform).

### Strategic Positioning

- **Do not compete on being the cheapest** — compete on professionalism, reliability, and automation
- **Professional is the hero tier** — it contains the highest perceived value and the strongest upgrade story
- **Annual pricing only** — presented as monthly equivalent for clarity, charged annually
- **14-day Professional trial** — lets prospects experience the automation before committing

### Value Comparison

| What the business gets | Cost with Portal (Professional) | Cost without Portal |
|------------------------|--------------------------------|---------------------|
| Invoice management | Included | €15–30/mo (basic tool) |
| Payment chasing | Automated | 2–4 hours/week manual time |
| Payment link processing | Included (Stripe fees separate) | €20–40/mo (separate tool) |
| Cash flow visibility | Real-time | €50–100/mo (accountant time) |
| P&L reporting | Automatic | €100–200/mo (bookkeeper) |
| VAT preparation | Built-in | €50–150/mo (accountant) |
| **Total alternative cost** | **€89/mo** | **€200–500+/mo** |

The Professional tier pays for itself within the first month for any business with 15+ invoices and regular purchase activity.
