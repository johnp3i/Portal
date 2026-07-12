# Phase 1 — Development Timetable

## Overview

This timetable covers the implementation of all Phase 1 (Professional tier) features. Build order is optimised for dependency and complexity — quick wins first, external integrations last.

---

## Module 1: Profit & Loss Summary

**Effort:** Low | **Dependencies:** None (uses existing invoice payments + purchases)

- [x] 1.1 Define P&L computation logic (Revenue = payments received, COGS = stock purchases, OpEx = expense purchases)
- [x] 1.2 Create P&L service with period-based calculations (month/quarter/year/custom)
- [x] 1.3 Create P&L controller and view (period selector, summary cards, breakdown table)
- [x] 1.4 Add trend comparison (vs previous period, vs same period last year)
- [x] 1.5 Add PDF export capability
- [x] 1.6 Add plan permission gate (`pnl` module key — Professional only)
- [x] 1.7 Add soft-gate teaser for Starter users on Dashboard
- [x] 1.8 Visual QA and mobile responsiveness check

---

## Module 2: Expense Categorisation Insights

**Effort:** Low | **Dependencies:** None (uses existing purchases + expense categories)

- [x] 2.1 Create expense insights service (aggregation by category, period comparison)
- [x] 2.2 Create expense insights controller and view (charts, breakdown table)
- [x] 2.3 Add pie/bar chart: spend by category (current period)
- [x] 2.4 Add trend lines: category spend over last 6–12 months
- [x] 2.5 Create `ExpenseCategoryBudget` table and budget configuration UI
- [x] 2.6 Add budget threshold alerts (exceeded/approaching)
- [x] 2.7 Add top suppliers per category breakdown
- [x] 2.8 Add month-over-month variance highlighting
- [x] 2.9 Add CSV export for category breakdown
- [x] 2.10 Add plan permission gate (`expense_insights` module key — Professional only)
- [x] 2.11 Add soft-gate teaser for Starter users on Purchase list
- [x] 2.12 Visual QA and mobile responsiveness check

---

## Module 3: Automated Payment Reminders

**Effort:** Medium | **Dependencies:** Email service (existing), background job infrastructure (new)

- [x] 3.1 Create `PaymentReminderSchedule` table (business-level config: days before/after due, frequency)
- [x] 3.2 Create `PaymentReminderLog` table (tracks each email sent per invoice)
- [x] 3.3 Create reminder schedule configuration UI (Settings → Payment Reminders)
- [x] 3.4 Design escalating email templates (friendly → firm → formal)
- [x] 3.5 Create reminder evaluation service (which invoices need reminders today?)
- [x] 3.6 Implement background job (daily execution — evaluate all overdue/approaching invoices)
- [x] 3.7 Implement reminder sending logic (respect opt-outs, skip disputed, skip partially-paid-recently)
- [x] 3.8 Add activity logging (reminder sent events)
- [x] 3.9 Add manual "Send Reminder" button on Invoice Detail (Starter: one-at-a-time)
- [x] 3.10 Add reminder history view on Invoice Detail (who was reminded, when)
- [x] 3.11 Add Dashboard summary widget ("X reminders sent this week, Y payments received after reminder")
- [x] 3.12 Add per-customer opt-out configuration
- [x] 3.13 Add plan permission gates (`payment_reminder_manual` for Starter, `payment_reminder_auto` for Professional)
- [x] 3.14 Add soft-gate teaser on Revenue Dashboard for Starter users
- [x] 3.15 Visual QA and mobile responsiveness check
- [x] 3.16 End-to-end testing: schedule → evaluate → send → log

---

## Module 4: Payment Instructions (Bank Transfer)

**Effort:** Low–Medium | **Dependencies:** Shared Invoice infrastructure (existing)

- [x] 4.1 Add SwiftBic column to BusinessPaymentDetail
- [x] 4.2 Add IsPaymentInstructionsEnabled column to Business
- [x] 4.3 Seed PaymentOnboard (Id=6) financial status
- [x] 4.4 Create PaymentInstructionsService (toggle, bank details query, declare payment, rate limit)
- [x] 4.5 Add toggle endpoint (MyBusinessController)
- [x] 4.6 Add payment-instructions GET endpoint (InvoiceViewController)
- [x] 4.7 Add declare-payment POST endpoint (InvoiceViewController)
- [x] 4.8 Inject "Pay by Bank Transfer" button and modal into shared invoice page
- [x] 4.9 Add Payment Instructions toggle to Business Settings
- [x] 4.10 Add SWIFT/BIC field to payment details form
- [x] 4.11 Add PaymentOnboard info banner to Invoice Detail
- [x] 4.12 Add PaymentOnboard to invoice list filters
- [x] 4.13 Visual QA and mobile responsiveness check
- [x] 4.14 End-to-end testing: toggle → share → pay button → declare → verify status *(bank transfer only — informative flow verified; Stripe Connect deferred to future)*

### Option C: Stripe Connect (Future Upgrade)

Bank transfer instructions can be supplemented (not replaced) with Stripe Connect for card payments and automatic reconciliation. This would require:
- Stripe Connect platform registration
- OAuth connect/disconnect flow
- Webhook endpoint with signature verification
- Auto-reconciliation (webhook → Payment record → invoice status update)
- Database tables: BusinessPaymentGateway, InvoicePaymentLink

Both payment methods would coexist on the shared invoice page — customers could choose "Pay by Card" or "Pay by Bank Transfer".

---

## Module 5: Cash Flow Forecasting

**Effort:** Medium | **Dependencies:** Benefits from Modules 1–4 being complete (richer data)

- [x] 5.1 Create `CashFlowSettings` table (starting balance, alert threshold per business)
- [x] 5.2 Create cash flow projection service (inflows from outstanding invoices by due date, outflows from avg expenses)
- [x] 5.3 Implement confidence weighting (customer payment history → likelihood of on-time payment)
- [x] 5.4 Create cash flow controller and view (30/60/90-day chart with running balance line)
- [x] 5.5 Add starting balance configuration (Settings → Cash Flow)
- [x] 5.6 Add projected inflows breakdown (which invoices contribute to each period)
- [x] 5.7 Add projected outflows breakdown (average expense categories per month)
- [x] 5.8 Add alert threshold: notify when projected balance drops below configured minimum
- [x] 5.9 Add scenario modelling: "what if Invoice X doesn't pay on time?"
- [x] 5.10 Add Dashboard widget (mini cash flow projection)
- [x] 5.11 Add plan permission gate (`cashflow` module key — Professional only)
- [x] 5.12 Add soft-gate teaser for Starter users on Revenue Dashboard
- [x] 5.13 Visual QA and mobile responsiveness check

---

## Module 6: Permission Gating Infrastructure

**Effort:** Medium | **Dependencies:** Must be built before or alongside Modules 1–5

- [x] 6.1 Create `SubscriptionPlan` table with seed data (Starter, Professional, Enterprise)
- [x] 6.2 Create `PlanModulePermission` table with seed data (module keys per plan)
- [x] 6.3 Create `BusinessSubscription` table
- [x] 6.4 Create `UserModulePermission` table
- [x] 6.5 Create `PlanPermissionFilter` (global authorization filter — checks business plan)
- [x] 6.6 Create `UserPermissionFilter` (checks user-level module access within plan)
- [x] 6.7 Create plan check middleware/service (injectable, used by controllers and views)
- [x] 6.8 Create "Feature not available on your plan" view (soft-gate page)
- [x] 6.9 Create "Read-only access" view variant for user-level readonly
- [x] 6.10 Add subscription management UI (Admin → Business Subscription)
- [x] 6.11 Assign all existing businesses to Professional plan (migration)
- [x] 6.12 Add user permission management UI (Business Settings → User Permissions)
- [x] 6.13 Integration test: Starter user blocked from Professional features
- [x] 6.14 Integration test: User with readonly access can view but not modify

---

## Module 7: Payment Schedules (Instalment Plans)

**Effort:** Medium | **Dependencies:** Revenue module (existing), Payment recording (existing), VAT Submission Periods (existing)

- [x] 7.1 Create `PaymentSchedule` table (per-invoice instalment plan: InvoiceId, CreatedByUserId, CreatedAtUtc, Notes)
- [x] 7.2 Create `PaymentScheduleInstalment` table (individual instalment: ScheduleId, Amount, DueDate, Status, PaymentId nullable)
- [x] 7.3 Create `PaymentScheduleHistory` table (tracks modifications: who changed what and when)
- [x] 7.4 Create PaymentScheduleService (CRUD, auto-suggest remaining amount, match payments to instalments)
- [x] 7.5 Create payment schedule UI on Invoice Detail page (create/view/edit schedule with instalment rows)
- [x] 7.6 Create payment schedule UI on Revenue InvoiceDetail page (same functionality)
- [x] 7.7 Implement auto-suggestion: when creating schedule, suggest remaining balance for next instalment
- [x] 7.8 Implement payment-to-instalment matching (record payment → auto-match to next due instalment)
- [x] 7.9 Handle partial instalment payments (warning + create new instalment for remainder)
- [x] 7.10 Implement schedule modification with history tracking (adjustments logged with before/after)
- [x] 7.11 Add VAT period warning: notify user when first instalment is after the invoice's VAT period submission date
- [x] 7.12 Add VAT advisory: suggest first instalment should cover at least the VAT amount (€TaxAmount)
- [x] 7.13 Display instalment status indicators (Upcoming, Due, Overdue, Paid, Partially Paid)
- [x] 7.14 Add user permission gate (`schedule_payments` module key or use existing `revenue` module)
- [x] 7.15 Visual QA and mobile responsiveness check
- [x] 7.16 End-to-end testing: create schedule → record payments → verify matching → modify schedule

### Future Sub-Tasks (Phase 2)
- [ ] 7.17 Customer-facing payment schedule agreement via shared invoice link (business proposes schedule, customer accepts/negotiates)
- [ ] 7.18 Per-instalment reminder integration with Payment Reminders module (send reminder N days before each instalment due date)
- [ ] 7.19 Payment schedule impact on Cash Flow Forecasting (use instalment dates instead of invoice due date for projections)

---

## Build Order & Dependencies

```
Module 6 (Permission Infrastructure) ←── Must be first or parallel with Module 1
    │
    ├── Module 1 (P&L Summary) ←── No dependencies, quick win
    │
    ├── Module 2 (Expense Insights) ←── No dependencies, quick win
    │
    ├── Module 3 (Payment Reminders) ←── Needs background job setup
    │
    ├── Module 4 (Stripe Connect) ←── External integration
    │
    └── Module 5 (Cash Flow) ←── Benefits from 1–4 being complete

Module 7 (Payment Schedules) ←── Uses Revenue + Payments + VAT infrastructure
```

**Recommended parallel tracks:**
- Track A: Module 6 (permissions) → Module 1 (P&L) → Module 2 (Expenses)
- Track B: Module 3 (Reminders) → Module 4 (Stripe) → Module 5 (Cash Flow)

Track A can start immediately. Track B can start once the background job infrastructure from 3.6 is in place.

---

## Completion Criteria

Each module is considered complete when:
- [ ] All sub-tasks checked off
- [ ] Plan permission gating verified (Starter blocked, Professional allowed)
- [ ] Soft-gate teasers visible to Starter users
- [ ] Mobile responsive at 375px and 810px
- [ ] No regressions in existing functionality
- [ ] Documentation updated (design doc or spec if applicable)

---

## Post-Phase 1 Milestones

- [ ] All 7 modules complete and verified
- [ ] Landing page updated with new tier pricing (Starter €39/Professional €79/Enterprise €149)
- [ ] Subscription management system live
- [ ] Existing users migrated to Professional (grandfathered)
- [ ] 14-day Professional trial flow implemented
- [ ] Phase 2 planning begins (Client Portal, Document Attachments, Activity Timeline)

---

## Next Phase

When Phase 1 is complete, proceed to:

**→ Phase 2: Operational Completeness** (`.kiro/docs/Phase2_Development_Timetable.md`)
- Client Portal (customer self-service)
- Document Attachments (file uploads on purchases/invoices/quotations)
- Activity Timeline & Notifications (real-time feed, email digests)
- Audit Log Access (permission-gated access to existing audit infrastructure)

Phase 2 delivers the Enterprise tier and positions the platform for growing teams with multi-user collaboration needs.
