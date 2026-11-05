# Payroll Register — Testing Scenarios (Multi-Employee Excel Register)

## Prerequisites

- Payroll module access (Enterprise tier) for the logged-in user.
- At least one FINALISED payslip period (PayslipStatusTypeId IN (3,5)) with several employees having payslips.
- At least one employee who has a finalised payslip in the period but is now INACTIVE / terminated (IsActive=0 or past EndDate) — used to verify terminated employees are still listed.
- A second business (different BusinessId) with its own finalised period — used to verify tenant isolation.
- SweetAlert2 + BlockUI loaded globally (standard layout).

## Scenario 1: Open the Payroll Register page

1. Sidebar → Payroll → Payroll Register (new nav item, after Contribution Report).
2. The page loads with a period dropdown sorted newest-first (Year, Month), each option labelled e.g. "July 2026 (Finalised)".
3. The latest finalised period is pre-selected; its eligible employees populate the multi-select.
4. Expected: Generate button enabled when eligible employees exist; disabled when none.

## Scenario 2: Generate for one employee (happy path)

1. Select the default period, pick ONE employee, click Generate.
2. BlockUI "Generating register..." shows, then clears; the .xlsx downloads as PayrollRegister_{periodId}.xlsx.
3. Open the workbook: Sheet 1 = "Summary - {Month} {Year}" with one employee row + bold TOTALS row.
4. Sheet 2 = that employee's detail: identity block, Earnings, Employee Deductions, Employer Contributions, totals block.
5. Expected: Summary row figures equal the detail sheet totals; money formatted #,##0.00; headers filled #0D5EA6 white bold.

## Scenario 3: Generate for several / all employees

1. Use Select All (or pick several), Generate.
2. Expected: sheet count = 1 (Summary) + one per selected eligible employee; TOTALS = arithmetic sum of the employee rows.

## Scenario 4: Terminated employee still included

1. Confirm the inactive/terminated employee (who has a finalised payslip in the period) appears in the selector.
2. Select them, Generate.
3. Expected: they get a Summary row and a detail sheet with their correct NAME (not "Unknown"). This verifies name resolution via GetEmployeesByIdsAsync, not the active-only query.

## Scenario 5: Deduction categorisation correctness

1. Pick an employee whose payslip has BOTH employee deductions (e.g. employee SI, GESY) and employer contributions (employer SI, GESY, Redundancy, etc.).
2. Open their detail sheet.
3. Expected: employee-side items appear ONLY under "Employee Deductions"; employer-side items ONLY under "Employer Contributions". None are misclassified into a single bucket (verifies the raw-entity DeductionCategoryTypeId split).

## Scenario 6: Validation & empty states

1. Click Generate with NO employee selected → SweetAlert2 warning "Select at least one employee."; no request fires.
2. Select a period with no finalised payslips (if available) → selector empty, Generate disabled, no-eligible message shown.
3. Change the period → employee selector refreshes and any previous selection is cleared.

## Scenario 7: Tenant isolation

1. As a user of Business A, confirm only Business A periods/employees appear and the workbook contains only Business A data.
2. Expected: no Business B employee or period data can ever appear.

## Verification Checklist

| # | Check | Expected | Pass |
|---|-------|----------|------|
| 1 | Nav item present | "Payroll Register" under Payroll, existing links unchanged | [ ] |
| 2 | Period dropdown | Newest-first, shows finalised/draft state from Status | [ ] |
| 3 | Single-employee workbook | Summary + 1 detail sheet, correct totals | [ ] |
| 4 | Multi-employee workbook | Sheet count = 1 + eligible selected; TOTALS = sum of rows | [ ] |
| 5 | Summary == detail totals | Per-employee Summary row equals detail totals block | [ ] |
| 6 | Terminated employee named | Appears with real name, not "Unknown" | [ ] |
| 7 | Deduction split | Employee vs employer items in correct sections | [ ] |
| 8 | No selection | SweetAlert2 warning, no request | [ ] |
| 9 | Empty/no-eligible period | Generate disabled + message | [ ] |
| 10 | Period change | Selector refreshed, prior selection cleared | [ ] |
| 11 | Tenant isolation | Only current business data | [ ] |
| 12 | Styling | #0D5EA6 headers, #,##0.00 money, sheet names <=31 chars & unique | [ ] |
| 13 | Existing reports intact | PeriodSummary / EarningsBreakdown / ContributionReport unchanged | [ ] |

## Database Queries for Manual Inspection

```sql
USE [Guardian]
GO

-- Finalised payslips for a period (what the register should include)
SELECT [payroll].[Payslip].[Id], [payroll].[Payslip].[EmployeeId],
       [payroll].[Payslip].[TotalEarnings], [payroll].[Payslip].[TotalEmployeeDeductions],
       [payroll].[Payslip].[NetSalary], [payroll].[Payslip].[TotalEmployerContributions]
FROM [payroll].[Payslip]
INNER JOIN [payroll].[PayslipPeriod] ON [payroll].[Payslip].[PayslipPeriodId] = [payroll].[PayslipPeriod].[Id]
WHERE [payroll].[PayslipPeriod].[Id] = @PeriodId
  AND [payroll].[PayslipPeriod].[BusinessId] = @BusinessId
  AND [payroll].[PayslipPeriod].[PayslipStatusTypeId] IN (3,5);

-- Deduction lines with their category (1=employee, 2=employer) to verify the split
SELECT [payroll].[PayslipDeductionLine].[PayslipId], [payroll].[PayslipDeductionLine].[DeductionTypeId],
       [payroll].[PayslipDeductionLine].[DeductionCategoryTypeId], [payroll].[PayslipDeductionLine].[CalculatedAmount]
FROM [payroll].[PayslipDeductionLine]
WHERE [payroll].[PayslipDeductionLine].[PayslipId] = @PayslipId;

-- Confirm a terminated employee still has a finalised payslip in the period
SELECT [payroll].[Employee].[Id], [payroll].[Employee].[Name],
       [payroll].[Employee].[IsActive], [payroll].[Employee].[EndDate]
FROM [payroll].[Employee]
WHERE [payroll].[Employee].[Id] = @EmployeeId;
```


---

## Phase 2 Addendum - Month Range, Contribution Totals, Percentages

The register now spans a FROM/TO month range (not a single period). The scenarios above still apply with these changes:

### Range selection (replaces single Period)
- The page has **From** and **To** month-year selectors. Default: To = latest finalised period, From = January of that year.
- Changing **either** From or To refreshes the eligible-employee list and clears the prior selection.
- If From is later than To, an inline red error shows ("The From month must not be later than the To month."), the employee list clears, Generate is blocked, and no request fires. The server also rejects From > To with a JSON error.

### Summary sheet (first sheet) - new scenario
1. Pick a range spanning several months (e.g. Jan-Dec) and multiple employees; Generate.
2. Expected: Summary columns = Employee Name | Total Earnings | Total Employee Deductions | Net Salary | [one column per employer contribution type] | Total Employer Contributions | Total Cost.
3. Each employee row sums across the whole range; the bottom TOTALS row gives each contribution type's grand total across all employees (the "totals for all contributions on the first page").
4. Contribution columns = the union of employer-contribution types across the selected employees, ordered by name. An employee with no amount for a given contribution shows 0.00 in that cell.

### Per-employee detail sheet - updated layout
1. Open any employee tab.
2. Expected: a ledger with one row per line item; one **month column per period in the range** (oldest to newest); a **Total** column; and a **%** column.
3. The % column shows each deduction/contribution line's rate (latest in-range rate if it changed mid-range); earnings have a blank %.
4. A month where the employee had no payslip shows 0.00 in that month's cells (not an error).
5. Section totals sum each month column + Total; the employee totals block sums across the range.

### Percentage / rate-change check
- Pick an employee/line whose rate changed within the range (if available). Expected: the % shows the LATEST in-range rate; every summed amount (row Total, section total, employee totals, summary grand totals) is unaffected by the % display.

### Filename
- Downloaded file is named ``PayrollRegister_{fromYear}{fromMonth}-{toYear}{toMonth}.xlsx``.

### Updated checklist additions

| # | Check | Expected | Pass |
|---|-------|----------|------|
| 14 | From/To selectors | Two month-year selects; defaults To=latest finalised, From=Jan of that year | [ ] |
| 15 | From > To guard | Inline error, list cleared, Generate blocked, no request; server also rejects | [ ] |
| 16 | Range refresh | Changing From or To refreshes employees + clears selection | [ ] |
| 17 | Summary contribution columns | One column per employer contribution type; union across employees; 0.00 when absent | [ ] |
| 18 | Summary contribution grand totals | TOTALS row gives each contribution's total across all employees | [ ] |
| 19 | Detail month columns | One column per month in range (oldest->newest) + Total + % | [ ] |
| 20 | Percentages | % on deduction/contribution lines (latest in-range rate); blank for earnings | [ ] |
| 21 | Missing month | 0.00 in that month's cells, employee still included | [ ] |
| 22 | Range filename | PayrollRegister_{fromY}{fromM}-{toY}{toM}.xlsx | [ ] |


---

## Phase 3 Addendum - On-Page Per-Employee Preview

The page now has TWO buttons: **Preview** (renders the register on the page, no download) and **Download** (the existing .xlsx export, unchanged). Preview and Download share one data source, so an on-page preview must match a download of the same selection cell-for-cell.

### Scenario P1: Preview happy path (single employee)
1. Pick a valid From/To range and ONE employee; click **Preview**.
2. Expected: BlockUI "Loading preview..." shows then clears; a per-employee ledger table appears in the section below the status card (no file downloads).
3. The table has: an employee-name heading; three sections (Earnings, Employee Deductions, Employer Contributions); one column per month in the range (e.g. "Jan 2025") + a Total column + a "%" column; a per-section total row; and an Employee_Totals block (Total Earnings, Total Employee Deductions, Net Salary, Total Employer Contributions, Total Cost).
4. Expected: `%` shows on deduction/contribution lines (e.g. "8.80%") and is blank for earnings; money is right-aligned, two-decimal; a month with no payslip shows 0.00.

### Scenario P2: Preview several / all employees
1. Select several employees (or Select All); click Preview.
2. Expected: one ledger table per selected eligible employee, in order, each in its own horizontally scrollable wrapper. There is NO cross-employee summary / grand-totals table in the preview (per-employee only).

### Scenario P3: Preview == Download (shared data)
1. For the same range + selection, click Preview, then click Download and open the .xlsx.
2. Expected: each employee's preview figures (every month cell, section totals, and the Employee_Totals block) equal that employee's detail sheet in the workbook. They can never differ.

### Scenario P4: Preview validation (mirrors Download)
1. Click Preview with From later than To → inline range error + SweetAlert2 warning; no request fires.
2. Click Preview with NO employee selected → SweetAlert2 warning "Select at least one employee."; no request fires.
3. Expected: identical guard behavior to Download; the server endpoint also rejects From>To / empty selection before touching data.

### Scenario P5: Stale-guard (preview clears on change)
1. Render a preview, then change From or To, or change the employee selection.
2. Expected: the previous preview clears immediately (it no longer reflects the current selection). Re-click Preview to render the new one.

### Scenario P6: Preview no-data / error
1. Pick a range+selection that yields no finalised payslip data (if reproducible).
2. Expected: the preview area clears and a SweetAlert2 message explains no data is available; no stack trace. A network/server error shows a generic SweetAlert2 error and clears the preview.

### Scenario P7: Download unchanged
1. Click Download exactly as before.
2. Expected: same file (PayrollRegister_{fromY}{fromM}-{toY}{toM}.xlsx), same content type, same sheets, same decline JSON paths. The Preview feature is purely additive.

### Phase 3 checklist additions

| # | Check | Expected | Pass |
|---|-------|----------|------|
| 23 | Two buttons | Preview (secondary) + Download (primary); both disabled when no eligible employees | [ ] |
| 24 | Preview renders on page | Per-employee ledger table(s) below the status card; no file downloads | [ ] |
| 25 | Preview layout | Earnings/Employee Deductions/Employer Contributions sections, month columns + Total + %, section totals, Employee_Totals block | [ ] |
| 26 | Preview per-employee only | One table per employee; NO cross-employee summary table | [ ] |
| 27 | Preview % and 0.00 | % on deduction/contribution lines (blank for earnings); missing month = 0.00 | [ ] |
| 28 | Preview == Download | Preview figures equal the workbook detail sheets for the same selection | [ ] |
| 29 | Preview validation | From>To and empty selection both warn and fire no request (client + server) | [ ] |
| 30 | Stale-guard | Preview clears when range or selection changes | [ ] |
| 31 | Preview no-data/error | Area cleared + SweetAlert2 message; no stack trace | [ ] |
| 32 | Download unchanged | Same filename, content type, sheets, decline paths as before | [ ] |