# Requirements Document

## Introduction

Automated Payment Reminders enables businesses to send escalating email reminders to customers about unpaid or overdue invoices. The system supports both manual (one-at-a-time) reminders for Starter plan users and fully automated daily evaluation with batch sending for Professional plan users. Businesses configure their own reminder schedules (days before/after due date, escalation tiers), and all reminder activity is logged for audit and reporting.

## Glossary

- **Reminder_Service**: The background evaluation engine that identifies which invoices require reminders on a given day and dispatches reminder emails accordingly.
- **Reminder_Schedule**: A business-level configuration defining when reminders are sent relative to an invoice's due date, including escalation tiers (friendly, firm, formal).
- **Reminder_Log**: An audit record capturing each individual reminder email sent, including the invoice, customer, recipient, tier, and timestamp.
- **Escalation_Tier**: A severity level for reminder communication — Friendly (pre-due), Firm (shortly overdue), or Formal (significantly overdue).
- **Opt_Out**: A per-customer flag indicating the customer has opted out of receiving automated payment reminders from the business.
- **Disputed_Invoice**: An invoice flagged as disputed by the business, which must be excluded from automated reminders.
- **Recent_Partial_Payment**: A partial payment received on an invoice within a configurable recency window (e.g., last 7 days), which suppresses reminders.
- **Portal**: The multi-tenant ASP.NET Core MVC back-office platform.
- **Business**: A tenant entity on the Portal platform; all data is isolated by BusinessId.
- **Invoice**: A financial document representing an obligation to pay, with a DueDate and financial status.
- **Customer**: A client entity registered under a Business, with optional email address.
- **Plan_Gate**: A permission check based on the business's subscription plan (Starter or Professional) controlling feature availability.

## Requirements

### Requirement 1: Reminder Schedule Configuration

**User Story:** As a business owner, I want to configure when payment reminders are sent relative to invoice due dates, so that I can control the timing and escalation of reminder communications.

#### Acceptance Criteria

1. THE Reminder_Schedule SHALL store the following per-tier configuration for each Business: days offset relative to DueDate (negative for before, positive for after), and the Escalation_Tier (Friendly, Firm, or Formal).
2. THE Reminder_Schedule SHALL include an IsEnabled flag per tier, allowing the business to independently enable or disable each Escalation_Tier (Friendly, Firm, Formal).
3. WHEN a Business has no Reminder_Schedule configured, THE Portal SHALL use system defaults: Friendly at -3 days (enabled), Firm at +7 days (disabled), Formal at +21 days (disabled).
4. WHEN a business user saves a Reminder_Schedule via the Settings UI, THE Portal SHALL validate that each days-offset value is an integer and that the Friendly tier offset is less than the Firm tier offset, which is less than the Formal tier offset.
5. THE Reminder_Schedule SHALL support configuring a maximum number of reminders per tier per invoice (e.g., send Firm reminder at most 2 times).
6. THE Reminder_Schedule SHALL support configuring a minimum interval in days between consecutive reminders of the same tier for the same invoice.
7. WHEN a Reminder_Schedule is updated, THE Portal SHALL apply the new configuration to future evaluations only and SHALL NOT retroactively re-evaluate past reminders.

### Requirement 2: Reminder Evaluation Logic

**User Story:** As a business owner, I want the system to evaluate which invoices need reminders each day, so that appropriate customers are contacted at the right time without manual effort.

#### Acceptance Criteria

1. WHEN the Reminder_Service executes daily evaluation for a Business, THE Reminder_Service SHALL identify all invoices where the current date matches a configured reminder trigger point (days offset from DueDate) for any Escalation_Tier.
2. THE Reminder_Service SHALL only consider invoices with InvoiceFinancialStatusTypeId of Unpaid (1), PartiallyPaid (2), or Overdue (4).
3. THE Reminder_Service SHALL exclude invoices where the associated Customer has Opt_Out enabled for the Business.
4. THE Reminder_Service SHALL exclude invoices flagged as disputed.
5. THE Reminder_Service SHALL exclude invoices that have received a Recent_Partial_Payment within the configured recency window.
6. THE Reminder_Service SHALL exclude invoices where the Customer has no email address on record.
7. THE Reminder_Service SHALL exclude invoices that have already received the maximum configured number of reminders for the matching Escalation_Tier.
8. THE Reminder_Service SHALL exclude invoices where the last reminder of the same tier was sent fewer days ago than the configured minimum interval.
9. THE Reminder_Service SHALL enforce tenant isolation — evaluation for one Business SHALL NOT access or affect data belonging to another Business.
10. THE Reminder_Service SHALL skip any Escalation_Tier where IsEnabled is false for the Business's Reminder_Schedule, regardless of whether the trigger date matches.

### Requirement 3: Reminder Email Sending

**User Story:** As a business owner, I want reminder emails to be sent automatically to customers with unpaid invoices, so that I can improve cash flow without manual follow-up.

#### Acceptance Criteria

1. WHEN the Reminder_Service identifies an invoice that requires a reminder, THE Reminder_Service SHALL send an email to the Customer's email address using the appropriate Escalation_Tier template.
2. THE Reminder_Service SHALL use the existing IEmailSender infrastructure to dispatch reminder emails via the appropriate EmailDepartment.
3. THE Reminder_Service SHALL include the invoice number, total outstanding amount, due date, and business name in every reminder email.
4. WHEN a Friendly tier reminder is sent, THE Reminder_Service SHALL use a courteous tone indicating the invoice is approaching or at its due date.
5. WHEN a Firm tier reminder is sent, THE Reminder_Service SHALL use a direct tone indicating the invoice is overdue and requesting prompt payment.
6. WHEN a Formal tier reminder is sent, THE Reminder_Service SHALL use a professional formal tone indicating the invoice is significantly overdue and may require further action.
7. IF the email delivery fails, THEN THE Reminder_Service SHALL log the failure in the Reminder_Log with the error details and SHALL NOT retry within the same evaluation cycle.

### Requirement 4: Reminder Audit Logging

**User Story:** As a business owner, I want every reminder sent to be logged, so that I have a complete audit trail of all communications with customers about unpaid invoices.

#### Acceptance Criteria

1. WHEN a reminder email is sent successfully, THE Portal SHALL create a Reminder_Log record containing: InvoiceId, CustomerId, BusinessId, recipient email, Escalation_Tier, timestamp (UTC), and a success status.
2. WHEN a reminder email fails to send, THE Portal SHALL create a Reminder_Log record containing the same fields plus the error reason and a failure status.
3. THE Reminder_Log SHALL be queryable by InvoiceId to support displaying reminder history on the Invoice Detail view.
4. THE Reminder_Log SHALL be queryable by BusinessId and date range to support the Dashboard summary widget.
5. THE Reminder_Log SHALL enforce tenant isolation — log records for one Business SHALL NOT be accessible to another Business.

### Requirement 5: Manual Reminder Sending

**User Story:** As a Starter plan user, I want to manually send a payment reminder from the Invoice Detail page, so that I can remind customers about specific unpaid invoices one at a time.

#### Acceptance Criteria

1. WHEN a user clicks "Send Reminder" on an Invoice Detail page, THE Portal SHALL send a reminder email to the associated Customer's email address.
2. THE Portal SHALL allow the user to select the Escalation_Tier (Friendly, Firm, or Formal) before sending a manual reminder.
3. IF the associated Customer has no email address, THEN THE Portal SHALL display an error message and SHALL NOT attempt to send the reminder.
4. IF the associated Customer has Opt_Out enabled, THEN THE Portal SHALL display a warning indicating the customer has opted out and SHALL require explicit confirmation before sending.
5. WHEN a manual reminder is sent, THE Portal SHALL create a Reminder_Log record with the same structure as automated reminders, marked as manually triggered.
6. THE Portal SHALL only display the "Send Reminder" button for invoices with InvoiceFinancialStatusTypeId of Unpaid (1), PartiallyPaid (2), or Overdue (4).

### Requirement 6: Reminder History Display

**User Story:** As a business user, I want to see a history of all reminders sent for a specific invoice, so that I can understand what communications have already been sent to the customer.

#### Acceptance Criteria

1. THE Portal SHALL display a reminder history panel on the Invoice Detail view showing all Reminder_Log records for that invoice.
2. THE reminder history panel SHALL display for each entry: the Escalation_Tier, the recipient email, the date/time sent, and whether it was manual or automated.
3. WHEN no reminders have been sent for an invoice, THE Portal SHALL display an empty state message in the history panel.
4. THE reminder history panel SHALL order entries by date descending (most recent first).

### Requirement 7: Customer Opt-Out Configuration

**User Story:** As a business user, I want to configure per-customer opt-out from payment reminders, so that I can respect customer preferences and avoid sending unwanted communications.

#### Acceptance Criteria

1. THE Portal SHALL provide an opt-out toggle on the Customer record for payment reminders.
2. WHEN a Customer's opt-out is enabled, THE Reminder_Service SHALL skip all invoices belonging to that Customer during automated evaluation.
3. WHEN a Customer's opt-out status changes, THE Portal SHALL apply the change immediately for future evaluations.
4. THE opt-out configuration SHALL be scoped to the Business tenant — opting out from one Business SHALL NOT affect other Businesses.

### Requirement 8: Background Job Execution

**User Story:** As a platform operator, I want the reminder evaluation to run automatically on a daily schedule, so that reminders are sent without manual intervention for Professional plan businesses.

#### Acceptance Criteria

1. THE Reminder_Service SHALL execute as a background job triggered once daily at a configurable time (default: 06:00 UTC).
2. WHEN the background job executes, THE Reminder_Service SHALL evaluate all active Businesses that have the `payment_reminder_auto` module permission.
3. IF the background job fails for one Business, THEN THE Reminder_Service SHALL continue processing remaining Businesses and SHALL log the error.
4. THE background job SHALL implement idempotency — running the same evaluation twice for the same date SHALL NOT result in duplicate reminders being sent.
5. THE background job SHALL process Businesses sequentially to avoid overwhelming the email service.

### Requirement 9: Plan Permission Gating

**User Story:** As a platform operator, I want to gate reminder features by subscription plan, so that manual reminders are available to Starter users and automated reminders are restricted to Professional users.

#### Acceptance Criteria

1. THE Portal SHALL gate the manual "Send Reminder" button behind the `payment_reminder_manual` module permission using the existing ModuleAccess attribute.
2. THE Portal SHALL gate the automated reminder schedule configuration UI behind the `payment_reminder_auto` module permission.
3. THE Portal SHALL gate the background job execution to only evaluate Businesses with the `payment_reminder_auto` module permission.
4. WHEN a Starter plan user navigates to the automated reminder settings, THE Portal SHALL display the standard soft-gate teaser page.
5. THE Portal SHALL allow access to reminder history view for all plan levels that have at least the `payment_reminder_manual` permission.

### Requirement 10: Dashboard Summary Widget

**User Story:** As a business owner, I want to see a summary of reminder activity on my dashboard, so that I can track the effectiveness of payment reminders at a glance.

#### Acceptance Criteria

1. THE Portal SHALL display a reminder summary widget on the Revenue Dashboard showing: total reminders sent in the current week, and total payments received within 7 days of a reminder being sent.
2. THE widget SHALL be gated behind the `payment_reminder_manual` module permission.
3. WHEN no reminders have been sent in the current week, THE widget SHALL display a zero-state with a brief explanation.
4. THE widget SHALL calculate "payments received after reminder" by correlating Payments recorded within 7 days of the most recent Reminder_Log entry for the same invoice.

### Requirement 11: Disputed Invoice Handling

**User Story:** As a business user, I want disputed invoices to be automatically excluded from reminders, so that I do not antagonise customers who have raised legitimate concerns.

#### Acceptance Criteria

1. THE Portal SHALL provide a mechanism to flag an invoice as disputed (a boolean `IsDisputed` field on the Invoice entity).
2. WHILE an invoice is flagged as disputed, THE Reminder_Service SHALL exclude it from both automated and manual reminder evaluation.
3. WHEN a disputed flag is removed from an invoice, THE Reminder_Service SHALL resume normal evaluation for that invoice in subsequent cycles.

### Requirement 12: Recent Partial Payment Suppression

**User Story:** As a business owner, I want reminders to be suppressed for invoices that recently received a partial payment, so that customers who are actively paying are not needlessly reminded.

#### Acceptance Criteria

1. THE Reminder_Schedule SHALL include a configurable recency window in days (default: 7 days) for partial payment suppression.
2. WHEN an invoice has received a Payment within the configured recency window, THE Reminder_Service SHALL suppress reminders for that invoice.
3. WHEN the recency window has elapsed since the last partial payment, THE Reminder_Service SHALL resume normal evaluation for that invoice.

### Requirement 13: Soft-Gate Teaser for Starter Users

**User Story:** As a Starter plan user, I want to see a teaser for automated reminders on the Revenue Dashboard, so that I am aware of the feature and motivated to upgrade.

#### Acceptance Criteria

1. WHILE a Business is on the Starter plan (lacks `payment_reminder_auto` permission), THE Portal SHALL display a soft-gate teaser card on the Revenue Dashboard promoting automated payment reminders.
2. THE teaser card SHALL describe the benefit of automated reminders and include a call-to-action to upgrade.
3. WHEN a Business upgrades to Professional plan, THE Portal SHALL replace the teaser card with the functional reminder summary widget.

### Requirement 14: Database Schema

**User Story:** As a developer, I want the reminder data stored in a dedicated schema with proper referential integrity, so that the feature is maintainable and consistent with platform conventions.

#### Acceptance Criteria

1. THE Portal database SHALL contain a `[reminder]` schema for all payment reminder tables.
2. THE `PaymentReminderSchedule` table SHALL include: Id, BusinessId (FK to Business), EscalationTier (varchar), DaysOffset (int), MaxRemindersPerTier (int), MinIntervalDays (int), PartialPaymentSuppressionDays (int), IsEnabled (bit) — controls whether this specific tier is active for the business, CreatedAtUtc (datetime), and UpdatedAtUtc (datetime). Each row represents one tier, so IsEnabled is per-tier not per-schedule.
3. THE `PaymentReminderLog` table SHALL include: Id, BusinessId (FK to Business), InvoiceId (FK to Invoice), CustomerId (FK to Customer), RecipientEmail (nvarchar), EscalationTier (varchar), IsSentSuccessfully (bit), ErrorMessage (nvarchar, nullable), IsManualTrigger (bit), SentAtUtc (datetime), and CreatedAtUtc (datetime).
4. THE PaymentReminderLog table SHALL have an index on (BusinessId, InvoiceId) for efficient history queries.
5. THE PaymentReminderLog table SHALL have an index on (BusinessId, SentAtUtc) for efficient dashboard widget queries.
6. THE Customer table SHALL include an `IsReminderOptedOut` column (bit, default 0) for per-customer opt-out.
7. THE Invoice table SHALL include an `IsDisputed` column (bit, default 0) for disputed invoice flagging.
