# Implementation Plan: VAT Period Assignment Workflow

## Overview

This plan replaces automatic VAT period assignment with user-controlled assignment. Purchases start unassigned and are explicitly claimed via form dropdowns or a bulk assignment panel on the VAT Detail page. An advisory warning before submission catches any unassigned purchases.

The implementation modifies existing services, controllers, and views. No database schema migration is needed — the `VatSubmissionPeriodId` column already exists as nullable on `[purchase].[Purchase]`.

## Tasks

- [x] 1. Remove auto-assignment logic
  - [x] 1.1 Remove `AssignVatPeriodAsync` from `PurchaseService.CreatePurchaseAsync`
    - Delete the `await AssignVatPeriodAsync(purchase);` line from `CreatePurchaseAsync`
    - Delete the entire `private async Task AssignVatPeriodAsync(Purchase purchase)` method
    - Verify `CreatePurchaseAsync` no longer sets `VatSubmissionPeriodId` unless explicitly provided
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.2 Ensure `UpdatePurchaseAsync` preserves user-set `VatSubmissionPeriodId`
    - Verify the update path passes through the `VatSubmissionPeriodId` value from the form
    - Add locking validation: if existing purchase is assigned to a submitted period, reject VatSubmissionPeriodId changes
    - _Requirements: 7.1, 7.2, 8.1_

  - [x] 1.3 Ensure `BulkCreatePurchasesAsync` passes through `VatSubmissionPeriodId` per row
    - Update `BulkPurchaseRowDto` to include optional `VatSubmissionPeriodId` (int?)
    - Pass through to the Purchase entity during bulk creation
    - _Requirements: 1.3, 4.3_

- [x] 2. Checkpoint — Verify removal compiles
  - Run `dotnet build` and ensure no compilation errors

- [x] 3. Repository layer additions
  - [x] 3.1 Add `GetUnassignedByDateRangeAsync` to `PurchaseRepository`
    - SELECT unassigned, non-cancelled purchases within date range, ordered by InvoiceDate DESC
    - Include Supplier and ExpenseCategory navigation data (JOIN or separate query)
    - Use full table names, SqlParameter, try/catch with `(Exception ex) { throw; }`
    - _Requirements: 5.2, 5.4, 12.1_

  - [x] 3.2 Add `CountUnassignedByDateRangeAsync` to `PurchaseRepository`
    - COUNT(*) of unassigned, non-cancelled purchases within date range
    - _Requirements: 6.1, 11.1_

  - [x] 3.3 Add `BulkAssignToPeriodAsync` to `PurchaseRepository`
    - UPDATE VatSubmissionPeriodId = @PeriodId WHERE Id IN (...) AND BusinessId = @BusinessId AND VatSubmissionPeriodId IS NULL AND IsCancelled = 0
    - Return rows affected count
    - _Requirements: 9.5_

  - [x] 3.4 Add `BulkUnassignFromPeriodAsync` to `PurchaseRepository`
    - UPDATE VatSubmissionPeriodId = NULL WHERE Id IN (...) AND BusinessId = @BusinessId AND purchase is not locked to a submitted period
    - Use NOT EXISTS subquery against VatSubmission to check submitted status
    - Return rows affected count
    - _Requirements: 10.4_

- [x] 4. Service layer additions
  - [x] 4.1 Add `AssignPurchasesToPeriodAsync` to `PurchaseService`
    - Validate period exists, belongs to business, is not submitted
    - Validate purchases belong to business, are not cancelled, are not locked
    - Call BulkAssignToPeriodAsync
    - Write audit log entry
    - Return ServiceResult with count
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 13.1_

  - [x] 4.2 Add `UnassignPurchasesFromPeriodAsync` to `PurchaseService`
    - Validate purchases belong to business
    - Validate none are locked to submitted periods
    - Call BulkUnassignFromPeriodAsync
    - Write audit log entries
    - Return ServiceResult with count
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 13.2_

  - [x] 4.3 Add `GetUnassignedForPeriodAsync` to `PurchaseService`
    - Resolve period dates from periodId
    - Call repository method
    - _Requirements: 5.2_

  - [x] 4.4 Add `CountUnassignedForPeriodAsync` to `PurchaseService`
    - Resolve period dates from periodId
    - Call repository count method
    - _Requirements: 6.1, 11.1_

- [x] 5. Checkpoint — Verify service layer compiles
  - Run `dotnet build` and ensure no compilation errors

- [x] 6. Controller endpoints
  - [x] 6.1 Add `GetUnassignedPurchases` GET endpoint to `VatController`
    - Accepts periodId, returns JSON array of unassigned purchases
    - Include supplier name, category name, invoice date, amounts
    - _Requirements: 5.2, 5.4_

  - [x] 6.2 Add `AxGetUnassignedCount` GET endpoint to `VatController`
    - Accepts periodId, returns JSON { count }
    - Used by the submission advisory check
    - _Requirements: 6.1_

  - [x] 6.3 Add `AxPostAssignPurchasesToPeriod` POST endpoint to `VatController`
    - Accepts AssignPurchasesRequest { PeriodId, PurchaseIds }
    - Calls service, returns Json(new { success, message, count })
    - ValidateAntiForgeryToken
    - _Requirements: 9.1–9.7_

  - [x] 6.4 Add `AxPostUnassignPurchasesFromPeriod` POST endpoint to `VatController`
    - Accepts UnassignPurchasesRequest { PurchaseIds }
    - Calls service, returns Json(new { success, message, count })
    - ValidateAntiForgeryToken
    - _Requirements: 10.1–10.6_

  - [x] 6.5 Update `PurchaseController.Create` to accept optional `VatSubmissionPeriodId`
    - Add to form model binding
    - Pass through to service (do not auto-assign)
    - _Requirements: 2.4, 2.5_

  - [x] 6.6 Update `PurchaseController.Edit` to handle `VatSubmissionPeriodId`
    - Add to form model binding
    - Validate locking: if existing assignment is to a submitted period, reject changes
    - Pass through to service
    - _Requirements: 3.2, 3.4, 3.5, 7.2_

  - [x] 6.7 Update `PurchaseController.BulkCreate` to accept optional `VatSubmissionPeriodId` per row
    - Add to BulkPurchaseRowDto
    - Pass through during entity creation
    - _Requirements: 4.1, 4.2, 4.3_

- [x] 7. Checkpoint — Verify controllers compile
  - Run `dotnet build` and ensure no compilation errors

- [x] 8. View changes — Purchase forms
  - [x] 8.1 Add VAT Period dropdown to `Purchase/Create.cshtml`
    - Optional dropdown after Invoice Date field
    - Shows unsubmitted periods ordered by most recent first
    - Default: "— Not assigned —" (empty value)
    - Helper text: "Assign to a VAT period now, or leave empty to assign later."
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 8.2 Add VAT Period dropdown to `Purchase/Edit.cshtml`
    - Shows currently assigned period as selected
    - Disabled with lock message if assigned to submitted period
    - Otherwise: editable with unsubmitted periods + "— Not assigned —"
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [x] 8.3 Add VAT Period column to `Purchase/BulkEntry.cshtml`
    - Dropdown per row (optional, default "— Not assigned —")
    - Add "Set all to..." batch control above grid
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 8.4 Update `PurchaseFormViewModel` with VAT period properties
    - Add VatSubmissionPeriodId (int?), UnsubmittedVatPeriods (List), IsVatPeriodLocked (bool), AssignedPeriodLabel (string?)
    - Populate in controller Create/Edit GET actions
    - _Requirements: 2.1, 3.1_

- [x] 9. View changes — VAT Detail page
  - [x] 9.1 Add Unassigned Purchases panel to `Vat/Detail.cshtml`
    - New section between Purchases breakdown and Filing Status
    - AJAX-loads unassigned purchases on page load
    - Table: checkbox, description/reference, supplier, invoice date, category, total, VAT amount
    - Select All checkbox in header
    - "Assign to this period" button (disabled when nothing selected)
    - "Dismiss" per-row action (hides row from panel without assigning)
    - Empty state: "All purchases in this date range have been assigned." with green checkmark
    - Only visible when period is NOT submitted
    - BlockUI during assignment, refresh both panels after success
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10_

  - [x] 9.2 Modify `markAsSubmitted()` in `Vat/Detail.cshtml`
    - Before submission: AJAX call to AxGetUnassignedCount
    - If count > 0: show SweetAlert2 warning with "Review First" and "Submit Anyway" buttons
    - If count == 0: show standard confirmation
    - "Review First" scrolls to Unassigned Purchases panel
    - "Submit Anyway" proceeds with submission
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [x] 9.3 Add "Unassign" action to assigned purchases in `Vat/Detail.cshtml`
    - Per-row "Unassign" button in the existing purchases table (only for unsubmitted periods)
    - Calls AxPostUnassignPurchasesFromPeriod with single purchase Id
    - Refresh both panels after success
    - _Requirements: 8.2, 8.3, 8.4_

- [x] 10. View changes — VAT Periods list
  - [x] 10.1 Add unassigned count badge to `Vat/Index.cshtml`
    - Show count of unassigned purchases per unsubmitted period
    - Badge style: amber pill with count (e.g., "5 unassigned")
    - Hidden when count is 0
    - Computed in controller and passed via ViewModel
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [x] 11. Checkpoint — Full integration test
  - Run `dotnet build` to verify everything compiles
  - Manual verification flow:
    1. Create a purchase without selecting a VAT period → verify VatSubmissionPeriodId is NULL
    2. Edit a purchase → assign to a period → verify it appears in that period's breakdown
    3. Navigate to VAT Detail → verify Unassigned Purchases panel shows relevant purchases
    4. Bulk-assign purchases → verify they move to the assigned purchases list
    5. Attempt to submit with unassigned purchases → verify advisory warning appears
    6. Submit the period → verify assigned purchases are now locked
    7. Attempt to unassign a locked purchase → verify rejection

- [x] 12. Property-based tests
  - [x]* 12.1 Write property test: Locking invariant
    - **Property: Purchases assigned to submitted periods cannot be unassigned or reassigned**
    - Generate purchases assigned to submitted periods; verify all unassign/reassign attempts fail
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4**

  - [x]* 12.2 Write property test: Tenant isolation
    - **Property: Assignment operations never cross business boundaries**
    - Generate purchases across multiple businesses; verify operations for one business never affect another
    - **Validates: Requirements 12.1, 12.2, 12.3, 12.4**

  - [x]* 12.3 Write property test: Assignment idempotency
    - **Property: Assigning already-assigned purchases to the same period is a no-op**
    - Generate purchases already assigned to a period; verify re-assignment to the same period succeeds without error
    - **Validates: Requirements 9.5**

  - [x]* 12.4 Write property test: Cancelled purchases excluded
    - **Property: Cancelled purchases are never included in unassigned counts or assignment operations**
    - Generate mix of cancelled and active purchases; verify cancelled ones are excluded from all operations
    - **Validates: Requirements 5.2, 9.3**

  - [x]* 12.5 Write property test: Count consistency
    - **Property: Unassigned count equals the number of purchases returned by the unassigned query**
    - For any date range, the count method and the list method return consistent results
    - **Validates: Requirements 6.1, 5.2**

- [x] 13. Final checkpoint
  - Run `dotnet test` and verify property-based tests pass
  - Run `dotnet build` for final compilation check

## Notes

- No database migration is needed — `VatSubmissionPeriodId` already exists as a nullable INT FK on `[purchase].[Purchase]`
- Existing auto-assigned purchases retain their assignments — this is a forward-only behaviour change
- The `AssignVatPeriodAsync` method is approximately 60 lines and can be safely deleted with no downstream dependencies
- The bulk assignment SQL uses `WHERE VatSubmissionPeriodId IS NULL` to prevent accidental reassignment — purchases already assigned to another period must be explicitly unassigned first
- Tasks marked with `*` are optional property-based tests
- Each task references specific requirements for traceability

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"], "description": "Remove auto-assignment, update existing flows" },
    { "id": 1, "tasks": ["2"], "description": "Checkpoint: compile" },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4"], "description": "Repository layer" },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3", "4.4"], "description": "Service layer" },
    { "id": 4, "tasks": ["5"], "description": "Checkpoint: compile" },
    { "id": 5, "tasks": ["6.1", "6.2", "6.3", "6.4", "6.5", "6.6", "6.7"], "description": "Controller endpoints" },
    { "id": 6, "tasks": ["7"], "description": "Checkpoint: compile" },
    { "id": 7, "tasks": ["8.1", "8.2", "8.3", "8.4"], "description": "Purchase form views" },
    { "id": 8, "tasks": ["9.1", "9.2", "9.3", "10.1"], "description": "VAT views" },
    { "id": 9, "tasks": ["11"], "description": "Checkpoint: full integration" },
    { "id": 10, "tasks": ["12.1", "12.2", "12.3", "12.4", "12.5"], "description": "Property-based tests" },
    { "id": 11, "tasks": ["13"], "description": "Final checkpoint" }
  ]
}
```
