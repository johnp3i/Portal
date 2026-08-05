# Requirements Document

## Introduction

Phase D of the Payroll module delivers the final integration layer — PAYE income tax calculation, Compliance module integration, and employer contribution reporting. This phase adds optional PAYE computation using Cyprus progressive tax bands (with annual projection from monthly salary), links payslip finalisation to the existing Business Applications Tracker (Compliance) module by auto-populating Social Insurance filing amounts, provides an employer contribution breakdown report, and seeds country-specific deduction templates for future multi-country expansion. The module remains gated to Enterprise-tier subscribers with the `payroll` module key. All data access uses existing tables in the `[payroll]` schema.

## Glossary

- **Payroll_System**: The payroll module within the Portal platform responsible for employee management, payslip calculation, payslip generation, reporting, and integration with other platform modules
- **PAYE_Engine**: The component that computes Pay As You Earn income tax for an employee using progressive tax bands and annual income projection
- **Calculation_Engine**: The existing singleton service (Phase A) responsible for computing earning totals, deduction amounts, net salary, and employer contributions for a payslip
- **Compliance_Module**: The existing Business Applications Tracker module within the Portal that manages regulatory filing obligations (including Social Insurance filings)
- **Compliance_Filing**: A record in the Business Applications Tracker representing a monthly regulatory filing obligation (e.g., Social Insurance return) with an EstimatedAmount field
- **Progressive_Tax_Band**: A tax rate bracket defined by a lower and upper income boundary, where income within the bracket is taxed at a specific percentage
- **Annual_Projection**: The calculation of projected annual income from a single month's gross salary (monthly gross multiplied by 12) used to determine the applicable PAYE tax band
- **Taxable_Income**: The employee's gross earnings minus Social Insurance and GESY employee deductions, used as the base for PAYE calculation
- **PAYE_Deduction_Line**: A PayslipDeductionLine record representing the monthly PAYE income tax amount, applied after Social Insurance and GESY deductions
- **Employer_Contribution_Report**: A report displaying total employer contributions grouped by deduction type for a selected period, linked to corresponding Compliance filings
- **Country_Template**: A set of pre-configured deduction types and rates specific to a country, importable by businesses during setup
- **Owner**: A business owner user with permissions to manage all aspects of their business within the Portal
- **SuperAdmin**: A platform-level administrator with elevated permissions across all businesses

## Requirements

### Requirement 1: PAYE Calculation Engine

**User Story:** As a business owner, I want the system to calculate PAYE income tax automatically using Cyprus progressive tax bands, so that employees who earn above the threshold have the correct tax deducted from their payslips.

#### Acceptance Criteria

1. THE PAYE_Engine SHALL compute annual PAYE tax using the Cyprus 2024 progressive tax bands: €0–€19,500 at 0%, €19,501–€28,000 at 20%, €28,001–€36,300 at 25%, €36,301–€60,000 at 30%, and over €60,000 at 35%
2. THE PAYE_Engine SHALL compute Annual_Projection by multiplying the employee's monthly Taxable_Income by 12
3. THE PAYE_Engine SHALL define Taxable_Income as TotalEarnings minus the sum of Social Insurance and GESY employee deductions for the payslip
4. THE PAYE_Engine SHALL apply tax bands progressively — each band applies only to income within that band's range, not to the entire salary
5. THE PAYE_Engine SHALL compute the monthly PAYE amount by dividing the calculated annual tax by 12
6. THE PAYE_Engine SHALL round the monthly PAYE amount to 2 decimal places using MidpointRounding.AwayFromZero
7. THE PAYE_Engine SHALL store tax band configuration in a `PayeTaxBand` table in the `[payroll]` schema with columns: Id, CountryCode (NVARCHAR(3)), LowerBound (DECIMAL(18,2)), UpperBound (DECIMAL(18,2), nullable for the top band), Rate (DECIMAL(5,4)), EffectiveFromYear (INT), EffectiveToYear (INT, nullable for current), CreatedAtUtc
8. THE PAYE_Engine SHALL look up tax bands where CountryCode matches the business's configured country and EffectiveFromYear is less than or equal to the payslip period year and EffectiveToYear is NULL or greater than or equal to the payslip period year
9. IF no valid tax bands exist for the business's country and payslip period year, THEN THE PAYE_Engine SHALL raise a validation error and prevent PAYE calculation for the affected payslip

### Requirement 2: PAYE Employee-Level Opt-In

**User Story:** As a business owner, I want to enable or disable PAYE per employee based on their income level, so that only employees earning above the tax threshold have PAYE deducted.

#### Acceptance Criteria

1. THE Payroll_System SHALL add an IsPayeApplicable (BIT, NOT NULL, DEFAULT 0) column to the Employee table
2. WHEN an employee's Annual_Projection (monthly Taxable_Income multiplied by 12) exceeds €19,500, THE Payroll_System SHALL allow IsPayeApplicable to be set to true
3. WHEN IsPayeApplicable is false for an employee, THE Calculation_Engine SHALL skip PAYE calculation entirely for that employee's payslips
4. THE Payroll_System SHALL display the IsPayeApplicable toggle on the Employee profile edit view with a descriptive label: "Subject to PAYE Income Tax"
5. THE Payroll_System SHALL display an informational note next to the toggle: "PAYE applies when projected annual income exceeds €19,500"
6. WHEN a business owner attempts to enable IsPayeApplicable for an employee whose projected annual income is at or below €19,500, THE Payroll_System SHALL display a warning: "This employee's projected annual income (€{amount}) does not exceed the PAYE threshold (€19,500). PAYE calculation will result in €0."

### Requirement 3: PAYE Integration into Payslip Calculation

**User Story:** As a business owner, I want PAYE tax applied to payslips in the correct order (after Social Insurance deductions), so that the tax is calculated on the correct taxable income base.

#### Acceptance Criteria

1. WHEN IsPayeApplicable is true for an employee, THE Calculation_Engine SHALL include a PAYE_Deduction_Line on the payslip
2. THE Calculation_Engine SHALL apply PAYE after Social Insurance and GESY employee deductions have been calculated, using the resulting Taxable_Income as the PAYE base
3. THE Calculation_Engine SHALL compute NetSalary as TotalEarnings minus TotalEmployeeDeductions (which now includes Social Insurance, GESY, and PAYE)
4. THE PAYE_Deduction_Line SHALL be stored as a PayslipDeductionLine with a reference to a dedicated PAYE DeductionType (Code: "PAYE", DeductionCategoryTypeId = 1)
5. THE Payroll_System SHALL display the PAYE deduction in the Employee Deductions section of the payslip view, positioned after Social Insurance and GESY lines
6. THE PAYE_Deduction_Line SHALL store the BaseAmount as the monthly Taxable_Income (not TotalEarnings) to clearly show the tax base
7. WHEN the PAYE calculation results in €0 (income below threshold), THE Calculation_Engine SHALL still include the PAYE_Deduction_Line with CalculatedAmount of €0 for transparency
8. THE Calculation_Engine SHALL record the applicable tax band details (rate applied, annual projected amount) in the PAYE_Deduction_Line for audit purposes

### Requirement 4: Compliance Integration — Social Insurance Filing

**User Story:** As a business owner, I want the total employer Social Insurance contribution to automatically populate the Compliance filing for the corresponding month, so that I do not need to manually calculate and enter the amount.

#### Acceptance Criteria

1. WHEN a PayslipPeriod transitions to Finalised status, THE Payroll_System SHALL calculate the total employer Social Insurance contribution across all payslips in the period
2. THE Payroll_System SHALL locate the corresponding Social Insurance Compliance_Filing for the same month and year within the same business
3. WHEN a matching Compliance_Filing exists, THE Payroll_System SHALL update its EstimatedAmount field with the calculated total employer Social Insurance contribution
4. IF no matching Compliance_Filing exists for the period, THEN THE Payroll_System SHALL log a warning and continue without failing the payslip finalisation
5. WHEN a PayslipPeriod is unlocked and re-finalised, THE Payroll_System SHALL recalculate the total and update the Compliance_Filing EstimatedAmount with the new value
6. THE Payroll_System SHALL only include employer Social Insurance contributions (DeductionType Code: "SI_EMPLOYER") in the Compliance filing amount — other employer contributions (Redundancy, Industrial Training, Social Cohesion, GESY) are excluded
7. THE Payroll_System SHALL store a cross-reference record linking the PayslipPeriod to the updated Compliance_Filing for audit traceability

### Requirement 5: Employer Contribution Breakdown Report

**User Story:** As a business owner, I want a report that shows total employer contributions grouped by type for a selected period, so that I can review costs and reconcile against Compliance filings.

#### Acceptance Criteria

1. THE Payroll_System SHALL provide an Employer_Contribution_Report view accessible from the payroll reports section
2. THE Employer_Contribution_Report SHALL display one row per employer contribution type (Social Insurance, Redundancy Fund, Industrial Training, Social Cohesion, GESY) with the total amount for the selected period
3. THE Employer_Contribution_Report SHALL provide a period selector allowing the user to choose a specific PayslipPeriod (Year/Month)
4. THE Employer_Contribution_Report SHALL display a detail section listing each employee's individual contribution amounts per type for the selected period
5. THE Employer_Contribution_Report SHALL display a footer row with the grand total of all employer contributions for the period
6. THE Employer_Contribution_Report SHALL include only data from payslips in Finalised or Re-finalised status
7. WHEN a Compliance_Filing exists for the selected period's Social Insurance, THE Employer_Contribution_Report SHALL display a link to the corresponding filing with status indicator (Filed/Pending)
8. THE Employer_Contribution_Report SHALL provide an "Export to Excel" action that downloads the report data as an XLSX file
9. THE Employer_Contribution_Report SHALL provide a "Download PDF" action that generates an A4 branded PDF of the report
10. WHEN no finalised payslips exist for the selected period, THE Employer_Contribution_Report SHALL display an empty state message: "No finalised payslips for {Month Name} {Year}"

### Requirement 6: Compliance Filing Detail — Expected Amount Display

**User Story:** As a business owner, I want to see a breakdown of how the estimated Social Insurance filing amount was calculated, so that I can verify the figure before submitting the filing.

#### Acceptance Criteria

1. THE Compliance_Module SHALL display the payroll-derived EstimatedAmount on the Social Insurance filing detail view
2. THE Compliance_Module SHALL show a calculation breakdown: "Based on {N} employees × {rate}% = €{amount}" for the employer Social Insurance contribution
3. WHEN multiple employees have different contribution amounts (due to varying gross salaries), THE Compliance_Module SHALL display the per-employee breakdown with individual amounts summing to the total
4. THE Compliance_Module SHALL indicate the source of the estimated amount as "Auto-calculated from Payroll — {Month Name} {Year}"
5. WHEN no payroll data exists for the filing period, THE Compliance_Module SHALL display: "No payroll data available for this period. Enter estimated amount manually."
6. THE Compliance_Module SHALL allow the business owner to override the auto-populated EstimatedAmount with a manual entry, with a note: "Manual override — differs from payroll calculation by €{difference}"

### Requirement 7: Country-Specific Deduction Templates

**User Story:** As a platform administrator, I want to seed country-specific deduction templates that businesses can import during setup, so that the platform can expand to new markets without code changes.

#### Acceptance Criteria

1. THE Payroll_System SHALL store country templates in a `CountryDeductionTemplate` table in the `[payroll]` schema with columns: Id (INT PK IDENTITY), CountryCode (NVARCHAR(3)), DeductionName (NVARCHAR(100)), Code (NVARCHAR(50)), IsPercentage (BIT), DeductionCategoryTypeId (TINYINT FK), DefaultRate (DECIMAL(5,4)), SortOrder (INT), IsActive (BIT), CreatedAtUtc
2. THE Payroll_System SHALL seed Cyprus (CY) deduction templates containing all current Cyprus deductions and contributions with their 2024 rates
3. THE Payroll_System SHALL provide a SuperAdmin interface for managing country templates (view, add, edit, deactivate templates per country)
4. THE Payroll_System SHALL structure the template table to support additional countries (Malta, UK) without schema changes
5. WHEN a new business imports a country template, THE Payroll_System SHALL create business-specific DeductionType and DeductionRateHistory records from the template defaults
6. THE Payroll_System SHALL prevent duplicate imports — if a business has already imported templates for a country, THE Payroll_System SHALL display a warning: "Templates for {Country} already imported. Import again will create duplicate records."
7. THE Payroll_System SHALL seed the PAYE tax bands for Cyprus (CY) with EffectiveFromYear = 2024 and EffectiveToYear = NULL (current)
8. THE SuperAdmin interface SHALL allow adding new countries with their respective tax bands and deduction templates without requiring code deployments

### Requirement 8: PAYE Data Schema

**User Story:** As a developer, I want the PAYE-related data structures properly defined in the payroll schema, so that tax calculations are stored accurately and support future multi-country expansion.

#### Acceptance Criteria

1. THE Payroll_System SHALL create a `PayeTaxBand` table in the `[payroll]` schema with referential integrity and appropriate indexes
2. THE PayeTaxBand table SHALL enforce: LowerBound is less than UpperBound (or UpperBound is NULL for the top band), Rate is between 0 and 1 (representing 0% to 100%), and no overlapping bands exist for the same CountryCode and effective year range
3. THE Payroll_System SHALL create a PAYE DeductionType record (Code: "PAYE", Name: "PAYE Income Tax", DeductionCategoryTypeId: 1, IsPercentage: false) as a system-seeded type per business upon template import
4. THE PAYE DeductionType SHALL have IsPercentage set to false because the PAYE amount is calculated via progressive bands rather than a flat percentage
5. THE Payroll_System SHALL add an IsPayeApplicable column (BIT NOT NULL DEFAULT 0) to the Employee table
6. THE PayslipDeductionLine for PAYE SHALL store: BaseAmount (monthly Taxable_Income), Rate (effective marginal rate for the employee's income level), and CalculatedAmount (monthly PAYE amount)
7. EACH table added in Phase D SHALL include a CreatedAtUtc column of type DATETIME NOT NULL with a default of GETUTCDATE()

### Requirement 9: Compliance Cross-Reference Tracking

**User Story:** As a business owner, I want an audit trail linking payslip periods to compliance filings, so that I can trace which payroll data was used to populate each filing.

#### Acceptance Criteria

1. THE Payroll_System SHALL create a `PayslipPeriodComplianceFiling` table in the `[payroll]` schema with columns: Id (INT PK IDENTITY), PayslipPeriodId (INT FK), ComplianceFilingId (INT FK), ContributionTotal (DECIMAL(18,2)), UpdatedAtUtc (DATETIME NOT NULL), UpdatedByUserId (NVARCHAR(450) FK), CreatedAtUtc (DATETIME NOT NULL DEFAULT GETUTCDATE())
2. WHEN the Payroll_System updates a Compliance_Filing EstimatedAmount, THE Payroll_System SHALL create or update a PayslipPeriodComplianceFiling record linking the PayslipPeriod to the filing
3. THE PayslipPeriodComplianceFiling record SHALL store the ContributionTotal that was sent to the Compliance_Filing
4. THE Payroll_System SHALL display the cross-reference history on the PayslipPeriod detail view showing: linked Compliance filing, amount sent, date updated, and updated by user
5. WHEN a PayslipPeriod is re-finalised, THE Payroll_System SHALL create a new PayslipPeriodComplianceFiling record (preserving history) rather than updating the existing record

### Requirement 10: Permission and Access Control

**User Story:** As a platform operator, I want all Phase D features restricted to authorised users with the payroll module key, so that only Enterprise-tier subscribers can access PAYE and compliance integration features.

#### Acceptance Criteria

1. THE Payroll_System SHALL enforce the `payroll` module key (Enterprise plan) for all Phase D controllers and actions
2. THE Payroll_System SHALL apply the standard [Authorize] attribute on all Phase D controller actions
3. THE Payroll_System SHALL restrict SuperAdmin template management (Requirement 7) to users with the SuperAdmin role
4. THE Payroll_System SHALL restrict Compliance filing updates to users with Owner or SuperAdmin roles
5. THE Payroll_System SHALL allow standard payroll users to view the Employer_Contribution_Report and PAYE information but not modify Compliance filings or manage templates
6. WHEN a user without Owner or SuperAdmin role attempts to modify Compliance filings or manage templates, THE Payroll_System SHALL deny the action and display an authorisation error

### Requirement 11: Verification and Validation

**User Story:** As a developer, I want a verification checkpoint to confirm all Phase D components integrate correctly, so that the module is production-ready before deployment.

#### Acceptance Criteria

1. THE Payroll_System SHALL pass all existing Phase A, B, and C tests without regression after Phase D changes
2. THE PAYE_Engine SHALL produce correct tax amounts for test cases at each band boundary: €19,500 (zero tax), €19,501 (first taxable euro), €28,000 (band 2 boundary), €36,300 (band 3 boundary), €60,000 (band 4 boundary), and €75,000 (top band)
3. THE Compliance integration SHALL correctly update filing amounts when payslips are finalised, and correctly recalculate when re-finalised after an unlock
4. THE Employer_Contribution_Report SHALL produce totals that match the sum of individual PayslipDeductionLine amounts for employer contributions in the selected period
5. THE Payroll_System SHALL enforce that PAYE is calculated only after Social Insurance and GESY deductions in the payslip calculation sequence
6. THE Payroll_System SHALL operate correctly when PAYE is disabled for all employees in a business (no PAYE lines generated, no errors)
