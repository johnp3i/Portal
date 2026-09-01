# External Platform Sales Import — Testing Scenarios

Tests the feature that lets a business import line-level sales from external systems (other billing platforms, online stores) into `[revenue].ExternalSalesRecord`, tagged to a registered External Platform and auto-assigned to the covering VAT period. Primary use case: **3 Inventors Limited** consolidating sales from its other platforms into one VAT return.

## Prerequisites

1. Run migrations against the Portal database:
   - `183_CreateExternalPlatformTable.sql`
   - `184_AddExternalPlatformIdToExternalSalesRecord.sql`
   - `185_SeedExternalPlatformImportPlanFeature.sql` (grants `external_platform_import` to Professional + Enterprise)
2. Business has **Professional or Enterprise** subscription
3. Z-Report feature enabled via **MyBusiness → Automation** (the Sales Import page's `Index` still gates on `IsZReportEnabled`)
4. At least one VAT submission period exists that covers the test invoice dates, and is **not** yet submitted
5. Prepare the sample CSVs below

**Sample canonical CSV (`guardian-sales.csv`) — matches the published contract:**
```csv
InvoiceNumber,InvoiceDate,NetAmount,VatAmount,TotalAmount,VatRate,CustomerName,Description,PaymentMethod,Currency
GRD-INV-2026-0040,2026-08-01,100.00,19.00,119.00,19,Acme Ltd,Consulting,bank_transfer,EUR
GRD-INV-2026-0041,2026-08-03,250.00,47.50,297.50,19,Beta Kiosk,Subscription,card,EUR
GRD-INV-2026-0042,2026-08-05,80.00,0.00,80.00,0,Gamma NGO,Exempt supply,card,EUR
GRD-INV-2026-0043,2026-08-19,540.00,102.60,642.60,19,Delta Group,Setup fee,card,EUR
```
Batch total = **€1,139.10**, VAT total = **€169.10**.

## Navigation

For Professional/Enterprise businesses, the sidebar under **Finance** shows a top-level **External Platforms** category (independent of the Z-Report toggle):

- **External Platforms** → `/ExternalPlatform` — register/manage platforms
- **Import Platform Sales** (sub-item) → `/SalesImport` — upload a platform's sales file

The imported records list is reached via **Sales Records** (`/SalesImport/Records`), which lives under the Z-Reports category when the Z-Report feature is enabled.

---

## Scenario 1: Register an External Platform

1. Log in as a Professional/Enterprise user
2. In the sidebar under **Finance**, click **External Platforms** (top-level item; route `/ExternalPlatform`)
3. Click **New Platform**
4. Enter Name = `Guardian`, Invoice Code = `grd` (lowercase to test normalization), Description = `Safeguarding platform`
5. Save
6. **Expected:** BlockUI → success Swal → list reloads; row shows **Guardian**, code badge **GRD** (uppercased), status **Active**

---

## Scenario 2: Platform Code Validation

| Input | Expected |
|-------|----------|
| Empty name | Warning: "Name required" |
| Code `GRD-1!` | Warning: "Invoice code must be 1–10 letters or numbers." |
| Code `TOOLONGCODE1` (11 chars) | Client-side maxlength blocks; server rejects if bypassed |
| Duplicate code `GRD` (second platform) | Error: "A platform with code 'GRD' already exists." |

---

## Scenario 3: Download Import Templates

1. Register platform `Guardian` (GRD) if not already
2. In the sidebar under **Finance → External Platforms**, click **Import Platform Sales** (route `/SalesImport`)
3. Select **Guardian (GRD)** in the External Platform dropdown
4. Click **Download template → CSV**
5. **Expected:** `external-sales-import-template.csv` downloads with the 10 canonical headers + 2 example rows using `GRD-INV-2026-0001` and `GRD-INV-2026-0002`
6. Click **Download template → Excel (.xlsx)**
7. **Expected:** `external-sales-import-template.xlsx` with a "Sales" sheet (blue header row, InvoiceNumber column as text) + an "Instructions" sheet describing each column
8. With **no platform selected**, download CSV → example rows use placeholder code `ABC`

---

## Scenario 4: Import Happy Path + VAT Period Assignment

1. On the **Import Platform Sales** page (`/SalesImport`), select **Guardian (GRD)**
2. Upload `guardian-sales.csv`
3. Click **Parse & Preview**
4. **Expected:** Preview shows 4 rows, all **Ready**, prefix column all **✓**, VAT Period column shows the covering period label (e.g. "Q3 2026"), batch total €1,139.10
5. Click **Confirm Import (4 records)**
6. **Expected:** SweetAlert2 → BlockUI → success → redirect to Sales Records
7. **Expected DB:** 4 `ExternalSalesRecord` rows with `ExternalPlatformId` = Guardian, `RevenueSourceId` = NULL, `VatSubmissionPeriodId` = the covering period, `IsActive = 1`

---

## Scenario 5: Prefix Mismatch Warning (non-blocking)

**File (`mixed-prefix.csv`):**
```csv
InvoiceNumber,InvoiceDate,NetAmount,VatAmount,TotalAmount
GRD-INV-2026-0050,2026-08-06,10.00,1.90,11.90
MYC-INV-2026-0088,2026-08-07,20.00,3.80,23.80
```
1. Import under platform **Guardian (GRD)**
2. **Expected:** Row 1 prefix **✓**; Row 2 prefix **⚠** with tooltip "Invoice number does not start with \"GRD-INV-\"."; both still **importable**
3. Row 2 status shows **Warning** with the prefix message
4. Confirm → both rows import (warning is advisory only)

---

## Scenario 6: VAT Period — Submitted (Locked) and Unassigned

1. Ensure a period covering **June 2026** exists and is **submitted** (marked as submitted)
2. Import a row dated `2026-06-14` under Guardian
3. **Expected:** VAT Period column shows **"Locked — period submitted"**; the record imports with `VatSubmissionPeriodId = NULL` (does not touch the filed return)
4. Import a row dated `2030-01-01` (no period covers it)
5. **Expected:** VAT Period column shows **"Unassigned"**; imports with `VatSubmissionPeriodId = NULL`

---

## Scenario 7: Duplicate Detection (same platform)

1. Import `guardian-sales.csv` once
2. Upload the same file again under Guardian
3. **Expected:** Preview shows 4 rows with **Duplicate** pills; Confirm shows "(0 records)"

---

## Scenario 8: Cross-Platform / Cross-Source Duplicate Warning

1. Register a second platform **MyChair (MYC)**
2. Import `GRD-INV-2026-0040 / 2026-08-01` under Guardian (creates the record)
3. Create a CSV with the SAME invoice number + date and import it under **MyChair**
4. **Expected:** Row flagged with a cross-source **Warning** — "Same invoice exists under \"Guardian\"" — non-blocking

---

## Scenario 9: Canonical Header Validation

| File header | Expected |
|-------------|----------|
| Missing `VatAmount` column | "The file is missing required column(s): VatAmount." |
| Missing `InvoiceNumber` and `TotalAmount` | "...missing required column(s): InvoiceNumber, TotalAmount." |
| All 5 required present (extra optional columns) | Parses successfully |
| `.xlsx` uploaded | "Only CSV files are accepted." |
| 6 MB CSV | "File size exceeds the 5 MB limit." |
| 1001+ data rows | "File contains more than 1000 data rows." |

---

## Scenario 10: TotalAmount Recomputation

**File (`no-total.csv`):**
```csv
InvoiceNumber,InvoiceDate,NetAmount,VatAmount,TotalAmount
GRD-INV-2026-0060,2026-08-08,200.00,38.00,0
```
1. Import under Guardian
2. **Expected:** `TotalAmount` recomputed to **238.00** (Net + VAT) since supplied value ≤ 0

---

## Scenario 11: Output VAT Includes Imported Sales

1. Import `guardian-sales.csv` (VAT total €169.10) assigned to the current unsubmitted period
2. Navigate to **VAT → the period's Detail** page
3. **Expected:**
   - An **"External Platform Sales"** section lists the 4 records (Platform = Guardian, Invoice #, Date, Net, VAT, Total) with a VAT total of **€169.10**
   - The period's **Output VAT** total includes this €169.10 (on top of invoices + Z-Reports − credit notes)
4. Open **VAT → Period Report** and the Revenue Dashboard VAT liability
5. **Expected:** Output VAT figures reflect the imported external sales VAT

---

## Scenario 12: Records List — Platform Column & Filter

1. Import records under Guardian and MyChair
2. Open **Sales Records** (`/SalesImport/Records`)
3. **Expected:** A **Platform** column shows the platform name per row; a **External Platform** filter dropdown is present
4. Filter by **Guardian** → only Guardian records shown
5. Cancel a record → strikethrough + "Cancelled"; Restore → back to Active

---

## Scenario 13: Deactivate / Reactivate Platform

1. On the **External Platforms** page (`/ExternalPlatform`), click **Deactivate** on Guardian
2. **Expected:** Swal warning (advisory that existing records keep their association) → row shows **Inactive**
3. Open **Import Platform Sales** (`/SalesImport`) → Guardian no longer appears in the platform dropdown
4. **Expected:** Existing Guardian records still visible in the Records list
5. Reactivate Guardian → reappears in the import dropdown

---

## Scenario 14: Tier Gating — Foundation User

1. Log in as a **Foundation** tier user
2. **Expected:** the **External Platforms** category does not appear in the sidebar; navigating directly to `/ExternalPlatform` shows the feature-not-available / upgrade page (blocked by `external_platform_import` gate)
3. On `/SalesImport`, the External Platform dropdown + template download are **not** shown (POS Sales Import path may still show if the business has `zreport_import`)
4. POST directly to `/SalesImport/AxPostParseFileForPlatform`
5. **Expected:** Blocked by the action-level `[ModuleAccess(ExternalPlatformImport)]`

---

## Scenario 15: Confirm Re-check (period submitted between preview and confirm)

1. Preview an import for a row dated in an **unsubmitted** period (VAT Period shows the label)
2. Before clicking Confirm, mark that period as **submitted** (e.g. in another tab)
3. Click **Confirm Import**
4. **Expected:** The record imports with `VatSubmissionPeriodId = NULL` — the commit re-resolves the covering unsubmitted period and refuses the now-submitted one (no change to the filed return)

---

## Database Verification Checklist

- [ ] `[revenue].[ExternalPlatform]` has the registered platform with uppercased `PlatformCode`, unique per `BusinessId`
- [ ] `[revenue].[ExternalSalesRecord]` rows have `ExternalPlatformId` set and `RevenueSourceId = NULL` for platform imports
- [ ] `VatSubmissionPeriodId` set to the covering unsubmitted period; NULL when covering period is submitted or none exists
- [ ] `TotalAmount` = `NetAmount + VatAmount` when supplied Total ≤ 0
- [ ] Cancelled records have `IsActive = 0` and are excluded from Output VAT
- [ ] `[dbo].[AuditLog]` contains an **"ExternalPlatformSalesImport"** action with the platform name, file, count, and total
- [ ] `[dbo].[PlanFeature]` has `external_platform_import` (IsIncluded=1) for Professional and Enterprise plans only
- [ ] VAT submission Output VAT for the period includes `SUM(ExternalSalesRecord.VatAmount)` for active, period-assigned records

## Related

- Spec: `.kiro/specs/external-platform-sales-import/`
- Export contract for external teams: `.kiro/docs/external-platform-sales-export-guideline.md`
- Mockups: `.kiro/docs/mockups/external-platform-manage.html`, `external-platform-import-upload.html`, `external-platform-import-preview.html`
