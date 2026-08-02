# Payroll Phase A — Testing Scenarios

## Prerequisites
- SQL migrations 166 and 167 applied to Portal database
- User logged in with Enterprise tier subscription (payroll module access)
- SuperAdmin account available for earning/deduction type template management

## Scenario 1: Department Management

### Steps
1. Navigate to /Payroll/Departments
2. Create department "Kitchen" → success
3. Create department "Service" → success
4. Try creating "Kitchen" again → "A department with this name already exists"
5. Edit "Service" to "Front of House" → success
6. Deactivate "Front of House" (no employees) → success, shows Inactive

### Edge Cases
7. Assign employee to department, then try deactivating → "Cannot deactivate a department that has active employees"

## Scenario 2: Employee Management

### Steps
1. Navigate to /Payroll/Employees → Add Employee
2. Fill: Name "Giannis Papamichael", SIN "1274909", ID "1034896", Start Date 2024-03-01, Salary Type Full-time, Base Salary €1,000, Hourly Rate €10, Department Kitchen
3. Save → success, appears in list
4. Edit → change Position to "Head Barista" → Save
5. Deactivate → shows Inactive with faded row

### Validation
6. Missing Name → error
7. Duplicate SIN → "An employee with this social insurance number already exists"
8. Duplicate ID Number → error

## Scenario 3: Default Earnings

### Steps
1. Edit Giannis → Default Earnings section
2. Add: Basic Salary €1,000
3. Add: Overtime, Multiplier 1.5, Hours 10
4. Save → verify persisted on reload

## Scenario 4: Deduction Template Import

### Steps
1. Navigate to /Payroll/DeductionConfig → empty state
2. Click "Import Country Template" → modal shows 7 Cyprus templates
3. Select all → Import → success
4. Verify two sections: Employee Deductions (SI 8.80%, GESY 2.65%) and Employer Contributions (SI 8.80%, Redundancy 1.20%, Industrial Training 0.50%, Social Cohesion 2.00%, GESY 2.90%)

### Rate History
5. Click "View History" on Social Insurance → shows 8.80% from 1 Jan 2024
6. Add new rate 9.00% from 1 Jan 2025 → old rate gets EffectiveTo set

## Scenario 5: Period Creation and Batch Generation

### Steps
1. Navigate to /Payroll/Periods → Create July 2027
2. Click "Generate Payslips"
3. Verify preview: summary cards + employee table with correct calculations
4. For €1,000 basic: Net = €885.50, Employee deductions = €114.50, Employer = €154.00
5. Click "Confirm & Generate" → period status becomes Preview

## Scenario 6: Payslip Detail

### Steps
1. Open Giannis payslip from PeriodDetail
2. Verify: Summary cards (Earnings, Net, Employer Cost)
3. Verify: Earnings table with Basic + Overtime lines
4. Verify: Employee Deductions section (SI + GESY)
5. Verify: Employer Contributions section (5 items separated)
6. Verify: Net = TotalEarnings - TotalEmployeeDeductions

## Scenario 7: Edit Earning Lines (Preview)

### Steps
1. Click "Edit Earning Lines" on Preview payslip
2. Change overtime hours from 10 to 15
3. Save → recalculation triggers
4. Verify all amounts updated

## Scenario 8: Manager Notes

### Steps
1. Enter "€40 taxi reimbursement" in notes → Save → verify persisted
2. Try > 2000 chars → validation error

## Scenario 9: Finalise Period

### Steps
1. Click "Finalise Period" → warning → confirm
2. Status changes to Finalised
3. Edit buttons disappear, PDF/Email buttons appear
4. Try editing earning lines → blocked

## Scenario 10: Overtime Multiplier

### Steps
1. Default (empty) → uses 1.5
2. Set 4.0 (Christmas) → valid
3. Set 5.0 → "Overtime multiplier must be between 1.0 and 4.0"

## Scenario 11: Plan Permission

### Steps
1. Foundation user → /Payroll → upgrade page
2. Professional user → /Payroll → upgrade page
3. Enterprise user → full access
4. Sidebar shows Payroll only for Enterprise

## Verification Checklist

| # | Check | Pass? |
|---|-------|-------|
| 1 | Migrations apply without error | |
| 2 | Seed data: lookups + earning types + deduction templates | |
| 3 | Department CRUD with duplicate prevention | |
| 4 | Employee CRUD with unique SIN/IdNumber | |
| 5 | Default Earnings save and load | |
| 6 | Template import creates business-specific copies | |
| 7 | Rate history: new rate closes old | |
| 8 | Batch generation: €1,000 → Net €885.50 | |
| 9 | Deductions apply to full gross | |
| 10 | Rounding: €750 × 2.65% = €19.88 | |
| 11 | Overtime default multiplier 1.5 | |
| 12 | Overtime max multiplier 4.0 | |
| 13 | Finalise locks all editing | |
| 14 | Deductions/Contributions in separate sections | |
| 15 | Enterprise-only access | |
| 16 | Tenant isolation enforced | |
