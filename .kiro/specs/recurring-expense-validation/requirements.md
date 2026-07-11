# Requirements Document

## Introduction

This feature introduces Recurring Expense Validation — a system that allows business users to define expected recurring purchase patterns per supplier (optionally scoped to an expense category). During VAT period submission or via a standalone validation view, the system checks whether all expected purchases have been recorded and reports missing or incomplete entries. This prevents forgotten invoices from slipping through, especially for digital-only bills that are easily lost.

The validation is advisory — it warns but never blocks VAT submission.

## Glossary

- **RecurringRule**: A configured expectation that a supplier should have purchases recorded at a defined frequency, stored in `[Billing].[SupplierRecurringRule]`.
- **Validation_Service**: The back-end service responsible for evaluating recurring rules against actual purchase records for a given period.
- **Rule_Management_UI**: The interface where business users create, edit, activate, and deactivate recurring expense rules.
- **Validation_Report**: The output showing pass/warn/fail status per rule for a given date range or VAT period.
- **VAT_Submission_Panel**: An integrated validation section within the VAT period submission view that runs recurring expense checks before submission.
- **Standalone_Validation_View**: An independent page accessible from the Purchases module navigation that allows running recurring expense validation at any time.
- **Frequency**: The expected billing interval in months (1 = monthly, 2 = bimonthly, 3 = quarterly, etc.).
- **Grace_Period**: A configurable number of days extending the lookup window beyond period boundaries to accommodate invoices dated near period edges.
- **Expected_Amount**: An optional amount the system expects to find in at least one purchase per expected occurrence, validated within a configurable tolerance percentage.
- **Amount_Tolerance**: A percentage defining the acceptable variance around the Expected_Amount (e.g., 5% allows ±5% of the configured amount).

## Requirements

### Requirement 1: SupplierRecurringRule Table Schema

**User Story:** As a platform developer, I want a dedicated table to store recurring expense rules per business and supplier, so that rules are persisted and can be managed by business users.

#### Acceptance Criteria

1. THE Portal_Database SHALL contain a `[Billing].[SupplierRecurringRule]` table with columns: Id (INT, PK, identity), BusinessId (INT, NOT NULL, FK to `[portal].[Business]`), SupplierId (INT, NOT NULL, FK to `[dbo].[Suppliers]`), ExpenseCategoryId (INT, NULL, FK to `[purchase].[ExpenseCategory]`), FrequencyMonths (INT, NOT NULL), ExpectedAmount (DECIMAL(18,2), NULL), AmountTolerancePercent (DECIMAL(5,2), NULL, default 5.00), GracePeriodDays (INT, NOT NULL, default 0), Description (NVARCHAR(200), NOT NULL), IsActive (BIT, NOT NULL, default 1), IsDeleted (BIT, NOT NULL, default 0), CreatedAtUtc (DATETIME, NOT NULL, default GETUTCDATE()).
2. THE Portal_Database SHALL enforce foreign key constraints from SupplierRecurringRule.BusinessId to `[portal].[Business].[Id]`, from SupplierRecurringRule.SupplierId to `[dbo].[Suppliers].[Id]`, and from SupplierRecurringRule.ExpenseCategoryId to `[purchase].[ExpenseCategory].[Id]`.
3. THE Portal_Database SHALL include a non-clustered index on (BusinessId, SupplierId) for optimised tenant-scoped queries.
4. THE Portal_Database SHALL include a non-clustered index on BusinessId for tenant-filtered listing queries.

### Requirement 2: Rule Configuration Constraints

**User Story:** As a business user, I want flexibility in how I define recurring rules, so that I can match the actual billing patterns of my suppliers.

#### Acceptance Criteria

1. THE RecurringRule SHALL require FrequencyMonths to be a positive integer (minimum 1).
2. THE RecurringRule SHALL allow ExpenseCategoryId to be null, indicating the rule applies to any purchase from the supplier regardless of category.
3. WHEN ExpenseCategoryId is set, THE Validation_Service SHALL only count purchases from the specified supplier AND category when evaluating the rule.
4. THE RecurringRule SHALL allow ExpectedAmount to be null, indicating no amount validation is performed (frequency check only).
5. WHEN ExpectedAmount is set, THE RecurringRule SHALL require AmountTolerancePercent to be a non-null positive value.
6. THE RecurringRule SHALL allow GracePeriodDays to be 0 (strict period matching) or a positive integer.
7. THE RecurringRule SHALL require Description to be non-empty with a maximum length of 200 characters.
8. THE RecurringRule SHALL have an IsActive flag allowing rules to be deactivated without deletion.

### Requirement 3: Frequency-Based Validation

**User Story:** As a business user, I want the system to check whether my expected recurring purchases have been recorded for each month in a VAT period, so that I am warned about missing invoices before submission.

#### Acceptance Criteria

1. WHEN validating a rule against a date range, THE Validation_Service SHALL calculate the expected number of purchase occurrences as: `expectedCount = floor(periodMonths / frequencyMonths)`, with a minimum of 1 if the period length is greater than or equal to the frequency.
2. THE Validation_Service SHALL count the actual number of qualifying purchases from the specified supplier (and category, if set) within the date range (extended by grace period if configured).
3. WHEN the actual count equals or exceeds the expected count, THE Validation_Service SHALL mark the rule as PASS.
4. WHEN the actual count is greater than zero but less than the expected count, THE Validation_Service SHALL mark the rule as WARNING with details of how many are missing.
5. WHEN the actual count is zero, THE Validation_Service SHALL mark the rule as FAIL.
6. WHEN GracePeriodDays is greater than 0, THE Validation_Service SHALL extend the lookup start date backward by GracePeriodDays and the lookup end date forward by GracePeriodDays when searching for qualifying purchases.

### Requirement 4: Amount-Anchored Validation

**User Story:** As a business user, I want the system to verify that a specific expected amount has been recorded, so that I can catch missing fixed-cost invoices even when other purchases from the same supplier exist.

#### Acceptance Criteria

1. WHEN ExpectedAmount is configured on a rule, THE Validation_Service SHALL additionally verify that for each expected occurrence, at least one qualifying purchase has an AmountExcludingVat within the tolerance range.
2. THE tolerance range SHALL be calculated as: lower bound = `ExpectedAmount * (1 - AmountTolerancePercent/100)`, upper bound = `ExpectedAmount * (1 + AmountTolerancePercent/100)`.
3. WHEN all expected occurrences have at least one amount-matching purchase, THE Validation_Service SHALL mark the amount check as PASS.
4. WHEN some but not all expected occurrences have an amount-matching purchase, THE Validation_Service SHALL mark the amount check as WARNING.
5. WHEN no qualifying purchases match the expected amount within tolerance, THE Validation_Service SHALL mark the amount check as FAIL.
6. WHEN ExpectedAmount is null, THE Validation_Service SHALL skip amount validation entirely and evaluate only frequency.

### Requirement 5: Grace Period Behaviour

**User Story:** As a business user, I want a configurable grace period for suppliers whose billing date falls near VAT period boundaries, so that invoices are not incorrectly flagged as missing.

#### Acceptance Criteria

1. WHEN GracePeriodDays is 0, THE Validation_Service SHALL search for qualifying purchases strictly within the period start and end dates.
2. WHEN GracePeriodDays is greater than 0, THE Validation_Service SHALL extend the search window: lookupStartDate = periodStartDate minus GracePeriodDays, lookupEndDate = periodEndDate plus GracePeriodDays.
3. THE grace period SHALL NOT change the expected count calculation — it only widens the date window for finding qualifying purchases.
4. THE Rule_Management_UI SHALL allow setting GracePeriodDays between 0 and 15 (inclusive).

### Requirement 6: Validation Report Output

**User Story:** As a business user, I want a clear report showing which recurring expenses are recorded and which are missing, so that I can take action before submitting my VAT period.

#### Acceptance Criteria

1. THE Validation_Report SHALL display one row per active rule, showing: supplier name, category name (or "Any" if no category), description, frequency label, expected count, actual count, and status (Pass/Warning/Fail).
2. WHEN a rule has ExpectedAmount configured, THE Validation_Report SHALL additionally show the expected amount and whether amount-matching purchases were found.
3. THE Validation_Report SHALL use colour-coded status indicators: green for Pass, amber for Warning, red for Fail.
4. THE Validation_Report SHALL display a summary at the top showing total rules checked, number passing, number with warnings, and number failing.
5. THE Validation_Report SHALL sort results with Fail first, then Warning, then Pass — so the most critical issues are immediately visible.

### Requirement 7: VAT Submission Integration

**User Story:** As a business user, I want the recurring expense validation to run automatically when I view a VAT period for submission, so that I am immediately aware of any missing expenses.

#### Acceptance Criteria

1. THE VAT_Submission_Panel SHALL appear as a collapsible section on the VAT period detail/submission page.
2. THE VAT_Submission_Panel SHALL automatically load validation results when the page loads, using the VAT period's start and end dates.
3. THE VAT_Submission_Panel SHALL display the Validation_Report with pass/warn/fail indicators.
4. THE VAT_Submission_Panel SHALL NOT block VAT submission regardless of validation results — it is advisory only.
5. THE VAT_Submission_Panel SHALL include a "Re-validate" button to refresh results after the user records additional purchases.
6. WHEN no active recurring rules exist for the business, THE VAT_Submission_Panel SHALL display a message indicating no rules are configured, with a link to the rule management page.

### Requirement 8: Standalone Validation View

**User Story:** As a business user, I want to run recurring expense validation at any time without navigating to the VAT submission page, so that I can perform mid-period checks.

#### Acceptance Criteria

1. THE Standalone_Validation_View SHALL be accessible from the Purchases module navigation as a dedicated menu item.
2. THE Standalone_Validation_View SHALL allow the user to select a date range (from/to) or a specific VAT period to validate against.
3. WHEN a VAT period is selected, THE system SHALL use the period's start and end dates for validation.
4. WHEN a custom date range is selected, THE system SHALL validate using the specified from and to dates.
5. THE Standalone_Validation_View SHALL display the same Validation_Report as the VAT_Submission_Panel.
6. THE Standalone_Validation_View SHALL default to the current (open/unfiled) VAT period if one exists.

### Requirement 9: Rule Management UI

**User Story:** As a business user, I want an interface to create, edit, and manage my recurring expense rules, so that I can maintain my expected purchases as my business evolves.

#### Acceptance Criteria

1. THE Rule_Management_UI SHALL display a list of all non-deleted recurring rules for the current business, grouped by supplier.
2. THE Rule_Management_UI SHALL allow creating a new rule with fields: Supplier (required, autocomplete), Category (optional, autocomplete), Frequency (required, dropdown: Monthly/Bimonthly/Quarterly/Custom), Expected Amount (optional), Amount Tolerance % (shown when amount is set, default 5%), Grace Period Days (default 0, max 15), Description (required).
3. THE Rule_Management_UI SHALL allow editing any field of an existing rule.
4. THE Rule_Management_UI SHALL allow activating and deactivating rules via a Disable/Enable button.
5. THE Rule_Management_UI SHALL allow soft-deleting a rule with a SweetAlert2 confirmation dialog. Soft-delete sets IsDeleted = 1 and the rule is no longer displayed.
6. THE Rule_Management_UI SHALL validate all required fields and constraints before saving.
7. THE Rule_Management_UI SHALL display confirmation feedback upon successful save, edit, disable, enable, or delete using SweetAlert2.
8. THE Rule_Management_UI SHALL be accessible from the Purchases module navigation.
9. THE Rule_Management_UI SHALL show active rules with Edit, Disable, and Delete action buttons.
10. THE Rule_Management_UI SHALL show inactive (disabled) rules with Enable and Delete action buttons, visually distinguished (greyed out).

### Requirement 10: Multiple Rules Per Supplier

**User Story:** As a business user, I want to define multiple recurring rules for the same supplier with different categories, so that I can track distinct billing expectations (e.g., hosting vs. SSL from the same vendor).

#### Acceptance Criteria

1. THE system SHALL allow multiple active RecurringRules for the same supplier, differentiated by ExpenseCategoryId and/or Description.
2. WHEN multiple rules exist for the same supplier, THE Validation_Service SHALL evaluate each rule independently.
3. THE Validation_Report SHALL display each rule as a separate line item, even when they share the same supplier.

### Requirement 11: Tenant Isolation

**User Story:** As a business user, I want my recurring expense rules to be completely isolated from other businesses, so that my configuration and validation data remain private.

#### Acceptance Criteria

1. THE Validation_Service SHALL filter all RecurringRule queries by the authenticated user's BusinessId.
2. THE Validation_Service SHALL filter all purchase lookups by the authenticated user's BusinessId.
3. THE Rule_Management_UI SHALL only display and allow modification of rules belonging to the authenticated user's business.
4. THE Standalone_Validation_View and VAT_Submission_Panel SHALL only validate rules and purchases for the authenticated user's business.

### Requirement 12: Validation API Endpoint

**User Story:** As a front-end developer, I want a dedicated API endpoint to run recurring expense validation, so that both the VAT submission panel and standalone view can request evaluation via AJAX.

#### Acceptance Criteria

1. THE system SHALL expose an API endpoint that accepts a date range (startDate, endDate) and returns the full validation report as JSON.
2. THE response SHALL include a summary object with totalRules, passCount, warningCount, and failCount.
3. THE response SHALL include an array of rule result objects, each containing: ruleId, supplierName, categoryName, description, frequency, expectedCount, actualCount, status (pass/warning/fail), and optionally expectedAmount and amountMatched (boolean).
4. THE endpoint SHALL evaluate only active rules (IsActive = 1) for the current business.
5. THE endpoint SHALL respond within a reasonable timeframe suitable for page load.

### Requirement 13: Deactivated Rule Behaviour

**User Story:** As a business user, I want to deactivate rules without losing them, so that I can temporarily suspend validation for suppliers whose billing has changed.

#### Acceptance Criteria

1. WHEN a rule is deactivated (IsActive = 0), THE Validation_Service SHALL exclude it from all validation runs.
2. THE Rule_Management_UI SHALL clearly indicate deactivated rules (greyed out or visually distinguished).
3. THE Rule_Management_UI SHALL allow reactivating a previously deactivated rule.
4. Deactivated rules SHALL be retained indefinitely and displayed in the management UI unless soft-deleted.
5. WHEN a rule is soft-deleted (IsDeleted = 1), THE system SHALL exclude it from all queries, validation runs, and the management UI. It is never physically removed from the database.
6. THE delete action SHALL set IsDeleted = 1 via an UPDATE statement, never a DELETE statement.

### Requirement 14: Cancelled Purchase Exclusion

**User Story:** As a business user, I want cancelled purchases to be excluded from validation counts, so that only actual recorded expenses satisfy recurring rules.

#### Acceptance Criteria

1. THE Validation_Service SHALL exclude purchases where IsCancelled = 1 from all qualifying purchase counts and amount matching.
2. THIS exclusion SHALL apply consistently across frequency checks and amount-anchored validation.
