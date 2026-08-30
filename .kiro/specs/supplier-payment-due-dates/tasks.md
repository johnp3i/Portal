# Implementation Plan: Supplier Payment Due Dates

## Overview

Add two optional date fields to purchases — `SupplierDueDate` (the supplier's actual deadline) and `TargetPaymentDate` (the business owner's internal target). The system uses an "effective due date" (`TargetPaymentDate ?? SupplierDueDate`) for all dashboard, indicator, and future email logic. Implementation proceeds: DB migration → Entity → Repository → ViewModel + Controller → Views → Dashboard widget → CSV export.

## Tasks

- [x] 1. Database migration and entity
  - [x] 1.1 Create SQL migration script
    - Create `Portal.Database/Migrations/181_AddPaymentDatesToPurchase.sql`
    - Add `USE [Portal]` header
    - `ALTER TABLE [purchase].[Purchase] ADD [SupplierDueDate] DATE NULL`
    - `ALTER TABLE [purchase].[Purchase] ADD [TargetPaymentDate] DATE NULL`
    - Wrap each in IF NOT EXISTS check for idempotency
    - _Requirements: 1.1_

  - [x] 1.2 Add date properties to Purchase entity
    - Add `public DateOnly? SupplierDueDate { get; set; }` after `InvoiceDate`
    - Add `public DateOnly? TargetPaymentDate { get; set; }` after `SupplierDueDate`
    - _Requirements: 1.2_

- [x] 2. Repository layer updates
  - [x] 2.1 Add both columns to all SELECT queries
    - Add `[SupplierDueDate], [TargetPaymentDate]` to the column list in: `GetAllByBusinessIdAsync`, `GetByIdAndBusinessIdAsync`, `GetFilteredAsync`, `GetUnassignedByDateRangeAsync`
    - _Requirements: 1.2_

  - [x] 2.2 Add both columns to InsertAsync
    - Add `[SupplierDueDate], [TargetPaymentDate]` to the column list and values
    - Add null-safe SqlParameters for both
    - _Requirements: 1.3_

  - [x] 2.3 Add both columns to UpdateAsync
    - Add `[SupplierDueDate] = @SupplierDueDate, [TargetPaymentDate] = @TargetPaymentDate` to SET clause
    - Add null-safe SqlParameters for both
    - _Requirements: 1.4_

  - [x] 2.4 Add GetUpcomingDueByBusinessIdAsync method
    - New method: `Task<List<Purchase>> GetUpcomingDueByBusinessIdAsync(int businessId, DateOnly cutoffDate)`
    - SELECT non-cancelled purchases where effective due date (COALESCE TargetPaymentDate, SupplierDueDate) is not null and <= cutoffDate
    - ORDER BY COALESCE(TargetPaymentDate, SupplierDueDate) ASC
    - Include full column list
    - `catch (Exception ex) { throw; }`
    - _Requirements: 2.1, 4.2_

- [x] 3. Checkpoint — Ensure migration, entity, and repository compile

- [x] 4. ViewModel and controller updates
  - [x] 4.1 Add both dates to PurchaseFormViewModel
    - Add `public DateOnly? SupplierDueDate { get; set; }`
    - Add `public DateOnly? TargetPaymentDate { get; set; }`
    - _Requirements: 1.3, 1.4_

  - [x] 4.2 Update MapFormToEntity to include both dates
    - Add `SupplierDueDate = model.SupplierDueDate` and `TargetPaymentDate = model.TargetPaymentDate`
    - _Requirements: 1.3_

  - [x] 4.3 Update Edit GET to map both dates from entity to view model
    - Add `model.SupplierDueDate = purchase.SupplierDueDate;`
    - Add `model.TargetPaymentDate = purchase.TargetPaymentDate;`
    - _Requirements: 1.4_

  - [x] 4.4 Add client-side validation warning for target after supplier deadline
    - In Create.cshtml and Edit.cshtml, add a hidden inline amber warning div below the Target Payment Date field
    - Add JS: on `change` of either date input, check if both have values and TargetPaymentDate > SupplierDueDate
    - If true, show the warning div: "Target date is after the supplier deadline." styled in amber (#C8912E)
    - If false, hide the warning div
    - No server-side validation — the data is valid, just unusual. Form still submits.
    - _Requirements: 1.6, 1.9_

- [x] 5. Purchase Create and Edit views
  - [x] 5.1 Add both date fields to Create.cshtml
    - Add two optional date inputs after Invoice Date in the same form-grid row
    - Supplier Due Date: label "Supplier Due Date", helper text "The actual payment deadline from the supplier's invoice."
    - Target Payment Date: label "Target Payment Date", helper text "When you want to pay — can be earlier than the supplier deadline."
    - Both optional, type="date", no required attribute
    - _Requirements: 1.3, 1.5, 1.7, 1.8_

  - [x] 5.2 Add both date fields to Edit.cshtml
    - Same pattern as Create, pre-populated from model
    - _Requirements: 1.4_

- [x] 6. Purchase list — Due column with indicators
  - [x] 6.1 Add Due column to the purchase list table
    - Add `<th>Due</th>` after the existing Date column in `Index.cshtml`
    - Compute effective due date: TargetPaymentDate ?? SupplierDueDate
    - Render: null → "—", overdue → red text + "Overdue" pill, today → amber "Today", within 7 days → amber text, otherwise → normal text
    - When only SupplierDueDate is set (no target), show date with muted "(supplier)" label
    - When TargetPaymentDate is set, show as primary date
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

- [x] 7. Checkpoint — Ensure purchase create/edit/list compile and render

- [x] 8. Dashboard widget — Upcoming Supplier Payments
  - [x] 8.1 Create UpcomingSupplierPaymentDto and add to DashboardViewModel
    - DTO with: PurchaseId, SupplierName, Description, TotalAmount, EffectiveDueDate, SupplierDueDate, TargetPaymentDate, Status
    - Add `List<UpcomingSupplierPaymentDto> UpcomingSupplierPayments` to `DashboardViewModel`
    - _Requirements: 4.1, 4.3_

  - [x] 8.2 Populate widget data in HomeController
    - In the `if (scope.ShowPurchase)` block, call `GetUpcomingDueByBusinessIdAsync(businessId, today + 14 days)`
    - Collect distinct SupplierId values from results
    - Batch-load supplier names: `_dbContext.Suppliers.Where(s => supplierIds.Contains(s.Id)).Select(s => new { s.Id, s.Name }).ToDictionaryAsync()`
    - Map to UpcomingSupplierPaymentDto, resolving SupplierName from the dictionary
    - Compute effective due date (TargetPaymentDate ?? SupplierDueDate) and status per entry (overdue/today/due_soon/upcoming)
    - Take top 5, populate `model.UpcomingSupplierPayments`
    - _Requirements: 4.2, 4.3, 4.4, 4.6_

  - [x] 8.3 Render the widget in Dashboard view
    - New section in purchase-scoped area of Home/Index.cshtml
    - Table with: Supplier, Description (truncated), Amount, Due Date, Status pill
    - When TargetPaymentDate differs from SupplierDueDate, show "(supplier: DD MMM)" below the primary date
    - "View all purchases" link if more than 5 exist
    - Empty state: "No upcoming supplier payments."
    - _Requirements: 4.1, 4.4, 4.5, 4.7, 4.8_

- [x] 9. Exports
  - [x] 9.1 Add both date columns to CSV export
    - Find CSV export logic in `PurchaseController.ExportCsv`
    - Add "Supplier Due Date" and "Target Payment Date" to the CSV header after "Date"
    - Add `p.SupplierDueDate` and `p.TargetPaymentDate` to each row, formatted as yyyy-MM-dd when present, empty when null
    - _Requirements: 5.1, 5.2_

  - [x] 9.2 Add both date columns to PDF export
    - Open `Portal.Web/Views/Purchase/_ExportPdf.cshtml`
    - Add "Supplier Due" and "Target Date" columns after the existing "Date" column in the thead
    - Add corresponding `<td>` cells in each row, formatted as dd MMM yyyy when present, empty when null
    - _Requirements: 5.3, 5.4_

- [x] 10. Final checkpoint — Ensure everything compiles and renders correctly
  - Verify zero diagnostics
  - Verify purchase create/edit shows both date fields
  - Verify purchase list shows Due column with correct indicators
  - Verify dashboard shows Upcoming Supplier Payments widget
  - Verify CSV export includes both columns

## Notes

- No property-based tests — this is a data field + display feature with minimal business logic
- Both date fields are optional everywhere — no existing data is affected
- The "effective due date" concept (TargetPaymentDate ?? SupplierDueDate) is computed at read time, not stored
- The Purchase list is server-rendered — indicators are Razor-rendered
- The dashboard widget reuses the existing `scope.ShowPurchase` flag — no new permission needed
- Requirement 6 (weekly email escalation) is documented as a design note for future Proposal #7 implementation — NOT built in this spec
- The two-date model enables progressive escalation: target missed → weekly nudge → supplier deadline approaching → critical warning
- **Known limitation:** Bulk Entry and CSV Import do NOT include the new date fields. Purchases created through these flows will have both dates as NULL. Can be enhanced in a later phase.
- **Validation:** Target > Supplier warning is client-side JS only (inline amber message). No server-side validation. The form still submits.
- **Supplier name resolution:** The dashboard widget uses a batch lookup via `_dbContext.Suppliers` — not EF navigation properties (since the repository returns entities via `FromSqlRaw`)
- All SQL uses full table names (no aliases) per project standards
- All catch blocks use `catch (Exception ex) { throw; }` per coding golden rules
- Bottom-up ordering: DB → Entity → Repository → ViewModel + Controller → Views → Dashboard → Exports

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4"] },
    { "id": 2, "tasks": ["3"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3", "4.4"] },
    { "id": 4, "tasks": ["5.1", "5.2", "6.1"] },
    { "id": 5, "tasks": ["7"] },
    { "id": 6, "tasks": ["8.1"] },
    { "id": 7, "tasks": ["8.2", "8.3"] },
    { "id": 8, "tasks": ["9.1", "9.2"] },
    { "id": 9, "tasks": ["10"] }
  ]
}
```
