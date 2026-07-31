# Requirements Document

## Introduction

Phase A of the Payroll module delivers the Core Engine — the minimum viable payslip generation capability for the Portal platform. This phase covers employee and department management, earning and deduction type configuration with historical rate tracking, payslip period lifecycle management, a calculation engine that produces net salary and employer contributions, and batch payslip generation with preview and confirmation. The module is gated to Enterprise-tier subscribers only and operates exclusively in EUR. All tables reside in the `[payroll]` schema.

## Glossary

- **Payroll_System**: The payroll module within the Portal platform responsible for employee management, payslip calculation, and payslip generation
- **Calculation_Engine**: The component that computes earning totals, deduction amounts, net salary, and employer contributions for a payslip
- **Department**: An organisational grouping of employees within a business, used for reporting purposes
- **Employee**: A person employed by a business, with a full profile including salary details, contact information, and department assignment
- **EarningType**: A system-seeded classification of income on a payslip (Basic, Overtime, Bonus, Paid Holidays, Part-time)
- **DeductionType**: A configured deduction or contribution type belonging to exactly one category (Deduction from employee OR Contribution by employer), with percentage-based or fixed rates, scoped to a specific business
- **DeductionCategoryType**: A lookup table distinguishing Deductions (Id=1, taken from employee salary) from Contributions (Id=2, paid by employer on top of salary)
- **DeductionRateHistory**: A record tracking the effective rate of a deduction type over time, using EffectiveFromUtc and EffectiveToUtc date boundaries
- **PayslipPeriod**: A year/month container representing a payroll processing cycle for a business, with a status lifecycle (Draft → Preview → Finalised)
- **Payslip**: A calculated pay record for a single employee within a payslip period, containing earning lines, deduction lines, and computed totals
- **PayslipEarningLine**: An individual earning entry on a payslip, linked to an EarningType, with optional overtime multiplier and hours
- **PayslipDeductionLine**: An individual deduction entry on a payslip, linked to a DeductionType, calculated from the historical rate applicable to the period
- **OvertimeMultiplier**: A configurable factor (default 1.5, maximum 4.0) applied to hourly rate and overtime hours to compute overtime earnings
- **ManagerNotes**: An optional free-text field on a payslip for informational comments that do not affect calculations
- **TotalEarnings**: The sum of all PayslipEarningLine amounts on a payslip
- **NetSalary**: TotalEarnings minus TotalEmployeeDeductions
- **TotalEmployerContributions**: The sum of all employer-portion deduction calculated amounts
- **ModuleAccess_Gate**: The authorisation mechanism that restricts payroll module access to Enterprise-tier subscribers with the `payroll` module key

## Requirements

### Requirement 1: Department Management

**User Story:** As a business owner, I want to manage departments within my organisation, so that I can group employees for payroll processing and future reporting.

#### Acceptance Criteria

1. THE Payroll_System SHALL provide CRUD operations for Department records scoped to the authenticated business
2. WHEN a Department is created, THE Payroll_System SHALL store the BusinessId, Name, IsActive flag, and CreatedAtUtc timestamp
3. WHEN a Department is deactivated, THE Payroll_System SHALL set IsActive to false and retain the Department record for historical reference
4. THE Payroll_System SHALL prevent creation of duplicate Department names within the same business
5. WHEN a Department has active employees assigned, THE Payroll_System SHALL prevent deletion of the Department

### Requirement 2: Employee Management

**User Story:** As a business owner, I want to manage employee profiles with full details including salary configuration, so that I can generate accurate payslips.

#### Acceptance Criteria

1. THE Payroll_System SHALL provide CRUD operations for Employee records scoped to the authenticated business
2. WHEN an Employee is created, THE Payroll_System SHALL store: Name, Position, SocialInsuranceNumber, IdNumber, Phone, Email, StartDate, SalaryType, BaseSalary, BankAccount, and IsActive flag
3. WHEN an Employee has SalaryType that includes overtime eligibility, THE Payroll_System SHALL require a HourlyRate value
4. THE Payroll_System SHALL allow optional assignment of an Employee to a Department via DepartmentId
5. WHEN an Employee is deactivated, THE Payroll_System SHALL set IsActive to false and retain the Employee record for historical payslip reference
6. THE Payroll_System SHALL validate that SocialInsuranceNumber and IdNumber are unique within the same business
7. WHEN an Employee EndDate is set, THE Payroll_System SHALL exclude the Employee from future payslip generation for periods after the EndDate
8. THE Payroll_System SHALL maintain a `SalaryType` lookup table (Id TINYINT PK, Name NVARCHAR(50)) with seeded values: (1, 'Full-time'), (2, 'Part-time'), (3, 'Hourly'). The Employee table SHALL reference SalaryType.Id via a TINYINT foreign key rather than storing a string.
9. ALL SalaryType values are overtime-eligible and require HourlyRate when overtime earning lines are used on a payslip.

### Requirement 3: Earning Types Configuration

**User Story:** As a system administrator, I want pre-configured earning types available for all businesses, so that payslip earning lines use consistent classifications.

#### Acceptance Criteria

1. THE Payroll_System SHALL seed the following EarningTypes on deployment: Basic, Overtime, Bonus, Paid Holidays, Part-time
2. EACH EarningType SHALL have an Id, Name, Code, IsActive flag, and SortOrder for display ordering
3. THE Payroll_System SHALL display EarningTypes in SortOrder sequence when presenting earning line options
4. THE Payroll_System SHALL prevent modification of system-seeded EarningType Code values

### Requirement 4: Deduction and Contribution Types with Rate History

**User Story:** As a business owner, I want deductions and contributions clearly separated into categories with historical rate tracking, so that I can understand what is deducted from employees vs what the business contributes on top of salary.

#### Acceptance Criteria

1. THE Payroll_System SHALL maintain a `DeductionCategoryType` lookup table with two entries: Id=1 Name="Deduction" (deducted from employee salary) and Id=2 Name="Contribution" (paid by employer on top of salary).
2. EACH DeductionType record SHALL reference exactly one DeductionCategoryType via a TINYINT foreign key — a type is either a Deduction OR a Contribution, never both.
3. THE Payroll_System SHALL store DeductionType records scoped to a specific business via BusinessId.
4. THE Payroll_System SHALL provide importable country templates (e.g., Cyprus defaults) that a business owner can import into their business, creating business-specific DeductionType records.
5. THE Payroll_System SHALL seed Cyprus deduction templates containing:
   - **Deductions (from employee):** Social Insurance 8.8%, GESY 2.65%
   - **Contributions (by employer):** Social Insurance 8.8%, Redundancy Fund 1.2%, Industrial Training Fund 0.5%, Social Cohesion Fund 2.0%, GESY 2.9%
6. WHEN a deduction rate changes, THE Payroll_System SHALL create a new DeductionRateHistory record with the new Rate and EffectiveFromUtc date, and set EffectiveToUtc on the previous record.
7. THE Payroll_System SHALL enforce that only one DeductionRateHistory record per DeductionType has a NULL EffectiveToUtc value (representing the current rate).
8. THE Payroll_System SHALL provide a management UI for business owners to view, add, and modify their business-specific deduction and contribution types, clearly separated by category.
9. WHEN a new rate history entry is created, THE Payroll_System SHALL validate that EffectiveFromUtc does not overlap with existing active rate periods for the same DeductionType.
10. THE Payroll_System SHALL allow business owners to create custom deduction or contribution types beyond the imported templates.
11. THE Payroll_System SHALL display deductions and contributions in separate sections on all views (payslip detail, batch preview, management UI) to clearly communicate what is taken from the employee vs what the business pays.

### Requirement 5: Payslip Period Management

**User Story:** As a business owner, I want to manage payslip periods with a clear status lifecycle, so that I can control the payroll processing workflow.

#### Acceptance Criteria

1. THE Payroll_System SHALL allow creation of PayslipPeriod records with Year, Month, and initial Status of Draft, scoped to the authenticated business
2. THE Payroll_System SHALL enforce the status transition sequence: Draft → Preview → Finalised. Statuses SHALL be stored as a `PayslipStatusType` lookup table with TINYINT Id.
3. EACH Payslip and PayslipPeriod SHALL reference the PayslipStatusType.Id rather than storing a status string.
4. WHEN a PayslipPeriod transitions to Finalised, THE Payroll_System SHALL record ProcessedAtUtc with the current UTC timestamp
5. THE Payroll_System SHALL prevent creation of duplicate PayslipPeriod records for the same Year and Month within a business
6. WHILE a PayslipPeriod is in Finalised status, THE Payroll_System SHALL prevent modifications to any Payslip within that period
7. WHEN a PayslipPeriod is in Draft status, THE Payroll_System SHALL allow generation and editing of payslips within that period
8. WHILE a PayslipPeriod is in Preview status, THE Payroll_System SHALL allow editing of individual payslip earning lines, adding/removing payslips, and recalculating totals.

### Requirement 6: Calculation Engine — Earning Lines

**User Story:** As a business owner, I want the system to calculate overtime earnings automatically using configurable multipliers, so that I can handle various overtime scenarios without manual computation.

#### Acceptance Criteria

1. WHEN a PayslipEarningLine has EarningType of Overtime, THE Calculation_Engine SHALL compute Amount as OvertimeHours multiplied by Employee HourlyRate multiplied by OvertimeMultiplier
2. THE Calculation_Engine SHALL use a default OvertimeMultiplier of 1.5 when no explicit value is provided
3. THE Calculation_Engine SHALL allow OvertimeMultiplier values between 1.0 and 4.0 inclusive
4. WHEN a PayslipEarningLine has an EarningType other than Overtime, THE Calculation_Engine SHALL use the manually entered Amount value
5. THE Payroll_System SHALL support multiple PayslipEarningLines per Payslip (e.g., Part-time base plus Paid Holidays on the same payslip)
6. THE Calculation_Engine SHALL compute TotalEarnings as the sum of all PayslipEarningLine Amount values for a Payslip

### Requirement 7: Calculation Engine — Deductions

**User Story:** As a business owner, I want deductions calculated automatically against the full gross salary using historically accurate rates, so that payslips comply with regulatory requirements.

#### Acceptance Criteria

1. THE Calculation_Engine SHALL apply each applicable DeductionType to the TotalEarnings (full gross) of the Payslip
2. WHEN calculating a deduction, THE Calculation_Engine SHALL look up the DeductionRateHistory record where EffectiveFromUtc is less than or equal to the period date AND EffectiveToUtc is NULL or greater than the period date
3. WHEN a DeductionType IsPercentage is true, THE Calculation_Engine SHALL compute CalculatedAmount as BaseAmount multiplied by Rate divided by 100
4. THE Calculation_Engine SHALL compute TotalEmployeeDeductions as the sum of all CalculatedAmount values for DeductionTypes with DeductionCategoryTypeId = 1 (Deduction).
5. THE Calculation_Engine SHALL compute TotalEmployerContributions as the sum of all CalculatedAmount values for DeductionTypes with DeductionCategoryTypeId = 2 (Contribution).
6. THE Calculation_Engine SHALL compute NetSalary as TotalEarnings minus TotalEmployeeDeductions
7. THE Calculation_Engine SHALL store the DeductionRateHistoryId on each PayslipDeductionLine to preserve the exact rate used in the calculation
8. IF no valid DeductionRateHistory record exists for the period date, THEN THE Calculation_Engine SHALL raise a validation error and prevent payslip generation for the affected employee
9. THE Calculation_Engine SHALL round each deduction CalculatedAmount to 2 decimal places using standard financial rounding (MidpointRounding.AwayFromZero). Rounding SHALL be applied per deduction line, not on the final total.
10. THE Calculation_Engine SHALL use the first day of the payslip period month as the reference date for deduction rate history lookups (e.g., 2027-07-01 for July 2027).

### Requirement 8: Batch Payslip Generation

**User Story:** As a business owner, I want to generate payslips for all active employees in a period at once, so that monthly payroll processing is efficient.

#### Acceptance Criteria

1. WHEN a batch generation is initiated for a PayslipPeriod, THE Payroll_System SHALL generate a Payslip for each active Employee in the business who does not have an EndDate before the period start
2. THE Payroll_System SHALL present a preview of all generated payslips before requiring confirmation
3. WHEN the user confirms batch generation, THE Payroll_System SHALL transition the PayslipPeriod status from Draft to Preview
4. THE Payroll_System SHALL calculate each Payslip using the Calculation_Engine with the employee's configured earning lines and all applicable deductions
5. IF any Employee fails validation during batch generation (e.g., missing HourlyRate for overtime), THEN THE Payroll_System SHALL report the specific validation error per Employee and exclude the failed Employee from the batch without blocking generation of valid payslips
6. THE Payroll_System SHALL display a summary of the batch generation showing: total employees processed, total payroll cost, total employer contributions, and any employees excluded with reasons

### Requirement 9: Individual Payslip View

**User Story:** As a business owner, I want to view a full breakdown of each payslip, so that I can verify the calculations for individual employees.

#### Acceptance Criteria

1. THE Payroll_System SHALL display each Payslip with a breakdown of all PayslipEarningLines grouped by EarningType
2. THE Payroll_System SHALL display all PayslipDeductionLines separated into Employee Deductions and Employer Contributions sections
3. THE Payroll_System SHALL display the computed values: TotalEarnings, TotalEmployeeDeductions, NetSalary, TotalEmployerContributions, and Total Cost to Business (TotalEarnings plus TotalEmployerContributions)
4. WHEN a Payslip has ManagerNotes, THE Payroll_System SHALL display the notes in a clearly labelled section
5. THE Payroll_System SHALL display the rate and base amount used for each deduction line alongside the calculated amount

### Requirement 10: Manager Notes

**User Story:** As a business owner, I want to add informational notes to a payslip, so that I can record contextual information such as expense reimbursements or special circumstances.

#### Acceptance Criteria

1. THE Payroll_System SHALL allow a ManagerNotes field of up to 2000 characters on each Payslip
2. THE Payroll_System SHALL treat ManagerNotes as informational only — notes SHALL NOT affect any calculation performed by the Calculation_Engine
3. WHEN a ManagerNotes value is saved, THE Payroll_System SHALL preserve the full text without truncation up to the 2000 character limit

### Requirement 11: Plan Permission Gate

**User Story:** As a platform operator, I want the payroll module restricted to Enterprise-tier subscribers, so that it is accessible only to businesses with the appropriate subscription level.

#### Acceptance Criteria

1. THE Payroll_System SHALL require the `payroll` module key in the business subscription to grant access
2. WHEN a user without the `payroll` module key attempts to access any payroll route, THE Payroll_System SHALL return an authorisation denial and redirect to the subscription upgrade page
3. THE Payroll_System SHALL enforce the ModuleAccess_Gate on all payroll controllers using the ModuleAccess attribute
4. THE Payroll_System SHALL apply the standard [Authorize] attribute on all payroll controller actions in addition to the ModuleAccess_Gate

### Requirement 12: Data Schema and Integrity

**User Story:** As a developer, I want all payroll data stored in a dedicated schema with proper referential integrity, so that the data model is maintainable and consistent with platform conventions.

#### Acceptance Criteria

1. THE Payroll_System SHALL store all payroll tables in the `[payroll]` schema
2. THE Payroll_System SHALL enforce referential integrity via foreign keys: Employee.DepartmentId → Department.Id, Payslip.EmployeeId → Employee.Id, Payslip.PayslipPeriodId → PayslipPeriod.Id, PayslipEarningLine.PayslipId → Payslip.Id, PayslipEarningLine.EarningTypeId → EarningType.Id, PayslipDeductionLine.PayslipId → Payslip.Id, PayslipDeductionLine.DeductionTypeId → DeductionType.Id, PayslipDeductionLine.DeductionRateHistoryId → DeductionRateHistory.Id, DeductionRateHistory.DeductionTypeId → DeductionType.Id
3. THE Payroll_System SHALL use DECIMAL(18,2) for all monetary values (BaseSalary, HourlyRate, Amount, BaseAmount, Rate, CalculatedAmount, TotalEarnings, TotalEmployeeDeductions, NetSalary, TotalEmployerContributions)
4. THE Payroll_System SHALL use DECIMAL(4,2) for OvertimeMultiplier and DECIMAL(6,2) for OvertimeHours
5. EACH table in the payroll schema SHALL include a CreatedAtUtc column of type DATETIME NOT NULL with a default of GETUTCDATE()
6. THE Payroll_System SHALL use EUR as the sole currency for all monetary calculations in Phase A
7. THE Payroll_System SHALL use a `SalaryType` lookup table with TINYINT primary key. The Employee.SalaryTypeId column SHALL reference SalaryType.Id via a foreign key.

### Requirement 13: Employee Default Earnings Configuration

**User Story:** As a business owner, I want to configure recurring earning lines per employee, so that batch payslip generation automatically includes the correct earnings without manual entry each month.

#### Acceptance Criteria

1. THE Payroll_System SHALL provide an EmployeeDefaultEarnings configuration per Employee, defining one or more recurring earning lines.
2. EACH EmployeeDefaultEarnings record SHALL specify: EarningTypeId, Amount (or OvertimeHours + OvertimeMultiplier for Overtime type), and an optional Description.
3. WHEN batch payslip generation is initiated, THE Payroll_System SHALL use the EmployeeDefaultEarnings configuration to pre-populate earning lines for each employee.
4. THE user SHALL be able to override or supplement the default earning lines during the preview/edit phase before finalisation.
5. WHEN an Employee has no EmployeeDefaultEarnings configured, THE Payroll_System SHALL create a single default earning line using the Employee's BaseSalary as a Basic type earning.
6. THE Payroll_System SHALL allow a business owner to add, edit, and remove EmployeeDefaultEarnings records from the Employee profile view.

### Requirement 14: Payslip PDF Generation, Email, and Signature

**User Story:** As a business owner, I want to generate a PDF payslip for each employee and send it by email with an optional signature, so that employees receive professional documentation of their pay.

#### Acceptance Criteria

1. THE Payroll_System SHALL generate a branded A4 PDF payslip for each finalised Payslip, following the same structure as the reference CSV layout (Earnings, Deductions, Contributions sections with totals).
2. THE PDF SHALL include: business name and address, employee name and details, period (month/year), all earning lines, all deduction lines, all contribution lines, Net Salary, and Total Cost to Business.
3. THE Payroll_System SHALL provide a "Download PDF" action on the individual payslip detail view.
4. THE Payroll_System SHALL provide a "Send by Email" action that sends the PDF payslip as an attachment to the employee's configured email address.
5. WHEN sending by email, THE Payroll_System SHALL validate that the employee has a non-empty Email field before allowing the send action.
6. THE Payroll_System SHALL optionally append the business signature (from the existing Signature module) to the PDF payslip when the business has a signature configured and the user chooses to include it.
7. THE "Send by Email" action SHALL support batch sending for all payslips in a finalised period (send to all employees with valid email addresses).
8. THE Payroll_System SHALL log each email send action for audit purposes (who sent, when, to which employee).
