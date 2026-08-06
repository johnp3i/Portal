# Payroll Phase C — Testing Scenarios (Reporting & Export)

## Prerequisites

- All Phase A migrations applied (166, 167) and Phase B migrations applied (PayslipAuditLog, PayslipAuditActionType, Purchase/Supplier alterations)
- Phase C migrations applied (PayslipEmailLog table, email audit schema)
- Phase A and B seed data in place (PayslipStatusType values 1–5, EarningTypes, DeductionTemplates, PayslipAuditActionType values)
- User logged in with Enterprise tier subscription (payroll module access)
- Owner account available (has `IsOwner` claim set to `true`)
- SuperAdmin account available (is in `SuperAdmin` role)
- Standard user account available (no Owner claim, no SuperAdmin role, but has payroll module access)
- At least one period in Finalised or Re-finalised status with 3+ payslips (from Phase A/B flow)
- At least one employee with a valid email address configured
- At least one employee without an email address (for edge case testing)
- PuppeteerSharp installed and headless Chromium available on server environment
- `appsettings.json` configured with batch email settings:
  ```json
  "Payroll": {
    "BatchEmailMaxSize": 50,
    "BatchEmailDelayBetweenSendsMs": 500
  }
  ```
- SMTP/email provider configured and reachable (or test mailbox available)
- SignalR hub registered and accessible for the authenticated user

## Scenario 1: Payslip PDF Download

### Steps
1. Navigate to /Payroll/PeriodDetail/{id} with a Finalised period
2. Click on an individual payslip row to open the payslip detail
3. Verify "Download PDF" button is visible
4. Click "Download PDF"
5. Verify browser downloads a PDF file named `Payslip_{EmployeeName}_{Month}_{Year}.pdf`
6. Open the PDF and verify:
   - A4 page size (portrait)
   - Company branding/logo at top
   - Employee name, position, and department
   - Period (month/year) displayed
   - All earning lines with amounts
   - All deduction lines with amounts
   - Total Earnings, Total Deductions, Net Salary summary
   - Employer contributions section (if applicable)
   - Manager notes (if present)

### Edge Cases
7. Download PDF for payslip with no earning lines → PDF still generates with €0.00 totals
8. Download PDF for payslip with very long employee name → name wraps correctly, no overflow
9. Download PDF for payslip in Re-finalised status → same flow, includes updated data

## Scenario 2: Download All (ZIP)

### Steps
1. Navigate to /Payroll/PeriodDetail/{id} with a Finalised period containing 3+ payslips
2. Verify "Download All" button is visible
3. Click "Download All"
4. Verify BlockUI shows "Generating payslips..." message
5. Verify browser downloads a ZIP file named `Payslips_{Month}_{Year}.zip`
6. Extract the ZIP and verify:
   - One PDF per finalised payslip in the period
   - Each PDF named `Payslip_{EmployeeName}_{Month}_{Year}.pdf`
   - Each PDF contains correct data for the respective employee
   - ZIP does not include payslips in Draft/Preview/Unlocked status

### Edge Cases
7. Download All on period with only 1 payslip → ZIP still generated with single PDF inside
8. Download All on period with 50+ payslips → ZIP generates successfully (may take longer, BlockUI remains visible)
9. Download All for Re-finalised period → includes only Re-finalised payslips with updated totals

## Scenario 3: Employee Payslip History

### Steps
1. Navigate to Employee Detail page for an employee with payslips across multiple periods
2. Click "Payslip History" tab or section
3. Verify list shows all payslips for this employee, newest first
4. Verify each row shows: Period (Month Year), Status badge, Net Salary, Download PDF link
5. Use year filter dropdown → select a specific year
6. Verify list filters to show only payslips from that year
7. Verify summary row at top/bottom shows:
   - Total payslips displayed
   - Total earnings for the filtered period
   - Total net salary for the filtered period
8. Click "Download PDF" link on any row → downloads that individual payslip PDF

### Edge Cases
9. Employee with no payslips → "No payslip history found" message displayed
10. Employee with payslips spanning 3+ years → year filter shows all relevant years
11. Filter by year with no payslips → empty state message, summary shows €0.00

## Scenario 4: Annual Summary

### Steps
1. Navigate to Employee Detail page → Payslip History section
2. Click "Annual Summary" button (or select year and click "Annual Summary")
3. Verify the annual summary view shows:
   - Employee name and year in header
   - Monthly breakdown table: each month with Earnings, Deductions, Net Salary
   - Annual totals row at bottom: sum of all months
   - Employer contributions total for the year
4. Click "Download PDF" on the annual summary
5. Verify PDF generates with:
   - Branded A4 layout
   - Full 12-month grid (months without payslips show €0.00 or "—")
   - Correct annual totals matching sum of monthly values

### Edge Cases
6. Annual summary for year with only 1 payslip → shows single month, rest blank
7. Annual summary where employee joined mid-year → months before joining show "—"
8. Annual summary including Re-finalised payslips → uses updated Re-finalised values

## Scenario 5: Earnings Breakdown Report

### Steps
1. Navigate to sidebar → "Earnings Breakdown" link
2. Verify the report page loads with period filter (dropdown of Finalised/Re-finalised periods)
3. Select a period from the dropdown
4. Click "Generate Report"
5. Verify the report displays:
   - Grouped by earning type (e.g., Basic Salary, Overtime, Bonus, Commission)
   - Each group shows: earning type name, total amount, number of lines/employees
   - Grand total at the bottom
6. Verify only Finalised (3) and Re-finalised (5) payslips are included in totals
7. Click "Export to Excel"
8. Verify Excel file downloads with:
   - Branded header row (background #0D5EA6, white text)
   - Columns: Earning Type, Employee Count, Total Amount
   - Data matches on-screen report exactly
   - File named `EarningsBreakdown_{Month}_{Year}.xlsx`

### Edge Cases
9. Period with no earning lines → empty report with message "No earning data for this period"
10. Period with 10+ earning types → all types displayed, scrollable
11. Filter by specific earning type (if available) → totals recalculate

## Scenario 6: Period Summary Report

### Steps
1. Navigate to sidebar → "Period Summary" link
2. Verify the report page loads with period filter and optional department filter
3. Select a period from the dropdown
4. Click "Generate Report"
5. Verify the report displays a consolidated table with:
   - One row per employee (sorted alphabetically by name)
   - Columns: Employee Name, Department, Total Earnings, Total Deductions, Net Salary, Employer Contributions
   - Totals row at the bottom summing all columns
6. Apply department filter → table filters to only employees in that department
7. Verify totals row updates to reflect filtered data
8. Click "Export to PDF" → downloads PDF with same tabular layout (branded A4)
9. Click "Export to Excel" → downloads Excel file with:
   - Branded header row (#0D5EA6)
   - All columns from the on-screen table
   - File named `PeriodSummary_{Month}_{Year}.xlsx`

### Edge Cases
10. Period with 1 employee → single row + totals row (totals match the single row)
11. Department filter with no employees → empty state: "No employees found for this department"
12. Period with 100+ employees → table renders with pagination or scroll, export includes all rows
13. Only Finalised (3) and Re-finalised (5) payslips included — Draft/Preview/Unlocked excluded

## Scenario 7: Send Payslip by Email

### Steps
1. Navigate to payslip detail for a Finalised payslip
2. Verify "Send by Email" button visible (Owner or SuperAdmin only)
3. Log in as Owner → click "Send by Email"
4. Verify SweetAlert2 confirmation: "Send payslip to {EmployeeName} at {email}?"
5. Click "Send"
6. Verify BlockUI shows "Sending..." message
7. Verify success message: "Payslip sent successfully to {email}"
8. Verify email received in employee's inbox with:
   - Subject line containing employee name and period
   - PDF attachment matching the downloadable payslip
   - Professional email body with company branding

### Edge Cases
9. Send to employee without email address → error: "Employee does not have an email address configured"
10. Email delivery failure (SMTP error) → error message: "Failed to send email. Please try again."
11. Send payslip for Re-finalised period → same flow, uses updated payslip data

## Scenario 8: Batch Email Send

### Steps
1. Navigate to /Payroll/PeriodDetail/{id} with a Finalised period (3+ employees with email)
2. Verify "Email All Payslips" button visible (Owner or SuperAdmin only)
3. Click "Email All Payslips"
4. Verify SweetAlert2 confirmation: "Send payslips to {count} employees?"
5. Click "Send All"
6. Verify SignalR progress notifications appear in real-time:
   - "Sending 1 of {total}..."
   - "Sending 2 of {total}..."
   - Progress updates until complete
7. Verify completion message: "All payslips sent successfully ({count}/{total})"
8. Verify each employee receives their individual payslip PDF by email

### Edge Cases
9. Batch with some employees missing email → sends to those with email, reports: "Sent {sent} of {total}. {skipped} employees have no email address."
10. Partial failure (e.g., SMTP timeout on 1 email) → progress shows failure, final report: "Sent {sent}, Failed {failed} of {total}"
11. User navigates away during batch send → batch continues server-side, results visible in email audit log
12. Batch with configurable delay → verify ~500ms gap between sends (configurable via appsettings)

## Scenario 9: Duplicate Email Detection

### Steps
1. Send a payslip email (Scenario 7) → succeeds
2. Navigate back to the same payslip detail
3. Click "Send by Email" again
4. Verify SweetAlert2 warning: "This payslip was already emailed to {email} on {date}. Do you want to send again?"
5. Click "Cancel" → email not sent, no new log entry
6. Click "Send by Email" again → Click "Resend"
7. Verify email sent successfully
8. Verify new entry added to email audit log (two entries total for this payslip)

### Batch Duplicate Detection
9. Trigger "Email All Payslips" on a period where some payslips were already emailed
10. Verify warning: "{count} payslips have already been emailed. Do you want to resend all?"
11. Click "Resend All" → all payslips emailed (including previously sent ones)
12. Verify email log shows new entries for all payslips

## Scenario 10: Employee Statement Export

### Steps
1. Navigate to Employee Detail page
2. Click "Export Statement" button
3. Verify date range picker appears (From date, To date)
4. Select a date range spanning 3 months
5. Click "Generate"
6. Verify PDF downloads with:
   - Branded A4 layout
   - Employee name and statement period in header
   - All payslips within the date range listed chronologically
   - Monthly entries showing: Period, Earnings, Deductions, Net Salary
   - Running totals or summary at the bottom
   - File named `Statement_{EmployeeName}_{FromDate}_to_{ToDate}.pdf`

### Edge Cases
7. Date range with no payslips → PDF with header only and "No payslips found for this period" message
8. Date range spanning 12+ months → all months included, multi-page PDF if needed
9. Date range where employee was not yet active → shows only months where payslips exist

## Scenario 11: Email Audit Log

### Steps
1. Navigate to payslip detail for a payslip that has been emailed
2. Verify "Email History" section or tab visible
3. Verify log entries show:
   - Sent date/time (UTC formatted for display)
   - Recipient email address
   - Sent by user (full name)
   - Status (Success/Failed)
4. Navigate to /Payroll/PeriodDetail/{id}
5. Verify "Email Summary" section shows:
   - Total emails sent for this period
   - Successful count
   - Failed count
   - Last sent timestamp

### Edge Cases
6. Payslip never emailed → "No emails sent for this payslip" message
7. Period with mixed success/failure → summary shows correct breakdown
8. Multiple resends → all entries visible in chronological order

## Scenario 12: Role Restriction (Email)

### Steps
1. Log in as standard user (not Owner, not SuperAdmin)
2. Navigate to payslip detail for a Finalised payslip
3. Verify "Send by Email" button is NOT visible
4. Navigate to PeriodDetail → verify "Email All Payslips" button is NOT visible
5. Direct API POST to email endpoint → `{ success: false, message: "Only the business owner or a SuperAdmin can send payslip emails." }`

### Owner/SuperAdmin Validation
6. Log in as Owner → "Send by Email" and "Email All Payslips" buttons visible, actions succeed
7. Log in as SuperAdmin → same buttons visible, actions succeed

### View Access
8. Standard user CAN view email audit log (read-only)
9. Standard user CAN view email summary on period detail

## Scenario 13: Batch Size Limit

### Steps
1. Configure appsettings: `"BatchEmailMaxSize": 5` (low value for testing)
2. Navigate to PeriodDetail with 8 employees
3. Click "Email All Payslips"
4. Verify warning/error: "Batch email is limited to 5 recipients. This period has 8 eligible employees. Please reduce the batch or contact an administrator."
5. Verify email is NOT sent

### Configuration Validation
6. Change appsettings to `"BatchEmailMaxSize": 50` → period with 8 employees processes normally
7. Period with exactly the limit (50 employees) → processes successfully
8. Period with 51 employees → batch size exceeded error
9. Missing configuration key → falls back to default (50)

## Scenario 14: Mobile Responsive

### Steps (375px — Mobile)
1. Open Earnings Breakdown report at 375px viewport width
2. Verify: table scrolls horizontally or stacks vertically
3. Verify: filter controls stack vertically with full width
4. Verify: export buttons remain accessible (not hidden or overflowing)
5. Verify: text is readable without horizontal scrolling on the main content

### Steps (810px — Tablet)
6. Open Period Summary report at 810px viewport width
7. Verify: table columns visible without excessive horizontal scroll
8. Verify: filter panel and department dropdown accessible
9. Verify: export buttons (PDF/Excel) are not overlapping
10. Verify: pagination controls are usable at this width

### General Responsive Checks
11. Employee Payslip History at 375px → year filter and summary row readable
12. Email audit log at 375px → entries stack or table scrolls appropriately
13. Batch email progress notifications → visible and readable at mobile widths

## Scenario 15: Navigation Links

### Steps
1. Log in with payroll module access
2. Verify sidebar contains "Earnings Breakdown" link under Payroll section
3. Click "Earnings Breakdown" → navigates to /Payroll/EarningsBreakdown
4. Verify page loads correctly with period filter
5. Navigate back to sidebar
6. Verify sidebar contains "Period Summary" link under Payroll section
7. Click "Period Summary" → navigates to /Payroll/PeriodSummary
8. Verify page loads correctly with period and department filters

### Edge Cases
9. User without payroll module access → Payroll sidebar section not visible at all
10. Mobile sidebar (collapsed) → links accessible via hamburger menu
11. Active page highlighted in sidebar when on Earnings Breakdown or Period Summary

## Verification Checklist

| # | Check | Pass? |
|---|-------|-------|
| 1 | Phase C migration applies without error | |
| 2 | PayslipEmailLog table created with correct schema | |
| 3 | Individual payslip PDF downloads correctly (A4, branded) | |
| 4 | PDF contains all earning lines, deductions, and net salary | |
| 5 | Download All generates ZIP with one PDF per payslip | |
| 6 | ZIP excludes non-finalised payslips | |
| 7 | Employee Payslip History displays with year filter | |
| 8 | History summary row shows correct totals | |
| 9 | Annual Summary shows 12-month grid with totals | |
| 10 | Annual Summary PDF downloads correctly | |
| 11 | Earnings Breakdown groups by earning type | |
| 12 | Earnings Breakdown Excel export has branded header (#0D5EA6) | |
| 13 | Period Summary shows all employees with correct totals | |
| 14 | Period Summary department filter works | |
| 15 | Period Summary PDF and Excel exports work | |
| 16 | Reports only include Finalised (3) and Re-finalised (5) payslips | |
| 17 | Send Payslip by Email delivers PDF attachment | |
| 18 | Batch Email sends to all eligible employees | |
| 19 | SignalR progress notifications broadcast during batch send | |
| 20 | Duplicate email detection warns before resend | |
| 21 | Employee Statement PDF covers selected date range | |
| 22 | Email audit log records all send attempts | |
| 23 | Period-level email summary shows correct counts | |
| 24 | Only Owner/SuperAdmin can send emails | |
| 25 | Standard user can view email audit log (read-only) | |
| 26 | Batch size limit enforced per appsettings config | |
| 27 | Default batch size (50) used when config missing | |
| 28 | Earnings Breakdown report responsive at 375px | |
| 29 | Period Summary report responsive at 810px | |
| 30 | Sidebar navigation links to Earnings Breakdown and Period Summary | |
| 31 | Batch email delay configurable (default 500ms) | |
| 32 | Email failure does not block remaining batch sends | |
| 33 | Employee without email address handled gracefully | |
| 34 | System-generated supplier not visible in reports | |
| 35 | Tenant isolation enforced on all report endpoints | |

## Database Queries for Manual Inspection

```sql
-- Check email log entries
SELECT * FROM [payroll].[PayslipEmailLog] WHERE PayslipEmailLog.PayslipId = @PayslipId ORDER BY PayslipEmailLog.SentAtUtc DESC

-- Check email summary for a period
SELECT 
    COUNT(*) AS TotalSent,
    SUM(CASE WHEN PayslipEmailLog.IsSuccess = 1 THEN 1 ELSE 0 END) AS Successful,
    SUM(CASE WHEN PayslipEmailLog.IsSuccess = 0 THEN 1 ELSE 0 END) AS Failed
FROM [payroll].[PayslipEmailLog]
INNER JOIN [payroll].[Payslip] ON PayslipEmailLog.PayslipId = Payslip.Id
WHERE Payslip.PayslipPeriodId = @PeriodId

-- Check payslip status for report eligibility (only Finalised/Re-finalised)
SELECT Payslip.PayslipStatusTypeId, COUNT(*) AS PayslipCount
FROM [payroll].[Payslip]
WHERE Payslip.PayslipPeriodId = @PeriodId
GROUP BY Payslip.PayslipStatusTypeId

-- Verify earnings breakdown data
SELECT EarningType.Name, SUM(PayslipEarningLine.Amount) AS TotalAmount, COUNT(*) AS LineCount
FROM [payroll].[PayslipEarningLine]
INNER JOIN [payroll].[EarningType] ON PayslipEarningLine.EarningTypeId = EarningType.Id
INNER JOIN [payroll].[Payslip] ON PayslipEarningLine.PayslipId = Payslip.Id
WHERE Payslip.PayslipPeriodId = @PeriodId AND Payslip.PayslipStatusTypeId IN (3, 5)
GROUP BY EarningType.Name

-- Verify period summary totals
SELECT Employee.FirstName + ' ' + Employee.LastName AS EmployeeName, Payslip.TotalEarnings, Payslip.TotalEmployeeDeductions, Payslip.NetSalary, Payslip.TotalEmployerContributions
FROM [payroll].[Payslip]
INNER JOIN [payroll].[Employee] ON Payslip.EmployeeId = Employee.Id
WHERE Payslip.PayslipPeriodId = @PeriodId AND Payslip.PayslipStatusTypeId IN (3, 5)
ORDER BY Employee.FirstName, Employee.LastName

-- Check batch email settings
-- In appsettings.json: "Payroll": { "BatchEmailMaxSize": 50, "BatchEmailDelayBetweenSendsMs": 500 }

-- Verify system-generated supplier not visible in reports
SELECT * FROM [purchase].[Supplier] WHERE Supplier.IsSystemGenerated = 1

-- Check duplicate email detection (payslips emailed more than once)
SELECT PayslipEmailLog.PayslipId, COUNT(*) AS SendCount, MAX(PayslipEmailLog.SentAtUtc) AS LastSentAtUtc
FROM [payroll].[PayslipEmailLog]
WHERE PayslipEmailLog.IsSuccess = 1
GROUP BY PayslipEmailLog.PayslipId
HAVING COUNT(*) > 1

-- Verify email log entries per period with employee details
SELECT Employee.FirstName + ' ' + Employee.LastName AS EmployeeName, Employee.Email, PayslipEmailLog.SentAtUtc, PayslipEmailLog.IsSuccess, PayslipEmailLog.SentByUserId
FROM [payroll].[PayslipEmailLog]
INNER JOIN [payroll].[Payslip] ON PayslipEmailLog.PayslipId = Payslip.Id
INNER JOIN [payroll].[Employee] ON Payslip.EmployeeId = Employee.Id
WHERE Payslip.PayslipPeriodId = @PeriodId
ORDER BY PayslipEmailLog.SentAtUtc DESC

-- Check annual summary data for an employee
SELECT PayslipPeriod.Year, PayslipPeriod.Month, Payslip.TotalEarnings, Payslip.TotalEmployeeDeductions, Payslip.NetSalary, Payslip.TotalEmployerContributions
FROM [payroll].[Payslip]
INNER JOIN [payroll].[PayslipPeriod] ON Payslip.PayslipPeriodId = PayslipPeriod.Id
WHERE Payslip.EmployeeId = @EmployeeId AND PayslipPeriod.Year = @Year AND Payslip.PayslipStatusTypeId IN (3, 5)
ORDER BY PayslipPeriod.Month

-- Verify employee statement data for date range
SELECT PayslipPeriod.Year, PayslipPeriod.Month, Payslip.TotalEarnings, Payslip.TotalEmployeeDeductions, Payslip.NetSalary
FROM [payroll].[Payslip]
INNER JOIN [payroll].[PayslipPeriod] ON Payslip.PayslipPeriodId = PayslipPeriod.Id
WHERE Payslip.EmployeeId = @EmployeeId
    AND Payslip.PayslipStatusTypeId IN (3, 5)
    AND DATEFROMPARTS(PayslipPeriod.Year, PayslipPeriod.Month, 1) BETWEEN @FromDate AND @ToDate
ORDER BY PayslipPeriod.Year, PayslipPeriod.Month

-- Check role restrictions (verify user claims)
SELECT AspNetUsers.Id, AspNetUsers.Email, AspNetUserClaims.ClaimType, AspNetUserClaims.ClaimValue
FROM [dbo].[AspNetUsers]
LEFT JOIN [dbo].[AspNetUserClaims] ON AspNetUsers.Id = AspNetUserClaims.UserId
WHERE AspNetUserClaims.ClaimType = 'IsOwner'

-- Verify batch size configuration is respected (count eligible employees)
SELECT COUNT(*) AS EligibleEmployeeCount
FROM [payroll].[Payslip]
INNER JOIN [payroll].[Employee] ON Payslip.EmployeeId = Employee.Id
WHERE Payslip.PayslipPeriodId = @PeriodId
    AND Payslip.PayslipStatusTypeId IN (3, 5)
    AND Employee.Email IS NOT NULL
    AND Employee.Email != ''
```
