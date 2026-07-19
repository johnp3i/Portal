# Sales Invoice Import — Testing Scenarios

## Prerequisites

1. Run migration 130 (`130_CreateExternalSalesRecordTable.sql`) against the Portal database
2. Run `Seed_PlanFeature_ZReportImport.sql` (the `zreport_import` module gates both Z-Report and Sales imports)
3. Z-Report feature is enabled via **MyBusiness → Automation** toggle
4. At least one active Revenue Source exists
5. Business has Professional or Enterprise subscription
6. Prepare test CSV (sample below)

**Sample valid CSV (`test-sales.csv`):**
```csv
Date,Invoice No,Net,VAT,Total,Description,Payment Method,Customer ID
01/07/2026,POS-001,100.00,19.00,119.00,Coffee & pastry,Card,
01/07/2026,POS-002,25.50,4.85,30.35,Latte x2,Cash,
02/07/2026,POS-003,45.00,8.55,53.55,Lunch combo,Card,1001
02/07/2026,POS-004,12.00,2.28,14.28,Espresso,Cash,
03/07/2026,POS-005,220.00,41.80,261.80,Catering order,Card,1002
```

---

## Scenario 1: Navigation — Professional Tier

1. Log in as a Professional tier user
2. Check sidebar under Finance → Z-Reports
3. **Expected:** "Sales Records" sub-item visible
4. Click "Sales Records"
5. **Expected:** Records list page loads (empty if first time)

---

## Scenario 2: Upload Happy Path

1. Navigate to **Sales Records → Import Sales**
2. Optionally select a Revenue Source
3. Upload `test-sales.csv`
4. Click "Upload & Preview"
5. **Expected:** Redirect to Preview page showing 5 rows, all "Ready" status, batch total = €479.98

---

## Scenario 3: Preview & Confirm

1. On the Preview page, verify:
   - Total Rows: 5
   - Valid: 5
   - Duplicates: 0
   - Batch Total: €479.98
2. Click "Confirm Import (5 records)"
3. **Expected:** SweetAlert2 confirmation → BlockUI → success → redirect to Sales Records list
4. All 5 records appear in the list

---

## Scenario 4: Duplicate Detection

1. Import `test-sales.csv` once (creates POS-001 through POS-005)
2. Upload the same file again with the same Revenue Source
3. **Expected:** Preview shows 5 rows with yellow "Duplicate" pills
4. Confirm button shows "(0 records)" since duplicates are excluded

---

## Scenario 5: Validation Errors

**Test file with errors (`invalid-sales.csv`):**
```csv
Date,Invoice No,Net,VAT,Total,Description,Payment Method
bad-date,ERR-001,100.00,19.00,119.00,Bad date,Card
03/07/2026,ERR-002,-50.00,10.00,-40.00,Negative net,Cash
03/07/2026,ERR-003,100.00,-5.00,95.00,Negative vat,Card
```

1. Upload this file
2. **Expected:**
   - Row 1: "Invalid" (bad date)
   - Row 2: "Invalid" (negative net)
   - Row 3: "Invalid" (negative VAT)
3. Confirm button shows "(0 records)"

---

## Scenario 6: File Validation

| Action | Expected Error |
|--------|---------------|
| Upload .docx file | "Only CSV files are accepted." |
| Upload 6 MB CSV | "File size exceeds the 5 MB limit." |
| Upload CSV with 501+ rows | "File contains more than 500 data rows." |
| Upload empty CSV | "No data rows found in the file." |
| Upload CSV missing Date column | "Required column 'Date' not found in header." |

---

## Scenario 7: Revenue Source Optional

1. Upload a CSV without selecting a Revenue Source (dropdown on "— No source (general) —")
2. **Expected:** Import works — records created with `RevenueSourceId = NULL`
3. Verify in database

---

## Scenario 8: Customer ID Linking

1. Ensure customer with ID 1001 exists in the Customer table
2. Upload `test-sales.csv` (row 3 has Customer ID = 1001)
3. Confirm import
4. **Expected:** The record for POS-003 has `CustomerId = 1001` in the database

---

## Scenario 9: Cancel & Restore Records

1. Go to **Sales Records** list
2. Click "Cancel" on a record
3. **Expected:** SweetAlert2 confirmation → record shows strikethrough with "Cancelled" pill
4. Click "Restore" on the cancelled record
5. **Expected:** Record returns to "Active" state

---

## Scenario 10: Filter Records

1. Import records across multiple dates and sources
2. Filter by Revenue Source
3. **Expected:** Only matching records shown
4. Filter by Date Range
5. **Expected:** Only records within the range
6. Click "Clear"
7. **Expected:** All records shown

---

## Scenario 11: Semicolons & Alternative Column Names

**Test file (`alt-headers.csv`):**
```csv
Transaction Date;Invoice Number;Amount;Tax;Total;Item;Payment
01/07/2026;ALT-001;50.00;9.50;59.50;Test item;Cash
```

1. Upload this file
2. **Expected:** Auto-detects semicolons and maps alternative column names correctly
3. Preview shows 1 valid row

---

## Scenario 12: Tier Gating — Foundation User

1. Log in as Foundation tier user
2. Navigate directly to `/SalesImport`
3. **Expected:** Access denied / upgrade page shown
4. Try `/SalesImport/Records`
5. **Expected:** Same blocked behaviour

---

## Database Verification Checklist

- [ ] `[revenue].[ExternalSalesRecord]` records have correct `BusinessId`
- [ ] `RevenueSourceId` is NULL when no source selected, set when source chosen
- [ ] `CustomerId` FK is set when provided and customer exists
- [ ] `TotalAmount` = `NetAmount + VatAmount` when Total not in CSV
- [ ] Cancelled records have `IsActive = 0`
- [ ] `[dbo].[AuditLog]` contains "SalesInvoiceImport" action with batch count
