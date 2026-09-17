# Tasks: Editable Issued-but-Unpaid Invoices

> Refs: requirements.md, design.md. Each code task: build (0 error CS/RZ; MSB3021 is environmental)
> + relevant unit tests. UI verification is manual (cannot render here).

- [ ] 1. Add `HasSettlementAsync(invoice)` helper in `InvoiceService`: non-voided payments SUM +
      applied non-voided credit notes (reuse `CreditNoteRepository.GetTotalAppliedCreditAsync` and the
      payment SUM pattern from `InvoiceController.Detail`). Single source of truth for "settled".
  - _Req: 1.1b, 5 (financial-conventions steering)_

- [ ] 2. Add `GetEditEligibilityAsync(invoiceId)` → `InvoiceEditEligibility(CanEdit, Reason, IsIssued)`
      and a private `EnsureEditable(invoice)` (throws `InvalidOperationException(Reason)` when not
      editable) covering: Draft→ok; Issued + no settlement + VAT period not submitted (or none)→ok;
      settled→blocked; submitted VAT→blocked; Cancelled→blocked; not-found→blocked.
  - _Req: 1.1–1.4_

- [ ] 3. Replace the `InvoiceStatusTypeId != 1` guard with `EnsureEditable(invoice)` in the 6
      `InvoiceService` methods (UpdateInvoiceAsync, AddLineAsync, UpdateLineAsync, RemoveLineAsync,
      ApplyBulkDiscountAsync, RemoveBulkDiscountAsync).
  - _Req: 2.1, 2.3_

- [ ] 4. Apply the SAME eligibility check to the three section mutations
      (`InvoiceSectionService.AddSectionAsync/UpdateSectionAsync/RemoveSectionAsync`), which currently
      have no status guard. Wire to the shared rule (inject eligibility or call through IInvoiceService)
      — do not re-implement the logic.
  - _Req: 2.2, 2.3_

- [ ] 5. `InvoiceController.Edit` GET: redirect to Detail when `!CanEdit` (instead of `!= Draft`).
      Ensure Edit POST path is covered by the service guard.
  - _Req: 2.1_

- [ ] 6. Invoice-number immutability: `UpdateInvoiceAsync` ignores incoming `invoiceNumber` when the
      invoice is Issued; Edit view renders the number field `readonly` when issued.
  - _Req: 2.4_

- [ ] 7. VAT synchronization: after an issued-invoice edit that recomputes totals, refresh the
      persisted `VatSubmission` for the invoice's period via `CreateOrRecalculateAsync` — only when a
      row exists and is not submitted; never create a row just by editing. Choose one consistent hook
      point (guarded so Draft edits don't trigger it). Watch for `InvoiceService`→`VatSubmissionService`
      circular DI; fall back to repo + shared compute helper if needed.
  - _Req: 3.1, 3.2_

- [ ] 8. Issued invoice-date rule: allow date edit only if the new date stays within the same
      currently-assigned unsubmitted VAT period; otherwise refuse with a message pointing to the
      reassign flow.
  - _Req: 3.3_

- [ ] 9. Audit: write a distinguishable audit entry (e.g. `IssuedInvoiceRevised`) for issued-invoice
      edits. Confirm financial status is NOT recomputed by edits.
  - _Req: 4.1, 3.4_

- [ ] 10. UI: controller computes eligibility once and passes a `CanEdit` bool to `Detail.cshtml` /
      `Index.cshtml`; show Edit accordingly (Delete stays Draft-only); reword the Issue-dialog
      "cannot be edited" warning; add a non-blocking note in the Edit view when the invoice has an
      active share/acceptance.
  - _Req: 4.2, 4.3, 4.4, 4.5_

- [ ] 11. Unit tests: `GetEditEligibilityAsync`/`EnsureEditable` (all branches), `HasSettlementAsync`
      (payment/credit/both/voided-ignored), and VAT refresh (row updated / no row not created). Build + verify.
  - _Req: 1, 3, 5_

- [ ] 12. Manual verification: issue unpaid invoice → edit line → totals + period VAT update; record
      payment → edit blocked; submitted VAT period → edit blocked; paid/credit-noted → immutable;
      Draft unchanged; number read-only when issued. Confirm no regression to KPIs/receivables/PDF.
  - _Req: 5_

- [ ] 13. Final build 0 error CS/RZ; confirm Quotation flows untouched; note any deviation from the
      chosen date/VAT rules if implementation forced a change.
  - _Req: 5_
```
