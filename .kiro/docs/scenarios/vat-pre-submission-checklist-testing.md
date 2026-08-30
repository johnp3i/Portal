# Testing Scenarios: VAT Period Pre-Submission Checklist

The VAT Pre-Submission Checklist is an advisory, non-blocking panel on the VAT period
review page (`/Vat/Detail`). It runs automated checks (unassigned purchases/invoices,
zero-VAT invoices, purchase-count trend, input VAT discrepancy) and restates the computed
Output/Input/Net VAT, so the business owner can catch issues before filing. It never
blocks submission.

## Prerequisites

- Log in as a user with **VAT module access** (Full access for the submit step)
- Use a test business — referred to below as **"Meridian Consulting Ltd"**
- Ensure at least **two consecutive VAT periods** exist:
  - **Q1** (e.g. Jan–Mar) — the prior period, seeded with **8 purchases** (clears the trend baseline of 5)
  - **Q2** (e.g. Apr–Jun) — the current period under test, initially **unsubmitted** and empty
- Have access to the database (to verify no rows/audit entries are created by read-only requests)

> The checklist panel appears on `/Vat/Detail?periodId={id}` above the "Recurring Expense
> Check" panel. It loads via AJAX (`/Vat/AxGetPreSubmissionChecklist?periodId={id}`).

---

## Scenario 1: Read-Only Guarantee — fresh period creates nothing

Verifies the checklist endpoint is truly read-only and never persists a submission or writes audit noise.

1. Confirm **no `VatSubmission` row exists** for Q2 (a fresh, never-opened period)
2. Note the current row count of the `AuditLog` table
3. Request `/Vat/AxGetPreSubmissionChecklist?periodId={Q2}` directly in the browser
4. **Expected:** JSON returns `success: true` with figures (all `€0.00` for an empty period) and a list of items
5. **Expected:** **No `VatSubmission` row** was created for Q2
6. **Expected:** **No new `AuditLog` entry** (no "Created"/"Recalculated" action) was written by the GET
7. **Expected:** The `AuditLog` row count is unchanged from step 2

---

## Scenario 2: All-Clear State

1. In Q2, record **8 purchases**, each explicitly assigned to Q2, each with normal VAT
2. Issue **2 invoices** in Q2 with VAT, explicitly assigned to Q2
3. Navigate to `/Vat/Detail?periodId={Q2}`
4. **Expected:** The "Pre-Submission Checklist" panel renders in the advisory style (icon tile, "Advisory Check" eyebrow, collapsible)
5. **Expected:** Header badge shows green **"All clear"**
6. **Expected:** Every checklist item shows a green pass dot
7. **Expected:** Under the "Computed figures" divider, Output VAT / Input VAT / Net VAT **match the KPI cards** at the top of the page exactly
8. **Expected:** The "advisory only — does not block submission" note is visible
9. Click the panel header → **Expected:** the panel collapses; click again → it expands (chevron rotates)

---

## Scenario 3: Unassigned Purchases Item

1. In Q2, record a purchase dated within Q2's range but leave its VAT period **unassigned**
2. Reload `/Vat/Detail?periodId={Q2}`
3. **Expected:** The `unassigned_purchases` item shows an **amber warning** with the count
4. **Expected:** The count **matches** the existing "Unassigned Purchases" panel on the same page
5. **Expected:** The header badge increments the "N item(s) to review" total
6. Assign the purchase to Q2, reload
7. **Expected:** The item returns to **pass**

---

## Scenario 4: Unassigned Issued Invoices Item

1. Create an **issued** invoice dated within Q2 with **no explicit** VAT period assignment
2. Reload the Detail page
3. **Expected:** The `unassigned_invoices` item shows a **warning**, with detail explaining it's included via the date-range fallback but not explicitly assigned
4. Assign the invoice to Q2, reload
5. **Expected:** The item returns to **pass**

---

## Scenario 5: Zero-VAT Invoice Item (info, not warning)

1. Issue an invoice in Q2 with a **positive subtotal** and **€0 VAT**
2. Reload the Detail page
3. **Expected:** The `zero_vat_invoices` item shows a **blue info** dot prompting review of whether VAT should apply
4. **Expected:** This item does **NOT** increment the "N item(s) to review" total (info is excluded from the warning count)
5. Correct or remove the invoice, reload
6. **Expected:** The item returns to **pass**

---

## Scenario 6: Input VAT Discrepancy Item

1. Take a purchase whose **invoice date is in Q1** and explicitly assign it to **Q2** (a late inclusion)
2. Reload the Detail page
3. **Expected:** The `input_vat_discrepancy` item shows a **warning** stating the input VAT by date vs the reported figure, and "1 late purchase from a previous period included here"
4. **Expected:** This agrees with the existing **"Audit Discrepancy Detected"** card on the same page
5. Revert the assignment, reload
6. **Expected:** The item returns to **pass** ("Input VAT by date matches the reported figure")

---

## Scenario 7: Purchase-Count Trend — meaningful drop warns

1. With Q1 holding **8 purchases**, record only **3 purchases** in Q2 (a ~62% drop; prior ≥ 5 baseline)
2. Open `/Vat/Detail?periodId={Q2}`
3. **Expected:** The `purchase_count_trend` item shows a **warning** stating both counts and the percentage drop, e.g. "This period: 3. Previous period: 8. 62% fewer — is this expected…?"

---

## Scenario 8: Purchase-Count Trend — low-volume noise suppressed (baseline guard)

Verifies the minimum-baseline guard prevents false alarms on tiny periods.

1. Create a period **Q0** (before Q1) with only **2 purchases**
2. Ensure **Q1** has only **1 purchase**
3. Open `/Vat/Detail?periodId={Q1}`
4. **Expected:** Even though 1 vs 2 is a 50% drop, the prior baseline (2) is **below 5**, so `purchase_count_trend` shows **pass**, not a warning

---

## Scenario 9: Purchase-Count Trend — no prior period

1. Open the Detail page for the **earliest** VAT period (no period precedes it)
2. **Expected:** The `purchase_count_trend` item shows an **info** status: "No prior period is available for comparison."

---

## Scenario 10: Summary Badge Math

1. Arrange Q2 to have **only info items** (e.g. one zero-VAT invoice) and no warnings
2. **Expected:** Badge reads **"All clear"** (info items do not count)
3. Introduce one warning condition (e.g. an unassigned purchase)
4. **Expected:** Badge reads **"1 item to review"**
5. Introduce a second warning condition
6. **Expected:** Badge reads **"2 items to review"**

---

## Scenario 11: Submit-Time Confirmation — warnings present

1. With Q2 holding one or more **warning** conditions (include an unassigned purchase), click **"Mark as Submitted"**
2. **Expected:** A SweetAlert2 dialog appears summarising "N item(s) worth reviewing", listing the warning titles, with **Submit Anyway / Review First / Cancel**
3. Click **Review First**
4. **Expected:** Because unassigned purchases is one of the warnings, the page scrolls to the **Unassigned Purchases section** (which has the assign controls) — not merely the checklist panel
5. Remove the unassigned-purchase warning but keep another warning (e.g. the trend), click "Mark as Submitted" → **Review First**
6. **Expected:** The page scrolls to the **checklist panel** (no unassigned section to target)
7. Reopen the dialog, click **Submit Anyway**
8. **Expected:** Submission proceeds — the warning does **not** block it

---

## Scenario 12: Submit-Time Confirmation — clean period

1. Arrange Q2 with **no warnings** (all pass/info)
2. Click **"Mark as Submitted"**
3. **Expected:** The **standard** confirmation appears ("Are you sure you want to mark this VAT submission as filed?") with no extra friction

---

## Scenario 13: Submit-Time Fallback — checklist unavailable

1. Simulate the checklist failing to load (e.g. go offline, or temporarily disable the endpoint) so the panel shows "Unable to load the checklist."
2. Click **"Mark as Submitted"**
3. **Expected:** The flow **falls back to the legacy unassigned-purchases pre-flight** unchanged — it does not error, and submission still works

---

## Scenario 14: Submitted Period — read-only, no nagging

Verifies filed periods render the checklist without warning about issues that can no longer be acted on.

1. Ensure Q2 still has a condition that would be a warning (e.g. a zero-VAT invoice, or a discrepancy)
2. Submit Q2 (Mark as Submitted → Submit Anyway)
3. Reopen `/Vat/Detail?periodId={Q2}` (now filed)
4. **Expected:** The checklist still renders (read-only review)
5. **Expected:** Any item that would have been a **warning is downgraded to info** — the header badge reads **"All clear"** and the warning count is 0
6. **Expected:** The computed figures still display
7. **Expected:** The panel does not nag about pre-submission issues on an immutable period

---

## Scenario 15: Tenant Isolation

1. Log in as a **different business** that also has VAT periods
2. Open a Detail page and view the checklist
3. **Expected:** All counts and figures reflect **only** the current business's data (no leakage from Meridian)
4. Request `/Vat/AxGetPreSubmissionChecklist?periodId={id}` with a `periodId` belonging to **another tenant**
5. **Expected:** Returns `success: false` with a generic message and **no data**

---

## Notes

- The checklist is **advisory and non-blocking** — every warning can be overridden at submit time.
- Info items (zero-VAT invoices, no-prior-period trend) never contribute to the "N items to review" count.
- The `unassigned_purchases` and `purchase_count_trend` counts intentionally include **all** purchase origin types (including EU reverse charge) to match the VAT periods list page; the VAT-amount figures exclude EU reverse charge (zero VAT). This asymmetry is by design.
- Computed figures come from the persisted `VatSubmission` when it exists, otherwise from an in-memory computation — the checklist endpoint never creates or updates data.
