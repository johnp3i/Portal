# Changelog

All notable feature updates to the Portal platform are documented here. Organized by date (newest first).

---

## [2026-11-05] - Payroll Register: on-page per-employee Preview

**Added a Preview action alongside Download: render the register on the page as per-employee ledger tables (no file), from the same data source the Excel download uses so the two can never drift.**

### Added
- Preview button (next to Download) renders one per-employee ledger table below the filter: Earnings, Employee Deductions, Employer Contributions, per-section totals, and an Employee_Totals block (Total Earnings, Total Employee Deductions, Net Salary, Total Employer Contributions, Total Cost)
- Each preview table has one column per month in the range + Total + a `%` column (blank for earnings), horizontally scrollable for wide ranges; missing-month cells show 0.00
- New `AxGetPreviewRegister` JSON endpoint returns the per-employee projection (never a file); same From/To and non-empty-selection validation as Download, applied before the service is called
- Shared `GetRegisterDataAsync` now feeds both Download and Preview (the workbook is built from the same aggregates the preview is projected from), so preview and download agree cell-for-cell

### Changed
- `GenerateRegisterWorkbookAsync` refactored to delegate data assembly to `GetRegisterDataAsync` (behavior-preserving: same decline paths and byte-identical workbook); the six existing payroll reports and protected service signatures are unchanged
- Preview clears when the range or employee selection changes (stale-guard)

---

## [2026-11-05] - Payroll Register: month-range, per-contribution totals, percentages

**Reshaped the Payroll Register from a single period to a FROM/TO month range, with per-contribution-type totals on the summary page and percentage columns on both sheets.**

### Changed
- Register now spans a From/To month-year range (defaults: To = latest finalised period, From = January of that year) instead of one period; From must be <= To (validated client + server)
- Summary sheet gained one column per employer contribution type, with a grand-totals row giving each contribution's total across all selected employees
- Per-employee detail sheets are now a month-by-month ledger: one column per month in the range, a Total column, and a `%` column (latest in-range rate; blank for earnings)
- Workbook filename and summary sheet name reflect the selected range
- Removed the single-period multiple-payslip guard (no longer applicable to a range); same-period duplicates are summed in the pivot

## [2026-11-05] - Payroll Register (Multi-Employee Excel Report)

**New additive payroll report: pick a finalised period and one or more employees, download a single Excel workbook with a summary sheet plus one detail sheet per employee.**

### Added
- New `PayrollRegister` page (sidebar -> Payroll -> Payroll Register) with period selector (newest-first) and multi-select employee picker with Select All
- `AxGetGenerateRegister` endpoint produces one .xlsx: Summary sheet (one row per selected employee + grand totals) then one detail sheet per employee (earnings, employee deductions, employer contributions, net, total, per-employee totals)
- `AxGetEmployeesForPeriod` refreshes the eligible-employee list on period change (clears prior selection)
- Eligible employees = only those with a finalised payslip in the period; terminated/inactive employees with a finalised payslip are still included (new batched `GetEmployeesByIdsAsync` repository method)
- Reuses existing payroll data/services unchanged; the six existing payroll reports are untouched (additive only)
- vanilla fetch + BlockUI + SweetAlert2 (blob-or-JSON download handling); MyChair styling (#0D5EA6 headers, #,##0.00)

## [2026-07-28] â€” Stripe Connect (Card Payments via Connect)

**Enables businesses to accept card payments from customers on shared invoice links, processed via Stripe Connect (destination charges) with automatic webhook reconciliation.**

### Added
- Business onboarding: OAuth Standard Connect flow from Business Settings (Connect/Disconnect)
- "Pay by Card" button on shared invoice pages (visible when business connected + outstanding balance > 0)
- Stripe Checkout Session creation with destination charge to connected account (no platform fee)
- Webhook endpoint (`/stripe/connect-webhook`) with signature verification
- `checkout.session.completed` handler: creates Payment record, recalculates financial status, captures Stripe fee from BalanceTransaction
- `checkout.session.expired` handler: marks session as expired
- Idempotency: duplicate webhook events are safely skipped (unique constraint on StripeSessionId)
- Receipt auto-generation triggered on webhook payment creation (if enabled)
- **Card Payments view** at `/Revenue/CardPayments` â€” fee transparency dashboard
  - Summary cards: Total Received (Gross), Total Stripe Fees, Net Received, Transaction Count
  - Transaction table with Date, Invoice, Customer, Gross, Fee, Net, Status
  - Date range filters (This Month, Last Month, Last 3 Months, Custom)
  - CSV export for accounting reconciliation
- Stripe payment badge/icon in Invoice Detail payment history
- Error handling: graceful fallback when checkout creation fails (account restricted, Stripe down)
- Plan permission gating: `stripe_connect` module key (Professional and Enterprise only)
- Owner-only access control for Connect/Disconnect actions

### Database Migrations
- `153_CreateStripeConnectedAccountTable.sql` (creates `[stripe]` schema + ConnectedAccount table)
- `154_CreateStripeCheckoutSessionTable.sql` (CheckoutSession table for tracking)
- `155_SeedCardPaymentMethodType.sql` (adds "Card" to PaymentMethodType)

### Configuration
- `Stripe:SecretKey` â€” Platform secret key (User Secrets)
- `Stripe:ConnectClientId` â€” Connect platform client ID (User Secrets)
- `Stripe:ConnectWebhookSecret` â€” Webhook signing secret (User Secrets)
- `Stripe:ConnectOAuthRedirectUri` â€” OAuth callback URL (User Secrets)

---

## [2026-07-09] â€” Module 7: Payment Schedules (Instalment Plans)

**Structured instalment plan management for invoice payments with auto-matching, status tracking, and VAT advisory.**

### Added
- Payment Schedule section on Invoice Detail pages (Revenue and Invoice views)
- Create instalment plans per invoice with dynamic row addition/removal and real-time balance validation
- Instalment status tracking: Pending, Due, Overdue, Paid, PartiallyPaid (computed at read time)
- Auto-matching: recorded payments automatically allocated to next eligible instalment (priority: Due â†’ Overdue â†’ Pending)
- Partial payment handling: warning + remainder instalment creation for shortfalls
- Schedule modification with full audit history (field changed, old/new values, who/when)
- VAT period warning: alerts when first instalment due date is after VAT submission deadline
- Schedule deletion with SweetAlert2 confirmation (blocked if any payments matched)
- Read-only view for users without `schedule_payments` permission
- Permission constant `schedule_payments` added to PortalModules
- 8 AJAX endpoints on RevenueController (AxPost/AxGet pattern)
- PaymentService integration: match on record, revert on void
- **Payment Schedules Overview page** at `/Revenue/PaymentSchedules` â€” bird's-eye view of all active instalment plans
- Overview KPI cards: Total Scheduled, Collected, Due This Month, Overdue (colour-coded)
- Monthly Payment Plan timeline with year selector and proportional bars
- Active Schedules table with progress bars, status badges, and invoice links
- Client-side filtering (Status, Invoice, Customer) and pagination (10/page)
- Sidebar navigation link under Finance section (gated by `schedule_payments` permission)

### Database Migrations
- `106_CreatePaymentScheduleInstalmentStatusTypeTable.sql`
- `107_CreatePaymentScheduleTable.sql`
- `108_CreatePaymentScheduleInstalmentTable.sql`
- `109_CreatePaymentScheduleHistoryTable.sql`

---

## [2026-07-08] â€” Permission Access Fixes (Bugfix)

### Fixed
- Invoice Detail page no longer shows error popup for Starter users (reminder partials conditionally rendered based on plan)
- CreditNoteController attribute aligned with ModuleControllerMap (`credit` module, not `invoice`)
- Added 7 missing controllers to ModuleControllerMap (ExpenseCategory, ExpenseCategoryLimit, Statement, Logo, LineItemCatalog, LineItemCatalogManagement, ProposalSection)
- Created migration 105_AddAuditLogToProfessionalPlan.sql for consistent deployments
- Removed [ModuleAccess] from SuperAdmin AuditController (SuperAdmins always access regardless of plan)

---

## [2026-07-08] â€” Activity Log (Phase 2: Audit Log Redesign)

**Business-manager Activity Log replacing the admin-only Audit Log viewer with a timeline-style feed.**

### Added
- Activity Log page at `/Activity` â€” timeline-style feed with plain-English summaries
- Quick stats row: changes this week, active team members, most active area, last activity
- Business-friendly filters: "What changed", "Who made the change", "What type of change", date range
- Expandable detail panels: before/after diff for edits, initial values for creates, deleted values for deletes
- Colored timeline dots: green (created), blue (edited), red (deleted), amber (status changed)
- User name resolution (batch-loaded from MembershipDbContext, "{FirstName} {LastInitial}." format)
- Relative timestamps: "Just now", "2 min ago", "Yesterday at 14:32", "3 days ago"
- Entity links: invoice numbers, customer names link to their detail pages
- Sidebar navigation under Finance section (gated by `audit_log` module)
- Plan permission gating: Professional + Enterprise plans (ReadOnly access level)
- Mobile responsive layout (640px breakpoint)
- Existing SuperAdmin Audit Log at /Admin/Audit preserved unchanged

### No Database Migrations
- Reuses existing `[audit].[AuditLog]` table and indexes â€” no schema changes needed

---

## [2026-07-07] â€” End-to-End Testing (Modules 3 & 4)

### Added
- Integration test suite for Payment Reminders: schedule â†’ evaluate â†’ send â†’ log â†’ idempotency (5 test cases, all passing)
- Module 4 E2E marked complete (bank transfer informative flow verified; Stripe Connect deferred)

---

## [2026-07-07] â€” Module 5: Cash Flow Forecasting

**Forward-looking cash position projection with confidence-weighted inflows and historical expense analysis.**

### Added
- Cash Flow Forecast page at `/CashFlow` with storytelling-first design (hero card, flow visualization, Chart.js projection)
- 30/60/90-day projection horizon with period selector
- Projected inflows from outstanding invoices with customer confidence weighting (DaysLateAverage)
- Projected outflows from 6-month historical expense category averages
- Running balance line chart with alert threshold danger zone (chartjs-plugin-annotation)
- Scenario modelling: toggle invoices out of projection to see "what if" impact (session-only)
- Configurable starting balance and alert threshold (Settings section inline on page)
- Dashboard widget on Home Dashboard (compact 30-day projection summary)
- Sidebar navigation link under Finance section (gated by `cashflow` module)
- Soft-gate teaser on Revenue Dashboard for Starter users
- Plan permission gating (`cashflow` module key â€” Professional plan only)
- Mobile responsive layout (640px breakpoint)

### Database Migrations
- `104_CreateCashFlowSettingsTable.sql` (creates `[cashflow]` schema + CashFlowSettings table)

---

## [2026-07-06] â€” Module 4: Payment Instructions (Bank Transfer)

**Replaces original Module 4 (Stripe Connect) with a lightweight bank-transfer payment flow.**

### Added
- "Pay by Bank Transfer" button on shared invoice pages (visible when business enables the toggle)
- Payment Instructions modal with: bank name, IBAN, SWIFT/BIC, payee name, outstanding amount, due date, and suggested transfer reference
- Copy-to-clipboard buttons for IBAN and transfer reference
- "I've made the payment" customer declaration flow â†’ sets invoice to PaymentOnboard status
- PaymentOnboard (Id=6) financial status â€” customer declaration pending business verification
- Amber info banner on Invoice Detail page when status is PaymentOnboard
- PaymentOnboard in invoice list filters and badge display
- Business Settings toggle: "Show bank transfer payment option on shared invoices"
- SWIFT/BIC field on BusinessPaymentDetail (add/edit forms + display)
- Rate limiting on payment declarations (3 per share token per hour)
- Audit logging for all payment declarations (IP address, timestamp, share token)
- Per-invoice payment instructions override â€” tri-state toggle on Invoice Detail (force show / force hide / follow business default)

### Database Migrations
- `100_AddSwiftBicToBusinessPaymentDetail.sql`
- `101_AddIsPaymentInstructionsEnabledToBusiness.sql`
- `102_SeedPaymentOnboardFinancialStatus.sql`
- `103_AddPaymentInstructionsOverrideToInvoice.sql`

### Future: Option C (Stripe Connect)
Documented as a future upgrade path â€” card payments and auto-reconciliation via Stripe Connect would supplement (not replace) bank transfer instructions.

---

## [2026-07-06] â€” Module 3: Payment Reminder Enhancements

**Extended the base Payment Reminders module with open tracking, test sending, and upcoming preview.**

### Added
- **Open Tracking** â€” tracking pixel embedded in every reminder email; records when recipient opens (with open count and timestamps)
- **Test Reminder Sending** â€” send preview reminder to any email with [TEST] subject prefix; excluded from all caps/metrics
- **Upcoming Reminders Preview** â€” dedicated page at `/PaymentReminder/Upcoming` showing projected reminders for the next 7/14/30 days
- **Reminder History Page** â€” global paginated view at `/PaymentReminder/History` with filters (Tier, Status, Method, Date, Customer)
- Enhanced history panel on Invoice Detail with "Opened" column and "Test" badge
- Auto-creation of invoice share links when sending reminders (if no active share exists)
- Collapsible help sections on Settings and Upcoming pages
- Daily send time display (converted to local timezone) on Settings and Upcoming pages
- "Send Test Reminder" modal on Invoice Detail page
- Sidebar navigation links for Upcoming Reminders and Reminder History
- Soft-gate teaser on Revenue Dashboard for Starter users

### Fixed
- `IsReminderOptedOut` missing from CustomerRepository SELECT queries
- `IsDisputed` missing from InvoiceRepository SELECT queries
- Reminder history not loading on Invoice Detail (partial not included)
- History not refreshing after sending a test reminder
- Email "View Invoice" button: correct URL (`/invoice-view/` not `/invoice/`), proper styling, auto-share-link creation
- `onTierToggle` and `onSystemToggle` not exposed globally in payment-reminder-settings.js
- Email preview tab switching broken (wrong CSS selectors in JS)

### Database Migrations
- `CreateReminderSchemaAndScheduleTable.sql` (Portal.Infrastructure/Migrations/)
- `CreatePaymentReminderLogTable.sql` (Portal.Infrastructure/Migrations/)
- `AddReminderOptOutAndDisputedColumns.sql` (Portal.Infrastructure/Migrations/)
- `099_AddOpenTrackingAndTestSendToPaymentReminderLog.sql`

---

## [2026-07-06] â€” Module 3: Payment Reminders (Base Module)

**Automated and manual payment reminder system with escalating tiers.**

### Added
- `[reminder]` schema with PaymentReminderSchedule and PaymentReminderLog tables
- Reminder schedule configuration UI (Settings â†’ Payment Reminders)
- Three escalation tiers: Friendly, Firm, Formal â€” with configurable day offsets, max reminders, min intervals
- Background service (daily at 06:00 UTC) evaluating all eligible invoices
- Evaluation logic: respects opt-outs, disputed invoices, partial payment suppression, max caps, min intervals
- Manual "Send Reminder" button on Invoice Detail
- Reminder history panel on Invoice Detail
- Dashboard widget (reminders sent this week, payments received after reminder)
- Per-customer opt-out (`IsReminderOptedOut` on Customer)
- Per-invoice dispute flag (`IsDisputed` on Invoice)
- Plan permission gates: `payment_reminder_manual` (Starter), `payment_reminder_auto` (Professional)
- Email templates: Friendly (blue), Firm (amber), Formal (red) with tier-specific CTA buttons
- Mobile responsive CSS for all reminder pages

---

## [Previously Completed] â€” Module 1: Profit & Loss Summary

### Added
- P&L computation logic (Revenue, COGS, OpEx)
- Period-based calculations (month/quarter/year/custom)
- Trend comparison (vs previous period, vs same period last year)
- PDF export
- Plan permission gate (`pnl` â€” Professional only)
- Soft-gate teaser on Dashboard for Starter users

---

## [Previously Completed] â€” Module 2: Expense Categorisation Insights

### Added
- Expense insights service (aggregation by category, period comparison)
- Pie/bar charts: spend by category
- Trend lines: category spend over last 6â€“12 months
- ExpenseCategoryBudget table and budget configuration UI
- Budget threshold alerts (exceeded/approaching)
- Top suppliers per category breakdown
- Month-over-month variance highlighting
- CSV export
- Plan permission gate (`expense_insights` â€” Professional only)
- Soft-gate teaser on Purchase list for Starter users

---

## [Previously Completed] â€” Module 6: Permission Gating Infrastructure

### Added
- SubscriptionPlan, PlanModulePermission, BusinessSubscription, UserModulePermission tables
- PlanPermissionFilter (global authorization)
- UserPermissionFilter (user-level module access)
- Plan check middleware/service
- "Feature not available on your plan" soft-gate page
- "Read-only access" view variant
- Subscription management UI
- User permission management UI
- All existing businesses assigned to Professional plan
