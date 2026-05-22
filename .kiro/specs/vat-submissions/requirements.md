# Requirements Document

## Introduction

VAT Submissions (Module 6) enables business managers to calculate VAT periods derived from their business's VAT registration configuration, generate VAT submissions per period by computing output VAT (from issued invoices) and input VAT (from purchases, excluding EU Reverse Charge), and track submission status. The module auto-generates contiguous, non-overlapping periods from the VatRegistrationDate forward using VatPeriodLengthInMonths, and provides a UI for reviewing period breakdowns and marking submissions as filed with the tax authority.

The database tables (`[vat].[VatSubmissionPeriod]` and `[vat].[VatSubmission]`) already exist from migrations 017 and 018. The BusinessProfile entity already stores VatRegistrationDate (date) and VatPeriodLengthInMonths (int, constrained to 1, 2, 3, 4, 6, or 12). This module implements the application layer (repositories, services, controllers, and UI) following the established patterns from Module 0 (Platform Foundation) and Module 5 (Purchase & Expense Tracking).

## Glossary

- **VatSubmissionPeriod**: A calculated time range representing a single VAT reporting period for a Business tenant, stored in `[vat].[VatSubmissionPeriod]`. Each period has a PeriodStartDate, PeriodEndDate, and PeriodLabel.
- **VatSubmission**: A VAT return record for a specific period containing computed TotalOutputVat, TotalInputVat, and NetVatPayable, stored in `[vat].[VatSubmission]`.
- **VatSubmissionPeriodRepository**: A table repository extending GenericStoredProcedureRepository for VatSubmissionPeriod CRUD operations against `[vat].[VatSubmissionPeriod]`.
- **VatSubmissionRepository**: A table repository extending GenericStoredProcedureRepository for VatSubmission CRUD operations against `[vat].[VatSubmission]`.
- **VatPeriodGenerationService**: A scoped service implementing IVatPeriodGenerationService that derives VAT periods from a BusinessProfile's VatRegistrationDate and VatPeriodLengthInMonths.
- **VatSubmissionService**: A scoped service implementing IVatSubmissionService that creates submissions, computes VAT totals from invoices and purchases within a period, and manages submission status.
- **VatController**: An MVC controller handling HTTP requests for VAT period listing, submission detail, and marking submissions as submitted.
- **TotalOutputVat**: The sum of TaxAmount from all issued invoices (InvoiceStatusTypeId = 2, IsDeleted = false) with InvoiceDate falling within the period date range for the current tenant.
- **TotalInputVat**: The sum of VatAmount from all purchases with InvoiceDate falling within the period date range for the current tenant, excluding EU Reverse Charge purchases (PurchaseOriginTypeId = 2).
- **NetVatPayable**: The computed difference: TotalOutputVat minus TotalInputVat. A positive value indicates tax owed to the authority; a negative value indicates a refund due.
- **BusinessProfile**: The entity in `[portal].[BusinessProfile]` containing VatRegistrationDate and VatPeriodLengthInMonths fields used to derive period boundaries.
- **Tenant_Isolation**: The enforcement that users can only access VAT periods and submissions belonging to their own Business, implemented via EF Core global query filters on BusinessId.
- **AuditLog**: A record in `[audit].[AuditLog]` capturing significant data changes for traceability.

## Requirements

### Requirement 1: VAT Submission Period Repository

**User Story:** As a developer, I want a VatSubmissionPeriodRepository with CRUD operations, so that the service layer can persist and retrieve VAT period data following established repository patterns.

#### Acceptance Criteria

1. THE VatSubmissionPeriodRepository SHALL extend GenericStoredProcedureRepository with VatSubmissionPeriod as the type parameter.
2. THE VatSubmissionPeriodRepository SHALL provide a method to retrieve all VAT submission periods for a given BusinessId ordered by PeriodStartDate descending.
3. THE VatSubmissionPeriodRepository SHALL provide a method to retrieve a single VAT submission period by Id and BusinessId.
4. WHEN a new VAT submission period is created, THE VatSubmissionPeriodRepository SHALL insert a record into `[vat].[VatSubmissionPeriod]` with BusinessId, PeriodStartDate, PeriodEndDate, PeriodLabel, and CreatedAtUtc.
5. THE VatSubmissionPeriodRepository SHALL provide a method to retrieve the latest (most recent PeriodEndDate) VAT submission period for a given BusinessId.
6. THE VatSubmissionPeriodRepository SHALL use full table names in SQL queries without aliases.
7. THE VatSubmissionPeriodRepository SHALL use null-safe SQL parameters using `?? (object)DBNull.Value` for all nullable fields.
8. THE VatSubmissionPeriodRepository SHALL wrap all data access in try/catch with rethrow.

### Requirement 2: VAT Submission Repository

**User Story:** As a developer, I want a VatSubmissionRepository with CRUD operations, so that the service layer can persist and retrieve VAT submission data following established repository patterns.

#### Acceptance Criteria

1. THE VatSubmissionRepository SHALL extend GenericStoredProcedureRepository with VatSubmission as the type parameter.
2. THE VatSubmissionRepository SHALL provide a method to retrieve all VAT submissions for a given BusinessId.
3. THE VatSubmissionRepository SHALL provide a method to retrieve a single VAT submission by Id and BusinessId.
4. THE VatSubmissionRepository SHALL provide a method to retrieve a VAT submission by VatSubmissionPeriodId and BusinessId.
5. WHEN a new VAT submission is created, THE VatSubmissionRepository SHALL insert a record into `[vat].[VatSubmission]` with BusinessId, VatSubmissionPeriodId, TotalOutputVat, TotalInputVat, NetVatPayable, IsSubmitted, SubmittedAtUtc, Notes, and CreatedAtUtc.
6. WHEN a VAT submission is marked as submitted, THE VatSubmissionRepository SHALL update IsSubmitted to true and set SubmittedAtUtc to the current UTC time on the matching record.
7. THE VatSubmissionRepository SHALL use full table names in SQL queries without aliases.
8. THE VatSubmissionRepository SHALL use null-safe SQL parameters using `?? (object)DBNull.Value` for all nullable fields.
9. THE VatSubmissionRepository SHALL wrap all data access in try/catch with rethrow.

### Requirement 3: VAT Period Generation Service

**User Story:** As a business manager, I want VAT periods to be automatically generated from my VAT registration date and period length, so that I do not need to manually create period boundaries.

#### Acceptance Criteria

1. THE VatPeriodGenerationService SHALL implement the IVatPeriodGenerationService interface.
2. THE VatPeriodGenerationService SHALL be registered as a scoped service in the DI container.
3. WHEN generating periods, THE VatPeriodGenerationService SHALL use the BusinessProfile's VatRegistrationDate as the start date of the first period.
4. WHEN generating periods, THE VatPeriodGenerationService SHALL compute each period's end date by adding VatPeriodLengthInMonths to the period's start date and subtracting one day.
5. WHEN generating periods, THE VatPeriodGenerationService SHALL produce contiguous periods where the start date of period N+1 equals the end date of period N plus one day.
6. WHEN generating periods, THE VatPeriodGenerationService SHALL generate all periods from VatRegistrationDate up to and including the period that contains the current date.
7. THE VatPeriodGenerationService SHALL NOT generate periods that start after the current date.
8. WHEN generating periods, THE VatPeriodGenerationService SHALL assign a PeriodLabel in the format "DD MMM YYYY – DD MMM YYYY" (e.g., "01 Jan 2025 – 31 Mar 2025").
9. WHEN periods already exist in the database for the tenant, THE VatPeriodGenerationService SHALL only generate and persist new periods that do not yet exist (identified by BusinessId and PeriodStartDate unique constraint).
10. IF the BusinessProfile has no VatRegistrationDate set (default value), THEN THE VatPeriodGenerationService SHALL return an empty collection and not generate any periods.
11. THE VatPeriodGenerationService SHALL validate that VatPeriodLengthInMonths is one of the allowed values (1, 2, 3, 4, 6, 12) before generating periods.

### Requirement 4: VAT Submission Service

**User Story:** As a business manager, I want to create VAT submissions that automatically compute output VAT, input VAT, and net VAT payable for a given period, so that I can review my VAT liability before filing.

#### Acceptance Criteria

1. THE VatSubmissionService SHALL implement the IVatSubmissionService interface.
2. THE VatSubmissionService SHALL be registered as a scoped service in the DI container.
3. WHEN creating a submission for a period, THE VatSubmissionService SHALL compute TotalOutputVat as the sum of TaxAmount from all invoices where InvoiceStatusTypeId equals 2 (Issued), IsDeleted equals false, and InvoiceDate falls within the period's PeriodStartDate and PeriodEndDate (inclusive) for the current tenant.
4. WHEN creating a submission for a period, THE VatSubmissionService SHALL compute TotalInputVat as the sum of VatAmount from all purchases where InvoiceDate falls within the period's PeriodStartDate and PeriodEndDate (inclusive) for the current tenant, excluding purchases where PurchaseOriginTypeId equals 2 (EuReverseCharge).
5. WHEN creating a submission for a period, THE VatSubmissionService SHALL compute NetVatPayable as TotalOutputVat minus TotalInputVat.
6. WHEN a submission already exists for the specified period and tenant, THE VatSubmissionService SHALL recalculate and update the existing submission's TotalOutputVat, TotalInputVat, and NetVatPayable values.
7. THE VatSubmissionService SHALL set BusinessId from ICurrentTenantService when creating a submission.
8. IF the specified VatSubmissionPeriodId does not belong to the current tenant, THEN THE VatSubmissionService SHALL return a ServiceResult with success false and a descriptive error message.
9. THE VatSubmissionService SHALL return a ServiceResult with the created or updated VatSubmission on success.

### Requirement 5: Mark Submission as Submitted

**User Story:** As a business manager, I want to mark a VAT submission as filed with the tax authority, so that I can track which periods have been submitted and which are outstanding.

#### Acceptance Criteria

1. WHEN a submission is marked as submitted, THE VatSubmissionService SHALL set IsSubmitted to true and SubmittedAtUtc to the current UTC time.
2. IF the submission is already marked as submitted, THEN THE VatSubmissionService SHALL return a ServiceResult with success false and the message "This submission has already been marked as submitted."
3. IF the submission does not exist or does not belong to the current tenant, THEN THE VatSubmissionService SHALL return a ServiceResult with success false and a descriptive error message.
4. WHEN a submission is successfully marked as submitted, THE VatSubmissionService SHALL return a ServiceResult with success true.

### Requirement 6: VAT Controller

**User Story:** As a business manager, I want API endpoints to view VAT periods, view submission details, and mark submissions as filed, so that I can manage my VAT obligations through the web interface.

#### Acceptance Criteria

1. THE VatController SHALL require authentication via the Authorize attribute.
2. THE VatController SHALL require module access via the ModuleAccess attribute with PortalModules.Vat.
3. THE VatController SHALL delegate all business logic to IVatPeriodGenerationService and IVatSubmissionService.
4. WHEN a user navigates to the VAT periods list, THE VatController SHALL trigger period generation (to ensure periods are up to date) and return a view displaying all periods for the current tenant with their submission status.
5. WHEN a user requests a submission detail for a specific period, THE VatController SHALL create or recalculate the submission for that period and return a view displaying TotalOutputVat, TotalInputVat, and NetVatPayable.
6. WHEN a user requests to mark a submission as submitted, THE VatController SHALL mark the submission and return a JSON success response.
7. IF service validation fails, THEN THE VatController SHALL return a JSON error response with the validation message.
8. THE VatController SHALL use ValidateAntiForgeryToken on all POST actions.

### Requirement 7: VAT Periods List UI

**User Story:** As a business manager, I want a VAT periods list screen showing all my VAT periods with their submission status, so that I can see which periods need attention.

#### Acceptance Criteria

1. THE VAT periods list view SHALL display periods in a table layout following the MyChair Design System (Primary Blue #0D5EA6, Manrope headings, Inter body).
2. THE VAT periods list view SHALL display PeriodLabel, PeriodStartDate, PeriodEndDate, and submission status for each period.
3. THE VAT periods list view SHALL visually indicate submission status with distinct badges: "Submitted" (green, Success #129867) for submitted periods, "Pending" (orange, Warning #C8912E) for periods with an unsubmitted submission, and "Not Started" (grey) for periods without a submission.
4. THE VAT periods list view SHALL display periods ordered by PeriodStartDate descending (most recent first).
5. THE VAT periods list view SHALL provide an action link to view submission detail for each period.
6. WHEN a period has a submitted submission, THE VAT periods list view SHALL display the SubmittedAtUtc date alongside the status badge.

### Requirement 8: VAT Submission Detail UI

**User Story:** As a business manager, I want a VAT submission detail screen showing the output/input/net breakdown for a period, so that I can review my VAT liability before marking it as submitted.

#### Acceptance Criteria

1. THE VAT submission detail view SHALL display the period label and date range at the top of the page.
2. THE VAT submission detail view SHALL display TotalOutputVat, TotalInputVat, and NetVatPayable in a clear breakdown layout following the MyChair Design System.
3. WHEN NetVatPayable is positive, THE VAT submission detail view SHALL display the amount with a label indicating tax owed to the authority.
4. WHEN NetVatPayable is negative, THE VAT submission detail view SHALL display the absolute amount with a label indicating a refund is due.
5. WHEN NetVatPayable is zero, THE VAT submission detail view SHALL display zero with a label indicating no payment is due.
6. THE VAT submission detail view SHALL provide a "Mark as Submitted" button when the submission has not yet been marked as submitted.
7. WHEN the submission is already marked as submitted, THE VAT submission detail view SHALL display the SubmittedAtUtc date and hide the "Mark as Submitted" button.
8. WHEN a user clicks "Mark as Submitted", THE VAT submission detail view SHALL display a SweetAlert2 confirmation dialog with informational styling (confirmButtonColor: '#0D5EA6') before proceeding.
9. THE VAT submission detail view SHALL use BlockUI.show() before AJAX requests and BlockUI.hide() after completion.
10. THE VAT submission detail view SHALL use SweetAlert2 to display success and error messages after operations.
11. THE VAT submission detail view SHALL provide a "Back to Periods" navigation link.

### Requirement 9: Tenant Isolation

**User Story:** As a business user, I want to see only my own business's VAT periods and submissions, so that financial data remains private between tenants.

#### Acceptance Criteria

1. THE PortalDbContext SHALL apply a global query filter on VatSubmissionPeriod ensuring that only records matching the current tenant's BusinessId are returned.
2. THE PortalDbContext SHALL apply a global query filter on VatSubmission ensuring that only records matching the current tenant's BusinessId are returned.
3. WHEN creating any VAT record, THE respective service SHALL assign the BusinessId from ICurrentTenantService, preventing users from creating records under a different tenant.
4. IF a user attempts to access a VAT submission or period belonging to a different Business, THEN THE VatController SHALL return a NotFound response.

### Requirement 10: Audit Logging for VAT Submissions

**User Story:** As a business manager, I want all VAT submission changes to be audit logged, so that there is a traceable record of VAT filings for compliance and review.

#### Acceptance Criteria

1. WHEN a VAT submission is created, THE VatSubmissionService SHALL write an audit log entry recording the action "Created", the user, and the submission details (period, output, input, net).
2. WHEN a VAT submission is recalculated, THE VatSubmissionService SHALL write an audit log entry recording the action "Recalculated", the user, and the updated values.
3. WHEN a VAT submission is marked as submitted, THE VatSubmissionService SHALL write an audit log entry recording the action "MarkedAsSubmitted", the user, and the SubmittedAtUtc timestamp.
4. THE audit log entries SHALL include the BusinessId, TableName as "VatSubmission", RecordId as the VatSubmission Id, Action type, and Timestamp.

### Requirement 11: Period Generation Algorithm Correctness

**User Story:** As a business manager, I want the period generation algorithm to produce mathematically correct, contiguous periods, so that every day from my VAT registration date to the present is covered by exactly one period with no gaps or overlaps.

#### Acceptance Criteria

1. FOR ALL generated periods for a tenant, THE VatPeriodGenerationService SHALL produce periods where no two periods overlap (no date belongs to more than one period).
2. FOR ALL generated periods for a tenant, THE VatPeriodGenerationService SHALL produce periods where there are no gaps between consecutive periods (PeriodStartDate of period N+1 equals PeriodEndDate of period N plus one day).
3. FOR ALL generated periods, THE VatPeriodGenerationService SHALL produce periods where the duration of each period equals exactly VatPeriodLengthInMonths calendar months.
4. THE VatPeriodGenerationService SHALL produce a first period whose PeriodStartDate equals the BusinessProfile's VatRegistrationDate.
5. FOR ALL generated periods, THE VatPeriodGenerationService SHALL ensure PeriodEndDate is greater than PeriodStartDate.
6. WHEN VatPeriodLengthInMonths is 1, THE VatPeriodGenerationService SHALL produce monthly periods (e.g., 01 Jan – 31 Jan, 01 Feb – 28 Feb).
7. WHEN VatPeriodLengthInMonths is 3, THE VatPeriodGenerationService SHALL produce quarterly periods (e.g., 01 Jan – 31 Mar, 01 Apr – 30 Jun).
