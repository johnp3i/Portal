# Requirements Document

## Introduction

This feature redesigns how purchases are assigned to VAT submission periods. Currently, purchases are auto-assigned to a period based on invoice date. This is problematic because:

1. A purchase dated May 29th might intentionally belong to the June–August period (invoice received late, processed next cycle)
2. Users have no visibility or control over which period a purchase is assigned to
3. Purchases with `VatSubmissionPeriodId = NULL` are invisible during VAT submission preparation
4. There is no prompt to review or confirm assignments before filing

The new workflow puts the user in full control: purchases start unassigned and are explicitly claimed for a period — either at the point of recording (optional dropdown) or during VAT submission preparation (bulk assignment panel).

## Glossary

- **Unassigned_Purchase**: A purchase where `VatSubmissionPeriodId IS NULL` — not yet claimed by any VAT period.
- **Assigned_Purchase**: A purchase where `VatSubmissionPeriodId` is set — claimed for a specific period.
- **Locked_Purchase**: A purchase assigned to a period that has been marked as Submitted — cannot be reassigned.
- **Assignment_Panel**: A section on the VAT Detail page showing unassigned purchases eligible for the current period, with bulk-assign controls.
- **Period_Dropdown**: An optional VAT Period selector on the Purchase Create, Edit, and Bulk Entry forms.
- **Submission_Advisory**: A warning shown before marking a period as submitted when unassigned purchases exist within the period's date range.
- **Date_Range_Purchases**: Purchases whose `InvoiceDate` falls within a period's start and end dates, regardless of their current assignment status.

## Requirements

### Requirement 1: Remove Auto-Assignment

**User Story:** As a business user, I want new purchases to start without a VAT period assignment, so that I retain full control over which period each purchase belongs to.

#### Acceptance Criteria

1. WHEN a new purchase is created (via Create form, Bulk Entry, or CSV Import), THE system SHALL NOT automatically assign a `VatSubmissionPeriodId`.
2. THE `VatSubmissionPeriodId` SHALL remain NULL on new purchases unless the user explicitly selects a period during creation.
3. THE removal of auto-assignment SHALL apply to all purchase creation paths: single create, bulk create, and CSV import.
4. Existing purchases with previously auto-assigned `VatSubmissionPeriodId` values SHALL NOT be affected — their assignments remain intact.

### Requirement 2: VAT Period Dropdown on Purchase Create Form

**User Story:** As a business user, I want to optionally assign a purchase to a VAT period at the time of recording, so that I can categorise it immediately when I know which period it belongs to.

#### Acceptance Criteria

1. THE Purchase Create form SHALL include an optional "VAT Period" dropdown field.
2. THE dropdown SHALL display all VAT periods for the business that are NOT yet submitted, ordered by most recent first.
3. THE dropdown SHALL include a "— Not assigned —" default option (empty value).
4. WHEN the user selects a period, THE purchase SHALL be saved with `VatSubmissionPeriodId` set to the selected period's Id.
5. WHEN the user leaves the dropdown on "— Not assigned —", THE purchase SHALL be saved with `VatSubmissionPeriodId = NULL`.
6. THE dropdown SHALL NOT show periods that have already been marked as Submitted.

### Requirement 3: VAT Period Dropdown on Purchase Edit Form

**User Story:** As a business user, I want to change or assign the VAT period on an existing purchase, so that I can correct assignments or assign previously unassigned purchases.

#### Acceptance Criteria

1. THE Purchase Edit form SHALL include a "VAT Period" dropdown field.
2. THE dropdown SHALL display the currently assigned period (if any) as selected.
3. THE dropdown SHALL include all unsubmitted periods plus the currently assigned period (even if submitted, for display purposes).
4. WHEN the purchase is assigned to a Submitted period, THE dropdown SHALL be disabled with a note: "Locked — assigned to a submitted period."
5. WHEN the purchase is not locked, THE user SHALL be able to change the assignment to any unsubmitted period or clear it to "— Not assigned —".
6. THE dropdown SHALL display periods ordered by most recent first.

### Requirement 4: VAT Period Column in Bulk Entry

**User Story:** As a business user, I want to assign a VAT period when entering purchases in bulk, so that I can batch-assign all purchases to the same period during data entry.

#### Acceptance Criteria

1. THE Bulk Entry grid SHALL include a "VAT Period" column with a dropdown selector per row.
2. THE dropdown SHALL show unsubmitted periods, ordered by most recent first, plus "— Not assigned —" as default.
3. WHEN the user selects a period on a row, THAT purchase SHALL be saved with the selected `VatSubmissionPeriodId`.
4. THE Bulk Entry form SHALL include a "Set all to..." batch control that sets the VAT Period for all rows simultaneously.
5. THE batch control SHALL NOT override rows that the user has already individually set (or optionally: override all with confirmation).

### Requirement 5: Unassigned Purchases Panel on VAT Detail Page

**User Story:** As a business user, I want to see which purchases are unassigned when reviewing a VAT period, so that I can assign them before submission.

#### Acceptance Criteria

1. THE VAT Detail page SHALL include an "Unassigned Purchases" section, displayed between the Purchases breakdown and the Filing Status section.
2. THE panel SHALL show all purchases where `VatSubmissionPeriodId IS NULL` AND `IsCancelled = 0` AND `InvoiceDate` falls within the period's date range (PeriodStartDate to PeriodEndDate).
3. THE panel SHALL display a count of unassigned purchases in the section heading.
4. THE panel SHALL display each unassigned purchase with: Description/Reference, Supplier, Invoice Date, Category, Total Amount, VAT Amount.
5. THE panel SHALL include a "Select All" checkbox and individual checkboxes per purchase row.
6. THE panel SHALL include an "Assign to this period" button that sets `VatSubmissionPeriodId` on all selected purchases.
7. AFTER assignment, THE panel SHALL refresh to show remaining unassigned purchases (if any), and the main Purchases breakdown SHALL update.
8. THE panel SHALL include a "Dismiss" option per purchase (or selected batch) that hides it from the panel without assigning it — the purchase remains unassigned for a future period.
9. WHEN no unassigned purchases exist for the period's date range, THE panel SHALL display a success message: "All purchases in this date range have been assigned."
10. THE panel SHALL only be visible when the period is NOT yet submitted.

### Requirement 6: Submission Advisory Warning

**User Story:** As a business user, I want to be warned before submitting a VAT period if there are unassigned purchases within the date range, so that I don't accidentally omit expenses.

#### Acceptance Criteria

1. WHEN the user clicks "Mark as Submitted", THE system SHALL check for unassigned purchases (VatSubmissionPeriodId IS NULL, IsCancelled = 0) with InvoiceDate within the period's date range.
2. IF unassigned purchases exist, THE system SHALL show a SweetAlert2 warning dialog with: count of unassigned purchases, advisory text, and options "Review First" (navigate to panel) and "Submit Anyway" (proceed).
3. IF the user clicks "Submit Anyway", THE submission SHALL proceed normally.
4. IF the user clicks "Review First", THE page SHALL scroll to the Unassigned Purchases panel.
5. IF no unassigned purchases exist, THE submission SHALL proceed directly with a standard confirmation dialog.

### Requirement 7: Assignment Locking on Submitted Periods

**User Story:** As a business user, I want purchases locked to their assigned period after submission, so that my filed VAT returns remain accurate.

#### Acceptance Criteria

1. WHEN a VAT period is marked as Submitted, ALL purchases with `VatSubmissionPeriodId` pointing to that period SHALL be considered locked.
2. THE Purchase Edit form SHALL disable the VAT Period dropdown for locked purchases with the message: "Locked — assigned to a submitted period."
3. THE Assign/Unassign bulk actions SHALL NOT be available for purchases that are already assigned to a submitted period.
4. THE API endpoint for bulk assignment SHALL reject attempts to reassign purchases that belong to a submitted period, returning an appropriate error message.

### Requirement 8: Unassign from Period

**User Story:** As a business user, I want to remove a purchase from a VAT period assignment (before submission), so that I can move it to a different period.

#### Acceptance Criteria

1. THE Purchase Edit form SHALL allow clearing the VAT Period selection (back to "— Not assigned —") for purchases assigned to unsubmitted periods.
2. THE VAT Detail page's assigned purchases table SHALL include an "Unassign" action per purchase (shown only for unsubmitted periods).
3. WHEN a purchase is unassigned, THE `VatSubmissionPeriodId` SHALL be set to NULL.
4. THE unassigned purchase SHALL then appear in the Unassigned Purchases panel for any period whose date range includes its InvoiceDate.

### Requirement 9: Bulk Assignment API Endpoint

**User Story:** As a front-end developer, I want a dedicated endpoint to bulk-assign purchases to a VAT period, so that the Unassigned Purchases panel can perform efficient batch operations.

#### Acceptance Criteria

1. THE system SHALL expose a POST endpoint accepting: periodId (int) and purchaseIds (List<int>).
2. THE endpoint SHALL validate that the period exists, belongs to the current business, and is NOT submitted.
3. THE endpoint SHALL validate that all purchases belong to the current business and are not cancelled.
4. THE endpoint SHALL validate that none of the purchases are already assigned to a submitted period.
5. THE endpoint SHALL update `VatSubmissionPeriodId` on all valid purchases in a single operation.
6. THE endpoint SHALL return success with a count of assigned purchases, or failure with specific error details.
7. THE endpoint SHALL follow the AxPost naming convention: `AxPostAssignPurchasesToPeriod`.

### Requirement 10: Bulk Unassign API Endpoint

**User Story:** As a front-end developer, I want a dedicated endpoint to unassign purchases from a VAT period, so that the Detail page can efficiently remove assignments.

#### Acceptance Criteria

1. THE system SHALL expose a POST endpoint accepting: purchaseIds (List<int>).
2. THE endpoint SHALL validate that all purchases belong to the current business.
3. THE endpoint SHALL validate that none of the purchases are assigned to a submitted period (locked).
4. THE endpoint SHALL set `VatSubmissionPeriodId = NULL` on all valid purchases.
5. THE endpoint SHALL return success with a count of unassigned purchases.
6. THE endpoint SHALL follow the AxPost naming convention: `AxPostUnassignPurchasesFromPeriod`.

### Requirement 11: Unassigned Count in VAT Periods List

**User Story:** As a business user, I want to see how many unassigned purchases exist for each period at a glance, so that I know which periods need attention.

#### Acceptance Criteria

1. THE VAT Periods list page SHALL display a count of unassigned purchases per period (purchases where VatSubmissionPeriodId IS NULL and InvoiceDate falls within the period's date range).
2. THE count SHALL be displayed as a badge or label next to the period's status.
3. WHEN the count is 0, THE badge SHALL NOT be displayed (clean state).
4. THE count SHALL only appear for unsubmitted periods.

### Requirement 12: Tenant Isolation

**User Story:** As a business user, I want all assignment operations scoped to my business, so that my data remains private and secure.

#### Acceptance Criteria

1. ALL assignment queries and updates SHALL filter by the authenticated user's BusinessId.
2. THE Unassigned Purchases panel SHALL only show purchases belonging to the current business.
3. THE bulk assignment endpoint SHALL reject purchaseIds that don't belong to the current business.
4. THE VAT Period dropdown SHALL only show periods belonging to the current business.

### Requirement 13: Assignment Audit Trail

**User Story:** As a business user, I want period assignment changes to be logged, so that I have a record of what was assigned and when.

#### Acceptance Criteria

1. WHEN purchases are bulk-assigned to a period, THE system SHALL write an audit log entry recording: action ("BulkAssignToVatPeriod"), count of purchases, period Id, and timestamp.
2. WHEN a purchase is unassigned from a period, THE system SHALL write an audit log entry recording: action ("UnassignFromVatPeriod"), purchase Id, previous period Id, and timestamp.
3. WHEN a single purchase's VAT period is changed via the Edit form, THE audit log entry for the Update action SHALL include the VatSubmissionPeriodId field change.
