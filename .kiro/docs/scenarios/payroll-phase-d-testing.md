# Payroll Phase D — Testing Scenarios (PAYE Tax & Compliance Integration)

## Prerequisites

- All Phase A migrations applied (Department, Employee, EarningType, DeductionType, PayslipPeriod, Payslip, etc.)
- All Phase B migrations applied (PayslipAuditLog, PayslipAuditActionType, status types 1–5)
- All Phase C migrations applied (PayslipEmailLog, email audit)
- Phase D migrations applied:
  - `Seed_PayeTaxBand.sql` — creates [payroll].[PayeTaxBand] table
  - `Seed_CountryDeductionTemplate.sql` — creates [payroll].[CountryDeductionTemplate] table
  - `Seed_PayslipPeriodComplianceFiling.sql` — creates [payroll].[PayslipPeriodComplianceFiling] table
  - `Seed_Employee_IsPayeApplicable.sql` — adds IsPayeApplicable column to Employee
  - `Seed_DeductionType_IsPayeDeductible.sql` — adds IsPayeDeductible column, flags SI/GESY
  - `Seed_PayslipDeductionLine_NullableRateHistory.sql` — makes DeductionRateHistoryId nullable
  - `Seed_CyprusPAYETaxBands2024.sql` — inserts 5 Cyprus progressive bands
  - `Seed_CyprusDeductionTemplates.sql` — inserts 7 Cyprus deduction templates
- User logged in with Enterprise tier subscription (payroll module access)
- Owner account available (has `IsOwner` claim set to `true`)
- SuperAdmin account available (is in `SuperAdmin` role)
- Standard user account available (payroll access but no Owner/SuperAdmin)
- At least 3 employees configured:
  - Employee A: BaseSalary €3,500/month (annual €42,000 — above PAYE threshold)
  - Employee B: BaseSalary €1,800/month (annual €21,600 — above threshold but PAYE base below after deductions)
  - Employee C: BaseSalary €1,200/month (annual €14,400 — below threshold)
- Business deduction types imported from Cyprus templates (SI, GESY, Redundancy, Industrial Training, Social Cohesion)
- PAYE DeductionType (Code='PAYE') created for the business
- A Social Insurance compliance filing with DueDate in the month AFTER the test period (e.g., if testing July, filing due in August)
- Business Applications Tracker (Compliance module) accessible

## Scenario 1: PAYE Toggle on Employee Profile

### Steps
1. Navigate to Payroll → Employees → select Employee A (€3,500/month)
2. Click "Edit" to open the EmployeeForm
3. Scroll to the "PAYE Income Tax" section
4. Verify checkbox "Subject to PAYE Income Tax" is visible with informational text
5. Check the checkbox (enable PAYE)
6. Verify BlockUI appears "Updating PAYE status..."
7. Verify success SweetAlert: "PAYE status updated."
8. Refresh the page → verify checkbox remains checked

### Edge Cases
9. Enable PAYE on Employee C (€1,200/month, annual €14,400 < €19,500)
   → Verify SweetAlert WARNING appears: "This employee's projected annual income (€14,400.00) does not exceed the PAYE threshold (€19,500). PAYE calculation will result in €0."
   → Click "Keep Enabled" → PAYE remains enabled
   → Re-toggle → click "Revert" → checkbox reverts to unchecked
10. Disable PAYE on Employee A → success message, no warning
11. Log in as standard user (not Owner/SuperAdmin) → "Subject to PAYE" section not actionable or endpoint returns "Only business owners or SuperAdmins can toggle PAYE status"

## Scenario 2: PAYE Calculation in Batch Generation Preview

### Steps
1. Ensure Employee A has IsPayeApplicable = true, Employee B has IsPayeApplicable = true, Employee C has IsPayeApplicable = false
2. Navigate to Payroll → Payslip Periods → Create a new period (e.g., July 2026)
3. Click "Generate Payslips" to open the batch preview
4. Verify Employee A's preview shows a PAYE deduction line:
   - Deduction name: "PAYE Income Tax"
   - Base Amount: earnings minus PAYE-deductible deductions (SI + GESY employee)
   - Rate: top marginal rate (e.g., 30.00% if income falls in the 30% band)
   - Amount: calculated monthly PAYE
5. Verify Employee B's preview:
   - PAYE base = €1,800 - SI(€158.40) - GESY(€47.70) = €1,593.90
   - Annual projected PAYE base = €19,126.80 (below €19,500 threshold)
   - PAYE amount = €0.00 (still shows the line but with €0)
6. Verify Employee C's preview:
   - NO PAYE deduction line at all (IsPayeApplicable = false)
7. Verify Net Salary correctly accounts for PAYE:
   - Employee A: Earnings - SI - GESY - PAYE = Net
   - Employee B: Earnings - SI - GESY - €0 = Net
   - Employee C: Earnings - SI - GESY = Net (same as before Phase D)

### Edge Cases
8. Employee with no deduction types configured → calculation engine returns validation error (same as Phase A behaviour)
9. Business with no PAYE DeductionType (Code='PAYE') → orchestrator skips PAYE line gracefully (no PAYE appended)
10. Business country not in mapping (e.g., "Portugal") → defaults to "CY" bands
11. No tax bands configured for the year → orchestrator returns validation error

## Scenario 3: PAYE on Payslip Detail

### Steps
1. Confirm the batch generation from Scenario 2
2. Navigate to PeriodDetail → click Employee A's payslip
3. Verify the Deductions section shows "PAYE Income Tax" line:
   - Base Amount = taxable income (earnings - PAYE-deductible deductions)
   - Rate = top marginal rate as percentage (e.g., 30.00)
   - Calculated Amount = monthly PAYE
4. Verify the Net Salary = TotalEarnings - all employee deductions (including PAYE)
5. Verify employer contributions are NOT affected by PAYE (same as without PAYE)

### Calculation Verification (Employee A: €3,500/month)
6. Expected values:
   - SI Employee: €3,500 × 8.80% = €308.00
   - GESY Employee: €3,500 × 2.65% = €92.75
   - PAYE base: €3,500 - €308.00 - €92.75 = €3,099.25
   - Annual projected: €3,099.25 × 12 = €37,191.00
   - Tax: 0% on €0–€19,500 (€0) + 20% on €19,500.01–€28,000 (€1,700) + 25% on €28,000.01–€36,300 (€2,075) + 30% on €36,300.01–€37,191 (€267.30) = €4,042.30
   - Monthly PAYE: €4,042.30 / 12 = €336.86
   - Rate displayed: 30.00% (top marginal rate)
   - Net Salary: €3,500 - €308.00 - €92.75 - €336.86 = €2,762.39

## Scenario 4: SaveEarningLines Recalculates PAYE

### Steps
1. Navigate to Employee A's payslip (from Scenario 3) — period must be in Preview or Unlocked status
2. Edit earning lines: change Basic Salary from €3,500 to €4,000
3. Save earning lines
4. Verify PAYE is recalculated with the new earnings:
   - New PAYE base: €4,000 - (€4,000 × 8.80%) - (€4,000 × 2.65%) = €4,000 - €352 - €106 = €3,542
   - Annual projected: €3,542 × 12 = €42,504
   - PAYE recalculated with higher amount
5. Verify Net Salary updates accordingly

### Edge Cases
6. Save earning lines on employee with IsPayeApplicable = false → no PAYE line, same as Phase A/B/C
7. Save earning lines that reduce income below threshold → PAYE becomes €0.00

## Scenario 5: Compliance Integration on Finalisation

### Steps
1. Ensure a Social Insurance compliance filing exists with DueDate in the month AFTER the period (e.g., period = July 2026 → filing DueDate in August 2026)
2. Navigate to PeriodDetail for the Preview period (from Scenario 2/3)
3. Finalise the period (Preview → Finalised)
4. Verify finalisation succeeds (SweetAlert success)
5. Navigate to the compliance filing (Business Applications → August 2026 Social Insurance)
6. Verify EstimatedAmount is populated with the sum of SI employer contributions:
   - Employee A SI Employer: €308.00
   - Employee B SI Employer: €158.40
   - Employee C SI Employer: €105.60
   - Total: €572.00
7. Navigate back to PayrollCompliance → Contribution Report
8. Select the July 2026 period
9. Verify the compliance link section shows: "Linked to Social Insurance filing (€572.00)"

### Edge Cases
10. No matching Social Insurance filing exists → finalisation still succeeds (non-blocking), Serilog warning logged
11. Compliance service throws exception → finalisation still succeeds (wrapped in try/catch)
12. Period with no employer SI lines (e.g., all employees have custom deductions without SI) → compliance integration skips gracefully

## Scenario 6: Compliance Integration on Re-Finalisation

### Steps
1. Unlock the July 2026 period (Owner/SuperAdmin)
2. Modify Employee A's earning (e.g., add a bonus line of €500)
3. Re-finalise the period
4. Verify a NEW PayslipPeriodComplianceFiling record is created (not updating the previous one)
5. Verify the compliance filing EstimatedAmount is updated with the new SI total:
   - Employee A SI Employer: (€3,500 + €500) × 8.80% = €352.00 (if earnings changed)
   - New total reflects updated amounts
6. Navigate to Contribution Report → verify compliance history shows TWO entries:
   - Original finalisation record
   - Re-finalisation record with updated amount

### Edge Cases
7. Multiple re-finalisations → each creates a new cross-reference record (full audit trail)
8. Filing status changed to "Submitted" between first finalisation and re-finalisation → EstimatedAmount still updated (no status check)

## Scenario 7: Employer Contribution Report

### Steps
1. Navigate to sidebar → "Contribution Report" link (under Payroll section)
2. Verify the page loads with a period dropdown
3. Select a Finalised or Re-finalised period from the dropdown
4. Click "View Report"
5. Verify Summary section shows contribution type boxes:
   - SI Employer total
   - Redundancy total
   - Industrial Training total
   - Social Cohesion total
   - GESY Employer total
   - Grand Total (highlighted)
6. Verify Detail table shows per-employee breakdown:
   - Columns: Employee Name, SI, Redundancy, Ind Training, Soc Cohesion, GESY, Total
   - Footer row with grand totals
7. Verify compliance link section (if filing linked)
8. Click "Export to Excel" → downloads Excel file

### Edge Cases
9. Period with no finalised payslips → "No finalised payslips for July 2026" empty state
10. Period in Draft/Preview status → contribution data may be empty (only Finalised/Re-finalised included)
11. Employee with only some employer contributions (e.g., imported subset of templates) → shows available types, others show €0.00

## Scenario 8: SuperAdmin — Country Deduction Templates

### Steps
1. Log in as SuperAdmin
2. Navigate to Platform Admin → Country Deduction Templates (/PayrollTemplate)
3. Verify page shows country dropdown (Cyprus, Malta, United Kingdom)
4. Select "Cyprus" → verify 7 templates displayed:
   - SI Employee (8.80%, Cat: Deduction, PAYE Deductible: ✓)
   - GESY Employee (2.65%, Cat: Deduction, PAYE Deductible: ✓)
   - SI Employer (8.80%, Cat: Contribution)
   - Redundancy (1.20%, Cat: Contribution)
   - Industrial Training (0.50%, Cat: Contribution)
   - Social Cohesion (2.00%, Cat: Contribution)
   - GESY Employer (2.90%, Cat: Contribution)
5. Click "Add Template" → SweetAlert form modal opens
6. Fill in: Name "Holiday Fund", Code "Holiday_Fund", Category "Employer Contribution", Rate 0.0200, Sort Order 8
7. Click "Create" → template appears in table
8. Click "Edit" on the new template → change rate to 0.0250 → "Update"
9. Click "Deactivate" → confirmation dialog → confirm → template shows "Inactive" badge

### Edge Cases
10. Create template with duplicate code → validation should warn or fail gracefully
11. Non-SuperAdmin navigates to /PayrollTemplate → access denied (403 or redirect)
12. Deactivated template does NOT appear in import list (only active templates are imported)

## Scenario 9: SuperAdmin — PAYE Tax Bands

### Steps
1. From the Templates page, click "PAYE Tax Bands" link
2. Verify page shows CY tax bands for current year:
   - €0 – €19,500 at 0%
   - €19,500.01 – €28,000 at 20%
   - €28,000.01 – €36,300 at 25%
   - €36,300.01 – €60,000 at 30%
   - €60,000.01 – No limit at 35%
3. Click "Add Tax Band" → fill in: Lower €60,000.01, Upper blank (top band), Rate 0.3700, From Year 2025
4. Click "Create" → new band appears (for future year)
5. Click "Edit" on the €60,000+ band → change rate from 0.35 to 0.37 → "Update"
6. Verify the update is reflected in the table

### Edge Cases
7. Add band with LowerBound >= UpperBound → validation error
8. Add band with Rate > 1 or Rate < 0 → validation error
9. Band with no UpperBound (NULL) → displayed as "No limit"
10. Multiple band sets for different years → query filters by year

## Scenario 10: Import Country Templates to Business

### Steps
1. Log in as business Owner
2. Navigate to Payroll → Deduction Config
3. Verify "Import Templates" functionality is available (from Phase A)
4. Import Cyprus templates → creates business-scoped DeductionTypes:
   - Verify 7 deduction types created with correct rates (8.80, 2.65, etc.)
   - Verify PAYE DeductionType also created (Code='PAYE', IsPercentage=false)
   - Verify IsPayeDeductible flag propagated (SI and GESY marked as true)
5. Attempt to import again → error: "Templates already imported: [names]"

### Rate Conversion Verification
6. Check DeductionRateHistory for imported types:
   - CountryDeductionTemplate.DefaultRate = 0.0880
   - DeductionRateHistory.Rate = 8.80 (multiplied by 100)
   - This matches the percentage display format used by the calculation engine

## Scenario 11: Zero Regression — IsPayeApplicable = false

### Steps
1. Ensure Employee C has IsPayeApplicable = false
2. Generate a payslip for Employee C via batch generation
3. Verify the result is IDENTICAL to Phase A/B/C behaviour:
   - Same earning lines
   - Same deduction lines (SI, GESY — no PAYE line)
   - Same Net Salary calculation
   - No PAYE-related fields or lines
4. Verify employer contributions are unchanged

### Validation
5. Compare Employee C's payslip totals with a manually calculated result using only the Phase A engine
6. Results must match exactly — the orchestrator returns the engine result unchanged when IsPayeApplicable = false

## Scenario 12: PAYE Deduction Line Storage

### Steps
1. Generate and confirm payslips with PAYE enabled (Employee A)
2. Query the database directly:
   ```sql
   SELECT PayslipDeductionLine.DeductionTypeId, PayslipDeductionLine.BaseAmount,
          PayslipDeductionLine.Rate, PayslipDeductionLine.CalculatedAmount,
          PayslipDeductionLine.DeductionRateHistoryId
   FROM [payroll].[PayslipDeductionLine]
   INNER JOIN [payroll].[DeductionType]
       ON PayslipDeductionLine.DeductionTypeId = DeductionType.Id
   WHERE DeductionType.Code = 'PAYE'
     AND PayslipDeductionLine.PayslipId = @PayslipId
   ```
3. Verify:
   - DeductionRateHistoryId IS NULL (PAYE uses progressive bands, not rate history)
   - BaseAmount = PAYE taxable base (earnings minus PAYE-deductible deductions)
   - Rate = top marginal rate × 100 (e.g., 30.00 for the 30% band)
   - CalculatedAmount = monthly PAYE amount
   - DeductionCategoryTypeId = 1 (employee deduction)

## Scenario 13: Compliance Filing Cross-Reference History

### Steps
1. Finalise a period → compliance integration runs
2. Unlock and re-finalise → compliance integration runs again
3. Query the cross-reference table:
   ```sql
   SELECT PayslipPeriodComplianceFiling.Id, PayslipPeriodComplianceFiling.ContributionTotal,
          PayslipPeriodComplianceFiling.UpdatedAtUtc, PayslipPeriodComplianceFiling.UpdatedByUserId,
          PayslipPeriodComplianceFiling.CreatedAtUtc
   FROM [payroll].[PayslipPeriodComplianceFiling]
   WHERE PayslipPeriodComplianceFiling.PayslipPeriodId = @PeriodId
   ORDER BY PayslipPeriodComplianceFiling.CreatedAtUtc DESC
   ```
4. Verify TWO records exist (one per finalisation)
5. Verify each record has different ContributionTotal if amounts changed
6. Verify UpdatedByUserId matches the user who triggered finalisation

## Scenario 14: 1-Month Offset Compliance Filing Lookup

### Steps
1. Create a July 2026 payroll period
2. Create a Social Insurance filing with DueDate = 2026-08-10
3. Finalise July → verify the August filing is found and updated
4. Create a December 2026 payroll period
5. Create a Social Insurance filing with DueDate = 2027-01-15 (wraparound)
6. Finalise December → verify the January 2027 filing is found and updated

### Edge Cases
7. No filing for the expected month → warning logged, finalisation succeeds
8. Multiple filings for the same month → first one found is updated (query returns first match)

## Scenario 15: Navigation & Access

### Steps
1. Log in with payroll module access
2. Verify sidebar contains "Contribution Report" link under Payroll section
3. Click "Contribution Report" → navigates to /PayrollCompliance/ContributionReport
4. Verify page loads with period dropdown

### SuperAdmin Navigation
5. Log in as SuperAdmin
6. Verify "Country Deduction Templates" accessible at /PayrollTemplate
7. Verify "PAYE Tax Bands" link available from template page
8. Verify non-SuperAdmin cannot access /PayrollTemplate (returns 403 or redirect)

### Tenant Isolation
9. Log in as business Owner for Business A
10. View Contribution Report → only shows data for Business A's periods
11. Log in as different business → verify complete data isolation

## Verification Checklist

| # | Check | Pass? |
|---|-------|-------|
| 1 | Phase D migrations apply without error | |
| 2 | PayeTaxBand table created with CHECK constraints | |
| 3 | CountryDeductionTemplate table created with FK to DeductionCategoryType | |
| 4 | PayslipPeriodComplianceFiling table created with FKs to PayslipPeriod and BusinessApplication | |
| 5 | Employee.IsPayeApplicable column added (BIT NOT NULL DEFAULT 0) | |
| 6 | DeductionType.IsPayeDeductible column added and SI/GESY flagged | |
| 7 | PayslipDeductionLine.DeductionRateHistoryId is now nullable | |
| 8 | Cyprus PAYE bands seeded (5 bands, 2024, rates 0%–35%) | |
| 9 | Cyprus deduction templates seeded (7 templates) | |
| 10 | PAYE toggle on employee form works (enable/disable with warning) | |
| 11 | Only Owner/SuperAdmin can toggle PAYE | |
| 12 | Batch generation preview shows PAYE line for enabled employees | |
| 13 | PAYE calculation uses correct progressive band formula | |
| 14 | PAYE base = TotalEarnings minus PAYE-deductible employee deductions | |
| 15 | PAYE Rate field = top marginal rate (not effective rate) | |
| 16 | PAYE deduction line stored with DeductionRateHistoryId = NULL | |
| 17 | IsPayeApplicable = false → identical result to Phase A/B/C (zero regression) | |
| 18 | SaveEarningLines recalculates PAYE when earnings change | |
| 19 | Finalisation triggers compliance integration (non-blocking) | |
| 20 | Compliance integration sums ONLY SI_Contribution lines (DeductionCategoryTypeId = 2) | |
| 21 | 1-month offset: July payroll → August filing lookup | |
| 22 | December wraparound: December payroll → January of next year | |
| 23 | Missing compliance filing → warning logged, finalisation succeeds | |
| 24 | Compliance integration failure → finalisation still succeeds | |
| 25 | Re-finalisation creates NEW cross-reference record (history preserved) | |
| 26 | Contribution Report loads with period filter | |
| 27 | Summary boxes show per-type totals | |
| 28 | Detail table shows per-employee breakdown with footer totals | |
| 29 | Compliance filing link displayed when cross-reference exists | |
| 30 | Export to Excel downloads correctly | |
| 31 | SuperAdmin can manage country deduction templates (CRUD) | |
| 32 | SuperAdmin can manage PAYE tax bands (CRUD) | |
| 33 | Template deactivation hides from import list | |
| 34 | Tax band validation: Rate 0–1, LowerBound < UpperBound | |
| 35 | Import templates creates business-scoped types with rate conversion (×100) | |
| 36 | Import creates PAYE DeductionType (IsPercentage=false) if not present | |
| 37 | Duplicate import detection prevents double-import | |
| 38 | Sidebar navigation includes "Contribution Report" link | |
| 39 | Non-SuperAdmin cannot access /PayrollTemplate | |
| 40 | Tenant isolation enforced on all Phase D endpoints | |

## Database Queries for Manual Inspection

```sql
-- Verify PAYE tax bands for Cyprus 2024
SELECT PayeTaxBand.CountryCode, PayeTaxBand.LowerBound, PayeTaxBand.UpperBound,
       PayeTaxBand.Rate, PayeTaxBand.EffectiveFromYear, PayeTaxBand.EffectiveToYear
FROM [payroll].[PayeTaxBand]
WHERE PayeTaxBand.CountryCode = 'CY'
ORDER BY PayeTaxBand.LowerBound

-- Verify country deduction templates
SELECT CountryDeductionTemplate.CountryCode, CountryDeductionTemplate.DeductionName,
       CountryDeductionTemplate.Code, CountryDeductionTemplate.DefaultRate,
       CountryDeductionTemplate.IsPayeDeductible, CountryDeductionTemplate.IsActive
FROM [payroll].[CountryDeductionTemplate]
WHERE CountryDeductionTemplate.CountryCode = 'CY'
ORDER BY CountryDeductionTemplate.SortOrder

-- Check which employees have PAYE enabled
SELECT Employee.Name, Employee.BaseSalary, Employee.IsPayeApplicable
FROM [payroll].[Employee]
WHERE Employee.BusinessId = @BusinessId
ORDER BY Employee.Name

-- Check IsPayeDeductible flags on business deduction types
SELECT DeductionType.Name, DeductionType.Code, DeductionType.IsPayeDeductible,
       DeductionType.DeductionCategoryTypeId
FROM [payroll].[DeductionType]
WHERE DeductionType.BusinessId = @BusinessId
ORDER BY DeductionType.Code

-- Verify PAYE deduction lines (nullable DeductionRateHistoryId)
SELECT PayslipDeductionLine.PayslipId, DeductionType.Code,
       PayslipDeductionLine.BaseAmount, PayslipDeductionLine.Rate,
       PayslipDeductionLine.CalculatedAmount, PayslipDeductionLine.DeductionRateHistoryId
FROM [payroll].[PayslipDeductionLine]
INNER JOIN [payroll].[DeductionType]
    ON PayslipDeductionLine.DeductionTypeId = DeductionType.Id
INNER JOIN [payroll].[Payslip]
    ON PayslipDeductionLine.PayslipId = Payslip.Id
WHERE Payslip.PayslipPeriodId = @PeriodId
  AND DeductionType.Code = 'PAYE'

-- Verify employer contributions for contribution report
SELECT Employee.Name, DeductionType.Name AS ContributionType, DeductionType.Code,
       PayslipDeductionLine.CalculatedAmount
FROM [payroll].[PayslipDeductionLine]
INNER JOIN [payroll].[Payslip] ON PayslipDeductionLine.PayslipId = Payslip.Id
INNER JOIN [payroll].[Employee] ON Payslip.EmployeeId = Employee.Id
INNER JOIN [payroll].[DeductionType] ON PayslipDeductionLine.DeductionTypeId = DeductionType.Id
WHERE Payslip.PayslipPeriodId = @PeriodId
  AND PayslipDeductionLine.DeductionCategoryTypeId = 2
  AND Payslip.PayslipStatusTypeId IN (3, 5)
ORDER BY Employee.Name, DeductionType.Code

-- Verify compliance filing cross-references
SELECT PayslipPeriodComplianceFiling.Id, PayslipPeriodComplianceFiling.PayslipPeriodId,
       PayslipPeriodComplianceFiling.ComplianceFilingId,
       PayslipPeriodComplianceFiling.ContributionTotal,
       PayslipPeriodComplianceFiling.UpdatedAtUtc, PayslipPeriodComplianceFiling.UpdatedByUserId
FROM [payroll].[PayslipPeriodComplianceFiling]
WHERE PayslipPeriodComplianceFiling.PayslipPeriodId = @PeriodId
ORDER BY PayslipPeriodComplianceFiling.CreatedAtUtc DESC

-- Verify compliance filing EstimatedAmount was updated
SELECT BusinessApplication.Id, BusinessApplication.DueDate, BusinessApplication.Status,
       BusinessApplication.EstimatedAmount
FROM [compliance].[BusinessApplication]
INNER JOIN [compliance].[ApplicationType]
    ON BusinessApplication.ApplicationTypeId = ApplicationType.Id
WHERE BusinessApplication.BusinessId = @BusinessId
  AND ApplicationType.Name = 'Social Insurance'
  AND YEAR(BusinessApplication.DueDate) = @DueYear
  AND MONTH(BusinessApplication.DueDate) = @DueMonth

-- Verify PAYE calculation manually (for Employee A at €3,500/month)
-- PAYE-deductible deductions: SI(8.80%) + GESY(2.65%) = 11.45%
-- PAYE base: €3,500 × (1 - 0.1145) = €3,099.25/month
-- Annual: €37,191.00
-- Band 1: €0–€19,500 @ 0% = €0
-- Band 2: €19,500.01–€28,000 @ 20% = €1,700.00
-- Band 3: €28,000.01–€36,300 @ 25% = €2,075.00
-- Band 4: €36,300.01–€37,191 @ 30% = €267.30
-- Annual tax: €4,042.30
-- Monthly PAYE: €4,042.30 / 12 = €336.86
-- Top marginal rate: 30%
```
