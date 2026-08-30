# Requirements Document

## Introduction

Purchases are recorded in the platform but have no payment deadline tracking. The business owner tracks supplier payment dates in their head or on a separate spreadsheet. They miss a payment, get a late fee, or worse — a critical service gets cut off.

This feature adds two optional date fields to purchases:

- **Supplier Due Date** — the actual payment deadline from the supplier's invoice. This is a fact. Missing this date can result in late fees, service disconnection, or penalties.
- **Target Payment Date** — the business owner's internal deadline for when they want to pay. This is a decision. A business may set this earlier than the supplier deadline to clear expenses before month-end, manage cash flow, or avoid last-minute pressure.

The dashboard and list indicators operate primarily against the **Target Payment Date** (the business commitment). If only the Supplier Due Date is set, it falls back to that. This model supports a future escalation pattern: the weekly financial snapshot email (Proposal #7) will warn the business when a target payment date has passed, and escalate weekly until the supplier due date — at which point the consequence is real (e.g., electricity disconnection, late fees).

Foundation tier — this is operational infrastructure, not intelligence.

## Glossary

- **Purchase**: An expense record in `[purchase].[Purchase]` representing money the business owes or has paid to a supplier
- **SupplierDueDate**: The actual payment deadline from the supplier's invoice. NULL means no external deadline recorded.
- **TargetPaymentDate**: The business owner's internal payment target. NULL means no target set. When set, this is the date the dashboard and indicators track against.
- **Effective_Due_Date**: The date used for dashboard/indicator logic: `TargetPaymentDate ?? SupplierDueDate`. If both are NULL, no payment tracking applies.
- **Upcoming_Payments_Widget**: A new dashboard section showing purchases with an effective due date approaching within the next 14 days
- **Overdue_Purchase**: A purchase where the effective due date is in the past and the purchase is not cancelled

## Requirements

### Requirement 1: Purchase Date Fields

**User Story:** As a business operator, I want to record both the supplier's official payment deadline and my own target payment date when I enter a purchase, so that I can track my internal commitments separately from external deadlines.

#### Acceptance Criteria

1. THE Portal database SHALL add `[SupplierDueDate] DATE NULL` and `[TargetPaymentDate] DATE NULL` columns to the existing `[purchase].[Purchase]` table
2. THE Purchase entity SHALL include `DateOnly? SupplierDueDate` and `DateOnly? TargetPaymentDate` properties
3. THE Purchase create form SHALL display two optional date input fields after the Invoice Date field: "Supplier Due Date" and "Target Payment Date"
4. THE Purchase edit form SHALL display both fields, pre-populated with existing values when editing
5. WHEN both fields are left empty, THE system SHALL store NULL for both (no payment tracking)
6. WHEN TargetPaymentDate is provided, THE system SHALL validate that it is not after the SupplierDueDate (if SupplierDueDate is also provided) — the target should be on or before the actual deadline
7. THE system SHALL allow TargetPaymentDate to be set without SupplierDueDate (the business knows when they want to pay even if the supplier deadline isn't formally tracked)
8. THE system SHALL allow SupplierDueDate to be set without TargetPaymentDate (the business records the deadline but doesn't set an internal target)
9. WHEN both dates are provided and TargetPaymentDate is after SupplierDueDate, THE form SHALL display an inline amber warning message below the Target Payment Date field: "Target date is after the supplier deadline." The form SHALL still allow submission — this is a soft warning, not a hard block.
10. THE Bulk Entry form and CSV Import flow SHALL NOT include the new date fields in this phase. Purchases created via bulk entry or CSV import will have both dates as NULL. This is a known limitation — bulk entry prioritises speed over deadline tracking.

### Requirement 2: Effective Due Date Logic

**User Story:** As a business operator, I want the platform to track my payment commitments using my target date when set, falling back to the supplier deadline when no target exists, so that the dashboard reflects what I'm actually managing against.

#### Acceptance Criteria

1. THE system SHALL compute an effective due date for each purchase as: `TargetPaymentDate ?? SupplierDueDate`
2. WHEN both dates are NULL, THE purchase SHALL have no effective due date and will not appear in payment tracking widgets or overdue indicators
3. ALL dashboard widgets, list page indicators, and future email notifications SHALL use the effective due date for status calculations

### Requirement 3: Purchase List — Due Date Display and Indicators

**User Story:** As a business operator, I want to see payment deadlines on my purchase list with clear visual indicators for overdue and due-soon items, so that I can prioritise payments at a glance.

#### Acceptance Criteria

1. THE Purchase list page table SHALL display a "Due" column after the existing Date (Invoice Date) column
2. THE Due column SHALL show the effective due date (TargetPaymentDate ?? SupplierDueDate). If TargetPaymentDate is set, show it. If only SupplierDueDate is set, show it with a subtle "(supplier)" label.
3. WHEN the effective due date is in the past and the purchase is not cancelled, THE list page SHALL display the date with an "Overdue" indicator (red text and pill)
4. WHEN the effective due date is today and the purchase is not cancelled, THE list page SHALL display "Today" with an amber indicator
5. WHEN the effective due date is within the next 7 days, THE list page SHALL display the date with a "Due Soon" amber indicator
6. WHEN the effective due date is more than 7 days away, THE list page SHALL display the date in normal text
7. WHEN both dates are NULL, THE list page SHALL show "—" in the Due column

### Requirement 4: Dashboard — Upcoming Supplier Payments Widget

**User Story:** As a business operator, I want to see upcoming supplier payment deadlines on my dashboard without navigating to the purchases page, so that I never miss a payment and can plan cash flow.

#### Acceptance Criteria

1. THE Dashboard SHALL display an "Upcoming Supplier Payments" section when the user has purchase module access
2. THE widget SHALL show purchases with an effective due date within the next 14 days (including overdue from the past), ordered by effective due date ascending (nearest due first)
3. THE widget SHALL display for each entry: Supplier name, Description (truncated), Amount (TotalAmount with currency symbol), Due Date (effective, formatted), and a status indicator
4. THE widget SHALL show a maximum of 5 entries, with a "View all purchases" link if more exist
5. WHEN no purchases have an effective due date within range, THE widget SHALL show: "No upcoming supplier payments."
6. THE status indicator colours SHALL follow: Overdue → red (#C24A4A), Due Today → amber (#C8912E), Due within 7 days → amber (#C8912E), Due 8-14 days → blue (#0D5EA6)
7. THE widget SHALL be positioned in the purchase-scoped area of the dashboard, after the existing Expenses KPI gauge
8. WHEN TargetPaymentDate is set and differs from SupplierDueDate, THE widget SHALL show the target date as the primary date with a small "(supplier: DD MMM)" reference below it

### Requirement 5: Purchase CSV and PDF Export

**User Story:** As a business operator, I want both dates included when I export purchases, so that my external records include full payment deadline information.

#### Acceptance Criteria

1. THE Purchase CSV export SHALL include "Supplier Due Date" and "Target Payment Date" columns after the "Invoice Date" column
2. WHEN a date is NULL, THE CSV export SHALL show an empty cell for that row
3. THE Purchase PDF export (`_ExportPdf.cshtml`) SHALL include "Supplier Due Date" and "Target Payment Date" columns after the "Date" column
4. WHEN a date is NULL, THE PDF export SHALL show an empty cell for that row

### Requirement 6: Future Integration — Weekly Financial Snapshot Email (Design Note)

This requirement is NOT implemented in this spec. It documents the intended integration point for Proposal #7 (Weekly Financial Snapshot Email).

#### Intended Escalation Behaviour

1. WHEN a purchase's TargetPaymentDate has passed and the purchase is not cancelled, THE weekly email SHALL include it in a "Missed Payment Targets" section with the supplier name, amount, target date, and supplier due date
2. THE weekly email SHALL continue including the purchase every week until either: (a) the purchase is cancelled, or (b) the SupplierDueDate passes
3. WHEN the SupplierDueDate passes (or is approaching within 3 days), THE weekly email SHALL escalate the entry to a "Critical — Supplier Deadline" section with a warning that service disconnection, late fees, or penalties may apply
4. THIS escalation pattern ensures the business owner gets progressively more urgent reminders: first a gentle nudge (target missed), then weekly persistence, then a critical warning before real consequences hit
