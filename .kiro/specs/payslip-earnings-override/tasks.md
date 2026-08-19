# Implementation Plan: Payslip Earnings Override & Salary Register

## Overview

This plan implements two Payroll enhancements: (1) Editable Earnings at Batch Generate Preview — allowing payroll managers to override earning lines per employee before confirming batch generation, with server-side recalculation via the existing `PayslipCalculationOrchestrator`; and (2) Salary Register Page — a new page at `/Payroll/SalaryRegister` with employee salary overview, department/status filtering, and inline quick-edit of BaseSalary. Implementation proceeds bottom-up: DTOs → Service methods → Controller endpoints → Views → Client-side JS.

## Tasks

- [x] 1. Create DTOs and request/response models
  - [x] 1.1 Create EarningLineOverride DTO and RecalculateEmployeeRequest
    - Create `EarningLineOverride` class with properties: EarningTypeId (int), Description (string?), Amount (decimal), OvertimeMultiplier (decimal?), OvertimeHours (decimal?)
    - Create `RecalculateEmployeeRequest` class with properties: EmployeeId (int), PeriodId (int), EarningLines (List<EarningLineOverride>)
    - Create `RecalculationResult` class with properties: Success (bool), Error (string?), TotalEarnings (decimal), TotalEmployeeDeductions (decimal), NetSalary (decimal), TotalEmployerContributions (decimal)
    - _Requirements: 4.1, 8.1, 8.3_

  - [x] 1.2 Create ConfirmBatchWithOverridesRequest and EmployeeEarningsOverride DTOs
    - Create `EmployeeEarningsOverride` class with properties: EmployeeId (int), EarningLines (List<EarningLineOverride>)
    - Create `ConfirmBatchWithOverridesRequest` class with properties: PeriodId (int), Overrides (List<EmployeeEarningsOverride>)
    - _Requirements: 5.1, 8.1_

  - [x] 1.3 Create SalaryRegisterViewModel and SalaryRegisterRow DTOs
    - Create `SalaryRegisterRow` class with properties: EmployeeId (int), EmployeeName (string), DepartmentName (string?), SalaryType (string — "Monthly" or "Hourly"), BaseSalary (decimal), HourlyRate (decimal?), IsActive (bool)
    - Create `SalaryRegisterViewModel` class with properties: Employees (List<SalaryRegisterRow>), Departments (List<DepartmentDto>), SelectedDepartmentId (int?), SelectedIsActive (bool?), TotalEmployees (int), TotalMonthlyPayroll (decimal)
    - Create `UpdateBaseSalaryRequest` class with properties: EmployeeId (int), NewSalary (decimal)
    - _Requirements: 9.3, 9.5, 10.1, 11.1_

- [x] 2. Service layer — Earnings recalculation and batch confirm with overrides
  - [x] 2.1 Implement RecalculateEmployeeAsync in PayrollService
    - Add method `RecalculateEmployeeAsync(int employeeId, int periodId, int businessId, List<EarningLineOverride> overriddenLines)` to IPayrollService and PayrollService
    - Fetch the employee record via existing repository method to provide the orchestrator with employee-specific flags (IsPayeApplicable, deduction configuration)
    - Fetch the period record to get the period date for deduction rate lookups
    - Load applicable deductions with rates for the business (reuse existing logic from GeneratePayslipsPreviewAsync)
    - Build calculation input from the overridden earning lines (map EarningLineOverride to the orchestrator's expected EarningLineInput format)
    - Call `PayslipCalculationOrchestrator.CalculateWithPayeAsync` with the constructed input and employee.IsPayeApplicable
    - Return `RecalculationResult` with TotalEarnings = sum of earning line amounts, plus deductions/net/employer cost from orchestrator result
    - Wrap in try/catch (Exception ex), return RecalculationResult with Success=false and Error message on failure
    - _Requirements: 4.1, 8.2, 8.3_

  - [ ]* 2.2 Write property test for recalculation arithmetic invariant
    - **Property 2: Recalculation Arithmetic Invariant**
    - Test: For any valid set of earning line amounts, TotalEarnings equals sum of all amounts, and NetSalary equals TotalEarnings minus TotalEmployeeDeductions
    - **Validates: Requirements 4.1**

  - [x] 2.3 Implement ConfirmBatchGenerationWithOverridesAsync in PayrollService
    - Add method `ConfirmBatchGenerationWithOverridesAsync(int periodId, int businessId, List<EmployeeEarningsOverride> overrides)` to IPayrollService and PayrollService
    - Extract shared payslip creation logic from the existing `ConfirmBatchGenerationAsync` into a private helper method (e.g., `CreatePayslipRecordsAsync`) that both methods can call — avoid duplicating the payslip record creation code
    - For each employee: if an override exists in the overrides list, use overridden earning lines; otherwise use default earnings/BaseSalary (same fallback logic as GeneratePayslipsPreviewAsync)
    - Call `PayslipCalculationOrchestrator.CalculateWithPayeAsync` per employee with the appropriate earning lines
    - Create payslip records using the shared helper
    - Must NOT modify Employee.BaseSalary or EmployeeDefaultEarnings records
    - Wrap in try/catch (Exception ex), return ServiceResult
    - _Requirements: 5.1, 5.2, 5.3, 8.2_

  - [ ]* 2.4 Write property test for override does not mutate permanent data
    - **Property 4: Override Does Not Mutate Permanent Data**
    - Test: For any employee and any set of earning line overrides applied and confirmed, the employee's BaseSalary and EmployeeDefaultEarnings records remain unchanged
    - **Validates: Requirements 5.2, 5.3**

- [x] 3. Service layer — Salary Register
  - [x] 3.1 Implement GetSalaryRegisterAsync in PayrollService
    - Add method `GetSalaryRegisterAsync(int businessId, int? departmentId, bool? isActive)` to IPayrollService and PayrollService
    - Query Employees table joined with Departments using full table names (no aliases)
    - Filter by DepartmentId when departmentId is not null; filter by IsActive when isActive is not null
    - Default isActive to true when null (initial page load shows active employees)
    - Map SalaryTypeId: 1 → "Monthly", 2 → "Hourly"
    - Order results by EmployeeName ascending
    - Compute TotalEmployees (count of filtered results) and TotalMonthlyPayroll (sum of BaseSalary where SalaryTypeId = 1 AND IsActive = true in the filtered set)
    - Include list of all departments for filter dropdown
    - Wrap in try/catch (Exception ex)
    - _Requirements: 9.3, 9.4, 9.5, 10.1, 10.2, 10.3, 10.4, 10.5_

  - [ ]* 3.2 Write property test for filter correctness
    - **Property 7: Filter Correctness**
    - Test: For any department filter and status filter, every employee in the result set has matching DepartmentId (when not "All") and matching IsActive (when not "All")
    - **Validates: Requirements 10.2, 10.3**

  - [ ]* 3.3 Write property test for salary register ordering
    - **Property 8: Salary Register Ordering**
    - Test: For any set of employees returned, the result is ordered alphabetically by EmployeeName ascending
    - **Validates: Requirements 9.4**

  - [ ]* 3.4 Write property test for summary totals
    - **Property 6: Summary Totals Equal Sum of Visible Employees**
    - Test: TotalMonthlyPayroll equals sum of BaseSalary for all filtered employees where SalaryTypeId = 1 AND IsActive = true
    - **Validates: Requirements 9.5, 10.5**

  - [x] 3.5 Implement UpdateBaseSalaryAsync in PayrollService
    - Add method `UpdateBaseSalaryAsync(int employeeId, int businessId, decimal newSalary)` to IPayrollService and PayrollService
    - Validate newSalary > 0; return ServiceResult.Fail if invalid
    - Update Employee.BaseSalary via existing PayrollRepository update method
    - Wrap in try/catch (Exception ex), return ServiceResult
    - _Requirements: 11.2, 11.3_

  - [ ]* 3.6 Write property test for quick-edit salary validation
    - **Property 9: Quick-Edit Salary Validation**
    - Test: For any input value, validation accepts it if and only if it is a finite numeric value strictly greater than zero
    - **Validates: Requirements 11.2**

  - [ ]* 3.7 Write property test for quick-edit salary persistence
    - **Property 10: Quick-Edit Salary Persistence**
    - Test: For any valid positive salary value confirmed, querying the employee's BaseSalary after the operation returns that exact value
    - **Validates: Requirements 11.3**

- [x] 4. Checkpoint — Ensure all service layer code compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Controller layer — Editable Earnings endpoints
  - [x] 5.1 Implement AxPostRecalculateEmployee endpoint in PayrollController
    - Add `[HttpPost] [ValidateAntiForgeryToken] AxPostRecalculateEmployee([FromBody] RecalculateEmployeeRequest request)` action
    - Call `PayrollService.RecalculateEmployeeAsync` with request parameters and businessId from session/claims
    - Return `Json(new { success, totalEarnings, totalEmployeeDeductions, netSalary, totalEmployerContributions, error })`
    - Catch (Exception ex), return `Json(new { success = false, message = "An unexpected error occurred." })`
    - _Requirements: 8.1, 8.3, 8.4_

  - [x] 5.2 Implement AxPostConfirmBatchWithOverrides endpoint in PayrollController
    - Add `[HttpPost] [ValidateAntiForgeryToken] AxPostConfirmBatchWithOverrides([FromBody] ConfirmBatchWithOverridesRequest request)` action
    - Call `PayrollService.ConfirmBatchGenerationWithOverridesAsync` with request parameters and businessId
    - Return `Json(new { success, message })`
    - Catch (Exception ex), return `Json(new { success = false, message = "An unexpected error occurred." })`
    - _Requirements: 5.1, 8.1_

- [x] 6. Controller layer — Salary Register endpoints
  - [x] 6.1 Implement SalaryRegister action in PayrollController
    - Add `[HttpGet] SalaryRegister(int? departmentId, bool? isActive)` action at route /Payroll/SalaryRegister
    - Call `PayrollService.GetSalaryRegisterAsync` and return the SalaryRegister view with the ViewModel
    - _Requirements: 9.1, 9.2_

  - [x] 6.2 Implement AxPostUpdateBaseSalary endpoint in PayrollController
    - Add `[HttpPost] [ValidateAntiForgeryToken] AxPostUpdateBaseSalary([FromBody] UpdateBaseSalaryRequest request)` action
    - Call `PayrollService.UpdateBaseSalaryAsync` with request parameters and businessId
    - Return `Json(new { success, message })`
    - Catch (Exception ex), return `Json(new { success = false, message = "An unexpected error occurred." })`
    - _Requirements: 11.3, 11.6_

  - [x] 6.3 Implement AxGetSalaryRegisterData endpoint for AJAX filter refreshes
    - Add `[HttpGet] AxGetSalaryRegisterData(int? departmentId, bool? isActive)` action
    - Call `PayrollService.GetSalaryRegisterAsync` and return JSON with employees array, totalEmployees, totalMonthlyPayroll
    - Catch (Exception ex), return `Json(new { success = false, message = "An unexpected error occurred." })`
    - _Requirements: 10.2, 10.3, 10.5_

- [x] 7. Checkpoint — Ensure controller layer compiles
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Views — BatchGenerate.cshtml enhancements (Editable Earnings)
  - [x] 8.1 Add Edit button column to employee preview table
    - Add a new actions column at the end of each employee row in BatchGenerate.cshtml
    - Render an "Edit" button (secondary style) in each row with `data-employee-id` attribute
    - When an employee has an active override, add `modified` CSS class to the row and display a visual indicator badge
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 8.2 Create Edit Earnings Modal markup
    - Add modal HTML to BatchGenerate.cshtml with: employee name header, earning lines container (each line shows type name, description, editable amount input), Save button, Cancel button, and Reset to Default button (visible only when an override is already applied)
    - Amount inputs: type="number", min="0", step="0.01"
    - Include inline validation error display area per input field
    - Reset to Default button: removes the employee's entry from earningsOverrides Map, triggers recalculation with original defaults, closes modal
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2_

- [x] 9. Views — SalaryRegister.cshtml (new page)
  - [x] 9.1 Create SalaryRegister.cshtml view
    - Create new Razor view at Portal.Web/Views/Payroll/SalaryRegister.cshtml
    - Add topbar with heading "Salary Register" and description
    - Add filter panel in `.glass.card-pad` (margin-bottom:22px) with: Department dropdown (All Departments + each department), Status dropdown (Active [default], Inactive, All)
    - Add main content `.glass.card-pad` with table: columns Employee Name, Department, Salary Type, Base Salary (€), Hourly Rate (€), Status
    - Add summary footer row showing total employees count and total monthly payroll cost with clarifying text "(active monthly employees only)"
    - Make BaseSalary cell clickable (cursor:pointer, data-employee-id, data-current-salary attributes)
    - Order employees by name ascending
    - Display empty state message when no employees match filters
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 10.1, 10.4, 11.1_

  - [x] 9.2 Add "Salary Register" item to Payroll sidebar navigation
    - Position between "Employees" and "Periods" in the sidebar
    - _Requirements: 9.2_

- [x] 10. Client-side JavaScript — Editable Earnings (BatchGenerate.cshtml)
  - [x] 10.1 Implement client-side override state management
    - Create `earningsOverrides` Map keyed by employeeId
    - Each entry stores: earningLines array and recalculation result
    - On page load, initialize from server-rendered earning line data (output each employee's earning lines as a JSON object in a `<script>` block so JS can populate the modal without additional AJAX calls)
    - _Requirements: 5 AC4, 7.1_

  - [x] 10.2 Implement Edit button click → open modal with earning lines
    - On Edit button click: get employeeId, check if override exists in Map
    - If override exists: populate modal with overridden amounts (not original defaults)
    - If no override: populate with original earning lines (DefaultEarnings or single BaseSalary line)
    - Show modal with employee name and populated earning line inputs
    - _Requirements: 2.1, 2.3, 2.4, 7.1_

  - [x] 10.3 Implement modal validation and Save flow
    - On input change: validate numeric ≥ 0, show inline error for negatives, treat empty as 0
    - On Save click: BlockUI.show → collect earning lines → fetch AxPostRecalculateEmployee → BlockUI.hide
    - On success: store override in Map, update employee row (Total Earnings, Deductions, Net Salary, Employer Cost), add modified indicator, close modal, update summary cards
    - On error: Swal.fire error, preserve previous values
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 8.4_

  - [x] 10.4 Implement Cancel button behavior
    - On Cancel click: close modal, make no changes to preview state
    - _Requirements: 6.1, 6.2_

  - [x] 10.5 Implement Confirm Batch with overrides
    - On batch confirm: if earningsOverrides Map has entries, serialize overrides and call AxPostConfirmBatchWithOverrides instead of the standard confirm endpoint
    - If no overrides exist, call the standard AxPostConfirmBatch endpoint (existing behaviour)
    - BlockUI.show → fetch → BlockUI.hide → Swal.fire success/error
    - _Requirements: 5.1_

  - [ ]* 10.6 Write property test for earning line validation
    - **Property 1: Earning Line Validation Accepts Only Non-Negative Numerics**
    - Test: For any input value, validation accepts it if and only if it is a finite numeric value ≥ 0
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4**

- [x] 11. Client-side JavaScript — Salary Register (SalaryRegister.cshtml)
  - [x] 11.1 Implement filter change → AJAX table refresh
    - On Department or Status dropdown change: BlockUI.show → fetch AxGetSalaryRegisterData with selected filters → BlockUI.hide
    - On success: re-render table body with new data, update summary footer totals
    - On error: Swal.fire error, preserve previous table state
    - _Requirements: 10.2, 10.3, 10.5_

  - [x] 11.2 Implement Quick Edit Salary via SweetAlert2
    - On BaseSalary cell click: open Swal.fire input modal pre-filled with current salary
    - Add inputValidator: reject if value ≤ 0 or non-numeric
    - On confirm: BlockUI.show → fetch AxPostUpdateBaseSalary → BlockUI.hide → Swal.fire success → update cell value in DOM without full reload
    - On cancel: no changes
    - On error: Swal.fire error, cell value unchanged
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

- [x] 12. Final checkpoint — Ensure all tests pass and features integrate correctly
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties (FsCheck.Xunit)
- No new database tables required — both features use existing Employee, EmployeeDefaultEarnings, EarningType, and Department data
- All SQL uses full table names (no aliases) per project standards
- All catch blocks use `catch (Exception ex)` per coding golden rules
- All AJAX controller methods use AxPost/AxGet prefix convention
- UI follows BlockUI + SweetAlert2 pattern (no native alerts)
- Earnings overrides are ephemeral — stored client-side in a JS Map, passed in AJAX calls, never persisted to database
- The existing PayslipCalculationOrchestrator.CalculateWithPayeAsync is reused for identical deduction/PAYE logic
- Salary Register follows the server-rendered page + AJAX filter pattern consistent with Employees and Periods pages

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "2.3", "3.1", "3.5"] },
    { "id": 2, "tasks": ["2.2", "2.4", "3.2", "3.3", "3.4", "3.6", "3.7"] },
    { "id": 3, "tasks": ["5.1", "5.2", "6.1", "6.2", "6.3"] },
    { "id": 4, "tasks": ["8.1", "8.2", "9.1", "9.2"] },
    { "id": 5, "tasks": ["10.1", "10.2", "10.3", "10.4", "10.5", "10.6", "11.1", "11.2"] }
  ]
}
```
