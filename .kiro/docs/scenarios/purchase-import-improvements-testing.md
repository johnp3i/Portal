# Purchase Import Improvements — Testing Scenarios

## Scenario 1: Multi-Section File (Meta CSV)

1. Upload the Meta invoice CSV using the META template (Header Row: 10, Data Start: 11)
2. **Expected:** Only the first payment section (MasterCard) is imported — parser stops at the second "Date,Transaction ID,Amount,Currency" header
3. The "Ad credit" section and footer text should NOT appear as rows
4. You should see ~34 valid rows (the MasterCard payments) — not 44+ with garbage rows

---

## Scenario 2: Auto-Detection Fallback

1. Create a template with deliberately wrong settings (e.g., Header Row: 50 for the DatabaseMart CSV)
2. Select that template and upload the DatabaseMart CSV
3. **Expected:** The system falls back to auto-detection, shows the Preview page with an amber warning banner: "Template did not match. Results shown using auto-detection."
4. Rows should still be parsed correctly via auto-detect

---

## Scenario 3: Template Test (Before Saving)

1. Go to Parser Templates → click "Create Template" or "Edit" on an existing one
2. Set up column mappings (e.g., Date → InvoiceDate, Amount → TotalAmount)
3. Click "Test (first 5 rows)" after selecting a sample CSV file
4. **Expected:** A mini-preview table appears inside the modal showing the first 5 parsed rows with Date, Invoice No, Description, Amount, Country columns
5. If mappings are wrong (e.g., wrong header row), the result shows: "No rows parsed. Header row X may not contain the expected column names."

---

## Scenario 4: Create Missing Categories

1. Upload a CSV where a column contains category names that don't exist in your system (e.g., "Digital Marketing", "Cloud Services")
2. On the Preview page, those rows should show a warning: "Expense category is required — assign before import"
3. **Expected:** An amber banner appears: "Some rows reference categories that don't exist yet." with a "Create Missing Categories" button
4. Click the button
5. **Expected:** BlockUI → success SweetAlert ("2 categories created.") → page reloads → rows now show the resolved category and status changes to Valid

---

## Scenario 5: Import History

1. Successfully import a file (confirm the import)
2. Navigate back to the Import Purchases page
3. **Expected:** An "Import History" section appears below the upload form showing: Date, Details (file name + total), Rows count
4. The most recent import should be at the top
5. Import another file → history shows 2 entries

---

## Scenario 6: Send to Bulk Entry

1. Upload a CSV and reach the Preview page
2. Click "Send to Bulk Entry"
3. **Expected:** SweetAlert confirmation → BlockUI → redirects to /Purchase/BulkEntry
4. The Bulk Entry form should be pre-populated with all imported rows (dates, amounts, invoice numbers filled in)
5. An info SweetAlert confirms: "X rows transferred from file import. Review and submit when ready."
6. The import session should be deleted (going back to /PurchaseImport shows no pending session)

---

## Scenario 7: Cancel Import

1. Upload a CSV and reach the Preview page
2. Click "Cancel Import"
3. **Expected:** SweetAlert confirmation ("Cancel this import? The parsed data will be discarded.")
4. Confirm → redirects to /PurchaseImport
5. The session should be deleted — uploading the same file again creates a fresh session

---

## Scenario 8: Session Limit (5 max)

1. Upload 5 different files WITHOUT confirming any (just leave them on preview pages by navigating away)
2. Try to upload a 6th file
3. **Expected:** Error message: "Too many active import sessions. Please confirm or cancel existing imports before starting a new one."
4. Wait 24 hours (or manually clear sessions) → the limit resets

---

## Scenario 9: BOM File Handling

1. Open Notepad, save a CSV with "Save as UTF-8 with BOM" (the default in Windows Notepad)
2. Use auto-detection (no template) to upload
3. **Expected:** Headers are matched correctly — the BOM character doesn't interfere with column name matching (e.g., "Date" is found, not "\uFEFFDate")

---

## Scenario 10: Excel Magic-Byte Validation

1. Take a .docx file and rename it to .xlsx
2. Try to upload it
3. **Expected:** Error: "The file could not be read. Please verify the format is a valid Excel file."
4. Upload a real .xlsx file → works normally

---

## Quick Smoke Test (3 minutes)

1. Upload the DatabaseMart sample CSV with auto-detect → 6 rows shown
2. Click "Create Missing Categories" if any appear, or bulk-apply a category
3. Confirm Import → success
4. Go back to Import page → Import History shows the entry
5. Upload the Meta CSV with the META template → only ~34 rows (no second section)
6. Click "Send to Bulk Entry" → Bulk Entry form pre-populated
