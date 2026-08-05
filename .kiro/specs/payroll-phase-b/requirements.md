# Requirements Document

## Introduction

Phase B of the Payroll module delivers Audit, Unlock, and P&L Integration capabilities on top of the Phase A Core Engine. This phase enables business owners and SuperAdmins to unlock finalised payslip periods for editing, tracks every field-level change in an immutable audit log, and automatically synchronises payroll expense entries with the Profit & Loss system. The period status lifecycle is extended to include Unlocked and Re-finalised states. All new tables reside in the `[payroll]` schema. The module remains gated to Enterprise-tier subscribers with the `payroll` module key.

## Glossary

- **Payroll_System**: The payroll module within the Portal platform responsible for employee management, payslip calculation, payslip generation, audit tracking, and P&L integration
- **Calculation_Engine**: The component that computes earning totals, deduction amounts, net salary, and employer contributions for a payslip
- **PayslipPeriod**: A year/month container representing a payroll processing cycle for a business, with a status lifecycle (Draft → Preview → Finalised → Unlocked → Re-finalised)
- **PayslipStatusType**: A lookup table storing the valid period/payslip statuses: Draft (1), Preview (2), Finalised (3), Unlocked (4), Re-finalised (5)
- **PayslipAuditLog**: An append-only record tracking who changed what field on a payslip, when the change occurred, and the old and new values
- **AuditAction**: A lookup type classifying the nature of an audit event: Unlocked, Edited, Re-finalised
- **P&L_System**: The Profit & Loss module within the Portal platform that tracks business income and expenses
- **P&L_Entry**: An expense record in the P&L system representing a cost to the business, categorised by type
- **Salary_Cost_Category**: The P&L expense category representing the total earnings (gross salary) across all payslips in a finalised period
- **Employer_Contributions_Category**: The P&L expense category representing the total employer contributions across all payslips in a finalised period
- **Unlock_Action**: The process of transitioning a PayslipPeriod from Finalised (or Re-finalised) status back to Unlocked status, enabling editing
- **Re-finalisation**: The process of transitioning an Unlocked PayslipPeriod back to a finalised state after edits are complete, recalculating all totals
- **Owner**: A business owner user with permissions to manage all aspects of their business within the Portal
- **SuperAdmin**: A platform-level administrator with elevated permissions across all businesses
- **Field_Change**: A single modification to a payslip data field, recorded with old value, new value, and metadata

## Requirements

### Requirement 1: Period Status Lifecycle Extension

**User Story:** As a business owner, I want the payslip period to support Unlocked and Re-finalised states, so that finalised periods can be edited when corrections are needed.

#### Acceptance Criteria

1. THE Payroll_System SHALL extend the PayslipStatusType lookup table to include: Unlocked (4) and Re-finalised (5)
2. THE Payroll_System SHALL enforce the following status transitions: Draft → Preview → Finalised → Unlocked → Re-finalised → Unlocked (cycle permitted)
3. WHEN a PayslipPeriod is in Finalised or Re-finalised status, THE Payroll_System SHALL allow transition to Unlocked status only
4. WHEN a PayslipPeriod is in Unlocked status, THE Payroll_System SHALL allow transition to Re-finalised status only
5. THE Payroll_System SHALL prevent any status transition that does not conform to the defined lifecycle sequence
6. WHEN a PayslipPeriod transitions to Re-finalised, THE Payroll_System SHALL record ProcessedAtUtc with the current UTC timestamp

### Requirement 2: Payslip Period Unlock

**User Story:** As a business owner or SuperAdmin, I want to unlock a finalised payslip period, so that I can correct errors discovered after finalisation.

#### Acceptance Criteria

1. THE Payroll_System SHALL restrict the Unlock_Action to users with Owner or SuperAdmin roles only
2. WHEN a user without Owner or SuperAdmin role attempts to unlock a PayslipPeriod, THE Payroll_System SHALL deny the action and display an authorisation error
3. WHEN a PayslipPeriod is unlocked, THE Payroll_System SHALL transition the period status from Finalised (or Re-finalised) to Unlocked
4. WHEN a PayslipPeriod is unlocked, THE Payroll_System SHALL transition all Payslip records within that period to Unlocked status
5. WHEN a PayslipPeriod is unlocked, THE Payroll_System SHALL create a PayslipAuditLog entry with Action "Unlocked" for each payslip in the period, recording the UserId and Timestamp
6. THE Payroll_System SHALL display a warning dialog before executing the Unlock_Action with the message: "Editing will affect P&L for {Month} {Year}" where Month and Year correspond to the period being unlocked

### Requirement 3: Payslip Editing After Unlock

**User Story:** As a business owner, I want to edit individual payslips within an unlocked period, so that I can correct earning lines, deduction configurations, or manager notes.

#### Acceptance Criteria

1. WHILE a PayslipPeriod is in Unlocked status, THE Payroll_System SHALL allow modifications to PayslipEarningLine records (add, edit, remove) within that period
2. WHILE a PayslipPeriod is in Unlocked status, THE Payroll_System SHALL allow modifications to the ManagerNotes field on Payslips within that period
3. WHILE a PayslipPeriod is in Unlocked status, THE Payroll_System SHALL allow adding or removing Payslips from the period (e.g., adding a missed employee or removing an incorrectly included one)
4. WHEN a PayslipEarningLine is modified, THE Payroll_System SHALL recalculate the Payslip totals using the Calculation_Engine (TotalEarnings, TotalEmployeeDeductions, NetSalary, TotalEmployerContributions)
5. WHILE a PayslipPeriod is in Finalised or Re-finalised status, THE Payroll_System SHALL prevent any modifications to Payslips within that period

### Requirement 4: Field-Level Audit Trail

**User Story:** As a business owner, I want every change to a payslip recorded with full detail, so that I have a complete history of who changed what and when for compliance and accountability.

#### Acceptance Criteria

1. THE Payroll_System SHALL create a `PayslipAuditLog` table in the `[payroll]` schema with columns: Id (INT PK IDENTITY), PayslipId (INT FK), UserId (NVARCHAR(450) FK), PayslipAuditActionTypeId (TINYINT FK), FieldName (NVARCHAR(100)), OldValue (NVARCHAR(500) nullable), NewValue (NVARCHAR(500) nullable), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
2. THE Payroll_System SHALL maintain a `PayslipAuditActionType` lookup table with values: Unlocked (1), Edited (2), Re-finalised (3)
3. WHEN any field on a Payslip or its child records (PayslipEarningLine, PayslipDeductionLine) is modified, THE Payroll_System SHALL create a PayslipAuditLog entry with Action "Edited", the FieldName, OldValue, and NewValue
4. WHEN a PayslipEarningLine Amount is changed, THE Payroll_System SHALL record the FieldName as "EarningLine:{EarningTypeName}:Amount" with the old and new decimal values as strings
5. WHEN a PayslipEarningLine is added or removed, THE Payroll_System SHALL record the FieldName as "EarningLine:{EarningTypeName}" with OldValue NULL (for addition) or NewValue NULL (for removal)
6. WHEN the ManagerNotes field is changed, THE Payroll_System SHALL record the FieldName as "ManagerNotes" with the old and new text values
7. THE Payroll_System SHALL enforce immutability on PayslipAuditLog records — no UPDATE or DELETE operations are permitted on this table
8. EACH PayslipAuditLog entry SHALL record the UserId of the authenticated user who performed the change

### Requirement 5: Re-finalisation

**User Story:** As a business owner, I want to re-finalise an unlocked period after making corrections, so that the period is locked again and P&L entries are updated.

#### Acceptance Criteria

1. WHEN re-finalisation is initiated, THE Calculation_Engine SHALL recalculate all Payslip totals within the period (TotalEarnings, TotalEmployeeDeductions, NetSalary, TotalEmployerContributions)
2. WHEN a PayslipPeriod is re-finalised, THE Payroll_System SHALL transition the period status from Unlocked to Re-finalised
3. WHEN a PayslipPeriod is re-finalised, THE Payroll_System SHALL transition all Payslip records within that period to Re-finalised status
4. WHEN a PayslipPeriod is re-finalised, THE Payroll_System SHALL create a PayslipAuditLog entry with Action "Re-finalised" for each payslip in the period, recording the UserId and Timestamp
5. WHEN a PayslipPeriod is re-finalised, THE Payroll_System SHALL trigger the P&L adjustment process (reverse old entries and create corrected entries)
6. THE Payroll_System SHALL validate that all Payslips within the period pass calculation validation before allowing re-finalisation (e.g., no missing deduction rates)

### Requirement 6: P&L Integration on Finalisation

**User Story:** As a business owner, I want payroll expenses automatically recorded in my P&L when a period is finalised, so that my financial reports are always up to date without manual data entry.

#### Acceptance Criteria

1. WHEN a PayslipPeriod transitions to Finalised status, THE Payroll_System SHALL create a P&L_Entry in the Salary_Cost_Category with the sum of TotalEarnings across all Payslips in the period
2. WHEN a PayslipPeriod transitions to Finalised status, THE Payroll_System SHALL create a P&L_Entry in the Employer_Contributions_Category with the sum of TotalEmployerContributions across all Payslips in the period
3. EACH P&L_Entry SHALL reference the PayslipPeriodId to maintain traceability between payroll and P&L
4. THE P&L_Entry description SHALL include the period reference in the format "Payroll - {Month Name} {Year}" (e.g., "Payroll - July 2027")
5. THE Payroll_System SHALL create P&L entries within the same database transaction as the period status transition to ensure atomicity
6. IF P&L_Entry creation fails, THEN THE Payroll_System SHALL roll back the entire finalisation transaction and display an error to the user

### Requirement 7: P&L Adjustment on Re-finalisation

**User Story:** As a business owner, I want P&L entries corrected automatically when I edit and re-finalise a payslip period, so that my financial reports reflect the latest payroll figures.

#### Acceptance Criteria

1. WHEN a PayslipPeriod transitions to Re-finalised status, THE Payroll_System SHALL reverse (soft-delete or mark as superseded) the existing P&L entries associated with that PayslipPeriodId
2. WHEN a PayslipPeriod transitions to Re-finalised status, THE Payroll_System SHALL create new P&L entries with the recalculated totals (updated TotalEarnings sum and updated TotalEmployerContributions sum)
3. THE Payroll_System SHALL maintain a link between the original and replacement P&L entries for audit traceability
4. THE Payroll_System SHALL execute P&L reversal and new entry creation within the same database transaction as the re-finalisation
5. IF any P&L adjustment step fails, THEN THE Payroll_System SHALL roll back the entire re-finalisation transaction and display an error to the user
6. THE reversed P&L entries SHALL retain their original values for historical reporting purposes (soft-delete pattern, not hard-delete)

### Requirement 8: Unlock Warning Dialog

**User Story:** As a business owner, I want to be warned before unlocking a finalised period that it will affect P&L, so that I can make an informed decision before proceeding.

#### Acceptance Criteria

1. WHEN a user initiates the Unlock_Action, THE Payroll_System SHALL display a SweetAlert2 confirmation dialog before executing the unlock
2. THE warning dialog SHALL display the message: "Editing will affect P&L for {Month Name} {Year}" with the specific period identified
3. THE warning dialog SHALL include a "Proceed" confirmation button and a "Cancel" button
4. WHEN the user clicks "Cancel", THE Payroll_System SHALL abort the Unlock_Action and maintain the current Finalised status
5. WHEN the user clicks "Proceed", THE Payroll_System SHALL execute the Unlock_Action with full audit logging
6. THE warning dialog SHALL use the warning icon style to clearly communicate the significance of the action

### Requirement 9: Audit History View

**User Story:** As a business owner, I want to view a chronological timeline of all changes made to a payslip, so that I can review the history of corrections and understand who made each change.

#### Acceptance Criteria

1. THE Payroll_System SHALL provide an audit history view accessible from the individual payslip detail page
2. THE audit history view SHALL display all PayslipAuditLog entries for the selected payslip in reverse chronological order (newest first)
3. EACH audit history entry SHALL display: the user who made the change (full name), the action type (Unlocked/Edited/Re-finalised), the field name (human-readable), old value, new value, and the timestamp formatted in the business locale
4. WHEN the action type is "Unlocked" or "Re-finalised", THE audit history view SHALL display the entry without FieldName, OldValue, or NewValue (these are status-change events, not field edits)
5. THE Payroll_System SHALL provide a period-level audit summary view showing all audit events across all payslips within a PayslipPeriod, grouped by payslip (employee name)
6. THE audit history view SHALL be read-only — no modification or deletion of audit entries is permitted through the UI

### Requirement 10: Data Schema and Integrity (Phase B Additions)

**User Story:** As a developer, I want the Phase B audit and P&L tables stored in the payroll schema with proper referential integrity, so that the extended data model remains consistent with Phase A conventions.

#### Acceptance Criteria

1. THE Payroll_System SHALL store the PayslipAuditLog table in the `[payroll]` schema
2. THE Payroll_System SHALL enforce referential integrity: PayslipAuditLog.PayslipId → Payslip.Id, PayslipAuditLog.PayslipAuditActionTypeId → PayslipAuditActionType.Id
3. THE Payroll_System SHALL store the PayslipAuditActionType lookup table in the `[payroll]` schema with TINYINT primary key
4. THE Payroll_System SHALL use NVARCHAR(100) for FieldName and NVARCHAR(500) for OldValue and NewValue to accommodate descriptive field identifiers and serialised values
5. THE PayslipAuditLog table SHALL include a CreatedAtUtc column of type DATETIME NOT NULL with a default of GETUTCDATE()
6. THE Payroll_System SHALL add a PayslipPeriodId reference column to the P&L expense entry table to link payroll-generated entries back to their source period
7. THE Payroll_System SHALL prevent cascade deletes on PayslipAuditLog — deleting a Payslip SHALL NOT delete its audit history

### Requirement 11: Permission and Access Control

**User Story:** As a platform operator, I want unlock and re-finalisation restricted to authorised roles, so that only appropriate personnel can modify finalised payroll data.

#### Acceptance Criteria

1. THE Payroll_System SHALL restrict the Unlock_Action to users with Owner or SuperAdmin roles
2. THE Payroll_System SHALL restrict the Re-finalisation action to users with Owner or SuperAdmin roles
3. THE Payroll_System SHALL allow standard payroll users (non-Owner, non-SuperAdmin) to view audit history but not perform unlock or re-finalisation
4. THE Payroll_System SHALL hide the Unlock button from the UI for users without the required role
5. WHEN a direct API request for unlock or re-finalisation is made by an unauthorised user, THE Payroll_System SHALL return an authorisation denial response
6. THE Payroll_System SHALL continue to enforce the `payroll` module key (Enterprise plan) for all Phase B features
