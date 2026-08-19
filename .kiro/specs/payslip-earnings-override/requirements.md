# Requirements Document

## Introduction

This feature delivers two Payroll enhancements:

1. **Editable Earnings at Batch Generate Preview** — Adds the ability to override individual employee earnings on the Batch Generate preview page before confirming payslip generation. If an employee's salary needs a one-off adjustment for a particular period (e.g., unpaid leave, late start), the user can modify the calculated earnings before committing. The override is per-period only and does not alter the employee's permanent salary record.

2. **Salary Register Page** — A dedicated view at `/Payroll/SalaryRegister` showing each employee's monthly salary information in a clean grid format. Allows payroll managers to review and compare pay levels across the organisation, filter by department or status, and quickly edit base salaries.

## Glossary

- **Batch_Generate_Preview**: The page (`/Payroll/BatchGenerate`) that displays calculated payslip data for all active employees before the user confirms generation.
- **Earnings_Override**: A temporary modification to one or more earning line amounts for a specific employee within the current payslip period. The override does not persist beyond this batch generation session.
- **Earning_Line**: A single row in an employee's earnings breakdown, identified by an earning type (e.g., Basic Salary, Overtime, Allowance) and an amount.
- **Edit_Earnings_Modal**: The modal dialog that opens when a user clicks the Edit button on an employee row, displaying that employee's earning lines for modification.
- **Calculation_Engine**: The `PayslipCalculationOrchestrator` service that computes deductions, net salary, and employer cost from a set of earning line inputs.
- **Preview_Session**: The in-memory state of the Batch Generate preview, including any overrides the user has applied, that exists from page load until batch confirmation or page navigation.
- **BaseSalary**: The permanent monthly salary amount stored on the employee record.
- **Default_Earnings**: Pre-configured earning line breakdowns assigned to an employee, used as the basis for payslip generation when they exist.
- **Salary_Register**: A dedicated page (`/Payroll/SalaryRegister`) that displays a tabular overview of all employees with their salary information, filterable by department and status.
- **Salary_Type**: The pay method for an employee — Monthly (fixed BaseSalary per month) or Hourly (paid by HourlyRate × hours worked).
- **Quick_Edit_Salary**: The ability to click on an employee's salary value in the Salary Register and update it inline or via modal, persisting the change to the employee's permanent BaseSalary.

## Requirements

### Requirement 1: Display Edit Action per Employee Row

**User Story:** As a payroll manager, I want to see an Edit button on each employee row in the Batch Generate preview, so that I can initiate an earnings adjustment for a specific employee.

#### Acceptance Criteria

1. WHEN the Batch_Generate_Preview page loads with valid payslip data, THE Batch_Generate_Preview SHALL display an "Edit" button in each employee row.
2. THE Batch_Generate_Preview SHALL render the Edit button in a dedicated actions column at the end of each employee row.
3. WHILE the employee row is in its default (unmodified) state, THE Edit button SHALL be visually neutral (secondary style).
4. WHILE the employee row has an active Earnings_Override applied, THE Batch_Generate_Preview SHALL display a visual indicator (highlighted row or badge) to signal the row has been modified.

### Requirement 2: Open Edit Earnings Modal

**User Story:** As a payroll manager, I want to click the Edit button and see a modal with the employee's current earning lines, so that I can review and modify individual amounts.

#### Acceptance Criteria

1. WHEN the user clicks the Edit button for an employee, THE Edit_Earnings_Modal SHALL open displaying that employee's name and their current Earning_Line items for this period.
2. THE Edit_Earnings_Modal SHALL display each Earning_Line with the earning type name, description, and a pre-filled editable amount field.
3. WHEN the employee has Default_Earnings configured, THE Edit_Earnings_Modal SHALL display those earning lines with their current amounts.
4. WHEN the employee has no Default_Earnings, THE Edit_Earnings_Modal SHALL display a single "Basic Salary" earning line pre-filled with the employee's BaseSalary amount.
5. THE Edit_Earnings_Modal SHALL include a Save button and a Cancel button.

### Requirement 3: Validate Earning Line Amounts

**User Story:** As a payroll manager, I want the system to validate my earnings input, so that I cannot submit invalid or negative values.

#### Acceptance Criteria

1. WHEN the user modifies an Earning_Line amount, THE Edit_Earnings_Modal SHALL accept only numeric values greater than or equal to zero.
2. IF the user enters a negative value for an Earning_Line amount, THEN THE Edit_Earnings_Modal SHALL display an inline validation error and prevent saving.
3. IF the user leaves an Earning_Line amount field empty, THEN THE Edit_Earnings_Modal SHALL treat the value as zero.
4. THE Edit_Earnings_Modal SHALL allow a total earnings value of zero (e.g., employee on full unpaid leave).

### Requirement 4: Recalculate Preview on Override Save

**User Story:** As a payroll manager, I want the system to recalculate deductions, net salary, and employer cost when I save modified earnings, so that the preview reflects accurate totals.

#### Acceptance Criteria

1. WHEN the user saves modified earning lines in the Edit_Earnings_Modal, THE Calculation_Engine SHALL recalculate deductions, net salary, and employer contributions using the overridden earning amounts.
2. WHEN recalculation completes successfully, THE Batch_Generate_Preview SHALL update the employee's row with the new Total Earnings, Deductions, Net Salary, and Employer Cost values.
3. WHEN recalculation completes successfully, THE Batch_Generate_Preview SHALL update the summary cards (Total Payroll Cost, Total Employer Contributions) to reflect the changed totals.
4. IF recalculation fails for the overridden earnings, THEN THE Batch_Generate_Preview SHALL display an error message and retain the previous values for that employee.

### Requirement 5: Persist Overrides Only for Current Batch Confirmation

**User Story:** As a payroll manager, I want the overrides to apply only to this payslip period's generation, so that the employee's permanent salary record is not affected.

#### Acceptance Criteria

1. WHEN the user confirms batch generation with overrides applied, THE Batch_Generate_Preview SHALL use the overridden earning line amounts (not the original Default_Earnings or BaseSalary) when creating payslip records.
2. THE Batch_Generate_Preview SHALL NOT modify the employee's BaseSalary field in the permanent employee record.
3. THE Batch_Generate_Preview SHALL NOT modify the employee's Default_Earnings records.
4. WHEN the user navigates away from the Batch_Generate_Preview page without confirming, THE Preview_Session SHALL discard all Earnings_Override data.

### Requirement 6: Cancel Override Without Saving

**User Story:** As a payroll manager, I want to cancel my edits in the modal without applying them, so that I can abandon changes if needed.

#### Acceptance Criteria

1. WHEN the user clicks Cancel in the Edit_Earnings_Modal, THE Edit_Earnings_Modal SHALL close without applying any changes to the preview.
2. WHEN the user clicks Cancel, THE Batch_Generate_Preview SHALL retain the previously displayed values for that employee (either original or a previously saved override).

### Requirement 7: Re-edit a Previously Overridden Employee

**User Story:** As a payroll manager, I want to re-open the modal for an employee I already overrode, so that I can make further adjustments before confirming.

#### Acceptance Criteria

1. WHEN the user clicks Edit on an employee row that already has an Earnings_Override applied, THE Edit_Earnings_Modal SHALL display the overridden amounts (not the original Default_Earnings or BaseSalary).
2. WHEN the user saves new values for a previously overridden employee, THE Calculation_Engine SHALL recalculate using the latest overridden values.

### Requirement 8: Server-Side Recalculation Endpoint

**User Story:** As a payroll manager, I want the recalculation to use the same server-side engine as the original preview, so that deduction rules and tax calculations remain accurate.

#### Acceptance Criteria

1. THE Batch_Generate_Preview SHALL send the overridden earning lines to a dedicated AJAX endpoint for recalculation.
2. THE Calculation_Engine SHALL apply the same deduction rules, PAYE tax logic, and employer contribution formulas to the overridden earnings as it applies to the original earnings.
3. WHEN the AJAX recalculation endpoint receives earning lines, THE Calculation_Engine SHALL return the recalculated Total Earnings, Total Deductions, Net Salary, and Employer Contributions for the employee.
4. IF the AJAX request fails due to a network or server error, THEN THE Batch_Generate_Preview SHALL display an error notification and preserve the pre-existing values.


### Requirement 9: Salary Register Page and Navigation

**User Story:** As a payroll manager, I want a dedicated Salary Register page accessible from the Payroll sidebar, so that I can quickly view all employees' monthly salary information in one place.

#### Acceptance Criteria

1. THE Payroll_Controller SHALL expose a SalaryRegister action at the route /Payroll/SalaryRegister that renders the Salary_Register page.
2. THE Payroll sidebar navigation SHALL include a "Salary Register" item positioned between "Employees" and "Periods".
3. THE Salary_Register page SHALL display a table with columns: Employee Name, Department, Salary Type (Monthly/Hourly), Base Salary (€), Hourly Rate (€), Status (Active/Inactive).
4. THE Salary_Register page SHALL display all employees for the authenticated business, ordered by name ascending.
5. THE Salary_Register page SHALL show a summary footer row with: total number of employees and total monthly payroll cost (sum of BaseSalary for all active monthly employees).

### Requirement 10: Salary Register Filtering

**User Story:** As a payroll manager, I want to filter the Salary Register by department and status, so that I can focus on specific groups of employees.

#### Acceptance Criteria

1. THE Salary_Register page SHALL display a filter panel with a Department dropdown (listing all departments plus "All Departments" option) and a Status dropdown (Active, Inactive, All).
2. WHEN the user selects a department filter, THE Salary_Register page SHALL display only employees belonging to the selected department.
3. WHEN the user selects a status filter, THE Salary_Register page SHALL display only employees matching the selected status.
4. THE Salary_Register page SHALL default to showing Active employees across All Departments.
5. WHEN filters change, THE summary footer SHALL update to reflect the filtered totals.

### Requirement 11: Quick Edit Salary from Register

**User Story:** As a payroll manager, I want to quickly update an employee's base salary directly from the Salary Register, so that I can make salary adjustments without navigating to the full employee form.

#### Acceptance Criteria

1. WHEN the user clicks on an employee's Base Salary value in the Salary_Register, THE Salary_Register SHALL open a SweetAlert2 modal pre-filled with the current BaseSalary amount.
2. THE Quick_Edit_Salary modal SHALL validate that the entered value is a positive number.
3. WHEN the user confirms the new salary value, THE Payroll_Service SHALL update the employee's BaseSalary in the permanent employee record.
4. WHEN the update succeeds, THE Salary_Register SHALL refresh the row to show the new value without a full page reload.
5. WHEN the user cancels the modal, THE Salary_Register SHALL make no changes.
6. THE Quick_Edit_Salary SHALL use the standard BlockUI + SweetAlert2 AJAX pattern.
