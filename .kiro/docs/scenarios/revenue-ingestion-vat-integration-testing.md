# Revenue Ingestion — VAT Integration & Advanced Features Testing

## Prerequisites

1. All migrations 126–129 have been run against the Portal database
2. Z-Report feature is enabled via **MyBusiness → Automation** toggle
3. At least one active Revenue Source exists (e.g. "Main POS")
4. At least 2 VAT periods exist for the business (run VAT period generation if needed)
5. At least one VAT period is **not yet submitted** (pending status)
6. At least one Z-Report exists and is assigned to a pending period
7. The business has an active subscription (Foundation tier or above)
8. Run the seed script `Seed_ZReports_FilteringTest.sql` for bulk test data

---

## Scenario 1: VAT Period Assignment — Dropdown on Entry Form

1. Navigate to **Z-Reports → New Z-Report**
2. **Expected:** A "VAT Period" dropdown appears in the header fields grid
3. The dropdown shows "— Not assigned —" as the default option
4. All VAT periods for the business appear in the dropdown, ordered by most recent first
5. Each period displays its `PeriodLabel` (e.g. "Mar–May 2026")
6. Select a period and create the Z-Report
7. **Expected:** The Z-Report is saved with the selected `VatSubmissionPeriodId`
8. Check the Z-Reports list — the "VAT Period" column shows the assigned period label

---

## Scenario 2: VAT Period Assignment — Date-Range Fallback

1. Create a new Z-Report with Period Start = a date within a known unsubmitted VAT period
2. Leave the VAT Period dropdown on "— Not assigned —"
3. Submit the Z-Report
4. **Expected:** The Z-Report is automatically assigned to the period whose date range covers the SummaryDate
5. Check the Z-Reports list — "VAT Period" column shows the auto-assigned period (green pill)

---

## Scenario 3: VAT Period Assignment — No Matching Period

1. Create a Z-Report with a Period Start date that doesn't fall within any existing VAT period (e.g. a very old date)
2. Leave the VAT Period dropdown on "— Not assigned —"
3. Submit the Z-Report
4. **Expected:** Z-Report is saved successfully with `VatSubmissionPeriodId = NULL`
5. The list shows "Unassigned" in the VAT Period column

---

## Scenario 4: VAT Period Assignment — Cannot Assign to Submitted Period

1. Identify a VAT period that has been marked as **submitted**
2. Try to create a Z-Report and select that submitted period from the dropdown
3. **Expected:** Server returns error: "Cannot assign to a submitted VAT period."
4. The Z-Report is NOT saved

---

## Scenario 5: Locked Z-Report — Submitted Period

1. Create a Z-Report assigned to a pending VAT period
2. Navigate to **VAT → Detail** for that period and mark it as submitted
3. Return to Z-Reports and click "Edit" on the Z-Report assigned to that period
4. **Expected:**
   - A locked banner appears: "Locked — assigned to a submitted VAT period."
   - All form fields are disabled (greyed out)
   - The "Save Changes" button is hidden
   - The VAT Period dropdown is disabled
   - Only the "Back to List" button is available

---

## Scenario 6: VAT Period Report — Z-Reports Section

1. Assign at least 2 Z-Reports to the same pending VAT period
2. Navigate to **VAT → Detail** for that period
3. Click "Full Period Report"
4. **Expected:** Between "1. Sales Amount by Month" and "2. Purchases Amount by Month" there is a new section: **"External Revenue (Z-Reports)"**
5. The section shows a table with columns: Source, Z-Report #, Period, Net Amount, VAT Amount, Total, Discount
6. Each assigned Z-Report appears as a row
7. A "Period Total" row at the bottom sums all columns
8. The Output VAT summary card at the top includes Z-Report VAT in its total

---

## Scenario 7: VAT Period Report — No Z-Reports Assigned

1. Navigate to the Period Report for a period that has NO Z-Reports assigned
2. **Expected:** The "External Revenue (Z-Reports)" section shows: "No Z-Reports assigned to this period."

---

## Scenario 8: VAT Period Report — Feature Disabled

1. Disable the Z-Report toggle via **MyBusiness → Automation**
2. Navigate to a VAT Period Report
3. **Expected:** The "External Revenue (Z-Reports)" section does NOT appear at all
4. Re-enable the toggle for subsequent tests

---

## Scenario 9: VAT Detail Page — Z-Reports Section

1. Assign Z-Reports to a pending VAT period
2. Navigate to **VAT → Detail** for that period
3. **Expected:** Between "Sales Invoices" and "Purchases" sections, a new section appears: **"External Revenue (Z-Reports)"**
4. The section shows:
   - A "Z-Report VAT Total" KPI card (blue, showing sum of all Z-Report VAT)
   - A table with columns: Revenue Source, Z-Report #, Period, VAT Amount, Assignment
   - Each row shows the Z-Report data with a green "Explicit" pill for assignment status
   - A total row at the bottom

---

## Scenario 10: VAT Detail Page — No Z-Reports

1. Navigate to VAT Detail for a period with no Z-Reports
2. **Expected:** The Z-Reports section shows: "No Z-Reports assigned to this period."

---

## Scenario 11: Output VAT Calculation Includes Z-Reports

1. Create a Z-Report with TotalVat = €100.00, assigned to a pending period
2. Navigate to **VAT → Detail** for that period
3. **Expected:** The "Output VAT (Sales)" card includes the €100.00 from the Z-Report
4. The Net VAT Payable reflects the addition
5. Click "Full Period Report" — verify the Output VAT summary also includes the Z-Report VAT

---

## Scenario 12: Output VAT — Cancelled Z-Report Excluded

1. Cancel a Z-Report that was assigned to a period
2. Navigate to **VAT → Detail** for that period
3. **Expected:** The cancelled Z-Report does NOT appear in the Z-Reports section
4. Its VAT is NOT included in the Output VAT total
5. Restore the Z-Report
6. **Expected:** It reappears in the section and its VAT is included again

---

## Scenario 13: Document Attachments — Upload on Z-Report

1. Create a new Z-Report and save it
2. **Expected:** After saving, the page shows the Z-Report in edit mode
3. Below the Totals section, a "Document Attachments" panel appears
4. Upload a PDF file (simulating the original Z-Report printout)
5. **Expected:** File uploads successfully with EntityType = "RevenueSummary"
6. The attachment appears in the panel with download/preview options
7. Upload a second file (image — e.g. JPG of a receipt)
8. **Expected:** Both attachments visible

---

## Scenario 14: Document Attachments — Not Shown on Create

1. Navigate to **Z-Reports → New Z-Report**
2. **Expected:** The Document Attachments panel is NOT visible on the create form
3. Fill in the form and submit
4. **Expected:** After redirect to the list (or if editing immediately), the attachment panel becomes available

---

## Scenario 15: Revenue Dashboard — Includes Z-Report Revenue

1. Create Z-Reports with various SummaryDates across the last 12 months
2. Navigate to **Revenue → Dashboard**
3. **Expected:** The monthly revenue chart includes Z-Report TotalGross amounts
4. Months that include POS revenue should show higher totals than invoice-only months
5. The "Invoiced vs Collected" chart includes Z-Report TotalGross in the "Invoiced" bars

---

## Scenario 16: Revenue Dashboard — Feature Disabled

1. Disable Z-Reports via **MyBusiness → Automation**
2. Navigate to **Revenue → Dashboard**
3. **Expected:** Dashboard KPIs and charts only show Portal-issued invoices (Z-Report revenue excluded)
4. No change in displayed values compared to before Z-Reports existed
5. Re-enable the feature

---

## Scenario 17: Subscription Tier Gating — Active Subscription

1. Ensure the business has an active subscription (any tier: Foundation, Professional, Enterprise)
2. Navigate to `/ZReport/Index`
3. **Expected:** Page loads normally — Z-Reports list displayed
4. Navigate to `/ZReport/Sources`
5. **Expected:** Revenue Sources page loads normally
6. Navigate to `/ZReport/Entry`
7. **Expected:** Entry form loads normally

---

## Scenario 18: Subscription Tier Gating — No Active Subscription

1. Temporarily set the business subscription status to "cancelled" or remove the subscription record
2. Navigate to `/ZReport/Index`
3. **Expected:** Access denied — redirected to subscription required page or 403 JSON response for AJAX
4. Try `/ZReport/Sources` and `/ZReport/Entry`
5. **Expected:** Same blocked behaviour
6. Try calling `/ZReport/AxPostCreateSource` via AJAX
7. **Expected:** JSON response `{ success: false, message: "Your subscription is inactive..." }` with status 403
8. Restore the subscription for subsequent tests

---

## Scenario 19: VAT Period Column in Z-Reports List

1. Create Z-Reports: some assigned to periods, some unassigned
2. Navigate to Z-Reports list
3. **Expected:** A "VAT Period" column appears between "Total" and "Created"
4. Assigned Z-Reports show a green pill with the period label (e.g. "Mar–May 2026")
5. Unassigned Z-Reports show grey "Unassigned" text

---

## Scenario 20: End-to-End VAT Filing Flow with Z-Reports

Complete flow simulating a real VAT filing that includes POS revenue:

1. Enable Z-Reports on MyBusiness
2. Create Revenue Source "Cafe POS"
3. Create 3 Z-Reports for the current period:
   - Z-78001: Period 01/07–07/07, Net €5000, VAT 5% → €250
   - Z-78002: Period 08/07–14/07, Net €4800, VAT 5% → €240
   - Z-78003: Period 15/07–18/07, Net €3200, VAT 5% → €160
4. Assign all three to the current VAT period
5. Navigate to **VAT → Detail** for the current period
6. **Expected:** Output VAT includes €650 from Z-Reports (250+240+160)
7. Click "Full Period Report"
8. **Expected:** Z-Reports section shows all 3 entries with Period Total: Net €13,000, VAT €650, Total €13,650
9. Mark the period as submitted
10. Return to Z-Reports list — try to edit Z-78001
11. **Expected:** Locked — cannot edit
12. Verify the Revenue Dashboard charts include the €13,650 gross revenue from these Z-Reports

---

## Database Verification Checklist

After completing the scenarios, verify in the database:

- [ ] `[revenue].[RevenueSummary].VatSubmissionPeriodId` is set correctly for assigned Z-Reports
- [ ] `[revenue].[RevenueSummary].VatSubmissionPeriodId` is NULL for unassigned Z-Reports
- [ ] `[vat].[VatSubmission].TotalOutputVat` includes Z-Report VAT when recalculated
- [ ] `[dbo].[DocumentAttachment]` records exist with `EntityType = 'RevenueSummary'` and correct `EntityId`
- [ ] `[dbo].[AuditLog]` contains "Restore" actions for restored Z-Reports
- [ ] Cancelled Z-Reports (`IsActive = 0`) are NOT included in VAT calculations
