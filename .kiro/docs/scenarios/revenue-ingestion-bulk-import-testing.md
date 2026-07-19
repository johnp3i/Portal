# Z-Report Bulk Import — Testing Scenarios

## Prerequisites

1. All migrations 126–129 have been run against the Portal database
2. Run `Seed_PlanFeature_ZReportImport.sql` to add the `zreport_import` module to Professional and Enterprise plans
3. Z-Report feature is enabled via **MyBusiness → Automation** toggle
4. At least one active Revenue Source exists (e.g. "Main POS")
5. The business has a **Professional** or **Enterprise** subscription (Foundation users should NOT see the import option)
6. Prepare test CSV files (samples below)

**Sample valid CSV (`test-zreports.csv`):**
```csv
Date From,Date To,Z-Number,VAT Rate,Net Sales,VAT Amount,Discount,Export Date
01/11/2021,01/11/2021,78390,5,640.00,32.00,20.00,02/11/2021 08:15
01/11/2021,01/11/2021,78390,9,40.50,3.42,2.10,02/11/2021 08:15
02/11/2021,02/11/2021,78391,5,700.00,35.00,17.00,03/11/2021 08:20
02/11/2021,02/11/2021,78391,9,23.80,2.68,1.50,03/11/2021 08:20
30/11/2021,30/11/2021,78419,5,19495.03,974.75,750.00,08/01/2022 12:40
30/11/2021,30/11/2021,78419,9,1066.33,95.97,36.72,08/01/2022 12:40
```

---

## Scenario 1: Navigation — Professional Tier Access

1. Log in as a Professional tier user with Z-Reports enabled
2. Check the sidebar under Finance → Z-Reports
3. **Expected:** Three sub-items visible: "Z-Reports", "Revenue Sources", "Import Z-Reports"
4. Click "Import Z-Reports"
5. **Expected:** Upload page loads with Revenue Source dropdown and file upload area

---

## Scenario 2: Navigation — Foundation Tier Gating

1. Log in as a Foundation tier user with Z-Reports enabled
2. Check the sidebar under Finance → Z-Reports
3. **Expected:** Only "Z-Reports" and "Revenue Sources" visible — "Import Z-Reports" is NOT shown
4. Navigate directly to `/ZReportImport`
5. **Expected:** Access denied — redirected to upgrade/soft-gate page

---

## Scenario 3: Upload Happy Path

1. Navigate to **Import Z-Reports**
2. Select "Main POS" from the Revenue Source dropdown
3. Upload `test-zreports.csv` (drag & drop or browse)
4. Click "Upload & Preview"
5. **Expected:** BlockUI "Parsing file..." → redirect to Preview page
6. Preview shows:
   - CSV Rows: 6
   - Z-Reports: 3 (grouped by Date From + Date To + Z-Number)
   - Source: Main POS
   - Duplicates: 0
   - Total Gross: computed sum

---

## Scenario 4: Preview — Correct Grouping

On the Preview page from Scenario 3, verify:

| # | Z-Report # | Period | Net | VAT | Total | VAT Lines |
|---|---|---|---|---|---|---|
| 1 | 78390 | 01/11/2021 | €680.50 | €35.42 | €715.92 | 5%: €640.00, 9%: €40.50 |
| 2 | 78391 | 02/11/2021 | €723.80 | €37.68 | €761.48 | 5%: €700.00, 9%: €23.80 |
| 3 | 78419 | 30/11/2021 | €20,561.36 | €1,070.72 | €21,632.08 | 5%: €19,495.03, 9%: €1,066.33 |

All rows show green "Ready" status pill.

---

## Scenario 5: Confirm Import

1. On the Preview page, click "Confirm Import (3 Z-Reports)"
2. **Expected:** SweetAlert2 confirmation dialog: "Import 3 Z-Report(s) into your revenue records?"
3. Confirm
4. **Expected:** BlockUI "Importing Z-Reports..." → SweetAlert2 success with total gross → redirect to Z-Reports list
5. Navigate to Z-Reports list → verify all 3 new records appear with correct amounts

---

## Scenario 6: File Validation Errors

| Action | Expected Error |
|--------|---------------|
| Upload without selecting Revenue Source | "Upload & Preview" button stays disabled |
| Upload a .docx file | "Only CSV files are accepted for Z-Report import." |
| Upload a 6 MB CSV | "File size exceeds the 5 MB limit." |
| Upload a CSV with 501+ data rows | "File contains more than 500 data rows." |
| Upload an empty CSV (header only) | "No data rows found in the file." |

---

## Scenario 7: CSV Column Validation

**Test file with missing required column (`no-znumber.csv`):**
```csv
Date From,Date To,VAT Rate,Net Sales,VAT Amount
01/11/2021,01/11/2021,5,640.00,32.00
```

1. Upload this file
2. **Expected:** Error: "Required column 'Z-Number' not found in header."

---

## Scenario 8: Row-Level Validation Errors

**Test file with invalid data (`invalid-rows.csv`):**
```csv
Date From,Date To,Z-Number,VAT Rate,Net Sales,VAT Amount,Discount,Export Date
bad-date,01/11/2021,99001,5,640.00,32.00,0,
01/11/2021,01/11/2021,,5,640.00,32.00,0,
01/11/2021,01/11/2021,99003,5,invalid,32.00,0,
```

1. Upload this file
2. **Expected:** Errors shown:
   - "Row 2: Invalid 'Date From' value."
   - OR "Row 3: Z-Number is empty."
   - OR "Row 4: Invalid 'Net Sales' value."

---

## Scenario 9: Duplicate Detection

1. First, import `test-zreports.csv` successfully (creates Z-Reports 78390, 78391, 78419)
2. Upload the same `test-zreports.csv` again with the same Revenue Source
3. **Expected:** Preview page shows all 3 groups with yellow "Duplicate" status pills
4. Duplicates count shows 3
5. "Confirm Import" button shows (0 Z-Reports) since duplicates are excluded by default

---

## Scenario 10: Period Range Z-Reports (Multi-Day)

**Test file with period-spanning Z-Reports (`monthly.csv`):**
```csv
Date From,Date To,Z-Number,VAT Rate,Net Sales,VAT Amount,Discount,Export Date
01/11/2021,30/11/2021,MONTH-NOV,5,19495.03,974.75,750.00,08/01/2022 12:40
01/11/2021,30/11/2021,MONTH-NOV,9,1066.33,95.97,36.72,08/01/2022 12:40
01/12/2021,31/12/2021,MONTH-DEC,5,18200.00,910.00,600.00,10/01/2022 09:00
```

1. Upload this file
2. **Expected:** Preview shows 2 Z-Reports:
   - MONTH-NOV: Period 01/11/2021 – 30/11/2021 (2 VAT lines)
   - MONTH-DEC: Period 01/12/2021 – 31/12/2021 (1 VAT line)
3. Both show "Ready" status
4. Confirm import → verify in database that `PeriodEndDate` is set correctly

---

## Scenario 11: Semicolon-Separated CSV

**Test file using semicolons (`semicolons.csv`):**
```csv
Date From;Date To;Z-Number;VAT Rate;Net Sales;VAT Amount;Discount;Export Date
01/07/2026;01/07/2026;SC-001;5;500.00;25.00;10.00;02/07/2026 08:00
```

1. Upload this file
2. **Expected:** Parses correctly — semicolons auto-detected as separator
3. Preview shows 1 Z-Report with correct values

---

## Scenario 12: Feature Toggle — Z-Reports Disabled

1. Disable Z-Reports via **MyBusiness → Automation**
2. Navigate to `/ZReportImport`
3. **Expected:** Redirect to Revenue Dashboard (same guard as Z-Report pages)

---

## Scenario 13: No Revenue Sources

1. Deactivate all Revenue Sources
2. Navigate to **Import Z-Reports**
3. **Expected:** Warning banner: "No revenue sources configured. Create a revenue source before importing Z-Reports."
4. The Revenue Source dropdown is empty
5. "Upload & Preview" button stays disabled

---

## Scenario 14: Cancel Import

1. Upload a valid CSV and reach the Preview page
2. Click "Cancel & Upload New"
3. **Expected:** Redirect back to the upload page — no records imported
4. Verify no new Z-Reports were created in the database

---

## Scenario 15: Large Batch Import

1. Create a CSV with 100 rows (50 Z-Reports × 2 VAT lines each)
2. Upload and preview
3. **Expected:** Preview shows 50 Z-Reports, all grouped correctly
4. Confirm import
5. **Expected:** All 50 Z-Reports created in a single transaction
6. Check Z-Reports list with pagination (50 + existing records)

---

## Database Verification Checklist

After completing the scenarios, verify in the database:

- [ ] `[revenue].[RevenueSummary]` records created with correct `BusinessId` and `RevenueSourceId`
- [ ] `[revenue].[RevenueSummary].TotalNet/TotalVat/TotalGross` match the sum of child lines
- [ ] `[revenue].[RevenueSummaryLine]` records have correct `RevenueSummaryId` FK
- [ ] `[revenue].[RevenueSummary].PeriodEndDate` is NULL for same-day reports, set for multi-day
- [ ] `[revenue].[RevenueSummary].ExportedAtUtc` is set when CSV contains Export Date
- [ ] `[dbo].[AuditLog]` contains "ZReportBulkImport" action with batch count and total
- [ ] No orphaned `RevenueSummaryLine` records (all have valid parent FK)
- [ ] Duplicate detection correctly identifies Z-Reports by BusinessId + RevenueSourceId + ZReportNumber

---

## Sample CSV for Kennedy's Cafe (Full Month)

Based on the real-world example from the Revenue Ingestion Brief:

```csv
Date From,Date To,Z-Number,VAT Rate,Net Sales,VAT Amount,Discount,Export Date
01/11/2021,30/11/2021,78419,5,19495.03,974.75,,08/01/2022 12:40
01/11/2021,30/11/2021,78419,9,1066.33,95.97,,08/01/2022 12:40
```

**Expected result after import:**
- 1 RevenueSummary: SummaryDate=2021-11-01, PeriodEndDate=2021-11-30, ZReportNumber="78419"
- TotalNet=20561.36, TotalVat=1070.72, TotalGross=21632.08, TotalDiscount=NULL
- 2 RevenueSummaryLines: (5%, €19495.03, €974.75) and (9%, €1066.33, €95.97)
