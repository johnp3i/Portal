# Requirements: VAT Period Pre-Submission Checklist

## Introduction

Before a business owner marks a VAT period as submitted, they currently rely on
memory and manual review to confirm the numbers are complete and correct. Common
mistakes — unassigned purchases, invoices that never got a VAT period, zero-VAT
invoices that should have carried VAT, or a period with suspiciously few purchases —
are only caught after submission, when correcting them is costly.

This feature adds an **informational, non-blocking pre-submission checklist** to the
VAT period review page (`/Vat/Detail`). It runs a set of automated checks against the
period's data and presents each as a pass / warning / info line, alongside the
computed Output / Input / Net VAT figures. The checklist highlights potential issues
so the owner can fix them before filing, but it never blocks submission — the owner
stays in control.

The feature reuses existing infrastructure: the VAT aggregation logic in
`VatSubmissionService`, the unassigned-purchase counting in `IPurchaseService`, and
the advisory-panel UI pattern already established by the "Recurring Expense Check"
panel on the same page.

## Glossary

- **Period**: A `VatSubmissionPeriod` — a date range (`PeriodStartDate`..`PeriodEndDate`) for one VAT return.
- **Submission**: A `VatSubmission` row holding computed Output/Input/Net VAT and the `IsSubmitted` flag for a period.
- **Explicit assignment**: A transactional row (invoice/purchase/credit note) whose `VatSubmissionPeriodId` equals the period's Id.
- **Date-range fallback**: An invoice/purchase with `VatSubmissionPeriodId == NULL` whose `InvoiceDate` falls within the period; it is still counted in the period's VAT figures for backward compatibility.
- **Checklist item**: A single automated check with a status (Pass / Warning / Info), a title, and a human-readable detail message.
- **Advisory / non-blocking**: The checklist reports issues but does not prevent the "Mark as Submitted" action.

## Requirements

### Requirement 1 — Display a pre-submission checklist on the review page

**User Story:** As a business owner reviewing a VAT period before filing, I want an automated checklist of potential issues, so that I can catch mistakes before they reach the tax authority.

#### Acceptance Criteria

1. WHEN a user opens `/Vat/Detail` for a period THEN the system SHALL display a "Pre-Submission Checklist" advisory panel on the page.
2. The checklist panel SHALL follow the existing advisory-panel visual pattern used by the "Recurring Expense Check" panel (collapsible `.glass.card-pad`, header with icon and summary badge, per-item rows with colored status indicators).
3. The panel SHALL include an explicit note that the checklist is advisory only and does not block submission.
4. WHEN the period is already submitted (`IsSubmitted == true`) THEN the checklist SHALL still render (read-only review), but the summary SHALL reflect that the period is filed.
5. The checklist SHALL be scoped to the current tenant (`CurrentBusinessId`); no data from other businesses SHALL appear.

### Requirement 2 — Checklist item: unassigned purchases in the period

**User Story:** As a business owner, I want to know if purchases in this period's date range have not been assigned to the period, so that I don't omit deductible input VAT.

#### Acceptance Criteria

1. The checklist SHALL include an item reporting the count of purchases within the period's date range that have `VatSubmissionPeriodId == NULL` and are not cancelled.
2. IF the count is greater than zero THEN the item status SHALL be Warning and the detail SHALL state the count.
3. IF the count is zero THEN the item status SHALL be Pass.
4. The item SHALL reuse the existing `IPurchaseService.CountUnassignedForPeriodAsync` logic so the count matches the existing "Unassigned Purchases" panel on the same page.

### Requirement 3 — Checklist item: unassigned issued invoices in the period

**User Story:** As a business owner, I want to know if issued invoices dated within this period have no explicit VAT period assignment, so that my records are clean and every invoice is deliberately accounted for.

#### Acceptance Criteria

1. The checklist SHALL include an item reporting the count of issued invoices (`InvoiceStatusTypeId == 2`, not deleted) whose `InvoiceDate` falls within the period's date range and whose `VatSubmissionPeriodId == NULL`.
2. IF the count is greater than zero THEN the item status SHALL be Warning and the detail SHALL explain that these invoices are included via the date-range fallback but are not explicitly assigned.
3. IF the count is zero THEN the item status SHALL be Pass.

### Requirement 4 — Checklist item: zero-VAT issued invoices

**User Story:** As a business owner, I want to be alerted to issued invoices in this period that carry no VAT, so that I can confirm the zero-rating is intentional and not a data-entry error.

#### Acceptance Criteria

1. The checklist SHALL include an item reporting the count of issued invoices (`InvoiceStatusTypeId == 2`, not deleted) belonging to the period (explicit assignment or date-range fallback) where `TaxAmount == 0` AND `Subtotal > 0`.
2. IF the count is greater than zero THEN the item status SHALL be Info and the detail SHALL state the count and prompt the user to review whether VAT should apply.
3. IF the count is zero THEN the item status SHALL be Pass.

### Requirement 5 — Checklist item: purchase count vs prior period

**User Story:** As a business owner, I want to know if this period has noticeably fewer purchases than the previous period, so that I can check whether I forgot to record some expenses.

#### Acceptance Criteria

1. The checklist SHALL include an item comparing the count of purchases belonging to this period (explicit or date-range fallback, not cancelled) against the count for the immediately preceding period (by `PeriodStartDate`).
2. IF a preceding period exists AND this period's purchase count is at least 33% lower than the preceding period's count THEN the item status SHALL be Warning and the detail SHALL state both counts and the percentage drop.
3. IF a preceding period exists and the drop is less than 33% (or the count is higher) THEN the item status SHALL be Pass and the detail SHALL state both counts.
4. IF no preceding period exists THEN the item status SHALL be Info and the detail SHALL state that no prior period is available for comparison.

### Requirement 6 — Checklist item: input VAT discrepancy

**User Story:** As a business owner, I want to be told if the input VAT computed by invoice dates differs from the input VAT computed by period assignment, so that I understand why the reported figure may differ from expectation.

#### Acceptance Criteria

1. The checklist SHALL include an item reflecting the existing discrepancy detection (`TotalInputVat` vs `InputVatByDate`).
2. IF the two figures differ THEN the item status SHALL be Warning and the detail SHALL state both figures and reference late purchases included and purchases reported later (reusing the existing `LatePurchasesIncluded` / `PurchasesReportedLater` counts).
3. IF the two figures are equal THEN the item status SHALL be Pass.

### Requirement 7 — Checklist item: computed VAT figures

**User Story:** As a business owner, I want the checklist to restate the computed Output, Input, and Net VAT, so that I can confirm the headline numbers at the point of final review.

#### Acceptance Criteria

1. The checklist SHALL include Pass items restating Output VAT, Input VAT, and Net VAT payable, formatted with the business currency symbol.
2. These items SHALL use the figures already computed by `VatSubmissionService` for the period (no independent recalculation that could diverge).

### Requirement 8 — Overall checklist summary

**User Story:** As a business owner, I want a single summary indicator for the checklist, so that I can tell at a glance whether anything needs my attention.

#### Acceptance Criteria

1. The panel header SHALL display a summary badge derived from the item statuses: "All Clear" when no Warning items exist, otherwise "N item(s) to review" where N is the count of Warning items.
2. Info items SHALL NOT count toward the "to review" total.
3. The summary badge color SHALL follow the design system: success green (`#129867`) for all clear, warning amber (`#C8912E`) when items need review.

### Requirement 9 — Integration with the submit action

**User Story:** As a business owner, when I click "Mark as Submitted" while warnings exist, I want to be reminded, so that I make a deliberate choice.

#### Acceptance Criteria

1. WHEN the user clicks "Mark as Submitted" AND the checklist has one or more Warning items THEN the system SHALL show a SweetAlert2 confirmation summarising that N item(s) are flagged, with options to proceed or cancel.
2. The confirmation SHALL NOT prevent submission if the user chooses to proceed.
3. The existing unassigned-purchases pre-flight check SHALL be preserved or subsumed into this confirmation without regressing current behaviour.
4. WHEN no Warning items exist THEN the standard submission confirmation SHALL be shown (no additional friction).

### Requirement 10 — Data access, conventions, and non-functional

#### Acceptance Criteria

1. The checklist data SHALL be served by a new read-only AJAX endpoint named `AxGetPreSubmissionChecklist(int periodId)` on `VatController`, returning `Json(new { success, ... })`.
2. The endpoint SHALL validate that the period belongs to the current tenant; IF it does not THEN it SHALL return `success = false` without leaking data.
3. All new SQL/EF queries SHALL filter by `BusinessId` and use the same assignment/date-range rules as the existing VAT aggregation, so counts and figures are consistent with the rest of the page.
4. All catch blocks SHALL use `catch (Exception ex)`; the endpoint SHALL fail safe (return `success = false` with a generic message) rather than throw to the user.
5. The client SHALL load the checklist with a plain `fetch` (read operation, no BlockUI), consistent with the other read-only breakdown loads on the page; the submit-time confirmation SHALL use SweetAlert2 per the UI standards.
6. No new database tables or columns SHALL be required; the feature reads existing data only.
```