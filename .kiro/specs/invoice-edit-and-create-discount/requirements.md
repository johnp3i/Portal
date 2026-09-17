# Requirements: Editable Issued-but-Unpaid Invoices

## Introduction

Driven by "customers make mistakes and want to correct them": today an invoice is editable only
while in **Draft** status; issuing locks it permanently. We will allow an **Issued** invoice that
has **no recorded payment and no applied credit note** to be corrected, subject to VAT-safety
guards. This is the chosen **Option A (revise in place, guarded)** over Option B (void & reissue /
credit-note only).

> A second idea — adding the invoice-level bulk discount to the New Invoice page — was investigated
> and **dropped**: bulk discount is a persisted-document operation (needs a saved invoice id +
> persisted lines), and the New Invoice page persists nothing until submit, so it isn't applicable
> there. Not in scope.

## Glossary
- **Lifecycle status** (`InvoiceStatusTypeId`): Draft=1, Issued=2, Cancelled=3.
- **Financial status** (`InvoiceFinancialStatusTypeId`): Unpaid=1, PartiallyPaid=2, Paid=3,
  Overdue=4, WrittenOff=5. (Overdue is a **derived** state per the financial-conventions steering;
  the persisted value is advisory.)
- **Has settlement**: exists a non-voided `Payment` for the invoice (SUM `Amount` where `!IsVoided`
  > 0) OR any applied non-voided credit note. Per financial-conventions, "settled" always subtracts
  payments AND applied credit notes.
- **Adjustment line**: an `InvoiceLine` with `IsAdjustmentLine = true` (invoice-level bulk discount).
- **VAT period submitted**: the invoice's `VatSubmissionPeriodId` points at a period whose
  `VatSubmission.IsSubmitted == true`.
- **Persisted VAT figures**: `VatSubmission.TotalOutputVat/TotalInputVat/NetVatPayable` — a row that
  may exist for a period (created by `VatSubmissionService.CreateOrRecalculateAsync`) and which
  `GetPreSubmissionChecklistAsync` prefers over recomputation when present. **These can go stale if
  an invoice in the period changes and the row is not refreshed.**

---

### Requirement 1: Eligibility predicate
**User Story:** As a business owner, I want to fix an issued invoice that hasn't been paid yet, so
that a customer's correction doesn't force me to void and recreate it.

#### Acceptance Criteria
1. An invoice SHALL be editable WHEN `InvoiceStatusTypeId == Draft(1)` (unchanged), OR WHEN ALL of:
   a. `InvoiceStatusTypeId == Issued(2)`, AND
   b. it has **no settlement** (no non-voided payments AND no applied non-voided credit notes), AND
   c. its assigned VAT period is **not submitted** (or it has no VAT period assigned).
2. IF an issued invoice has any settlement, editing SHALL be refused with a clear message directing
   the user to the credit-note flow.
3. IF an issued invoice's VAT period is already submitted, editing SHALL be refused with a message
   explaining the VAT period has been filed (mirroring the existing reassign-period rule).
4. Cancelled invoices SHALL never be editable.

### Requirement 2: Guard enforcement (defense in depth)
#### Acceptance Criteria
1. The eligibility check SHALL replace the current `InvoiceStatusTypeId != 1` guard in ALL six
   `InvoiceService` edit-mutation methods (`UpdateInvoiceAsync`, `AddLineAsync`, `UpdateLineAsync`,
   `RemoveLineAsync`, `ApplyBulkDiscountAsync`, `RemoveBulkDiscountAsync`) and in
   `InvoiceController.Edit` GET (redirect only when not editable).
2. Section mutations (`InvoiceSectionService.AddSectionAsync/UpdateSectionAsync/RemoveSectionAsync`)
   currently have **no** status guard — sections are editable on issued invoices today. This spec
   SHALL close that inconsistency by applying the SAME eligibility check to the three section
   mutations, so an ineligible (paid / filed) issued invoice cannot have its sections changed either.
3. The eligibility logic SHALL live in **one** shared helper (e.g.
   `InvoiceService.GetEditEligibilityAsync(invoiceId)`), so the rule is defined once, not
   copy-pasted with drift. The per-write service check is the **authority**; any UI flag derived
   from it is advisory.
4. Editing an issued invoice SHALL NOT change its `InvoiceNumber` (sequence integrity).
   `UpdateInvoiceAsync` currently overwrites the number when a value is supplied — for issued
   invoices the incoming number SHALL be ignored, and the Edit view SHALL render it read-only.

### Requirement 3: VAT correctness on total changes
#### Acceptance Criteria
1. WHEN any change alters an issued invoice's totals (quantity, unit price, VAT rate, reverse-charge
   flag, price tier, line add/remove, or bulk discount), the code SHALL recompute the invoice's
   `TaxAmount`/`TotalAmount` (existing `RecomputeAndUpdateTotalsAsync`).
2. AFTER such a change, IF a `VatSubmission` row exists for the invoice's (unsubmitted) VAT period,
   its persisted figures SHALL be refreshed (via `CreateOrRecalculateAsync`) so the period's stored
   Output VAT is not left stale. IF no row exists, no row SHALL be created solely by editing.
3. Editing the invoice date on an issued invoice SHALL be allowed only if the new date stays within
   the same currently-assigned unsubmitted VAT period; otherwise it SHALL be refused with a message
   directing the user to the explicit VAT-period reassign flow (which already guards submitted
   periods). This prevents silently moving an invoice into a different or filed period.
4. Financial status SHALL NOT be recomputed by an edit (editing an unpaid invoice cannot change its
   paid-ness; overdue remains derived). This is intentional.

### Requirement 4: Audit + UI
#### Acceptance Criteria
1. Every edit to an **issued** invoice SHALL write an `AuditLog` entry distinguishable from draft
   edits (e.g. Action prefixed/suffixed to indicate an issued-invoice revision).
2. `Detail.cshtml` / `Index.cshtml` SHALL show the Edit action for an issued invoice only when the
   eligibility predicate is satisfied (controller computes it once and passes a bool to the view);
   otherwise hide it, as today for non-Draft.
3. Soft-delete SHALL remain **Draft-only** (do not extend delete to issued invoices).
4. The "Once issued, it cannot be edited" warning on the Issue dialog SHALL be reworded to reflect
   that issued invoices remain correctable until a payment/credit note is recorded or the VAT period
   is filed.
5. WHERE an issued invoice being edited has an active share or customer acceptance, the Edit UI
   SHALL show a non-blocking informational note that changes will differ from what the customer saw.

### Requirement 5: No regressions
#### Acceptance Criteria
1. Draft editing behaviour SHALL be unchanged.
2. Paid / partially-paid / credit-noted / VAT-filed invoices SHALL remain immutable.
3. Dashboard KPIs, receivables, shares/acceptance, and PDF generation SHALL continue to read the
   (now possibly revised) stored totals correctly.
4. The build SHALL succeed with 0 `error CS` / `error RZ` after each change (an `MSB3021` DLL/exe
   copy-lock is environmental and does not count as a failure).

## Decision record
- **Option A (guarded in-place revise)** chosen over Option B. Rationale: matches the user's intent
  (correct genuine mistakes on unpaid invoices) while the settlement + submitted-VAT guards protect
  the only truly dangerous cases. Invoice number stays immutable post-issue.
- **Bulk discount on New Invoice: dropped** (not applicable pre-persistence).

## Out of scope
- Issued → Draft "un-issue" transition.
- Extending soft-delete to issued invoices.
- Any change to the credit-note mechanism.
- Quotation flows.
- Bulk discount on the New Invoice page.
- Automated visual tests (manual verification per project norm); unit tests for the eligibility
  predicate and settlement helper ARE in scope.
