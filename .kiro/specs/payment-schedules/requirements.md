# Requirements Document

## Introduction

The Payment Schedules (Instalment Plans) feature allows businesses to define structured instalment plans against invoices. When a customer agrees to pay an invoice in multiple instalments over time, the business can formally record this agreement, track each instalment's status, auto-match incoming payments, and maintain a full audit history of any schedule modifications. The feature also integrates VAT awareness to warn users when instalment timing conflicts with VAT submission obligations.

## Glossary

- **Portal**: The ASP.NET Core MVC 8 web application operated by business tenants
- **Payment_Schedule**: A structured plan attached to an invoice defining how the outstanding balance will be collected across multiple instalments over time
- **Instalment**: A single planned payment within a Payment Schedule, with a target amount and optional due date
- **Instalment_Status**: The current state of an instalment — one of: Pending, Due, Overdue, Paid, PartiallyPaid
- **Schedule_History_Entry**: An audit record capturing a single modification to a Payment Schedule or its instalments
- **Invoice**: A financial document in the [invoice].Invoice table representing an obligation to pay
- **Payment**: A monetary transaction recorded in the [revenue].Payment table against an invoice
- **VAT_Submission_Period**: A calculated time range in the [vat].VatSubmissionPeriod table representing a VAT reporting period
- **Outstanding_Balance**: The difference between an invoice's TotalAmount and the sum of non-voided payments recorded against it
- **Remainder_Instalment**: An automatically generated instalment created when a payment partially satisfies an existing instalment
- **Schedule_Payments_Permission**: A user-level permission (`schedule_payments`) that controls access to create, modify, and delete payment schedules

## Requirements

### Requirement 1: Create a Payment Schedule

**User Story:** As a business user with schedule_payments permission, I want to attach a payment schedule to an invoice, so that I can formally plan how the outstanding balance will be collected in instalments.

#### Acceptance Criteria

1. WHEN a user navigates to an invoice detail page, THE Portal SHALL display a "Create Payment Schedule" action if the user has the Schedule_Payments_Permission and no active Payment_Schedule exists for that invoice
2. WHEN the user initiates schedule creation, THE Portal SHALL display a form allowing entry of one or more instalments, each with an amount (required) and a due date (optional)
3. WHEN the user enters the first instalment amount, THE Portal SHALL auto-suggest the remaining Outstanding_Balance as the next instalment amount
4. THE Portal SHALL validate that the sum of all instalment amounts equals the invoice's current Outstanding_Balance before allowing schedule creation
5. WHEN the user submits a valid schedule, THE Portal SHALL persist the Payment_Schedule and its instalments to the database within a single transaction
6. IF the sum of instalment amounts does not equal the Outstanding_Balance, THEN THE Portal SHALL display a validation error indicating the discrepancy and the expected total

### Requirement 2: Instalment Status Tracking

**User Story:** As a business user, I want each instalment to reflect its current payment status, so that I can see at a glance which instalments are upcoming, due, overdue, or settled.

#### Acceptance Criteria

1. THE Portal SHALL assign each Instalment one of the following Instalment_Status values: Pending, Due, Overdue, Paid, PartiallyPaid
2. WHILE an instalment's due date is in the future, THE Portal SHALL display the Instalment_Status as Pending
3. WHEN an instalment's due date is today, THE Portal SHALL display the Instalment_Status as Due
4. WHEN an instalment's due date has passed and the instalment has not been fully paid, THE Portal SHALL display the Instalment_Status as Overdue
5. WHEN the total payment matched to an instalment equals or exceeds the instalment amount, THE Portal SHALL mark the Instalment_Status as Paid
6. WHEN a payment is matched to an instalment but the matched amount is less than the instalment amount, THE Portal SHALL mark the Instalment_Status as PartiallyPaid
7. WHILE an instalment has no due date assigned, THE Portal SHALL display the Instalment_Status as Pending regardless of the current date

### Requirement 3: Payment-to-Instalment Matching

**User Story:** As a business user, I want recorded payments to be automatically matched to the next due instalment, so that I do not have to manually link each payment to a specific instalment.

#### Acceptance Criteria

1. WHEN a payment is recorded against an invoice that has an active Payment_Schedule, THE Portal SHALL auto-match the payment to the earliest instalment with Instalment_Status of Due, Overdue, or Pending (in that priority order)
2. WHEN the payment amount equals the matched instalment amount, THE Portal SHALL mark the instalment as Paid
3. WHEN the payment amount exceeds the matched instalment amount, THE Portal SHALL mark the current instalment as Paid and apply the excess to the next eligible instalment in sequence
4. WHEN the payment amount is less than the matched instalment amount, THE Portal SHALL mark the instalment as PartiallyPaid and create a Remainder_Instalment for the difference
5. THE Portal SHALL set the Remainder_Instalment amount to the difference between the original instalment amount and the payment received
6. THE Portal SHALL assign no due date to the Remainder_Instalment but maintain a reference to the original instalment from which it was derived
7. WHEN a payment is recorded without an active Payment_Schedule, THE Portal SHALL record the payment as normal without any instalment matching logic

### Requirement 4: Partial Payment Handling

**User Story:** As a business user, I want clear visibility when a customer pays less than the scheduled instalment amount, so that I can track the shortfall and follow up accordingly.

#### Acceptance Criteria

1. WHEN a payment amount is less than the matched instalment's target amount, THE Portal SHALL display a warning to the user indicating the shortfall amount
2. THE Portal SHALL create a Remainder_Instalment linked to the original instalment with the shortfall amount
3. THE Portal SHALL display the Remainder_Instalment in the schedule view with a visual indicator showing it was derived from a partial payment
4. THE Portal SHALL include the Remainder_Instalment in the total schedule calculation so that the sum of all remaining instalments still equals the Outstanding_Balance

### Requirement 5: Schedule Modification with History

**User Story:** As a business user, I want to modify an existing payment schedule (add, remove, or change instalments), so that I can adapt to changing customer agreements while maintaining a full audit trail.

#### Acceptance Criteria

1. WHEN a user with Schedule_Payments_Permission modifies a Payment_Schedule, THE Portal SHALL record a Schedule_History_Entry capturing the field changed, old value, new value, user identity, and timestamp
2. THE Portal SHALL allow modification of instalment amounts for instalments that have not yet been marked as Paid
3. THE Portal SHALL allow modification of instalment due dates for instalments that have not yet been marked as Paid
4. THE Portal SHALL allow addition of new instalments to an existing schedule
5. THE Portal SHALL allow removal of instalments that have not yet been matched to any payment
6. WHEN instalments are modified, THE Portal SHALL revalidate that the sum of all instalment amounts equals the current Outstanding_Balance
7. IF revalidation fails after modification, THEN THE Portal SHALL display a validation error and prevent saving the changes
8. THE Portal SHALL display a history log showing all modifications made to the schedule, ordered by most recent first

### Requirement 6: Invoice Financial Status Auto-Update

**User Story:** As a business user, I want the invoice financial status to automatically reflect the payment schedule progress, so that the invoice list and detail pages always show accurate financial state.

#### Acceptance Criteria

1. WHEN all instalments in a Payment_Schedule are marked as Paid, THE Portal SHALL update the invoice's InvoiceFinancialStatusTypeId to Paid (3)
2. WHEN at least one instalment is marked as Paid or PartiallyPaid but others remain unpaid, THE Portal SHALL update the invoice's InvoiceFinancialStatusTypeId to PartiallyPaid (2)
3. WHEN a Payment_Schedule exists but no instalments have been paid, THE Portal SHALL retain the invoice's current InvoiceFinancialStatusTypeId without modification
4. WHEN a payment is voided that was previously matched to an instalment, THE Portal SHALL revert the instalment's status and recalculate the invoice's InvoiceFinancialStatusTypeId accordingly

### Requirement 7: VAT Period Warning

**User Story:** As a business user, I want to be warned when my instalment schedule timing conflicts with my VAT submission obligations, so that I can plan cash flow to cover VAT payments due before I receive full payment from the customer.

#### Acceptance Criteria

1. WHEN a Payment_Schedule is being created, THE Portal SHALL determine the invoice's VAT_Submission_Period and its submission deadline
2. WHEN the first instalment's due date is after the VAT_Submission_Period submission deadline, THE Portal SHALL display a warning message stating: "The VAT for this invoice (€{TaxAmount}) will need to be paid to the tax authority regardless of when you receive payment. Consider setting your first instalment to at least cover the VAT amount."
3. THE Portal SHALL display the warning as an informational notice that does not block schedule creation
4. WHEN the first instalment amount is less than the invoice's TaxAmount and the first instalment due date is after the submission deadline, THE Portal SHALL highlight the VAT amount in the warning to draw attention to the cash flow risk
5. IF the invoice is not assigned to any VAT_Submission_Period, THEN THE Portal SHALL not display the VAT warning

### Requirement 8: Payment Schedule Visibility

**User Story:** As a business user, I want to see the payment schedule and its progress directly on the invoice detail page, so that I have complete visibility without navigating elsewhere.

#### Acceptance Criteria

1. WHEN an invoice has an active Payment_Schedule, THE Portal SHALL display the schedule as a dedicated section on both the Revenue/InvoiceDetail and Invoice/Detail pages
2. THE Portal SHALL display each instalment showing: sequence number, amount, due date (or "No date" if unset), and current Instalment_Status with a colour-coded badge
3. THE Portal SHALL display a progress summary showing total paid, total remaining, and number of instalments completed vs total
4. WHEN no Payment_Schedule exists for an invoice, THE Portal SHALL display a prompt to create one (visible only to users with Schedule_Payments_Permission)

### Requirement 9: Permission-Gated Access

**User Story:** As a system administrator, I want payment schedule management to be restricted to authorised users only, so that not all users can create or modify instalment plans.

#### Acceptance Criteria

1. THE Portal SHALL enforce the Schedule_Payments_Permission before allowing creation of a Payment_Schedule
2. THE Portal SHALL enforce the Schedule_Payments_Permission before allowing modification of a Payment_Schedule
3. THE Portal SHALL enforce the Schedule_Payments_Permission before allowing deletion of instalments from a Payment_Schedule
4. WHEN a user without Schedule_Payments_Permission views an invoice with a Payment_Schedule, THE Portal SHALL display the schedule in read-only mode without edit or create actions
5. THE Portal SHALL make the Payment_Schedule section visible to all users who can view the invoice, regardless of Schedule_Payments_Permission

### Requirement 10: Database Schema

**User Story:** As a developer, I want the payment schedule data model to follow established Portal conventions, so that it integrates cleanly with the existing database architecture.

#### Acceptance Criteria

1. THE Portal_Database SHALL store Payment_Schedule and Instalment data in the [revenue] schema
2. THE Portal_Database SHALL enforce a unique constraint ensuring only one active Payment_Schedule exists per invoice at any time
3. THE Portal_Database SHALL include a PaymentScheduleInstalmentStatusType reference table seeded with: Pending (1), Due (2), Overdue (3), Paid (4), PartiallyPaid (5)
4. THE Portal_Database SHALL include a PaymentScheduleHistory table capturing: field changed, old value, new value, changed by user identity, and changed at timestamp
5. THE Portal_Database SHALL include a foreign key from PaymentScheduleInstalment to Payment allowing nullable linkage for payment matching
6. THE Portal_Database SHALL include CreatedAtUtc columns with GETUTCDATE() defaults on all new tables
7. THE Portal_Database SHALL include a foreign key from PaymentSchedule to [invoice].Invoice with cascade restrictions preventing orphaned schedules

### Requirement 11: Schedule Deletion

**User Story:** As a business user with schedule_payments permission, I want to be able to delete a payment schedule entirely, so that I can remove a plan that is no longer applicable.

#### Acceptance Criteria

1. WHEN a user with Schedule_Payments_Permission requests deletion of a Payment_Schedule, THE Portal SHALL display a SweetAlert2 confirmation dialog warning that this action cannot be undone
2. IF the Payment_Schedule has any instalments with Instalment_Status of Paid or PartiallyPaid, THEN THE Portal SHALL prevent deletion and display a warning that schedules with matched payments cannot be deleted
3. WHEN deletion is confirmed and permitted, THE Portal SHALL remove the Payment_Schedule and all its instalments from the database
4. WHEN a Payment_Schedule is deleted, THE Portal SHALL record a Schedule_History_Entry capturing the deletion event with the user identity and timestamp
