# Requirements — Invoice Deletion & Register Integrity

Status: **Draft for review** · Module: Invoice · Date: 2026-11-04

## Introduction

The customer invoice register must be a complete, gap-free, auditable sequence. Today, deleting a
draft invoice soft-deletes the row (`IsDeleted = 1`) but **hides** it from every list, leaving an
unexplained gap in the visible invoice-number sequence (e.g. the live register shows
`INV-1002-00001, 00003, 00005` with 00002 and 00004 missing). To an accountant or tax auditor an
unexplained gap reads as a destroyed/concealed invoice.

The real production data confirms the shape of the problem:

| Number | Id | Status | IsDeleted | TaxAmount |
|---|---|---|---|---|
| INV-1002-00001 | 1011 | Issued | 0 | 172.00 |
| INV-1002-00002 | 1012 | Draft | **1** | 0.00 |
| INV-1002-00003 | 1013 | Issued | 0 | 532.00 |
| INV-1002-00004 | 1014 | Draft | **1** | 5.70 |
| INV-1002-00005 | 1016 | Issued | 0 | 204.00 |

Both deleted rows are **drafts** (never issued, no filed VAT), so the deletions were
accounting-legitimate — the only defect is that the UI hides them, turning a lawful discard into an
unexplained gap. (The `Id` jump 1014→1016 is an internal identity gap and is irrelevant; only
`InvoiceNumber` must be gap-free.)

## Accounting rules this feature encodes (decided with the product owner)

1. **Only Draft invoices may be deleted.** An Issued invoice is a fiscal document; it is never
   deleted. The lawful way to cancel/reverse an issued invoice is a **credit note** (already
   supported). This existing guard stays.
2. **Deletion is a permanent soft-delete.** The row is retained forever (`IsDeleted = 1`), is **never
   hard-deleted**, and is **never restored**. Restore is explicitly disallowed because restoring a
   draft could reintroduce an invoice dated into / assigned to a VAT period that has since been filed
   (back-dating into a closed period) — a register-integrity violation. A retired number stays
   retired.
3. **Deleted drafts remain visible** in the invoice list, rendered struck-through / disabled with a
   "Deleted" badge, so the number sequence is provably complete. Detail view and PDF still work for a
   deleted draft (read-only); no Edit, no Restore.
4. **The number sequence never reuses a retired number.** The "next number" is strictly increasing;
   because deleted rows are retained and counted, the next invoice correctly advances past them. A
   DB-level uniqueness guarantee backs this.
5. **Deletion is attributable.** A deleted draft records **who** deleted it and **when**; the list
   shows the deleter's **name** (not raw user id) and the deletion time **in the business's local
   timezone**.
6. **Deletion requires deliberate confirmation.** Because it is irreversible and fiscally meaningful,
   deletion uses a two-step confirmation whose second step requires the user to type the word
   `DELETE`. Restricted to Full-access users (unchanged from today's `[ModuleAccess(Invoice, Full)]`).

> **Answer to "is it OK that a deleted draft's number jumps to the next?"** — Yes, and it is required.
> The next invoice takes the next number; the deleted number is retired and shown, never reused. A
> *visible, explained* gap is not a gap in accounting terms — every number is accounted for. What
> would be wrong is hiding the number (unexplained gap) or reusing it (false/duplicate sequence);
> this feature prevents both.

## Functional Requirements

### Deletion (tightening the existing action)

- **FR-1** Deleting an invoice remains restricted to **Draft** status and to **Full-access** users
  (existing behaviour in `DocumentSoftDeleteService.SoftDeleteInvoiceAsync` + the controller's
  `[ModuleAccess(PortalModules.Invoice, AccessLevels.Full)]`). No change to eligibility.
- **FR-2** On delete, the service records `DeletedAtUtc` (already) **and** a new `DeletedByUserId`
  (the acting user's id). Invoice lines are **not** removed (they are not removed today; this is
  reaffirmed so Detail/PDF still render the deleted draft's contents).
- **FR-3** The UI delete flow is a **two-step confirmation**:
  1. A warning dialog explaining the deletion is permanent and cannot be undone.
  2. A second dialog with a text input; the **Confirm** button is disabled until the user types
     `DELETE` (exact, case-sensitive). Only then does the AJAX delete fire.
  Uses the project's SweetAlert2 + BlockUI convention; no native dialogs.

### Register visibility

- **FR-4** The Invoice List (`GetPagedByBusinessIdAsync`) **includes** deleted invoices
  (remove the `IsDeleted = 0` filter from the paged list query), preserving the number sequence.
- **FR-5** Deleted rows render **struck-through and visually disabled** with a **"Deleted"** status
  badge (mirroring the voided-receipt styling), and show the invoice number, customer, dates, and
  total as retained in the row.
- **FR-6** Deleted rows expose a **tooltip / inline detail** of **who deleted it** (full name,
  resolved from `ApplicationUser.FirstName + LastName` in the Membership DB) and **when**
  (`DeletedAtUtc` converted to the business's local timezone via `IBusinessTimeZoneService`).
- **FR-7** Deleted rows keep **Detail** and **PDF** actions enabled (read-only). They do **not** show
  Edit, Delete, Preview-for-edit, Excel-for-edit, or any Restore action.
- **FR-8** Deleted invoices continue to be **excluded** from financial surfaces and exports: the
  CSV/PDF export (`GetAllFilteredByBusinessIdAsync`), receivables, dashboard KPIs, VAT, and statement
  queries keep their `IsDeleted = 0` filter. Only the **list view** surfaces deleted rows (for
  sequence transparency); they carry no financial weight.
- **FR-9** Filtering/search: deleted rows are shown within the normal paged results. (Open item: an
  optional "hide deleted" toggle — see Open Items.)

### Numbering integrity

- **FR-10** `GetNextSequentialNumberAsync` continues to compute the next number as
  `MAX(RIGHT(InvoiceNumber,5)) + 1` over **all** rows for the business **including deleted** (no
  `IsDeleted` filter), guaranteeing retired numbers are never reused.
- **FR-11** Add a **unique constraint/index on `(BusinessId, InvoiceNumber)`** so number reuse is
  impossible at the database level (defence in depth; today only `IX_Invoice_BusinessId` non-unique
  and `UX_Invoice_QuotationId` exist). Must be verified against existing data for no duplicates before
  applying.

## Non-Functional Requirements

- **NFR-1 No hard deletes, ever** — invoices are only soft-deleted; this is the invariant that keeps
  the sequence reuse-proof.
- **NFR-2 Tenant isolation** — all queries and the name/timezone resolution are scoped by
  `BusinessId`.
- **NFR-3 Cross-DB name resolution** — the deleter name comes from the **Membership** database
  (`ApplicationUser`), not a SQL join against the Portal DB; use the established
  `MembershipDbContext`/`UserManager` pattern (as `ActivityController` does for team members).
- **NFR-4 House standards** — `catch (Exception ex)`, raw-SQL repo style, SweetAlert2 + BlockUI,
  `AxPost`-style JSON endpoints, full table names in SQL.

## Acceptance Criteria

1. A Draft invoice can be deleted only after the user types `DELETE` in the second confirmation step;
   cancelling either step makes no change.
2. An Issued invoice offers no delete action and the service rejects a delete attempt on it.
3. After deleting a draft, the invoice list still shows that invoice number, struck-through, badged
   "Deleted", with Detail and PDF working and no Edit/Delete/Restore.
4. Hovering/!expanding a deleted row shows the deleter's full name and the deletion time in the
   business's local timezone.
5. There is no action, endpoint, or code path that un-deletes (restores) an invoice.
6. Creating a new invoice after deleting the highest-numbered draft produces the **next** number, not
   the deleted one; the deleted number never reappears on a live invoice.
7. The register, read top-to-bottom, shows a continuous invoice-number sequence with every number
   present (live or struck-through) — no unexplained gaps.
8. CSV/PDF export, receivables, dashboard, VAT, and statements do **not** include deleted invoices.
9. The database rejects any attempt to insert a second invoice with an existing `(BusinessId,
   InvoiceNumber)`.
10. Solution builds with `0 Error(s)`.

## Out of Scope

- Changing when invoice numbers are assigned (we keep "assigned at draft creation"; no move to
  "assign at issue").
- Any change to issued-invoice cancellation (credit notes already handle that).
- Quotation deletion behaviour (this spec is invoice-only; quotations share the service but are not
  a fiscal register in the same sense — can be a follow-up).
- Restoring/undeleting invoices (explicitly disallowed by decision).

## Open Items for design review

1. **"Hide deleted" toggle:** show deleted rows always (matches the voided-receipts screen), or add
   an optional filter to hide them? Recommendation: **always show** for register integrity, since
   hiding them recreates the exact problem this fixes. A filter could later hide them *visually*
   without affecting the sequence, but default must be "shown".
2. **Pagination & counts:** including deleted rows changes "Showing X of N". Confirm N should include
   deleted (recommended — they are part of the register).
3. **Unique-constraint rollout:** confirm there are no pre-existing duplicate `(BusinessId,
   InvoiceNumber)` pairs in production before adding the unique index (the migration should check and
   fail loudly if duplicates exist rather than silently skipping).
