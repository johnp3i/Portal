# Design: VAT Period Pre-Submission Checklist

## Overview

This feature adds an informational, non-blocking pre-submission checklist to the VAT
period review page (`/Vat/Detail`). It runs a fixed set of automated checks against a
period's data and returns them as a structured result rendered in an advisory panel
that mirrors the existing "Recurring Expense Check" panel on the same page.

The implementation is read-only. It introduces no new tables or columns and reuses the
existing VAT aggregation (`VatSubmissionService`) and unassigned-purchase counting
(`IPurchaseService`) so every number stays consistent with the rest of the page.

### Design goals

- **Consistency**: figures match the KPI cards and discrepancy card already on the page.
- **Non-blocking**: the checklist informs; it never prevents submission.
- **Convention-aligned**: new endpoint uses `AxGet`, tenant-scoped, `catch (Exception ex)`, fail-safe JSON.
- **Low blast radius**: additive change; the only edit to existing behaviour is folding the checklist warning count into the existing `markAsSubmitted()` pre-flight.

## Architecture

```
Views/Vat/Detail.cshtml (new advisory panel + JS loader)
        │  fetch GET /Vat/AxGetPreSubmissionChecklist?periodId=…
        ▼
VatController.AxGetPreSubmissionChecklist(int periodId)   [HttpGet]
        │  validates tenant ownership of period
        ▼
IVatSubmissionService.GetPreSubmissionChecklistAsync(periodId)   ── new
        │  ├─ reuse CreateOrRecalculateAsync → computed Output/Input/Net + discrepancy inputs
        │  ├─ reuse IPurchaseService.CountUnassignedForPeriodAsync
        │  └─ direct PortalDbContext queries (invoices/purchases) mirroring existing filters
        ▼
VatPreSubmissionChecklistDto  (summary + List<VatChecklistItemDto>)
        │  serialised to JSON
        ▼
Detail.cshtml JS renders items with pass/warning/info dots + header badge
```

The checklist logic lives in the **service layer** (`VatSubmissionService`), not the
controller, so the controller stays a thin HTTP adapter (per project MVC conventions).
The service already holds the aggregation logic and has the tenant + dbContext deps it
needs.

## Components and Interfaces

### 1. DTOs (new)

`Portal.Infrastructure/Models/VatPreSubmissionChecklistDto.cs`
(Flat `Portal.Infrastructure.Models` namespace, matching the existing `VatInvoiceBreakdownDto` — not a `Models/Vat/` subfolder.)

```csharp
public class VatPreSubmissionChecklistDto
{
    public bool IsSubmitted { get; set; }
    public int WarningCount { get; set; }          // items with status Warning
    public bool AllClear => WarningCount == 0;
    public string CurrencySymbol { get; set; } = "€";
    public List<VatChecklistItemDto> Items { get; set; } = new();
}

public class VatChecklistItemDto
{
    public string Key { get; set; } = null!;       // stable id, e.g. "unassigned_purchases"
    public string Status { get; set; } = null!;    // "pass" | "warning" | "info"
    public string Title { get; set; } = null!;
    public string Detail { get; set; } = null!;    // human-readable, pre-formatted
}
```

Rationale for `Status` as a string rather than an enum in the DTO: it is serialised
straight to JSON and consumed by JS as a CSS-class discriminator, matching how the
Recurring Expense Check panel already keys its pass/warning/fail dots. A private C#
enum is used internally in the service for clarity, mapped to these strings at the
boundary.

### 2. Service method (new) — `VatSubmissionService`

Add to `IVatSubmissionService`:

```csharp
Task<ServiceResult<VatPreSubmissionChecklistDto>> GetPreSubmissionChecklistAsync(int vatSubmissionPeriodId);
```

Implementation outline:

1. Resolve `businessId = _currentTenantService.CurrentBusinessId`.
2. Load the period via `_vatSubmissionPeriodRepository.GetByIdAndBusinessIdAsync`. If null → `ServiceResult.Fail` (controller maps to `success=false`).
3. Get computed figures + discrepancy inputs. Reuse `CreateOrRecalculateAsync(periodId)` which returns the `VatSubmission` (Output/Input/Net). Compute `inputVatByDate`, `latePurchasesIncluded`, `purchasesReportedLater` with the same queries the `Detail` action uses today (extract these into private helpers so the two call sites can share them — see "Reuse & de-duplication" below).
4. Build each checklist item (see "Checklist items" table).
5. Compute `WarningCount` = items where status == Warning. `IsSubmitted` from the submission.
6. Return the DTO.

Injected dependency addition: `VatSubmissionService` currently has no `IPurchaseService`.
Two options — inject `IPurchaseService` for `CountUnassignedForPeriodAsync`, or replicate
its one-line count query directly against `_portalDbContext`. **Decision: replicate the
count query inline** (it is a single `Where(...).CountAsync()` on `Purchases` already used
verbatim in `VatController.Index`), to avoid a new service-to-service dependency and any
risk of a DI cycle. The query is identical to the existing one, so counts stay consistent.

### 3. Controller endpoint (new) — `VatController`

```csharp
[HttpGet]
public async Task<IActionResult> AxGetPreSubmissionChecklist(int periodId)
{
    try
    {
        var result = await _vatSubmissionService.GetPreSubmissionChecklistAsync(periodId);
        if (!result.Success || result.Data == null)
            return Json(new { success = false, message = result.Message ?? "Unable to load checklist." });

        var d = result.Data;
        return Json(new
        {
            success = true,
            isSubmitted = d.IsSubmitted,
            warningCount = d.WarningCount,
            allClear = d.AllClear,
            items = d.Items.Select(i => new { key = i.Key, status = i.Status, title = i.Title, detail = i.Detail })
        });
    }
    catch (Exception ex)
    {
        return Json(new { success = false, message = "Failed to load the pre-submission checklist." });
    }
}
```

- `AxGet` prefix → automatically permitted for read-only users by `UserPermissionFilter`.
- No `[ValidateAntiForgeryToken]` (GET, read-only), matching sibling read endpoints (`GetInvoiceBreakdown`, `AxGetUnassignedCount`).
- Fail-safe: any exception returns `success=false`; the panel shows a graceful "unable to load" message rather than erroring the page.

### 4. View + client (Detail.cshtml)

Add a new collapsible advisory section **above** the existing "Recurring Expense Check"
panel (so the general checklist reads before the specialised recurring check), styled
identically: `.glass.card-pad`, header with icon tile + "Advisory Check" label +
"Pre-Submission Checklist" heading + a summary badge + chevron; body is a container the
JS fills.

Client JS (vanilla `fetch`, no BlockUI — read operation):

- `loadPreSubmissionChecklist()` — `fetch('/Vat/AxGetPreSubmissionChecklist?periodId=' + periodId)`, render items, set the header badge, and store `window.__vatChecklistWarningCount` for the submit flow.
- `renderChecklistItems(data, container)` — for each item, a row with a colored status dot (`pass`=`#129867`, `warning`=`#C8912E`, `info`=`#0D5EA6`), title, and detail text. Reuse the dot/row markup pattern from `renderRecurringResults`.
- Header badge: `data.allClear` → green "All Clear"; else amber "N item(s) to review".
- Called on `DOMContentLoaded` alongside the existing breakdown loaders.

Integration with `markAsSubmitted()` (Req 9): before the existing unassigned-purchases
pre-flight, check the cached warning count. If `> 0`, show a SweetAlert2 warning that
summarises "N checklist item(s) flagged" with Proceed / Review / Cancel. The existing
unassigned-purchases Swal is preserved; to avoid two stacked dialogs, the checklist
already includes the unassigned-purchases item, so we consolidate: if the checklist
loaded successfully, the checklist confirmation replaces the standalone unassigned Swal;
if the checklist failed to load, fall back to the current unassigned pre-flight unchanged.

**"Review First" scroll target:** to preserve the existing "scroll to the panel that
offers the fix" affordance, the checklist confirmation scrolls to the unassigned-purchases
section when that item is among the flagged warnings; otherwise it scrolls to the checklist
panel. This keeps the direct path to the assign/unassign controls when that is the actionable item.

## Checklist items

| Key | Status logic | Detail message (example) | Req |
|-----|--------------|---------------------------|-----|
| `unassigned_purchases` | Warning if count>0 else Pass | "3 purchase(s) in this period are not assigned to a VAT period." | 2 |
| `unassigned_invoices` | Warning if count>0 else Pass | "2 issued invoice(s) dated in this period have no explicit VAT period (included via date range)." | 3 |
| `zero_vat_invoices` | Info if count>0 else Pass | "1 issued invoice(s) have a subtotal but €0 VAT — review whether VAT should apply." | 4 |
| `purchase_count_trend` | Warning if drop≥33% & prior exists; Info if no prior; else Pass | "This period: 12 purchases. Previous: 20. 40% fewer — is this expected?" | 5 |
| `input_vat_discrepancy` | Warning if `TotalInputVat != InputVatByDate` else Pass | "Input VAT by date (€X) differs from reported (€Y). 1 late purchase included; 0 reported later." | 6 |
| `output_vat` | Pass (always) | "Output VAT: €X" | 7 |
| `input_vat` | Pass (always) | "Input VAT: €X" | 7 |
| `net_vat` | Pass (always) | "Net VAT payable: €X" (or "Refund due" / "No payment due") | 7 |

Ordering in the panel: the actionable checks first (unassigned purchases, unassigned
invoices, zero-VAT, purchase trend, discrepancy), then the three computed-figure Pass
rows, so attention lands on issues before the confirmatory numbers.

### Query definitions (all filtered by `BusinessId`, using full table names per SQL standards)

- **unassigned_purchases** — `Purchases` where `VatSubmissionPeriodId == null && !IsCancelled && InvoiceDate ∈ [start,end]`. Count. The predicate is **replicated inline** (not a call to `CountUnassignedForPeriodAsync`) to avoid a cross-service dependency; it is identical to the one in `VatController.Index` / `CountUnassignedForPeriodAsync`, so the counts match.
- **unassigned_invoices** — `Invoices` where `InvoiceStatusTypeId == 2 && !IsDeleted && VatSubmissionPeriodId == null && InvoiceDate ∈ [start,end]`. Count.
- **zero_vat_invoices** — `Invoices` where `InvoiceStatusTypeId == 2 && !IsDeleted && TaxAmount == 0 && Subtotal > 0 && (VatSubmissionPeriodId == periodId || (VatSubmissionPeriodId == null && InvoiceDate ∈ [start,end]))`. Count.
- **purchase_count_trend** — count of purchases belonging to this period (explicit or date-range fallback, `!IsCancelled`) vs the same for the immediately preceding period (found via `VatSubmissionPeriodRepository.GetImmediatelyPrecedingPeriodAsync`, the period with the greatest `PeriodStartDate` strictly less than this period's start). Drop% = `(prev - curr) / prev`. **Minimum-baseline guard:** the drop is only flagged as Warning when `prev >= PurchaseTrendMinBaseline` (5). Below that, a single missing purchase produces a large % swing and a noisy false alarm, so low-volume periods report Pass. When `prev` is below the baseline (or the count is higher, or the drop is under 33%), the item is Pass.
- **input_vat_discrepancy** — reuse the `TotalInputVat` from the submission and the `InputVatByDate` / late / later counts computed with the exact queries in `VatController.Detail` today. The comparison uses exact `decimal` inequality (`TotalInputVat != InputVatByDate`), identical to the existing `VatSubmissionDetailViewModel.HasDiscrepancy` — no new rounding tolerance is introduced, so the two agree. **UI note:** this condition is already surfaced by the existing "Audit Discrepancy Detected" card on the same page. The checklist intentionally restates it so the single-glance summary is complete; both indicators reflect the same underlying condition (not a bug).
- **output/input/net** — from the `VatSubmission` returned by `CreateOrRecalculateAsync`.

## Reuse & de-duplication

`VatController.Detail` currently computes `InputVatByDate`, `LatePurchasesIncluded`, and
`PurchasesReportedLater` inline. The checklist needs the same values. To avoid drift,
extract these three computations into private helper methods on `VatSubmissionService`
(e.g. `ComputeInputVatByDateAsync(period)`, `CountLatePurchasesAsync(period)`,
`CountPurchasesReportedLaterAsync(period)`) and have **both** the checklist method and
(optionally, in a follow-up) the Detail action use them. For this feature's scope, the
service method uses the helpers; the Detail action is left untouched to keep the change
minimal, accepting that the two compute the same values independently until a later
refactor. (Correctness is guaranteed because the predicates are identical; the risk is
only future maintenance, noted here explicitly.)

The checklist service method also loads `BusinessProfile` independently (for `CurrencySymbol`),
so on a `Detail` page load the profile is read twice — once by the action, once by the
checklist endpoint. This is a negligible extra read of the same row and is accepted for
simplicity; both read the identical value, so no divergence.

## Data Models

No schema changes. Existing tables read: `[vat].[VatSubmissionPeriod]`,
`[vat].[VatSubmission]`, `[dbo].[Invoice]` (or its actual schema), `[purchase].[Purchase]`,
`[dbo].[BusinessProfile]` (for currency symbol). No writes except the existing
`CreateOrRecalculateAsync` behaviour (which already upserts the submission row and writes
an audit log — unchanged).

**Read-only figures (updated):** the checklist endpoint is an `AxGet` and MUST NOT write.
It reads the persisted `VatSubmission` via `GetByPeriodIdAndBusinessIdAsync` when one
exists. When **no** submission row exists yet, it computes the figures **purely in-memory**
via a shared private helper `ComputeSubmissionFiguresAsync(businessId, period)` — it does
**not** call `CreateOrRecalculateAsync`, because that would insert a `VatSubmission` row and
write a "Created" audit-log entry from a read-only GET (a smell, and a write triggerable by
read-only users given `UserPermissionFilter` auto-permits `AxGet`). `CreateOrRecalculateAsync`
was refactored to use the same `ComputeSubmissionFiguresAsync` helper for its values and then
persist + audit, so the two paths share identical aggregation with zero duplication.

**Submitted-period behaviour (updated):** on a filed period the checklist is a read-only
review. All items are still shown, but any `Warning` status is downgraded to `Info` (so the
panel informs without nagging about issues that can no longer be acted on before submission,
and any discrepancy now reflects post-filing data drift rather than a pre-submission
problem). `WarningCount` is therefore 0 for submitted periods, and the summary badge reads
"All clear", satisfying Req 1.4.

## Error Handling

- Period not found / wrong tenant → service returns `Fail`; endpoint returns `success=false`; panel shows "Unable to load checklist for this period." No data leak.
- Any unexpected exception in the endpoint → caught with `catch (Exception ex)`, returns generic `success=false` message; page remains usable.
- Client fetch failure → panel container shows "Unable to load the checklist." and the submit flow falls back to the existing unassigned-purchases pre-flight.
- All service catch blocks use `catch (Exception ex)` per coding golden rules; repositories rethrow.

## Testing Strategy

Manual verification (per the requirements' acceptance criteria) is the primary path,
since the feature is read-only UI aggregation:

1. Panel renders in the advisory style; badge reflects warning count; collapsible works.
2. Each item toggles Pass↔Warning/Info as the underlying data condition is created/cleared.
3. `unassigned_purchases` count equals the existing Unassigned Purchases panel count.
4. `output/input/net` values equal the top KPI cards exactly.
5. `input_vat_discrepancy` agrees with the existing "Audit Discrepancy Detected" card.
6. Prior-period comparison: verify ≥33% drop warns, <33% passes, no-prior shows Info.
7. Submit with warnings → consolidated SweetAlert2 confirmation; proceed still submits.
8. Submit clean → standard confirmation, no extra friction.
9. Already-submitted period → checklist renders read-only; summary reflects filed state.
10. Tenant isolation: counts reflect only the current business.

Optional automated coverage (if the project's test project is used): unit-test the
drop-percentage threshold logic and the warning-count summarisation as pure functions by
factoring the status decisions into small testable methods.

## Design Decisions & Rationales

1. **Logic in the service, not the controller** — matches the MVC + service-layer pattern; keeps the controller a thin adapter; the service already owns VAT aggregation.
2. **Read-only figures: persisted row when present, in-memory compute otherwise** — the `AxGet` endpoint never writes. It reads the persisted `VatSubmission` when one exists; when none exists it computes figures in-memory via the shared `ComputeSubmissionFiguresAsync` helper rather than calling `CreateOrRecalculateAsync` (which would insert a row + audit entry from a read-only GET). `CreateOrRecalculateAsync` was refactored to reuse the same helper, so both paths share one aggregation implementation.
3. **Replicate the unassigned-purchase count inline rather than inject `IPurchaseService`** — avoids a new cross-service dependency and any DI-cycle risk; the predicate is identical, so counts stay consistent.
4. **`AxGet` endpoint** — read-only, auto-permitted for read-only users, matches naming convention for new AJAX endpoints.
5. **Consolidate the submit-time warning with the existing unassigned pre-flight** — prevents two stacked SweetAlert2 dialogs; falls back cleanly if the checklist fails to load.
6. **Status as string at the JSON boundary** — mirrors the existing advisory panel's JS contract; a private enum keeps the C# side readable.
7. **Credit-note check omitted from v1** — `CreditNote.VatSubmissionPeriodId` is non-nullable, so there is no true "unassigned" state; a date-vs-assignment check would duplicate the input-VAT discrepancy concept. Left out to avoid a low-value, confusing item; can be revisited.
8. **Minimum baseline on the purchase-count trend** — the drop is only flagged when the prior period had at least `PurchaseTrendMinBaseline` (5) purchases. On very low volumes a single missing purchase yields a large % swing; the guard prevents noisy false alarms for low-volume businesses while still catching genuine drops on established periods.
9. **Discrepancy shown in two places** — the input-VAT discrepancy already appears as the "Audit Discrepancy Detected" card; the checklist restates it so the summary is complete. Both reflect the same condition using the same exact `decimal` comparison; this is intentional, not duplication-by-accident.
10. **Deliberate origin-type asymmetry** — the record-completeness / trend counts (`unassigned_purchases`, `purchase_count_trend`) include ALL purchase origin types (matching `VatController.Index`), while the VAT-amount helpers exclude EU reverse charge (`PurchaseOriginTypeId == 2`, which carries zero VAT). This asymmetry is intentional and commented in code so it isn't "aligned" away by a future maintainer.
11. **Warnings downgraded to Info on submitted periods** — a filed period is immutable; its actionable checks can't be acted on and a discrepancy reflects post-filing drift. Downgrading Warning→Info keeps the information visible while making the summary reflect the filed state (Req 1.4).
12. **`ComputeSubmissionFiguresAsync` extracted** — the Output/Input/Net aggregation is now a single shared private helper used by both `CreateOrRecalculateAsync` (persisting path) and the checklist (read-only path). Note: `VatController.Detail` still computes its discrepancy inputs (`InputVatByDate`, late/later counts) inline; wiring it to the service helpers remains a deferred cleanup (the predicates are identical, so no correctness risk).
```