# Tasks — Invoice Deletion & Register Integrity

Implementation plan derived from `requirements.md` and `design.md`. Each task is checkable and maps to
acceptance criteria (AC) and functional requirements (FR). **Status: implemented; `Portal.Web` and
`Portal.Tests` build with 0 errors.**

- [x] 1. Database: add `DeletedByUserId` and the unique invoice-number constraint
  - [x] 1.1 `231_AddDeletedByUserIdToInvoice.sql` — `DeletedByUserId NVARCHAR(450) NULL` (idempotent).
  - [x] 1.2 `232_AddUniqueInvoiceNumberPerBusiness.sql` — duplicate pre-check (RAISERROR) then unique
        index `UX_Invoice_BusinessId_InvoiceNumber` (idempotent).
  - _FR-2, FR-10, FR-11, NFR-1, AC-6, AC-9_

- [x] 2. Entity + EF config
  - [x] 2.1 `Invoice.DeletedByUserId` added.
  - [x] 2.2 ConfigureInvoice: `DeletedByUserId` (maxlen 450) + unique index `(BusinessId, InvoiceNumber)`.
  - _FR-2, FR-11_

- [x] 3. Repository (`InvoiceRepository.cs`)
  - [x] 3.1 `SoftDeleteAsync(id, businessId, userId)` sets `DeletedByUserId`.
  - [x] 3.2 Paged list: dropped `IsDeleted = 0`; SELECT + reader emit `IsDeleted`, `DeletedAtUtc`,
        `DeletedByUserId`.
  - [x] 3.3 `GetNextSequentialNumberAsync` unchanged (counts all rows → no reuse);
        `GetByIdAndBusinessIdAsync`/`...Unfiltered`/by-quotation SELECTs extended with
        `DeletedByUserId` so EF materialization of the entity still succeeds (Detail/PDF load deleted).
  - [x] 3.4 Export `GetAllFilteredByBusinessIdAsync` keeps `IsDeleted = 0`.
  - _FR-2, FR-4, FR-8, FR-10, AC-3, AC-6, AC-8_

- [x] 4. DTO (`InvoiceListDto.cs`) — `IsDeleted`, `DeletedAtUtc`, `DeletedByUserId`, display-only
      `DeletedByName`, `DeletedAtLocalDisplay`.
  - _FR-5, FR-6_

- [x] 5. Soft-delete service — `SoftDeleteInvoiceAsync(int, string? userId)`; Draft-only guard kept;
      no restore path exists.
  - _FR-1, FR-2, AC-2, AC-5_

- [x] 6. List enrichment — `InvoiceController.EnrichDeletedInvoiceDisplayAsync`: batch name lookup via
      `MembershipDbContext.Users`; business-local time via `IBusinessTimeZoneService`; never throws.
  - _FR-6, NFR-3, AC-4_

- [x] 7. Controller `SoftDelete` passes acting `userId`; guards unchanged.
  - _FR-1, FR-2_

- [x] 8. View + JS
  - [x] 8.1 `Index.cshtml`: deleted rows struck-through/disabled (`.invoice-row-deleted`), "Deleted"
        badge, deleter name+local time, actions = Detail + PDF only.
  - [x] 8.2 Two-step delete; second step requires typing `DELETE` (SweetAlert2 `inputValidator`).
  - [x] 8.3 `Detail.cshtml`: deleted banner + "Deleted" pill; Edit/Issue/Delete/Share/Duplicate/Excel
        hidden when deleted; PDF/Preview/Print kept; delete JS upgraded to type-`DELETE`.
  - _FR-3, FR-5, FR-6, FR-7, AC-1, AC-3, AC-4_

- [x] 9. Verify
  - [x] 9.1 `Portal.Web` builds (RazorCompileOnBuild) → 0 errors. `Portal.Tests` builds → 0 errors
        (two `InvoiceController` test ctors updated for the new `MembershipDbContext` +
        `IBusinessTimeZoneService` params).
  - [ ] 9.2 Runtime walk of the 10 acceptance criteria against a database (needs the two migrations
        applied: 231, 232). Pending user deployment/verification.

## Deployment note
Apply migrations **231** and **232** before/with this release. `232` aborts loudly if a duplicate
`(BusinessId, InvoiceNumber)` already exists — resolve any duplicate first (none expected given the
soft-delete + MAX+1 model).

## Post-implementation review — findings resolved

A self-review after implementation found that hiding the UI buttons was not enough: the server-side
write paths did not refuse a deleted invoice, so a crafted/direct request could still mutate one.
Fixed:

- [x] **R1 (HIGH) — deleted invoice was still editable server-side.** `EvaluateEligibilityAsync`
      (the single chokepoint behind Edit + all line/adjustment mutations via `EnsureEditableAsync`)
      did not check `IsDeleted`. Added a guard: a deleted invoice is never editable. Closes Edit GET,
      Edit POST, AddLine/UpdateLine/RemoveLine, and adjustment add/update/remove in one place.
- [x] **R2 (HIGH) — deleted draft could be issued server-side.** `TransitionStatusAsync` had no
      `IsDeleted` guard, so a direct `POST /Invoice/TransitionStatus` could do Draft→Issued on a
      deleted draft — resurrecting a retired number as a live fiscal document (the exact hole the
      "no restore" decision prevents). Added an explicit `IsDeleted` guard that throws.
- [x] **R3 (LOW) — deleted invoice could be duplicated.** `DocumentDuplicationService.DuplicateInvoiceAsync`
      now refuses a deleted source (consistency; UI already hid the button).
- [x] **R4 (LOW) — migration 232 batch structure.** The duplicate-guard `RETURN` and the
      `CREATE UNIQUE INDEX` were in separate `GO` batches, so on duplicate data the index creation
      would still run and fail cryptically. Merged into one batch so the guard truly prevents it.
- [x] **R5 — regression test.** Added `Deleted_Draft_Invoice_Is_Not_Editable` to
      `InvoiceEditEligibilityTests` (10/10 pass).

Verified: `Portal.Web` builds 0 errors; `InvoiceEditEligibilityTests` 10/10 pass.
