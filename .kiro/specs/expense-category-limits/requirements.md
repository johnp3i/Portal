# Requirements Document

## Introduction

This feature introduces per-category spending limits for business accounts. A business can configure a maximum annual spending amount and/or a maximum per-VAT-submission-period spending amount for each expense category. When a user creates or edits a purchase, the system checks cumulative spending against these thresholds and displays a soft warning if the limit is exceeded or would be exceeded. The purchase is still allowed — this is advisory, not a hard block.

## Glossary

- **Limit_Service**: The back-end service responsible for calculating cumulative spending per expense category and comparing against configured limits.
- **ExpenseCategoryLimit**: The database entity stored in `[purchase].[ExpenseCategoryLimit]` representing the configured annual and/or period spending thresholds for a specific business and expense category combination.
- **Purchase_Form**: The Razor views (`Create.cshtml` and `Edit.cshtml`) where users enter purchase details including expense category and amount.
- **Limit_Warning**: The advisory UI message displayed to the user when a configured spending threshold has been exceeded or would be exceeded by the current purchase amount.
- **Limit_Management_UI**: The settings interface where business owners configure spending limits per expense category.
- **Annual_Limit**: The `AnnualLimitEur` column representing the maximum total spending (in EUR) for an expense category within a calendar year (January–December).
- **Period_Limit**: The `PeriodLimitEur` column representing the maximum total spending (in EUR) for an expense category within a single VAT submission period.
- **VAT_Submission_Period**: An existing time range record in `[vat].[VatSubmissionPeriod]` representing a business's VAT reporting period with start and end dates.

## Requirements

### Requirement 1: ExpenseCategoryLimit Table Schema

**User Story:** As a platform developer, I want a dedicated table to store spending limits per business and expense category, so that limits are persisted independently of purchases and can be managed by business owners.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[purchase].[ExpenseCategoryLimit]` table with columns: Id (INT, PK, identity), BusinessId (INT, NOT NULL, FK to `[portal].[Business]`), ExpenseCategoryId (INT, NOT NULL, FK to `[purchase].[ExpenseCategory]`), AnnualLimitEur (DECIMAL(18,2), nullable), PeriodLimitEur (DECIMAL(18,2), nullable), and CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE()).
2. THE Portal_Database SHALL enforce a unique constraint on `[purchase].[ExpenseCategoryLimit]` (BusinessId, ExpenseCategoryId) to allow at most one limit configuration per business per category.
3. THE Portal_Database SHALL enforce foreign key constraints from ExpenseCategoryLimit.BusinessId to `[portal].[Business].[Id]` and from ExpenseCategoryLimit.ExpenseCategoryId to `[purchase].[ExpenseCategory].[Id]`.
4. THE Portal_Database SHALL include a non-clustered index on ExpenseCategoryLimit.BusinessId for tenant-filtered query optimisation.

### Requirement 2: Limit Value Constraints

**User Story:** As a business owner, I want to configure either an annual limit, a period limit, or both for any expense category, so that I have flexibility in how I monitor spending.

#### Acceptance Criteria

1. THE ExpenseCategoryLimit entity SHALL allow AnnualLimitEur to be null, indicating no annual spending threshold is configured for that category.
2. THE ExpenseCategoryLimit entity SHALL allow PeriodLimitEur to be null, indicating no period spending threshold is configured for that category.
3. WHEN both AnnualLimitEur and PeriodLimitEur are null for an ExpenseCategoryLimit record, THE Limit_Service SHALL treat the category as having no configured limits and skip threshold evaluation.
4. WHEN a limit value is provided, THE Limit_Management_UI SHALL require it to be a positive decimal greater than zero.
5. THE Limit_Service SHALL evaluate AnnualLimitEur and PeriodLimitEur independently — exceeding one limit does not affect the evaluation of the other.

### Requirement 3: Annual Limit Evaluation

**User Story:** As a business owner, I want the system to check the year-to-date spending against my configured annual limit when a purchase is entered, so that I am warned before overspending in a category for the year.

#### Acceptance Criteria

1. WHEN a user selects an expense category on the Purchase_Form and AnnualLimitEur is configured for that category, THE Limit_Service SHALL calculate the sum of TotalAmount from all existing purchases in that category for the same business within the current calendar year (January 1 to December 31 of the InvoiceDate year).
2. WHEN the year-to-date total plus the current purchase TotalAmount exceeds the AnnualLimitEur value, THE Limit_Service SHALL return a warning indicating the annual limit is exceeded.
3. WHEN the year-to-date total alone (without the current purchase) already exceeds the AnnualLimitEur value, THE Limit_Service SHALL return a warning indicating the annual limit has already been exceeded.
4. WHEN the year-to-date total plus the current purchase TotalAmount is less than or equal to the AnnualLimitEur value, THE Limit_Service SHALL return no annual limit warning.
5. WHEN AnnualLimitEur is null for the selected category, THE Limit_Service SHALL skip annual limit evaluation and return no annual limit warning.

### Requirement 4: Period Limit Evaluation

**User Story:** As a business owner, I want the system to check the current VAT period spending against my configured period limit when a purchase is entered, so that I am warned before overspending in a category within a single VAT period.

#### Acceptance Criteria

1. WHEN a user selects an expense category on the Purchase_Form and PeriodLimitEur is configured for that category, THE Limit_Service SHALL identify the VAT_Submission_Period whose date range contains the purchase InvoiceDate for that business.
2. WHEN a matching VAT_Submission_Period is found, THE Limit_Service SHALL calculate the sum of TotalAmount from all existing purchases in that category for the same business where InvoiceDate falls within the period's PeriodStartDate and PeriodEndDate (inclusive).
3. WHEN the period total plus the current purchase TotalAmount exceeds the PeriodLimitEur value, THE Limit_Service SHALL return a warning indicating the period limit is exceeded.
4. WHEN the period total alone (without the current purchase) already exceeds the PeriodLimitEur value, THE Limit_Service SHALL return a warning indicating the period limit has already been exceeded.
5. WHEN the period total plus the current purchase TotalAmount is less than or equal to the PeriodLimitEur value, THE Limit_Service SHALL return no period limit warning.
6. WHEN PeriodLimitEur is null for the selected category, THE Limit_Service SHALL skip period limit evaluation and return no period limit warning.
7. IF no VAT_Submission_Period exists that contains the purchase InvoiceDate, THEN THE Limit_Service SHALL skip period limit evaluation and return no period limit warning.

### Requirement 5: Soft Warning Behaviour

**User Story:** As a business owner, I want spending limit warnings to be advisory only, so that I can still save purchases even when a limit is exceeded.

#### Acceptance Criteria

1. WHEN the Limit_Service returns one or more warnings, THE Purchase_Form SHALL display a Limit_Warning message to the user indicating which limits are exceeded and by how much.
2. THE Purchase_Form SHALL allow the user to save the purchase regardless of whether a Limit_Warning is displayed.
3. THE Limit_Warning SHALL clearly indicate it is advisory by including language such as "Warning" or "Advisory" and SHALL NOT use blocking language such as "Error" or "Rejected".
4. WHEN both annual and period limits are exceeded, THE Purchase_Form SHALL display both warnings simultaneously.
5. THE Limit_Warning SHALL display the configured limit amount, the current cumulative total, and the amount by which the limit is or would be exceeded.

### Requirement 6: AJAX Limit Check on Purchase Form

**User Story:** As a user entering a purchase, I want the limit check to happen automatically when I select a category or change the amount, so that I receive immediate feedback without submitting the form.

#### Acceptance Criteria

1. WHEN the expense category dropdown value changes on the Purchase_Form, THE Purchase_Form SHALL trigger an AJAX request to the Limit_Service endpoint to evaluate limits for the selected category and current amount.
2. WHEN the purchase TotalAmount field value changes on the Purchase_Form and an expense category is already selected, THE Purchase_Form SHALL trigger an AJAX request to the Limit_Service endpoint to re-evaluate limits.
3. THE AJAX limit check request SHALL include the BusinessId, ExpenseCategoryId, TotalAmount, InvoiceDate, and (for edit mode) the current PurchaseId to exclude from cumulative calculations.
4. WHEN the AJAX response contains warnings, THE Purchase_Form SHALL display the Limit_Warning in a visible location near the expense category or amount field.
5. WHEN the AJAX response contains no warnings, THE Purchase_Form SHALL clear any previously displayed Limit_Warning.
6. IF the AJAX limit check fails due to a network or server error, THEN THE Purchase_Form SHALL silently clear any warning and allow the user to continue without interruption.

### Requirement 7: Edit Mode Exclusion

**User Story:** As a user editing an existing purchase, I want the limit check to exclude the current purchase from cumulative totals, so that the warning accurately reflects the impact of my changes.

#### Acceptance Criteria

1. WHEN evaluating limits for a purchase being edited, THE Limit_Service SHALL exclude the current purchase record from the cumulative spending calculation.
2. WHEN the user changes the expense category on an existing purchase, THE Limit_Service SHALL recalculate using the new category's limits and cumulative totals, excluding the current purchase.
3. WHEN the user changes the TotalAmount on an existing purchase, THE Limit_Service SHALL recalculate using the updated amount against the cumulative totals (excluding the current purchase) for the assigned category.

### Requirement 8: Limit Management UI

**User Story:** As a business owner, I want a settings interface to configure spending limits per expense category, so that I can set, update, and remove limits as my budget evolves.

#### Acceptance Criteria

1. THE Limit_Management_UI SHALL display a list of all active expense categories for the current business with their configured AnnualLimitEur and PeriodLimitEur values.
2. THE Limit_Management_UI SHALL allow the business owner to set or update the AnnualLimitEur for any expense category.
3. THE Limit_Management_UI SHALL allow the business owner to set or update the PeriodLimitEur for any expense category.
4. THE Limit_Management_UI SHALL allow the business owner to clear (set to null) either limit individually without affecting the other.
5. WHEN the business owner saves a limit configuration, THE system SHALL create a new ExpenseCategoryLimit record if none exists for that business and category, or update the existing record.
6. THE Limit_Management_UI SHALL validate that limit values are positive decimals greater than zero before saving.
7. THE Limit_Management_UI SHALL display confirmation feedback upon successful save using SweetAlert2.

### Requirement 9: Tenant Isolation

**User Story:** As a business owner, I want my spending limits to be completely isolated from other businesses, so that my configuration and data remain private.

#### Acceptance Criteria

1. THE Limit_Service SHALL filter all ExpenseCategoryLimit queries by the authenticated user's BusinessId.
2. THE Limit_Service SHALL filter all cumulative spending calculations by the authenticated user's BusinessId.
3. THE Limit_Management_UI SHALL only display and allow modification of ExpenseCategoryLimit records belonging to the authenticated user's business.
4. THE AJAX limit check endpoint SHALL verify that the requested BusinessId matches the authenticated user's business context and reject mismatched requests.

### Requirement 10: Limit Check API Endpoint

**User Story:** As a front-end developer, I want a dedicated API endpoint to check spending limits, so that the Purchase_Form can request limit evaluation via AJAX.

#### Acceptance Criteria

1. THE system SHALL expose an API endpoint that accepts BusinessId, ExpenseCategoryId, TotalAmount, InvoiceDate, and an optional PurchaseId (for edit exclusion) and returns the limit evaluation result.
2. WHEN the endpoint receives a valid request, THE Limit_Service SHALL return a JSON response containing: a boolean indicating whether any warning exists, and an array of warning objects each containing the limit type (annual or period), the configured limit amount, the current cumulative total, and the projected total including the new purchase.
3. WHEN the endpoint receives a request for a category with no configured limits, THE system SHALL return a response with no warnings.
4. IF the endpoint receives an invalid ExpenseCategoryId or a category not belonging to the business, THEN THE system SHALL return a JSON response indicating no warnings (fail-safe, non-blocking behaviour).
5. THE endpoint SHALL respond within a reasonable timeframe suitable for real-time user interaction.
