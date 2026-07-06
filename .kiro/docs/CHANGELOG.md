# Changelog

All notable feature updates to the Portal platform are documented here. Organized by date (newest first).

---

## [2026-07-06] — Module 4: Payment Instructions (Bank Transfer)

**Replaces original Module 4 (Stripe Connect) with a lightweight bank-transfer payment flow.**

### Added
- "Pay by Bank Transfer" button on shared invoice pages (visible when business enables the toggle)
- Payment Instructions modal with: bank name, IBAN, SWIFT/BIC, payee name, outstanding amount, due date, and suggested transfer reference
- Copy-to-clipboard buttons for IBAN and transfer reference
- "I've made the payment" customer declaration flow → sets invoice to PaymentOnboard status
- PaymentOnboard (Id=6) financial status — customer declaration pending business verification
- Amber info banner on Invoice Detail page when status is PaymentOnboard
- PaymentOnboard in invoice list filters and badge display
- Business Settings toggle: "Show bank transfer payment option on shared invoices"
- SWIFT/BIC field on BusinessPaymentDetail (add/edit forms + display)
- Rate limiting on payment declarations (3 per share token per hour)
- Audit logging for all payment declarations (IP address, timestamp, share token)
- Per-invoice payment instructions override — tri-state toggle on Invoice Detail (force show / force hide / follow business default)

### Database Migrations
- `100_AddSwiftBicToBusinessPaymentDetail.sql`
- `101_AddIsPaymentInstructionsEnabledToBusiness.sql`
- `102_SeedPaymentOnboardFinancialStatus.sql`
- `103_AddPaymentInstructionsOverrideToInvoice.sql`

### Future: Option C (Stripe Connect)
Documented as a future upgrade path — card payments and auto-reconciliation via Stripe Connect would supplement (not replace) bank transfer instructions.

---

## [2026-07-06] — Module 3: Payment Reminder Enhancements

**Extended the base Payment Reminders module with open tracking, test sending, and upcoming preview.**

### Added
- **Open Tracking** — tracking pixel embedded in every reminder email; records when recipient opens (with open count and timestamps)
- **Test Reminder Sending** — send preview reminder to any email with [TEST] subject prefix; excluded from all caps/metrics
- **Upcoming Reminders Preview** — dedicated page at `/PaymentReminder/Upcoming` showing projected reminders for the next 7/14/30 days
- **Reminder History Page** — global paginated view at `/PaymentReminder/History` with filters (Tier, Status, Method, Date, Customer)
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

## [2026-07-06] — Module 3: Payment Reminders (Base Module)

**Automated and manual payment reminder system with escalating tiers.**

### Added
- `[reminder]` schema with PaymentReminderSchedule and PaymentReminderLog tables
- Reminder schedule configuration UI (Settings → Payment Reminders)
- Three escalation tiers: Friendly, Firm, Formal — with configurable day offsets, max reminders, min intervals
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

## [Previously Completed] — Module 1: Profit & Loss Summary

### Added
- P&L computation logic (Revenue, COGS, OpEx)
- Period-based calculations (month/quarter/year/custom)
- Trend comparison (vs previous period, vs same period last year)
- PDF export
- Plan permission gate (`pnl` — Professional only)
- Soft-gate teaser on Dashboard for Starter users

---

## [Previously Completed] — Module 2: Expense Categorisation Insights

### Added
- Expense insights service (aggregation by category, period comparison)
- Pie/bar charts: spend by category
- Trend lines: category spend over last 6–12 months
- ExpenseCategoryBudget table and budget configuration UI
- Budget threshold alerts (exceeded/approaching)
- Top suppliers per category breakdown
- Month-over-month variance highlighting
- CSV export
- Plan permission gate (`expense_insights` — Professional only)
- Soft-gate teaser on Purchase list for Starter users

---

## [Previously Completed] — Module 6: Permission Gating Infrastructure

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
