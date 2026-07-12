# Purchase Import Automation — Testing Scenarios

## Prerequisites

1. Run migrations 117–119 against your Portal database
2. Seed `purchase_import` into `PlanFeature` for Professional/Enterprise plans (same pattern as the attachments seed)
3. Ensure you have at least one active Supplier and one active Expense Category in your business
4. Log in as a Professional plan user
5. Prepare a test CSV file (sample below)

**Sample test file (`test-purchases.csv`):**
```csv
Date,Invoice No,Description,Amount,VAT,Total,Country
2026-07-01,INV-001,Office supplies,100.00,20.00,120.00,
2026-07-03,INV-002,Cloud hosting,250.00,50.00,300.00,
2026-07-05,INV-003,EU software license,80.00,0,80.00,Germany
2026-07-10,,Coffee beans,15.50,3.10,18.60,
```

---

## Scenario 1: Upload & Auto-Detect (Happy Path)

1. Navigate to **Purchasing → Import Purchases**
2. Select a supplier from the dropdown
3. Leave template as "Auto-detect (no template)"
4. Drag the sample CSV into the upload zone (or click browse)
5. Click "Upload & Preview"
6. **Expected:** BlockUI spinner → redirect to Preview page showing 4 rows, summary cards (4 Total, 4 Valid, 0 Invalid), batch total = €518.60

---

## Scenario 2: Preview & Confirm Import

1. On the Preview page, verify:
   - All 4 rows show green "Valid" status
   - Amounts display correctly in the Excl. VAT / VAT / Total columns
   - Row #3 shows Country = "Germany"
2. Click "Confirm Import (4 rows)"
3. **Expected:** SweetAlert2 confirmation dialog → BlockUI "Importing purchases..." → success message "4 purchases imported. Total: 518.60" → redirect to Purchase list
4. Navigate to Purchase list → verify all 4 new records appear

---

## Scenario 3: File Rejection

| Action | Expected Error |
|--------|---------------|
| Upload a .docx file | "Only CSV, XLSX, and XLS files are accepted." |
| Upload a 6 MB CSV | "File size exceeds the 5 MB limit." |
| Upload a CSV with 501+ data rows | "File contains more than 500 data rows." |
| Upload without selecting a supplier | "Upload & Preview" button stays disabled |

---

## Scenario 4: Validation Errors in Preview

Create a CSV with intentional errors:
```csv
Date,Invoice No,Amount,VAT,Total
bad-date,INV-100,100.00,20.00,120.00
2026-07-01,INV-101,-50.00,10.00,-40.00
2026-07-02,INV-102,200.00,-5.00,195.00
```

1. Upload this file
2. **Expected:** Preview shows 3 rows:
   - Row 1: red "Invalid" — "Invalid invoice date"
   - Row 2: red "Invalid" — "Amount must be greater than zero"
   - Row 3: red "Invalid" — "VAT amount cannot be negative"
3. "Confirm Import" button should be disabled (0 valid rows)

---

## Scenario 5: Row Removal

1. Upload a valid CSV with 4 rows
2. On the Preview page, click the ✕ button on row 2
3. **Expected:** Page reloads, row 2 disappears, summary shows "3 Total, 3 Valid"
4. Confirm import → only 3 purchases created

---

## Scenario 6: Duplicate Detection

1. Import the sample CSV file (creates 4 purchases with INV-001 through INV-003)
2. Upload the same file again for the same supplier
3. **Expected:** Preview shows rows with amber "Duplicate" badge and warning: "Potential duplicate — a matching purchase already exists."
4. Duplicates are warnings only — you can still confirm the import if you choose

---

## Scenario 7: Parser Template Usage

1. Navigate to **Purchasing → Import Purchases → Manage Templates**
2. Click "Create Template"
3. Fill in: Name = "Monthly Supplier CSV", Supplier = your test supplier, Format = CSV
4. Add mappings: Date → InvoiceDate, Invoice No → InvoiceNumber, Amount → AmountExcludingVat, VAT → VatAmount, Total → TotalAmount
5. Save the template
6. Go back to Import page, select the same supplier → the template appears in the dropdown
7. Select the template and upload the file
8. **Expected:** Parsing uses the template's column mappings (same result as auto-detect for this simple format)

---

## Scenario 8: Supplier Profile Defaults

1. On the Upload page, select a supplier
2. Note the "Supplier Defaults" section (may show "none")
3. Navigate to Template management and set supplier defaults: Category = "Office Expenses", Origin = Domestic, Country = "Cyprus"
4. Upload a CSV that does NOT have a Category or Country column
5. **Expected:** Preview shows each row with the default category and country pre-populated from the supplier profile

---

## Scenario 9: EU Reverse Charge Validation

Create a CSV with origin type:
```csv
Date,Amount,VAT,Total,Origin Type,Country
2026-07-01,500.00,100.00,600.00,EU Reverse Charge,
2026-07-02,300.00,0,300.00,EU Reverse Charge,Germany
```

1. Upload this file
2. **Expected:**
   - Row 1: Invalid — "EU Reverse Charge purchases must have zero VAT" AND "Country is required for this origin type"
   - Row 2: Valid (VAT = 0 and Country provided)

---

## Scenario 10: Plan Gating

1. Log in as a Starter/Foundation plan user (no `purchase_import` module)
2. Navigate to `/PurchaseImport`
3. **Expected:** Access denied / upgrade required page (via `ModuleAccessAttribute`)
4. The "Import Purchases" link should NOT appear in the sidebar navigation

---

## Scenario 11: Managed Templates (SuperAdmin)

1. Log in as a SuperAdmin
2. Go to Parser Templates and create a template — it will be tagged as "Managed"
3. Log in as a regular business user
4. **Expected:** The managed template appears in the list with a "Managed" badge and "Read-only" instead of Edit/Delete buttons
5. The user CAN use the template for imports but CANNOT edit or delete it

---

## Quick Smoke Test (5 minutes)

If you only have time for one pass:

1. Navigate to Import Purchases → select a supplier → upload the 4-row sample CSV
2. Preview loads with 4 valid rows and correct totals
3. Click Confirm → success message → 4 purchases visible in Purchase list
4. Go to Templates → Create a template → verify it appears in the list
5. Re-upload the same file → duplicate warnings appear on all rows
