# Design: Editable Issued-but-Unpaid Invoices

## Overview

Relax the invoice edit guard from "Draft only" to "Draft OR (Issued + no settlement + unfiled VAT
period)", enforced through **one shared eligibility predicate** used by every write path and by the
UI. Protect the invariants that make an issued invoice a legal document: no editing once settled or
VAT-filed, immutable invoice number, and — importantly — keep the period's **persisted** VAT figures
in sync after an edit.

### Verified current behaviour (from code)
- Edit guard today = `InvoiceStatusTypeId != 1` in **6** `InvoiceService` methods (UpdateInvoiceAsync,
  AddLineAsync, UpdateLineAsync, RemoveLineAsync, ApplyBulkDiscountAsync, RemoveBulkDiscountAsync) +
  `InvoiceController.Edit` GET.
- **Section services do NOT guard on status** (`InvoiceSectionService.Add/Update/RemoveSectionAsync`
  have no `InvoiceStatusTypeId` check) — a pre-existing inconsistency this spec will close.
- `UpdateInvoiceAsync` currently **overwrites `InvoiceNumber`** when a value is supplied.
- VAT figures are **persisted** on `VatSubmission` (TotalOutputVat/TotalInputVat/NetVatPayable),
  written by `VatSubmissionService.CreateOrRecalculateAsync`. `GetPreSubmissionChecklistAsync`
  **prefers the persisted row** when present. So editing an invoice's total without refreshing the
  row leaves the period's stored VAT stale. (This corrects an earlier wrong assumption that VAT is
  only aggregated at read time.)
- Settlement = non-voided payments SUM + applied non-voided credit notes
  (`CreditNoteRepository.GetTotalAppliedCreditAsync`; payment SUM pattern in `InvoiceController.Detail`).
- `RecomputeAndUpdateTotalsAsync` already handles adjustment lines and updates invoice totals.
- Lifecycle transitions (`ValidTransitionsMap`): Draft→Issued/Cancelled, Issued→Cancelled. No un-issue.

---

## 1. Shared eligibility predicate (single source of truth)

Add to `InvoiceService`:
```csharp
public sealed record InvoiceEditEligibility(bool CanEdit, string? Reason, bool IsIssued);
public async Task<InvoiceEditEligibility> GetEditEligibilityAsync(int invoiceId);
```
Logic:
1. Load invoice (tenant-scoped). Null → `(false, "Invoice not found", false)`.
2. `Cancelled(3)` → `(false, "Cancelled invoices cannot be edited", false)`.
3. `Draft(1)` → `(true, null, false)` (unchanged path).
4. `Issued(2)`:
   - `HasSettlementAsync(invoice)` (non-voided payments SUM + applied non-voided credit) > 0 →
     `(false, "This invoice has a payment or credit note recorded. Use a credit note to correct it.", true)`.
   - Else if `VatSubmissionPeriodId` set AND that period's `VatSubmission.IsSubmitted` →
     `(false, "The VAT period for this invoice has been filed and can no longer be changed.", true)`.
   - Else `(true, null, true)`.

Add a small `HasSettlementAsync(invoice)` helper so the settlement invariant lives in one place
(financial-conventions steering). Reuse existing repos; no new SQL patterns needed.

**Authority split:** the per-write service check (below) is authoritative. The controller calls
`GetEditEligibilityAsync` once to drive the UI (a bool passed to the view); the extra reads for the
UI are negligible and not a concern.

## 2. Replace the guards

In each of the 6 `InvoiceService` mutation methods, replace:
```csharp
if (invoice.InvoiceStatusTypeId != 1)
    throw new InvalidOperationException("Invoice can only be edited in Draft status");
```
with an eligibility check that throws `InvalidOperationException(reason)` when `!CanEdit`. Prefer a
private overload that takes the already-loaded `invoice` (these methods already have it) to avoid a
redundant reload: `EnsureEditable(invoice)` that runs the same logic as `GetEditEligibilityAsync`.

Apply the SAME `EnsureEditable` to the three **section** mutations
(`InvoiceSectionService.Add/Update/RemoveSectionAsync`), which currently have no guard. Since those
live in `InvoiceSectionService`, either inject the eligibility helper or move the check behind an
`IInvoiceService` call they can invoke. (Decide the cleanest wiring at implementation; the rule must
be the shared one, not a re-implementation.)

`InvoiceController.Edit` GET: redirect to Detail when `!CanEdit` (instead of `!= Draft`).

## 3. Invoice number immutability for issued
`UpdateInvoiceAsync`: when the invoice is Issued, ignore the incoming `invoiceNumber` (keep the
existing value). Draft keeps current behaviour. Edit view renders the number `readonly` when issued.

## 4. VAT synchronization (the key correctness fix)
After any issued-invoice edit that runs `RecomputeAndUpdateTotalsAsync`:
- Determine the invoice's `VatSubmissionPeriodId`. If set and its `VatSubmission` row exists and is
  **not** submitted, call `VatSubmissionService.CreateOrRecalculateAsync(periodId)` to refresh the
  persisted figures. If no row exists, do nothing (don't create one just by editing). If submitted,
  the edit was already blocked upstream.
- This covers total changes from quantity/price/VAT-rate/reverse-charge/tier/line add-remove/bulk
  discount — all funnel through `RecomputeAndUpdateTotalsAsync`, so hook the refresh there or in the
  service methods right after it (choose one consistent spot; hooking once inside
  `RecomputeAndUpdateTotalsAsync` is DRY but also fires for Draft — guard it to issued-with-row).
- Invoice-date edit on issued: allow only if the new date stays within the same currently-assigned
  unsubmitted period; otherwise refuse and direct the user to the reassign flow. (Conservative;
  revisit if too strict.)

> Consider a small dependency note: `InvoiceService` calling `VatSubmissionService` — check for
> circular DI. If problematic, refresh via the `VatSubmissionRepository` + the shared compute helper
> instead, or raise a lightweight domain event. Resolve at implementation.

## 5. Audit + UI
- Issued-invoice edits write an `AuditLog` with a distinguishing Action (e.g. `IssuedInvoiceRevised`
  or existing action + an `Issued` marker in values).
- `Detail.cshtml` / `Index.cshtml`: show Edit when the controller-provided `CanEdit` bool is true;
  Delete stays Draft-only; reword the Issue-dialog warning (~Detail.cshtml line 637).
- Edit view: if the invoice has an active share/acceptance (already queried in Detail via
  `GetActiveShareByInvoiceIdAsync` / acceptance), show a non-blocking note.

## 6. Edge cases
- Overdue-but-unpaid issued invoice: editable (no settlement). Financial status untouched (derived).
- Issued invoice with an adjustment line (bulk discount already applied): editable via the existing
  bulk-discount endpoints, which now pass the eligibility check.
- Concurrency: a payment recorded between the UI check and the write is caught by the authoritative
  per-write `EnsureEditable`.

## Testing strategy
- **Unit tests (in scope):**
  - `GetEditEligibilityAsync` / `EnsureEditable`: Draft→editable; Issued+unpaid+unfiled→editable;
    Issued+payment→blocked; Issued+applied credit note→blocked; Issued+submitted VAT period→blocked;
    Cancelled→blocked; not-found→blocked.
  - `HasSettlementAsync`: payment only; credit note only; both; voided payment ignored; voided credit
    ignored.
  - VAT refresh: editing an issued invoice whose period has an unsubmitted `VatSubmission` row
    updates that row's `TotalOutputVat`; no row → no row created.
- **Build:** 0 `error CS` / `error RZ` after each change (MSB3021 environmental).
- **Manual:** issue unpaid invoice → edit line → totals + period VAT update; record payment → edit
  blocked; submitted VAT period → edit blocked; paid/credit-noted → immutable; Draft unchanged;
  invoice number not editable when issued.

## Sequencing
1. Eligibility predicate + settlement helper (+ unit tests).
2. Swap the 6 service guards + section-service guards + controller GET.
3. Invoice-number immutability.
4. VAT synchronization + date rule (+ unit tests).
5. Audit + UI + Issue-dialog reword.
6. Full build + manual verification.
