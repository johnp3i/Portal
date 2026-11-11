# Technical Design — Invoice Deletion & Register Integrity

Status: **Draft for review** · Module: Invoice · Date: 2026-11-04

## Overview

Make the customer invoice register gap-free and auditable by (a) keeping deleted drafts visible in
the list instead of hiding them, (b) making deletion a permanent, attributable, type-`DELETE`-guarded
soft-delete with no restore path, and (c) guaranteeing invoice numbers are never reused. The change
is deliberately contained: the existing soft-delete model is correct; its only defect is that the UI
hides deleted rows and the delete action lacks attribution and friction.

Grounded in the current codebase:
- Soft-delete already exists: `Invoice.IsDeleted` + `Invoice.DeletedAtUtc` (migration `043`),
  `InvoiceRepository.SoftDeleteAsync`, `DocumentSoftDeleteService.SoftDeleteInvoiceAsync` (Draft-only
  guard), controller `SoftDelete` action gated `[ModuleAccess(Invoice, Full)]`.
- Numbering: `InvoiceService.CreateCreditNote`-style — number assigned at draft creation as
  `INV-{businessId}-{n:D5}` where `n = InvoiceRepository.GetNextSequentialNumberAsync` =
  `ISNULL(MAX(TRY_CAST(RIGHT(InvoiceNumber,5) AS INT)),0)+1` over **all** rows for the business.
- List query `InvoiceRepository.GetPagedByBusinessIdAsync` currently has `AND IsDeleted = 0`.
- No unique index on `(BusinessId, InvoiceNumber)` today (only `IX_Invoice_BusinessId`,
  `UX_Invoice_QuotationId`).
- Timezone: `IBusinessTimeZoneService.GetTimeZoneAsync(businessId)` → `TimeZoneInfo` (two-hop
  `Business.TimeZoneId` → `NotificationTimeZone.WindowsId`). Convert with
  `TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, Utc), tz)`.
- User name: `ApplicationUser.FirstName/LastName` live in the **Membership** DB; resolve via
  `MembershipDbContext` / `UserManager`, as `ActivityController` already does for team members.

## Components changed (file inventory)

| Layer | File | Change |
|-------|------|--------|
| Migration | `Portal.Database/Migrations/231_AddDeletedByUserIdToInvoice.sql` | **new** — add `DeletedByUserId NVARCHAR(450) NULL` |
| Migration | `Portal.Database/Migrations/232_AddUniqueInvoiceNumberPerBusiness.sql` | **new** — unique index on `(BusinessId, InvoiceNumber)` with a pre-check for duplicates |
| Entity | `Portal.Infrastructure/Entities/Invoice.cs` | add `string? DeletedByUserId` |
| EF config | `Portal.Infrastructure/Data/PortalDbContext.cs` (ConfigureInvoice) | map `DeletedByUserId` (maxlen 450); add the unique index mapping to match the migration |
| Repo | `Portal.Infrastructure/Repositories/InvoiceRepository.cs` | `SoftDeleteAsync` sets `DeletedByUserId`; **remove `IsDeleted = 0` from the paged list query**; list SELECT emits `IsDeleted`, `DeletedAtUtc`, `DeletedByUserId`; `GetNextSequentialNumberAsync` unchanged (confirm no IsDeleted filter) |
| DTO | `Portal.Infrastructure/Models/InvoiceListDto.cs` | add `bool IsDeleted`, `DateTime? DeletedAtUtc`, `string? DeletedByUserId`, plus display-only `string? DeletedByName`, `string? DeletedAtLocalDisplay` |
| Service iface | `Portal.Infrastructure/Services/IDocumentSoftDeleteService.cs` | `SoftDeleteInvoiceAsync(int, string userId)` (thread the acting user) |
| Service | `Portal.Infrastructure/Services/DocumentSoftDeleteService.cs` | accept + persist `userId`; guards unchanged (Draft-only) |
| Service | `Portal.Infrastructure/Services/InvoiceService.cs` (list assembly) | after paging, resolve deleter name (Membership) + local-time string for deleted rows |
| Controller | `Portal.Web/Controllers/InvoiceController.cs` | pass acting user id into `SoftDeleteInvoiceAsync`; list action already returns the DTOs |
| View | `Portal.Web/Views/Invoice/Index.cshtml` (+ its JS) | render deleted rows struck-through/disabled with "Deleted" badge, deleter tooltip, restricted actions; two-step type-`DELETE` confirm |

## Data model

### `Invoice.DeletedByUserId`
`NVARCHAR(450) NULL` (matches `CreatedByUserId` conventions elsewhere; 450 is the Identity key
length). Nullable: existing deleted rows (1012, 1014) have no recorded deleter — the UI shows
"Unknown" / "—" for those, which is honest.

Migration `231` (idempotent, `USE [Portal]`): add the column if absent.

### Unique `(BusinessId, InvoiceNumber)`
Migration `232` (idempotent): first assert no duplicates exist; if a duplicate is found, `RAISERROR`
and do not create the index (fail loudly per Open Item 3). Otherwise create
`UX_Invoice_BusinessId_InvoiceNumber`. Because deleted rows retain their number and the index spans
all rows, a retired number can never be reused even by a bug.

## Deletion flow (service + controller)

`IDocumentSoftDeleteService.SoftDeleteInvoiceAsync(int invoiceId, string userId)`:
- Unchanged guards: invoice exists, belongs to business, not already deleted, **Draft only**.
- `InvoiceRepository.SoftDeleteAsync(invoiceId, businessId, userId)` sets
  `IsDeleted = 1, DeletedAtUtc = GETUTCDATE(), DeletedByUserId = @UserId, UpdatedAtUtc = GETUTCDATE()`
  (extends the existing UPDATE; still keyed on `Id + BusinessId + IsDeleted = 0`).
- Lines are **not** touched (reaffirmed).

Controller `SoftDelete(int id)`:
- Resolve `userId = User.FindFirstValue(ClaimTypes.NameIdentifier)` and pass it in.
- Still `[ValidateAntiForgeryToken]` + `[ModuleAccess(PortalModules.Invoice, AccessLevels.Full)]`.
- Returns the existing JSON `{ success, message }`.

**No restore endpoint is added.** (Explicit non-goal; there must be no code path that clears
`IsDeleted`.)

### Two-step type-`DELETE` confirmation (client)
In `Views/Invoice/Index.cshtml` JS, the delete handler:
1. `Swal.fire` warning: "Deleting invoice {number} is permanent and cannot be undone." → Continue/Cancel.
2. `Swal.fire` with an `input` (`inputLabel: 'Type DELETE to confirm'`), `inputValidator` requiring
   exactly `DELETE`; confirm button disabled/blocked until matched.
3. On confirm: `BlockUI.show` → `fetch('/Invoice/SoftDelete', POST, antiforgery)` → `BlockUI.hide` →
   `Swal` success/error → reload the list.
Matches the UI-feedback house rule. (SweetAlert2's `preConfirm`/`inputValidator` gives the exact
type-to-confirm behaviour without native dialogs.)

## List query & DTO

`GetPagedByBusinessIdAsync`:
- **Remove** `AND [invoice].[Invoice].[IsDeleted] = 0`.
- Add to SELECT: `[IsDeleted]`, `[DeletedAtUtc]`, `[DeletedByUserId]`.
- `ORDER BY InvoiceDate DESC, Id DESC` unchanged (deleted rows interleave by date, preserving the
  visible sequence; the number is shown on every row so the sequence is auditable regardless of sort).

`InvoiceListDto` gains:
```csharp
public bool IsDeleted { get; set; }
public DateTime? DeletedAtUtc { get; set; }
public string? DeletedByUserId { get; set; }
// display-only, populated by the service after paging:
public string? DeletedByName { get; set; }
public string? DeletedAtLocalDisplay { get; set; }
```

### Name + local-time resolution (service layer, after paging)
In the invoice-list assembly path (controller/service that calls `GetPagedByBusinessIdAsync`):
1. Collect distinct non-null `DeletedByUserId` from the page's rows.
2. One Membership lookup: `membershipDbContext.Users.Where(u => ids.Contains(u.Id))
   .Select(u => new { u.Id, u.FirstName, u.LastName })` → dictionary. (Batch, not per-row.)
3. `tz = await businessTimeZoneService.GetTimeZoneAsync(businessId)` once.
4. For each deleted row: `DeletedByName = "{First} {Last}"` (or "Unknown" if id null/not found);
   `DeletedAtLocalDisplay = ConvertTimeFromUtc(DeletedAtUtc, tz).ToString("dd MMM yyyy HH:mm")`.

This keeps the cross-DB join out of raw SQL (Portal vs Membership are separate databases) and batches
the lookup (no N+1).

## View rendering (`Views/Invoice/Index.cshtml`)

The list is server-rendered (Razor `@foreach` over the DTOs, per the screenshot's static rows). For
each row:
- If `IsDeleted`: add a `is-deleted` CSS class → `text-decoration: line-through; opacity:.55;` on the
  row text (mirrors the voided-receipt rows). Replace the Status/Financial pills with a single
  **"Deleted"** badge (red-soft, like the receipts "VOIDED" pill). Actions cell shows **Detail** and
  **PDF** only. Add a small info affordance (ⓘ or title attribute) showing
  `Deleted by {DeletedByName} · {DeletedAtLocalDisplay}`.
- Else: unchanged (today's Detail / Preview / PDF / Excel actions).

Detail (`Invoice/Detail`) and the PDF endpoint already load by id and do not filter on `IsDeleted`
(verify `GetByIdAndBusinessIdAsync` has no IsDeleted filter — it does not), so they work for deleted
drafts read-only. If Detail currently exposes Edit/Issue actions, hide them when `invoice.IsDeleted`.

## What explicitly stays filtered to non-deleted (FR-8)

Do **not** touch the `IsDeleted = 0` filter in: `GetAllFilteredByBusinessIdAsync` (CSV/PDF export),
and any receivables / dashboard / VAT / statement queries. Deleted drafts are a register-visibility
concern only; they carry zero financial weight and must not leak into financial aggregates or exports.

## Error handling

| Operation | Failure | Result |
|---|---|---|
| Delete non-Draft | status ≠ 1 | `ServiceResult.Fail("Only draft invoices can be deleted.")` (existing) |
| Delete already-deleted | `IsDeleted = 1` | `ServiceResult.Fail("Invoice has already been deleted.")` (existing) |
| Membership name lookup fails | user not found / DB error | row shows "Unknown"; never throws the list |
| Timezone resolve fails | — | `IBusinessTimeZoneService` never throws; falls back to default zone |
| Unique-index insert collision | duplicate number (bug) | SQL throws; surfaced as a generic create error (should never happen by construction) |

## Testing notes

- Service unit test: delete a Draft records `DeletedByUserId`; delete an Issued fails; no restore
  method exists on the interface.
- Numbering: after soft-deleting the highest draft, `GetNextSequentialNumberAsync` still returns
  max+1 (not the deleted number) because deleted rows are counted.
- List: a deleted row is returned by the paged query and carries `IsDeleted = true` + resolved name +
  local-time string.
- Build gate: `dotnet build Portal.Web/Portal.Web.csproj -c Debug -p:RazorCompileOnBuild=true --nologo`
  → 0 errors. (Terminal note: known char-echo/exit-code glitch in this environment; verify via
  background process output or a redirected log file.)

## Build order

1. Migration 231 (DeletedByUserId) + entity + EF config.
2. Migration 232 (unique index) with duplicate pre-check.
3. Repo: SoftDeleteAsync (+userId), list query (drop IsDeleted filter, emit new cols), DTO fields.
4. Service iface/impl: thread userId; post-paging name + local-time resolution (Membership + tz).
5. Controller: pass acting user id.
6. View + JS: deleted-row styling, badge, restricted actions, deleter tooltip, two-step type-DELETE
   confirm.
7. Verify build + walk the 10 acceptance criteria.
