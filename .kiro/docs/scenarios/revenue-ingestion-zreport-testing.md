# Revenue Ingestion (Z-Reports) — Testing Scenarios

## Prerequisites

1. Run migrations 126–129 against your Portal database (schemas, RevenueSource, RevenueSummary, RevenueSummaryLine tables)
2. Log in as a user with Revenue module access
3. Navigate to **MyBusiness → Automation** tab and enable "Z-Report entry (POS revenue recording)" toggle
4. Verify the sidebar shows "Z-Reports" and "Revenue Sources" under Finance → Revenue

---

## Scenario 1: Enable Z-Report Feature Toggle

1. Navigate to **MyBusiness → Automation** tab
2. Locate the "Enable Z-Report entry (POS revenue recording)" toggle — it should be OFF by default
3. Toggle it ON
4. **Expected:** BlockUI spinner → SweetAlert2 success "Z-Report entry enabled. You can now record POS revenue." → toggle turns green
5. Check the sidebar under Finance
6. **Expected:** "Z-Reports" and "Revenue Sources" sub-items appear under Revenue/Receipts
7. Toggle it OFF again
8. **Expected:** SweetAlert2 success "Z-Report entry disabled." → sidebar items disappear on next navigation

---

## Scenario 2: Create First Revenue Source (Happy Path)

1. Navigate to **Finance → Revenue Sources**
2. **Expected:** Empty state message — "No revenue sources configured yet." with "Add First Source" button
3. Click "Add First Source"
4. **Expected:** Modal opens with title "Add Revenue Source"
5. Enter Name: "Main POS", Description: "Front counter register"
6. Click "Create"
7. **Expected:** BlockUI spinner → SweetAlert2 success "Revenue source created successfully." → page reloads → table shows 1 row with Name "Main POS", Status "Active"

---

## Scenario 3: Revenue Source Validation

| Action | Expected Error |
|--------|---------------|
| Submit with empty name | SweetAlert2 warning: "Revenue source name is required." |
| Submit with name > 200 chars | Server returns: "Revenue source name must not exceed 200 characters." |

---

## Scenario 4: Edit Revenue Source

1. On the Revenue Sources page, click "Edit" on "Main POS"
2. **Expected:** Modal opens with pre-filled Name and Description, title "Edit Revenue Source"
3. Change Name to "Main POS (Updated)"
4. Click "Save Changes"
5. **Expected:** BlockUI → SweetAlert2 success → page reloads → table shows updated name

---

## Scenario 5: Deactivate / Activate Revenue Source

1. Click "Deactivate" on an active source
2. **Expected:** SweetAlert2 confirmation dialog with warning icon and red "Yes, deactivate" button
3. Confirm deactivation
4. **Expected:** Status pill changes to red "Inactive", "Activate" button appears
5. Click "Activate" on the inactive source
6. **Expected:** SweetAlert2 confirmation with blue button → re-activated → pill returns to green "Active"

---

## Scenario 6: Create Z-Report — Manual Entry (Happy Path)

**Context:** Kennedy's Cafe monthly Z-Report

1. Navigate to **Finance → Z-Reports**
2. Click "New Z-Report"
3. Fill in header fields:
   - Revenue Source: "Main POS"
   - Period Start: 2021-11-01
   - Period End: 2021-11-30
   - Z-Report Number: 78419
   - Export Date: 2022-01-08 12:40
4. Click "Add VAT Line" and enter:
   - VAT Rate: 5, Net Amount: 19495.03, VAT Amount: 974.75
5. Click "Add VAT Line" again:
   - VAT Rate: 9, Net Amount: 1066.33, VAT Amount: 95.97
6. **Expected (live totals):**
   - Total Net: €20,561.36
   - Total VAT: €1,070.72
   - Total Gross: €21,632.08
   - Total Discount: €0.00
7. Click "Create Z-Report"
8. **Expected:** BlockUI → SweetAlert2 success "Z-Report created successfully." → redirects to Z-Reports list
9. Verify the new record appears in the table with correct Z-Report #, Source, Period, and financial amounts

---

## Scenario 7: Z-Report Validation Errors

| Action | Expected Error |
|--------|---------------|
| Submit without selecting Revenue Source | Client-side SweetAlert2: "Please select a Revenue Source." |
| Submit without Period Start date | Client-side SweetAlert2: "Period Start date is required." |
| Submit with 0 VAT lines | Client-side SweetAlert2: "At least one VAT line is required." |
| Submit with Period End before Period Start | Server returns: "Period end date cannot be before start date." |
| Add two lines with same VAT rate (e.g. 5% twice) | Server returns: "Duplicate VAT rate(s) found: 5.00%. Each VAT rate can only appear once per Z-Report." |
| Enter VAT rate = 150 | Server returns: "VAT line 1: VAT rate must be between 0 and 100." |
| Enter negative Net Amount | Server returns: "VAT line 1: Net amount cannot be negative." |

---

## Scenario 8: Duplicate Z-Report Detection

1. Create a Z-Report with Source "Main POS" and Z-Report Number "78419" (from Scenario 6)
2. Try to create another Z-Report with the same Source and Z-Report Number "78419"
3. **Expected:** Server error: "A Z-Report with number '78419' already exists for this revenue source (ID: X)."
4. Change the Z-Report Number to "78420" and submit
5. **Expected:** Success — no duplicate conflict

---

## Scenario 9: Edit Existing Z-Report

1. On the Z-Reports list, click "Edit" on Z-Report #78419
2. **Expected:** Entry form loads with pre-filled header fields and 2 VAT lines (5% and 9%)
3. Change the Z-Report Number to "78419-R"
4. Remove the 9% VAT line (click ✕)
5. **Expected:** Totals update in real-time (now showing only the 5% line totals)
6. Click "Save Changes"
7. **Expected:** BlockUI → SweetAlert2 success "Z-Report updated successfully." → redirect to list
8. Verify the updated record shows the new Z-Report number and updated financial amounts

---

## Scenario 10: Delete Z-Report

1. On the Z-Reports list, click "Delete" on a Z-Report
2. **Expected:** SweetAlert2 confirmation dialog: "Are you sure you want to delete Z-Report '78419-R'? This action cannot be undone."
3. Confirm deletion
4. **Expected:** BlockUI → SweetAlert2 success "Z-Report deleted successfully." → page reloads → record no longer in list
5. Verify in database: `IsActive = 0` (soft-delete, not hard-delete)

---

## Scenario 11: Z-Reports List Filtering

1. Create at least 3 Z-Reports with different sources, dates, and Z-Report numbers
2. Filter by Revenue Source — select one specific source
3. **Expected:** Only Z-Reports from that source appear
4. Filter by Date From / Date To — set a narrow range
5. **Expected:** Only Z-Reports within that period appear
6. Filter by Z-Report # — type a partial number
7. **Expected:** Partial match search (LIKE %value%)
8. Click "Clear" button
9. **Expected:** All filters reset, full list returns

---

## Scenario 12: Z-Reports List Pagination

1. Create 20+ Z-Reports (can be done via database seed)
2. Navigate to Z-Reports list
3. **Expected:** First page shows 15 records, pagination control at bottom shows "Showing 1–15 of 20+ records"
4. Click "Next" or page 2
5. **Expected:** Remaining records shown, "Previous" button enabled

---

## Scenario 13: Revenue Source Guard — Feature Disabled

1. Navigate to **MyBusiness → Automation** and disable the Z-Report toggle
2. Try accessing `/ZReport/Index` directly via URL
3. **Expected:** Redirect to Revenue Dashboard (not a 403 or error page)
4. Try `/ZReport/Sources`
5. **Expected:** Same redirect behaviour
6. Try `/ZReport/Entry`
7. **Expected:** Same redirect behaviour

---

## Scenario 14: Inactive Revenue Source — Cannot Be Selected

1. Deactivate a Revenue Source (e.g. "Bar Register")
2. Navigate to New Z-Report entry form
3. **Expected:** Only active sources appear in the dropdown — "Bar Register" is NOT listed
4. Attempt to submit via API with the inactive source's ID
5. **Expected:** Server error: "Revenue source is inactive. Please select an active source."

---

## Scenario 15: Z-Report with Discount

1. Create a new Z-Report with:
   - VAT Line 1: Rate 5%, Net 640.00, VAT 32.00, Discount 20.00
   - VAT Line 2: Rate 9%, Net 40.50, VAT 3.42, Discount 2.10
2. **Expected totals:**
   - Total Net: €680.50
   - Total VAT: €35.42
   - Total Gross: €715.92
   - Total Discount: €22.10
3. Submit and verify database values match

---

## Database Verification Checklist

After completing the scenarios, verify in the database:

- [x] `[revenue].[RevenueSource]` has records with correct BusinessId
- [x] `[revenue].[RevenueSummary]` headers have computed totals matching line sums
- [x] `[revenue].[RevenueSummaryLine]` has correct parent FK and VatRate values
- [x] Soft-deleted Z-Reports have `IsActive = 0`
- [x] `[dbo].[AuditLog]` contains entries for Create/Update/Delete/Activate/Deactivate actions on `revenue.RevenueSource` and `revenue.RevenueSummary`
