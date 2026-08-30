# Implementation Plan: VAT Period Pre-Submission Checklist

## Overview

Adds an informational, non-blocking pre-submission checklist to `/Vat/Detail`. The
checklist is served by a new read-only `AxGet` endpoint backed by a new service method
that reuses existing VAT aggregation and reads the already-persisted submission figures.
The UI is a new advisory panel mirroring the existing "Recurring Expense Check" panel,
plus a consolidated submit-time confirmation. No new tables or columns.

## Tasks

- [ ] 1. DTOs
  - [x] 1.1 Create `VatPreSubmissionChecklistDto` and `VatChecklistItemDto`
    - New file `Portal.Infrastructure/Models/VatPreSubmissionChecklistDto.cs` (flat `Portal.Infrastructure.Models` namespace, matching existing `VatInvoiceBreakdownDto` — not a `Models/Vat/` subfolder)
    - `VatPreSubmissionChecklistDto`: `IsSubmitted` (bool), `WarningCount` (int), `AllClear` (computed `WarningCount == 0`), `CurrencySymbol` (string, default "€"), `Items` (List<VatChecklistItemDto>)
    - `VatChecklistItemDto`: `Key` (string), `Status` (string: "pass"|"warning"|"info"), `Title` (string), `Detail` (string)
    - _Requirements: 1, 7, 8_

- [x] 2. Service layer — checklist computation
  - [x] 2.1 Add `GetPreSubmissionChecklistAsync` to `IVatSubmissionService`
    - Signature: `Task<ServiceResult<VatPreSubmissionChecklistDto>> GetPreSubmissionChecklistAsync(int vatSubmissionPeriodId)`
    - _Requirements: 1, 10.1_

  - [x] 2.2 Implement `GetPreSubmissionChecklistAsync` in `VatSubmissionService`
    - Resolve `businessId` from `_currentTenantService.CurrentBusinessId`
    - Load period via `_vatSubmissionPeriodRepository.GetByIdAndBusinessIdAsync`; if null → `ServiceResult.Fail("...")`
    - Read figures: call `_vatSubmissionRepository.GetByPeriodIdAndBusinessIdAsync`; if no row exists, fall back to `CreateOrRecalculateAsync` and use its result
    - Load `BusinessProfile` for `CurrencySymbol`
    - Build items (task 2.3–2.9), compute `WarningCount`, set `IsSubmitted`
    - Wrap in `try/catch (Exception ex)` and rethrow (controller handles user-facing failure)
    - _Requirements: 1.4, 1.5, 7.2, 10.2, 10.3, 10.4_

  - [x] 2.3 Item: unassigned purchases (`unassigned_purchases`)
    - Count `Purchases` where `BusinessId == businessId && VatSubmissionPeriodId == null && !IsCancelled && InvoiceDate` in `[PeriodStartDate, PeriodEndDate]`
    - Predicate **replicated inline** (not a call to `CountUnassignedForPeriodAsync`) to avoid an `IPurchaseService` dependency; identical to `VatController.Index` / `CountUnassignedForPeriodAsync`, so counts match
    - Status: Warning if count>0 else Pass
    - _Requirements: 2_

  - [x] 2.4 Item: unassigned issued invoices (`unassigned_invoices`)
    - Count `Invoices` where `BusinessId == businessId && InvoiceStatusTypeId == 2 && !IsDeleted && VatSubmissionPeriodId == null && InvoiceDate` in range
    - Status: Warning if count>0 else Pass
    - _Requirements: 3_

  - [x] 2.5 Item: zero-VAT issued invoices (`zero_vat_invoices`)
    - Count `Invoices` where `InvoiceStatusTypeId == 2 && !IsDeleted && TaxAmount == 0 && Subtotal > 0 && (VatSubmissionPeriodId == periodId || (VatSubmissionPeriodId == null && InvoiceDate in range))`
    - Status: Info if count>0 else Pass
    - _Requirements: 4_

  - [x] 2.6 Item: purchase count vs prior period (`purchase_count_trend`)
    - Current count: `Purchases` belonging to this period (explicit `== periodId` OR (null && InvoiceDate in range)), `!IsCancelled`
    - Find immediately preceding period: the period for this `businessId` with the greatest `PeriodStartDate` strictly less than this period's `PeriodStartDate` (query `VatSubmissionPeriods`, order by `PeriodStartDate` desc, take 1). Add a repository method if needed (task 2.10)
    - Prior count: purchases belonging to the prior period (same explicit/date-range rule)
    - Status: Warning if prior exists AND `prior >= PurchaseTrendMinBaseline` (5) AND `(prior-curr)/prior >= 0.33`; Info if no prior; else Pass. The minimum-baseline guard prevents noisy false alarms on low-volume periods. Detail states both counts and drop%
    - _Requirements: 5_

  - [x] 2.7 Item: input VAT discrepancy (`input_vat_discrepancy`)
    - `TotalInputVat` from the submission; `InputVatByDate` via helper (task 2.8); late/later counts via helpers
    - Status: Warning if `TotalInputVat != InputVatByDate` else Pass. Detail states both figures + late/later counts
    - _Requirements: 6_

  - [x] 2.8 Private helpers for discrepancy inputs
    - `ComputeInputVatByDateAsync(businessId, period)`, `CountLatePurchasesAsync(businessId, period)`, `CountPurchasesReportedLaterAsync(businessId, period)` — mirror the exact queries currently in `VatController.Detail` (origin type != 2, not cancelled)
    - _Requirements: 6.2_

  - [x] 2.9 Items: computed figures (`output_vat`, `input_vat`, `net_vat`)
    - Three Pass items formatted with `CurrencySymbol`; net uses "Tax owed"/"Refund due"/"No payment due" based on sign of `NetVatPayable`
    - _Requirements: 7_

  - [x] 2.10 Repository support for preceding period (if not already available)
    - Add `GetImmediatelyPrecedingPeriodAsync(int businessId, DateOnly beforeStartDate)` to `VatSubmissionPeriodRepository` (raw SQL, `TOP 1 ... WHERE BusinessId=@b AND PeriodStartDate < @d ORDER BY PeriodStartDate DESC`), full table names, null-safe
    - _Requirements: 5.1, 5.4_

- [x] 3. Controller endpoint
  - [x] 3.1 Add `AxGetPreSubmissionChecklist(int periodId)` to `VatController`
    - `[HttpGet]`, no antiforgery (read-only GET, matches sibling read endpoints)
    - Call service; on `!Success` return `Json(new { success = false, message })`
    - On success return `Json(new { success = true, isSubmitted, warningCount, allClear, items = [...] })` with lowercase camel keys per existing convention
    - `try/catch (Exception ex)` → fail-safe `Json(new { success = false, message = "Failed to load the pre-submission checklist." })`
    - _Requirements: 10.1, 10.2, 10.4_

- [x] 4. Checkpoint — backend build
  - Build the solution; ensure the new DTO, service method, repository method, and endpoint compile cleanly.

- [x] 5. View — advisory panel markup (`Views/Vat/Detail.cshtml`)
  - [x] 5.1 Add the "Pre-Submission Checklist" collapsible panel
    - Place above the existing Recurring Expense Check panel
    - Mirror its structure: `.glass.card-pad`, icon tile, "Advisory Check" eyebrow, "Pre-Submission Checklist" heading, summary badge span (`#checklistBadge`), chevron, and a `#checklistContent` container
    - Include the "advisory only, does not block submission" note
    - Match colors/markup from the mockup (`.kiro/docs/mockups/vat-pre-submission-checklist-mockup.html`)
    - _Requirements: 1.1, 1.2, 1.3, 8.3_

- [x] 6. Client JS (`Views/Vat/Detail.cshtml` @section Scripts)
  - [x] 6.1 Implement `loadPreSubmissionChecklist()`
    - Vanilla `fetch('/Vat/AxGetPreSubmissionChecklist?periodId=' + periodId)` (no BlockUI — read op)
    - On success: render items, set the header badge, cache `checklistWarningCount`, the warning titles, and a `checklistHasUnassignedWarning` flag for the submit confirmation
    - On failure: show "Unable to load the checklist." in the container; set warning count to null (submit falls back to existing pre-flight)
    - Call on `DOMContentLoaded` alongside existing loaders
    - _Requirements: 1.1, 8.1, 8.2, 10.5_

  - [x] 6.2 Implement `renderChecklistItems(data, container)` + badge
    - Row per item with status dot color (pass `#129867`, warning `#C8912E`, info `#0D5EA6`), title, detail
    - Badge: `allClear` → green "All Clear"; else amber "N item(s) to review"
    - _Requirements: 1.2, 8.1, 8.3_

  - [x] 6.3 Add collapse/expand toggle for the panel
    - Mirror `toggleRecurringPanel()` chevron rotation behaviour
    - _Requirements: 1.2_

  - [x] 6.4 Integrate with `markAsSubmitted()`
    - If `checklistWarningCount > 0`, show a SweetAlert2 warning summarising N flagged item(s) with Submit Anyway / Review First / Cancel; "Review First" scrolls to the unassigned-purchases section when that item is flagged (preserving the fix affordance), otherwise to the checklist panel
    - If the checklist loaded and warning count is 0 → standard confirmation
    - If the checklist failed to load (count null) → fall back to the existing unassigned-purchases pre-flight unchanged
    - Preserve the existing submit fetch/BlockUI/Swal success flow
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [ ] 7. Final checkpoint — build + manual verification
  - Build the solution; verify no errors
  - Manually verify against requirements: panel renders in advisory style; each item toggles pass/warning/info as data conditions change; unassigned count matches existing panel; computed figures match top KPI cards; discrepancy agrees with the existing "Audit Discrepancy Detected" card; prior-period ≥33% drop warns; submit-with-warnings shows the consolidated confirmation and still submits on proceed; submitted period renders read-only; tenant isolation holds

## Notes

- No new database tables or columns — reads existing data only
- New AJAX endpoint uses `AxGet` prefix (auto-permitted for read-only users by `UserPermissionFilter`)
- All catch blocks use `catch (Exception ex)`; repositories rethrow; endpoint fails safe
- All SQL/EF filters by `BusinessId`; assignment/date-range rules identical to existing VAT aggregation so figures stay consistent
- Figures read from the persisted `VatSubmission` (via `GetByPeriodIdAndBusinessIdAsync`) to avoid a second recalc + duplicate audit entries; fall back to `CreateOrRecalculateAsync` only if no row exists
- Client uses plain `fetch` for the read; submit-time confirmation uses SweetAlert2 per UI standards
- Credit-note check intentionally omitted from v1 (non-nullable FK → no true "unassigned" state)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.10"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "2.5", "2.6", "2.7", "2.8", "2.9"] },
    { "id": 3, "tasks": ["3.1"] },
    { "id": 4, "tasks": ["4"] },
    { "id": 5, "tasks": ["5.1"] },
    { "id": 6, "tasks": ["6.1", "6.2", "6.3", "6.4"] },
    { "id": 7, "tasks": ["7"] }
  ]
}
```