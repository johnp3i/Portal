# Requirements Document

## Introduction

This feature adds explicit VAT period assignment to invoices, mirroring the existing mechanism on purchases. Currently, an invoice's Output VAT contribution to a VAT period is determined solely by whether its `InvoiceDate` falls within the period's date range. This prevents deferring invoices to a different period — a legitimate need when tax rules allow corrections under a threshold (e.g., combined VAT under €1,000 may be deferred to the next period).

The feature introduces a nullable `VatSubmissionPeriodId` column on the Invoice table, auto-assignment logic on invoice creation, a manual reassignment action with double-confirmation, and an updated Output VAT computation that respects explicit assignments while remaining backward compatible with existing date-range matching.

## Glossary

- **Invoice**: A financial document in `[invoice].[Invoice]` representing an obligation to pay, scoped to a Business tenant.
- **VatSubmissionPeriod**: A time range in `[vat].[VatSubmissionPeriod]` representing a single VAT reporting period for a Business.
- **VatSubmission**: A record in `[vat].[VatSubmission]` representing the computed VAT totals for a specific period, which may be marked as submitted (immutable).
- **Output_VAT**: The total VAT charged on issued invoices within a given period — the amount owed to the tax authority.
- **Input_VAT**: The total VAT paid on purchases within a given period — the amount reclaimable from the tax authority.
- **Period_Assignment**: The explicit link between an Invoice and a VatSubmissionPeriod via the `VatSubmissionPeriodId` foreign key.
- **Date_Range_Matching**: The fallback mechanism that associates an invoice with a period based on `InvoiceDate` falling within `PeriodStartDate` and `PeriodEndDate`.
- **Cascading_Assignment**: The logic that, when the natural period is already submitted, assigns the invoice to the next chronologically unsubmitted period.
- **Invoice_Service**: The service layer component responsible for invoice business logic including creation and period assignment.
- **VatSubmission_Service**: The service layer component responsible for VAT submission computation and management.
- **Reassignment_Dialog**: The SweetAlert2 confirmation dialog shown when a user manually changes an invoice's VAT period assignment.

## Requirements

### Requirement 1: Add VatSubmissionPeriodId Column to Invoice

**User Story:** As a system administrator, I want the Invoice table to have an explicit VAT period reference, so that invoices can be assigned to specific periods independently of their invoice date.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a nullable `VatSubmissionPeriodId` column of type INT on the `[invoice].[Invoice]` table with a foreign key reference to `[vat].[VatSubmissionPeriod].[Id]`.
2. IF `VatSubmissionPeriodId` is NULL on an Invoice, THEN THE VatSubmission_Service SHALL include that invoice in the VatSubmissionPeriod whose `PeriodStartDate` and `PeriodEndDate` range contains the invoice's `InvoiceDate` (Date_Range_Matching).
3. IF `VatSubmissionPeriodId` on an Invoice references an existing record in `[vat].[VatSubmissionPeriod]`, THEN THE VatSubmission_Service SHALL assign the invoice to that referenced period and SHALL NOT apply Date_Range_Matching for that invoice.
4. THE Portal_Database SHALL include a non-clustered index on `[invoice].[Invoice].[VatSubmissionPeriodId]` filtered to rows where `VatSubmissionPeriodId IS NOT NULL` for query optimisation.

### Requirement 2: Auto-Assign VAT Period on Invoice Creation

**User Story:** As a business user, I want new invoices to be automatically assigned to the correct VAT period, so that I do not need to manually assign every invoice.

#### Acceptance Criteria

1. WHEN a new Invoice is created, THE Invoice_Service SHALL identify the VatSubmissionPeriod belonging to the same BusinessId whose `PeriodStartDate` <= `InvoiceDate` AND `PeriodEndDate` >= `InvoiceDate`.
2. WHEN the matching VatSubmissionPeriod has no VatSubmission record, or has a VatSubmission with `IsSubmitted` equal to false, THE Invoice_Service SHALL assign that period's identifier to the Invoice's `VatSubmissionPeriodId`.
3. WHEN the matching VatSubmissionPeriod has a VatSubmission with `IsSubmitted` equal to true, THE Invoice_Service SHALL apply Cascading_Assignment by searching forward through periods of the same BusinessId ordered by `PeriodStartDate` ascending, and assign the identifier of the first period that has no VatSubmission record or has a VatSubmission with `IsSubmitted` equal to false.
4. WHEN no VatSubmissionPeriod of the same BusinessId has a date range containing the Invoice's `InvoiceDate`, THE Invoice_Service SHALL leave `VatSubmissionPeriodId` as NULL.
5. WHEN Cascading_Assignment finds no unsubmitted period after the natural period within the same BusinessId, THE Invoice_Service SHALL leave `VatSubmissionPeriodId` as NULL.
6. THE Invoice_Service SHALL execute the period-matching and assignment within the same transaction as the Invoice creation, so that the Invoice is never persisted without the assignment attempt having completed.

### Requirement 3: Manual VAT Period Reassignment Action

**User Story:** As a business user, I want to manually reassign an invoice to a different VAT period, so that I can defer invoices for correction scenarios permitted by tax rules.

#### Acceptance Criteria

1. THE Invoice_Service SHALL expose an endpoint that accepts an Invoice identifier (INT) and a target VatSubmissionPeriod identifier (INT) for reassignment.
2. IF the provided Invoice identifier does not match an existing Invoice, THEN THE Invoice_Service SHALL reject the reassignment and return an error message indicating the Invoice was not found.
3. IF the provided target VatSubmissionPeriod identifier does not match an existing VatSubmissionPeriod, THEN THE Invoice_Service SHALL reject the reassignment and return an error message indicating the target period was not found.
4. WHEN a reassignment request is received, THE Invoice_Service SHALL validate that the target VatSubmissionPeriod's `BusinessId` matches the Invoice's `BusinessId`.
5. IF the target VatSubmissionPeriod's `BusinessId` does not match the Invoice's `BusinessId`, THEN THE Invoice_Service SHALL reject the reassignment and return an error message indicating a business mismatch.
6. IF the target VatSubmissionPeriod has a VatSubmission record where `IsSubmitted` equals true, THEN THE Invoice_Service SHALL reject the reassignment and return an error message indicating the target period is already submitted.
7. IF the Invoice's `IsDeleted` flag is true, THEN THE Invoice_Service SHALL reject the reassignment and return an error message indicating the invoice is deleted.
8. IF the Invoice's current `VatSubmissionPeriodId` already equals the target VatSubmissionPeriod identifier, THEN THE Invoice_Service SHALL reject the reassignment and return an error message indicating the invoice is already assigned to that period.
9. WHEN all validations pass, THE Invoice_Service SHALL update the Invoice's `VatSubmissionPeriodId` to the target period identifier and set `UpdatedAtUtc` to the current UTC timestamp.

### Requirement 4: Reassignment Confirmation Dialog with Financial Impact

**User Story:** As a business user, I want to see the financial consequences before confirming a period reassignment, so that I understand the impact on both the source and target VAT periods.

#### Acceptance Criteria

1. WHEN the user initiates a reassignment action, THE Portal SHALL display a Reassignment_Dialog using SweetAlert2 before executing the operation.
2. THE Reassignment_Dialog SHALL display the invoice's `InvoiceNumber`, the source period's `PeriodLabel`, and the target period's `PeriodLabel`.
3. THE Reassignment_Dialog SHALL display the invoice's `TaxAmount` formatted in the business currency that will be moved between periods.
4. THE Reassignment_Dialog SHALL display the projected Output_VAT totals for both the source period (current total minus the invoice's `TaxAmount`) and the target period (current total plus the invoice's `TaxAmount`), each formatted in the business currency.
5. THE Reassignment_Dialog SHALL use destructive styling with a red confirm button (`confirmButtonColor: '#C24A4A'`) since the action affects tax reporting.
6. WHEN the user confirms the Reassignment_Dialog, THE Portal SHALL execute the reassignment using the standard AJAX pattern (BlockUI.show → fetch → BlockUI.hide → Swal.fire result) and upon success SHALL display a success notification and refresh the invoice list to reflect the updated period assignment.
7. IF the reassignment request fails after the user confirms the Reassignment_Dialog, THEN THE Portal SHALL display an error notification indicating the reason for failure and leave the Invoice unchanged.
8. WHEN the user cancels the Reassignment_Dialog, THE Portal SHALL close the dialog, take no action, and leave the Invoice unchanged.

### Requirement 5: Update Output VAT Computation in CreateOrRecalculateAsync

**User Story:** As a business user, I want the VAT submission computation to respect explicit period assignments while remaining backward compatible, so that existing invoices without assignments continue to work correctly.

#### Acceptance Criteria

1. WHEN computing TotalOutputVat for a period, THE VatSubmission_Service SHALL sum the `TaxAmount` of all invoices belonging to the same Business where `VatSubmissionPeriodId` equals the target period identifier, `InvoiceStatusTypeId` equals 2, and `IsDeleted` is false.
2. WHEN computing TotalOutputVat for a period, THE VatSubmission_Service SHALL also sum the `TaxAmount` of all invoices belonging to the same Business where `VatSubmissionPeriodId` is NULL, `InvoiceDate` is greater than or equal to the period's `PeriodStartDate` and less than or equal to the period's `PeriodEndDate`, `InvoiceStatusTypeId` equals 2, and `IsDeleted` is false.
3. WHEN computing TotalOutputVat for a period, THE VatSubmission_Service SHALL apply criteria 1 and 2 as mutually exclusive sets — an invoice with a non-NULL `VatSubmissionPeriodId` SHALL only be evaluated under criterion 1, and an invoice with a NULL `VatSubmissionPeriodId` SHALL only be evaluated under criterion 2, ensuring no invoice is counted twice.
4. WHEN an invoice has a `VatSubmissionPeriodId` pointing to a different period, THE VatSubmission_Service SHALL exclude that invoice from the current period's TotalOutputVat even if its `InvoiceDate` falls within the current period's date range.
5. IF the period already has a VatSubmission with `IsSubmitted` equal to true, THEN THE VatSubmission_Service SHALL return the existing submission values without recomputing TotalOutputVat.

### Requirement 6: Database Migration Script

**User Story:** As a system administrator, I want a migration script that adds the new column and optionally backfills existing data, so that the schema change is deployed safely.

#### Acceptance Criteria

1. THE migration script SHALL add the `VatSubmissionPeriodId` column as a nullable INT to `[invoice].[Invoice]` only if the column does not already exist (idempotent).
2. THE migration script SHALL add a foreign key constraint named `FK_Invoice_VatSubmissionPeriod` from `[invoice].[Invoice].[VatSubmissionPeriodId]` to `[vat].[VatSubmissionPeriod].[Id]` only if the constraint does not already exist.
3. THE migration script SHALL create a non-clustered index named `IX_Invoice_VatSubmissionPeriodId` on `[invoice].[Invoice].[VatSubmissionPeriodId]` filtered to rows where `VatSubmissionPeriodId IS NOT NULL`, only if the index does not already exist.
4. THE migration script SHALL backfill existing invoices by setting `VatSubmissionPeriodId` to the `[vat].[VatSubmissionPeriod].[Id]` where the invoice's `InvoiceDate` falls between `PeriodStartDate` and `PeriodEndDate` (inclusive) and the invoice's `BusinessId` matches the period's `BusinessId`, only for invoices where `VatSubmissionPeriodId` is currently NULL and `IsDeleted` is false.
5. IF no matching VatSubmissionPeriod exists for an invoice during backfill, THEN THE migration script SHALL leave that invoice's `VatSubmissionPeriodId` as NULL.
6. IF multiple VatSubmissionPeriods match an invoice during backfill, THEN THE migration script SHALL assign the period with the earliest `PeriodStartDate`.
7. THE migration script SHALL be idempotent — executing the script multiple times SHALL produce the same database state as executing it once, SHALL not raise errors, and SHALL not modify rows where `VatSubmissionPeriodId` is already set.
