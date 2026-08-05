# Payroll Phase B — Testing Scenarios (Audit, Unlock, and P&L Integration)

## Prerequisites

- All Phase A migrations applied (166, 167) and Phase B migration applied (PayslipAuditLog, PayslipAuditActionType, Purchase/Supplier alterations)
- Phase A seed data in place (PayslipStatusType values 1–3, EarningTypes, DeductionTemplates)
- Phase B seed data applied: PayslipStatusType values 4 (Unlocked) and 5 (Re-finalised), PayslipAuditActionType values (1=Unlocked, 2=Edited, 3=Re-finalised)
- User logged in with Enterprise tier subscription (payroll module access)
- Owner account available (has `IsOwner` claim set to `true`)
- SuperAdmin account available (is in `SuperAdmin` role)
- Standard user account available (no Owner claim, no SuperAdmin role, but has payroll module access)
- At least one period in Finalised status with 2+ payslips (from Phase A flow: Department → Employees → Generate → Finalise)

## Scenario 1: Period Status Lifecycle

### Steps
1. Create a new period (e.g., August 2027) → status = Draft
2. Generate payslips → status = Preview
3. Finalise period → status = Finalised
4. Unlock period (Owner) → status = Unlocked
5. Re-finalise period (Owner) → status = Re-finalised
6. Unlock again (SuperAdmin) → status = Unlocked
7. Re-finalise again (Owner) → status = Re-finalised

### Validation
8. Try Draft → Finalised (skip Preview) → blocked
9. Try Finalised → Re-finalised (skip Unlocked) → blocked
10. Try Unlocked → Finalised (invalid direction) → blocked
11. Try Preview → Unlocked → blocked

## Scenario 2: Unlock Period

### Steps
1. Navigate to /Payroll/PeriodDetail/{id} with a Finalised period
2. Verify "Unlock Period" button visible for Owner
3. Click "Unlock Period" → SweetAlert2 warning: "Editing will affect P&L for {Month} {Year}"
4. Click "Cancel" → nothing changes, status remains Finalised
5. Click "Unlock Period" again → Click "Proceed"
6. Status changes to Unlocked (amber badge)
7. All payslips in period now show Unlocked status
8. Audit entries created (one per payslip, ActionType = Unlocked)

### Edge Cases
9. Unlock a Re-finalised period → same flow, status goes to Unlocked
10. Two browser tabs open same period → first unlock succeeds, second gets "Period status has been changed by another user. Please refresh and try again."

## Scenario 3: Re-finalise Period

### Steps
1. Navigate to /Payroll/PeriodDetail/{id} with an Unlocked period
2. Verify "Re-finalise" button visible for Owner
3. Click "Re-finalise" → SweetAlert2 info dialog: "P&L entries will be updated to reflect your changes."
4. Click "Cancel" → nothing changes, status remains Unlocked
5. Click "Re-finalise" again → Click "Re-finalise" button in dialog
6. Status changes to Re-finalised (green badge)
7. All payslips recalculated and show Re-finalised status
8. ProcessedAtUtc set to current UTC timestamp
9. Old P&L entries cancelled, new ones created with updated totals

### Edge Cases
10. Re-finalise with a payslip missing a deduction rate → error: "Cannot re-finalise: validation failed for {EmployeeName}"
11. Re-finalise an empty period (all payslips removed during unlock) → should handle gracefully (zero totals)

## Scenario 4: Edit Gating

### Steps
1. Open payslip in Draft period → edit buttons visible, can modify earning lines
2. Open payslip in Preview period → edit buttons visible, can modify earning lines
3. Finalise period → edit buttons disappear, "Edit Earning Lines" disabled
4. Unlock period → edit buttons reappear, can modify earning lines and manager notes
5. Re-finalise → edit buttons disappear again

### Validation
6. Direct API call to save earning lines on Finalised period → `{ success: false, message: "..." }`
7. Direct API call to save earning lines on Re-finalised period → `{ success: false, message: "..." }`
8. Direct API call to save earning lines on Unlocked period → `{ success: true }`

## Scenario 5: Field-Level Audit Trail — Earning Lines

### Steps
1. Unlock a Finalised period
2. Open payslip for employee "Giannis Papamichael"
3. Change Basic Salary from €1,000 to €1,100 → Save
4. Verify audit entry: FieldName = "EarningLine:Basic Salary:Amount", OldValue = "1000.00", NewValue = "1100.00"
5. Add new earning line: "Bonus" €200 → Save
6. Verify audit entry: FieldName = "EarningLine:Bonus", OldValue = null, NewValue = "200.00"
7. Remove the "Bonus" line → Save
8. Verify audit entry: FieldName = "EarningLine:Bonus", OldValue = "200.00", NewValue = null

### Duplicate Earning Type Handling
9. Employee has two "Overtime" lines (different shifts)
10. Change first Overtime amount → FieldName = "EarningLine:Overtime[0]:Amount"
11. Change second Overtime amount → FieldName = "EarningLine:Overtime[1]:Amount"

## Scenario 6: Field-Level Audit Trail — Manager Notes

### Steps
1. Unlock a Finalised period
2. Open payslip → Manager Notes is "€40 taxi reimbursement"
3. Change to "€60 taxi reimbursement (updated)" → Save
4. Verify audit entry: FieldName = "ManagerNotes", OldValue = "€40 taxi reimbursement", NewValue = "€60 taxi reimbursement (updated)"
5. Clear notes entirely → Save
6. Verify audit entry: FieldName = "ManagerNotes", OldValue = "€60 taxi reimbursement (updated)", NewValue = null

### Non-Audited Edits
7. Edit earning lines on a Preview period → no audit entries created (audit only for Unlocked status)
8. Edit manager notes on a Draft period → no audit entries created

## Scenario 7: Audit History View

### Steps
1. Navigate to PayslipAuditHistory for a payslip with multiple changes
2. Verify timeline shows entries in reverse chronological order (newest first)
3. Verify each entry shows: user full name, action badge (colour-coded), field name, old → new value, timestamp
4. Verify Unlocked event shows as simple marker (no field details)
5. Verify Re-finalised event shows as simple marker (no field details)
6. Verify Edited entries show field name and value diff
7. Verify the view is read-only — no edit/delete controls

## Scenario 8: Period Audit Summary

### Steps
1. Navigate to PeriodAuditSummary for a period with changes across multiple employees
2. Verify entries grouped by employee name
3. Verify each group is collapsible
4. Verify each group contains the same timeline format as the payslip-level view
5. Verify "View Audit Summary" button on PeriodDetail visible to all payroll users (not role-restricted)

## Scenario 9: P&L Integration on Finalisation

### Steps
1. Create period September 2027 with 3 employees:
   - Employee A: TotalEarnings = €1,200, TotalEmployerContributions = €184.80
   - Employee B: TotalEarnings = €1,500, TotalEmployerContributions = €231.00
   - Employee C: TotalEarnings = €900, TotalEmployerContributions = €138.60
2. Finalise period
3. Verify Purchase records created:
   - Salary Cost: InvoiceNumber = "PAY-2027-09-SAL", Amount = €3,600.00 (sum of TotalEarnings)
   - Employer Contributions: InvoiceNumber = "PAY-2027-09-EMP", Amount = €554.40 (sum of TotalEmployerContributions)
4. Verify both entries: Description = "Payroll - September 2027"
5. Verify both entries: InvoiceDate = 2027-09-30 (last day of period month)
6. Verify both entries: VatAmount = 0, PurchaseTypeId = 3, PurchaseOriginTypeId = 1
7. Verify PayslipPeriodId links back to the period
8. Verify "Payroll (Internal)" supplier exists with IsSystemGenerated = 1
9. Verify expense categories exist: "Payroll - Salary Cost" and "Payroll - Employer Contributions"

## Scenario 10: P&L Adjustment on Re-finalisation

### Steps
1. Take the finalised September 2027 period from Scenario 9
2. Unlock the period
3. Change Employee A's Basic Salary from €1,200 to €1,300
4. Re-finalise the period
5. Verify old Purchase entries: IsCancelled = 1, CancelledByUserId = current user's ID, CancelledAtUtc ≠ null
6. Verify old entries retain original amounts (€3,600 and €554.40 preserved)
7. Verify new Purchase entries created:
   - Salary Cost: Amount = €3,700.00 (€1,300 + €1,500 + €900)
   - Employer Contributions: recalculated based on new totals
8. Verify new entries: IsCancelled = 0, PayslipPeriodId = same period
9. Verify total of 4 Purchase records for this period (2 cancelled + 2 active)

## Scenario 11: Optimistic Concurrency

### Steps
1. Open a Finalised period in two browser sessions (different tabs or users)
2. In Tab A: click "Unlock Period" → Proceed
3. In Tab B: click "Unlock Period" → Proceed (before Tab A response arrives)
4. Verify: one tab succeeds (status = Unlocked), other tab shows "Period status has been changed by another user. Please refresh and try again."
5. Verify: no partial state — either all payslips are Unlocked with audit entries, or nothing changed

### Re-finalise Concurrency
6. Open an Unlocked period in two tabs
7. Both click Re-finalise simultaneously
8. One succeeds, the other gets concurrency error
9. Verify: P&L entries created only once (not duplicated)

## Scenario 12: Role Restriction

### Steps
1. Log in as standard user (not Owner, not SuperAdmin)
2. Navigate to /Payroll/PeriodDetail/{id} with Finalised period
3. Verify "Unlock Period" button is NOT visible
4. Direct API POST to /Payroll/AxPostUnlockPeriod → `{ success: false, message: "Only the business owner or a SuperAdmin can perform this action." }`
5. Direct API POST to /Payroll/AxPostRefinalisePeriod → same authorisation error
6. Verify standard user CAN view audit history (AxGetAuditHistory returns data)
7. Verify standard user CAN view period audit summary

### Owner Validation
8. Log in as Owner → Unlock button visible, unlock succeeds
9. Log in as SuperAdmin → Unlock button visible, unlock succeeds

## Scenario 13: Supplier Protection

### Steps
1. Finalise a period (triggers creation of "Payroll (Internal)" supplier)
2. Navigate to /Purchases/Suppliers
3. Verify "Payroll (Internal)" does NOT appear in the user-facing supplier list
4. Attempt to delete "Payroll (Internal)" via direct API → error: "This supplier is system-generated and cannot be deleted."
5. Verify payroll P&L entries still reference the supplier correctly

### Idempotency
6. Finalise a second period → no duplicate supplier created (EnsurePayrollPnlSetupAsync is idempotent)
7. Verify only one "Payroll (Internal)" supplier exists per business

## Verification Checklist

| # | Check | Pass? |
|---|-------|-------|
| 1 | Phase B migration applies without error | |
| 2 | PayslipStatusType values 4 and 5 seeded | |
| 3 | PayslipAuditActionType values 1, 2, 3 seeded | |
| 4 | Unlock Finalised period → status = Unlocked | |
| 5 | Unlock Re-finalised period → status = Unlocked | |
| 6 | Re-finalise Unlocked period → status = Re-finalised | |
| 7 | All payslip statuses cascade with period status | |
| 8 | Edits blocked on Finalised periods | |
| 9 | Edits blocked on Re-finalised periods | |
| 10 | Edits allowed on Unlocked periods | |
| 11 | Earning line changes create audit entries | |
| 12 | Manager notes changes create audit entries | |
| 13 | Audit entries have correct FieldName format | |
| 14 | Duplicate earning types use positional index | |
| 15 | Audit history ordered newest first | |
| 16 | Period audit summary grouped by employee | |
| 17 | P&L entries created on first finalisation | |
| 18 | P&L entries cancelled + recreated on re-finalisation | |
| 19 | Cancelled entries retain original amounts | |
| 20 | Optimistic concurrency prevents double-unlock | |
| 21 | Owner can unlock and re-finalise | |
| 22 | SuperAdmin can unlock and re-finalise | |
| 23 | Standard user cannot unlock or re-finalise | |
| 24 | Standard user can view audit history | |
| 25 | System-generated supplier hidden from user lists | |
| 26 | System-generated supplier cannot be deleted | |
| 27 | P&L description format: "Payroll - {Month} {Year}" | |
| 28 | InvoiceDate = last day of period month | |
| 29 | ProcessedAtUtc set on re-finalisation | |
| 30 | Unlock warning dialog shows correct month/year | |
| 31 | Transaction rollback on P&L failure | |
| 32 | Tenant isolation enforced on all endpoints | |

## Database Queries for Manual Inspection

```sql
-- Check period status
SELECT * FROM [payroll].[PayslipPeriod] WHERE Id = @PeriodId

-- Check payslip statuses match period
SELECT PayslipStatusTypeId, COUNT(*) FROM [payroll].[Payslip] WHERE PayslipPeriodId = @PeriodId GROUP BY PayslipStatusTypeId

-- Check audit entries for a payslip
SELECT * FROM [payroll].[PayslipAuditLog] WHERE PayslipId = @PayslipId ORDER BY CreatedAtUtc DESC

-- Check all audit entries for a period (via payslip join)
SELECT PayslipAuditLog.*, Payslip.Id AS PayslipId, Employee.FirstName, Employee.LastName
FROM [payroll].[PayslipAuditLog]
INNER JOIN [payroll].[Payslip] ON PayslipAuditLog.PayslipId = Payslip.Id
INNER JOIN [payroll].[Employee] ON Payslip.EmployeeId = Employee.Id
WHERE Payslip.PayslipPeriodId = @PeriodId
ORDER BY PayslipAuditLog.CreatedAtUtc DESC

-- Check P&L entries for a period
SELECT * FROM [purchase].[Purchase] WHERE PayslipPeriodId = @PeriodId ORDER BY IsCancelled, CreatedAtUtc DESC

-- Check cancelled vs active P&L entries
SELECT Purchase.IsCancelled, Purchase.InvoiceNumber, Purchase.TotalAmount, Purchase.CancelledByUserId, Purchase.CancelledAtUtc
FROM [purchase].[Purchase]
WHERE Purchase.PayslipPeriodId = @PeriodId
ORDER BY Purchase.IsCancelled, Purchase.CreatedAtUtc DESC

-- Check system-generated supplier exists
SELECT * FROM [purchase].[Supplier] WHERE IsSystemGenerated = 1

-- Verify only one system-generated supplier per business
SELECT Supplier.BusinessId, COUNT(*) AS SystemSupplierCount
FROM [purchase].[Supplier]
WHERE Supplier.IsSystemGenerated = 1
GROUP BY Supplier.BusinessId

-- Check expense categories created
SELECT * FROM [purchase].[ExpenseCategory] WHERE Name LIKE 'Payroll%'

-- Verify audit action types
SELECT * FROM [payroll].[PayslipAuditActionType]

-- Verify status type lookup (Phase B additions)
SELECT * FROM [payroll].[PayslipStatusType] WHERE Id IN (4, 5)

-- Check ProcessedAtUtc on re-finalised periods
SELECT PayslipPeriod.Id, PayslipPeriod.Year, PayslipPeriod.Month, PayslipPeriod.PayslipStatusTypeId, PayslipPeriod.ProcessedAtUtc
FROM [payroll].[PayslipPeriod]
WHERE PayslipPeriod.PayslipStatusTypeId = 5

-- Verify concurrency: check for duplicate P&L entries (should never happen)
SELECT Purchase.PayslipPeriodId, Purchase.IsCancelled, COUNT(*) AS EntryCount
FROM [purchase].[Purchase]
WHERE Purchase.PayslipPeriodId IS NOT NULL
GROUP BY Purchase.PayslipPeriodId, Purchase.IsCancelled
HAVING COUNT(*) > 2
```
